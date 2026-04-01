using WeUP.Contracts.Ingestion;

namespace WeUP.Domain.Moderation;

// ---------------------------------------------------------------------------
// Publish eligibility outcomes — explicit enum, not a boolean
// ---------------------------------------------------------------------------

public enum EligibilityOutcome
{
    AutoApproveEligible,
    ManualReviewRequired,
    BlockedMissingRequiredFields,
    BlockedDuplicateConflict,
    BlockedLowConfidence,
    BlockedPolicyViolation,
}

// ---------------------------------------------------------------------------
// Full confidence vector — all scoring dimensions explicit
// ---------------------------------------------------------------------------

public record PublishConfidenceVector(
    double Extraction,        // OCR / field extraction quality (0–1)
    double Geocode,           // address → lat/lng resolution quality (0–1)
    double Temporal,          // start/end time parse confidence (0–1)
    double VenueMatch,        // venue name resolution against known entities (0–1)
    double DupeRisk,          // inverse of dedupe match score (0 = high risk, 1 = clean)
    double SourceTrust,       // trust factor of the originating source (0–1)
    double ReviewConfidence)  // post-review signal if a reviewer has handled this (0–1)
{
    /// <summary>
    /// Weighted aggregate. Weights are centralized here — change once, applies everywhere.
    /// extraction=0.25, geocode=0.20, temporal=0.15, venue=0.10, dupe=0.15, source=0.10, review=0.05
    /// </summary>
    public double Aggregate =>
        Extraction    * 0.25 +
        Geocode       * 0.20 +
        Temporal      * 0.15 +
        VenueMatch    * 0.10 +
        DupeRisk      * 0.15 +
        SourceTrust   * 0.10 +
        ReviewConfidence * 0.05;
}

// ---------------------------------------------------------------------------
// Eligibility result
// ---------------------------------------------------------------------------

public record PublishEligibilityResult(
    EligibilityOutcome Outcome,
    PublishConfidenceVector ConfidenceVector,
    string[] Blockers,
    string[] Recommendations,
    string[] TriggeredRules,
    bool RequiresManualReview)
{
    public bool IsEligible => Outcome == EligibilityOutcome.AutoApproveEligible;
}

// ---------------------------------------------------------------------------
// Service interfaces
// ---------------------------------------------------------------------------

public interface IConfidenceScoringService
{
    PublishConfidenceVector Score(NormalizedEventCandidate candidate, double dedupeMatchScore = 0.0);
}

public interface IPublishEligibilityService
{
    PublishEligibilityResult Evaluate(NormalizedEventCandidate candidate, double dedupeMatchScore = 0.0);
}

public interface IEligibilityRuleSet
{
    void Apply(
        NormalizedEventCandidate candidate,
        PublishConfidenceVector vector,
        List<string> blockers,
        List<string> recommendations,
        List<string> triggeredRules);
}
