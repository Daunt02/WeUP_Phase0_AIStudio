using WeUP.Contracts.Ingestion;
using WeUP.Domain.Moderation;

namespace WeUP.Application.Moderation;

// ---------------------------------------------------------------------------
// Centralized thresholds — tune here, applied everywhere
// ---------------------------------------------------------------------------

internal static class EligibilityThresholds
{
    public const double AutoApprove       = 0.85;
    public const double ManualReview      = 0.55;
    public const double MinDimension      = 0.50;
    public const double MaxDupeRiskScore  = 0.55; // dedupe match score that triggers block
    public const double SourceTrustManual = 0.95;
    public const double SourceTrustLink   = 0.60;
    public const double SourceTrustFlyer  = 0.70;
    public const double SourceTrustVenue  = 0.75;
    public const double SourceTrustDefault = 0.50;
}

// ---------------------------------------------------------------------------
// Confidence scoring
// ---------------------------------------------------------------------------

public sealed class ConfidenceScoringService : IConfidenceScoringService
{
    public PublishConfidenceVector Score(NormalizedEventCandidate candidate, double dedupeMatchScore = 0.0)
    {
        var sourceTrust = candidate.SourceKind switch
        {
            "manual_submission" or "ManualSubmission" => EligibilityThresholds.SourceTrustManual,
            "flyer_upload"      or "FlyerUpload"      => EligibilityThresholds.SourceTrustFlyer,
            "pasted_url"        or "PastedUrl"        => EligibilityThresholds.SourceTrustLink,
            "venue_page"        or "VenuePage"        => EligibilityThresholds.SourceTrustVenue,
            _ => EligibilityThresholds.SourceTrustDefault,
        };

        // DupeRisk is inverse of dedupe match score: a 0.9 match is 0.1 dupe-safety
        var dupeRisk = Math.Max(0.0, 1.0 - dedupeMatchScore);

        return new PublishConfidenceVector(
            Extraction:      candidate.ExtractionConfidence,
            Geocode:         candidate.GeocodeConfidence,
            Temporal:        candidate.TemporalConfidence,
            VenueMatch:      0.5,  // Phase 0 stub: real venue resolution implemented in P20
            DupeRisk:        dupeRisk,
            SourceTrust:     sourceTrust,
            ReviewConfidence: 0.0  // elevated when a reviewer has signed off
        );
    }
}

// ---------------------------------------------------------------------------
// Rule set
// ---------------------------------------------------------------------------

/// <summary>
/// Deterministic, explainable eligibility rules.
/// Each rule appends to blockers/recommendations/triggeredRules if it fires.
/// </summary>
public sealed class PublishEligibilityRuleSet : IEligibilityRuleSet
{
    public void Apply(
        NormalizedEventCandidate candidate,
        PublishConfidenceVector vector,
        List<string> blockers,
        List<string> recommendations,
        List<string> triggeredRules)
    {
        // Rule: required fields
        if (string.IsNullOrWhiteSpace(candidate.Title))
        {
            blockers.Add("Title is required.");
            recommendations.Add("Provide a descriptive event title.");
            triggeredRules.Add("REQUIRED_FIELD:title");
        }
        if (string.IsNullOrWhiteSpace(candidate.VenueName))
        {
            blockers.Add("Venue name is required.");
            recommendations.Add("Specify a venue name or 'TBD'.");
            triggeredRules.Add("REQUIRED_FIELD:venue");
        }
        if (string.IsNullOrWhiteSpace(candidate.Address))
        {
            blockers.Add("Address is required.");
            recommendations.Add("Add a street address or location description.");
            triggeredRules.Add("REQUIRED_FIELD:address");
        }
        if (string.IsNullOrWhiteSpace(candidate.StartUtc))
        {
            blockers.Add("Start time is required.");
            recommendations.Add("Add a valid ISO 8601 start date/time.");
            triggeredRules.Add("REQUIRED_FIELD:startUtc");
        }

        // Rule: geocode confidence too low
        if (vector.Geocode < EligibilityThresholds.MinDimension)
        {
            blockers.Add($"Geocode confidence {vector.Geocode:F2} below minimum {EligibilityThresholds.MinDimension:F2}.");
            recommendations.Add("Verify or correct the address so geocoding can resolve it.");
            triggeredRules.Add("LOW_GEOCODE_CONFIDENCE");
        }

        // Rule: extraction confidence too low
        if (vector.Extraction < EligibilityThresholds.MinDimension)
        {
            blockers.Add($"Extraction confidence {vector.Extraction:F2} below minimum {EligibilityThresholds.MinDimension:F2}.");
            recommendations.Add("Review OCR/extraction results and correct any errors.");
            triggeredRules.Add("LOW_EXTRACTION_CONFIDENCE");
        }

        // Rule: temporal confidence too low
        if (vector.Temporal < EligibilityThresholds.MinDimension)
        {
            blockers.Add($"Temporal confidence {vector.Temporal:F2} below minimum {EligibilityThresholds.MinDimension:F2}.");
            recommendations.Add("Verify start/end time parses correctly to a valid UTC date.");
            triggeredRules.Add("LOW_TEMPORAL_CONFIDENCE");
        }

        // Rule: duplicate conflict — do not auto-publish ambiguous candidates
        if (vector.DupeRisk < (1.0 - EligibilityThresholds.MaxDupeRiskScore))
        {
            blockers.Add($"Unresolved duplicate conflict (dedupe match score indicates probable duplicate).");
            recommendations.Add("Resolve the duplicate before publication.");
            triggeredRules.Add("UNRESOLVED_DUPLICATE_CONFLICT");
        }

        // Rule: aggregate too low
        if (vector.Aggregate < EligibilityThresholds.ManualReview)
        {
            blockers.Add($"Aggregate confidence {vector.Aggregate:F2} below manual review threshold {EligibilityThresholds.ManualReview:F2}.");
            triggeredRules.Add("AGGREGATE_BELOW_MANUAL_THRESHOLD");
        }
    }
}

// ---------------------------------------------------------------------------
// Eligibility evaluator
// ---------------------------------------------------------------------------

public sealed class PublishEligibilityService(
    IConfidenceScoringService scoring,
    IEligibilityRuleSet ruleSet) : IPublishEligibilityService
{
    public PublishEligibilityResult Evaluate(NormalizedEventCandidate candidate, double dedupeMatchScore = 0.0)
    {
        var vector = scoring.Score(candidate, dedupeMatchScore);
        var blockers = new List<string>();
        var recommendations = new List<string>();
        var triggeredRules = new List<string>();

        ruleSet.Apply(candidate, vector, blockers, recommendations, triggeredRules);

        var outcome = DetermineOutcome(vector, blockers, triggeredRules);
        var requiresReview = outcome != EligibilityOutcome.AutoApproveEligible;

        return new PublishEligibilityResult(
            outcome, vector,
            [.. blockers], [.. recommendations], [.. triggeredRules],
            requiresReview);
    }

    private static EligibilityOutcome DetermineOutcome(
        PublishConfidenceVector vector,
        List<string> blockers,
        List<string> triggeredRules)
    {
        if (triggeredRules.Any(r => r.StartsWith("REQUIRED_FIELD")))
            return EligibilityOutcome.BlockedMissingRequiredFields;

        if (triggeredRules.Contains("UNRESOLVED_DUPLICATE_CONFLICT"))
            return EligibilityOutcome.BlockedDuplicateConflict;

        if (blockers.Count > 0)
            return EligibilityOutcome.BlockedLowConfidence;

        if (vector.Aggregate >= EligibilityThresholds.AutoApprove)
            return EligibilityOutcome.AutoApproveEligible;

        return EligibilityOutcome.ManualReviewRequired;
    }
}
