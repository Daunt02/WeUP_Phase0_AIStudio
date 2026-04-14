using WeUP.Contracts.Ingestion;
using WeUP.Domain.Moderation;

namespace WeUP.Application.Moderation;

public sealed class ConfidenceScoringService : IConfidenceScoringService
{
    public PublishConfidenceVector Score(
        NormalizedEventCandidate candidate,
        double dedupeMatchScore = 0.0,
        double reviewConfidence = 0.0)
    {
        var sourceTrust = candidate.SourceKind switch
        {
            "manual_submission" or "ManualSubmission" => PublishEligibilityPolicies.Phase0.SourceTrustManual,
            "flyer_upload" or "FlyerUpload" => PublishEligibilityPolicies.Phase0.SourceTrustFlyer,
            "pasted_url" or "PastedUrl" => PublishEligibilityPolicies.Phase0.SourceTrustLink,
            "venue_page" or "VenuePage" => PublishEligibilityPolicies.Phase0.SourceTrustVenue,
            _ => PublishEligibilityPolicies.Phase0.SourceTrustDefault,
        };

        // DupeRisk is inverse of dedupe match score: a 0.9 match is 0.1 dedupe safety.
        var dupeRisk = Math.Max(0.0, 1.0 - dedupeMatchScore);

        return new PublishConfidenceVector(
            Extraction: candidate.ExtractionConfidence,
            Geocode: candidate.GeocodeConfidence,
            Temporal: candidate.TemporalConfidence,
            VenueMatch: 0.50,
            DupeRisk: dupeRisk,
            SourceTrust: sourceTrust,
            ReviewConfidence: reviewConfidence,
            WeightsOverride: PublishEligibilityPolicies.Phase0.Policy.DimensionWeights);
    }
}

internal static class PublishEligibilityPolicies
{
    internal static class Phase0
    {
        public const double SourceTrustManual = 0.95;
        public const double SourceTrustLink = 0.60;
        public const double SourceTrustFlyer = 0.70;
        public const double SourceTrustVenue = 0.75;
        public const double SourceTrustDefault = 0.50;

        public static readonly AutoPublishPolicy Policy = new(
            PolicyId: "phase0.publish-gate.v1",
            Description: "Phase 0 deterministic publish gate: confidence, completeness, provenance, review, and dedupe safeguards.",
            AutoPublishThreshold: 0.85,
            ManualReviewThreshold: 0.55,
            BlockPublishThreshold: 0.40,
            MinimumDimensionThreshold: 0.50,
            DimensionMinimums: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["extraction"] = 0.50,
                ["geocode"] = 0.50,
                ["temporal"] = 0.50,
                ["venueMatch"] = 0.45,
                ["dupeRisk"] = 0.45,
                ["sourceTrust"] = 0.50,
            },
            DimensionWeights: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["extraction"] = 0.25,
                ["geocode"] = 0.20,
                ["temporal"] = 0.15,
                ["venueMatch"] = 0.10,
                ["dupeRisk"] = 0.15,
                ["sourceTrust"] = 0.10,
                ["reviewConfidence"] = 0.05,
            });
    }
}

public sealed class PublishEligibilityService(IConfidenceScoringService scoring) : IPublishEligibilityService
{
    public AutoPublishPolicy GetPolicy() => PublishEligibilityPolicies.Phase0.Policy;

    public PublishEligibilityResult Evaluate(NormalizedEventCandidate candidate, double dedupeMatchScore = 0.0)
    {
        var context = new PublishEligibilityContext(
            Candidate: candidate,
            ReviewState: PublishReviewState.NeedsReview,
            LifecycleStatus: "NEEDS_REVIEW",
            HasUnresolvedDedupeConflict: dedupeMatchScore >= 0.55,
            SourceIntegrityValid: !string.IsNullOrWhiteSpace(candidate.SourceKind) && !string.IsNullOrWhiteSpace(candidate.SourceRef),
            EvidenceChainComplete: candidate.EvidenceRefs is { Length: > 0 },
            VenueResolved: !string.IsNullOrWhiteSpace(candidate.VenueName),
            GeoValidated: !string.IsNullOrWhiteSpace(candidate.Address) && candidate.GeocodeConfidence >= PublishEligibilityPolicies.Phase0.Policy.MinimumDimensionThreshold,
            DedupeMatchScore: dedupeMatchScore,
            ReviewConfidence: 0.0,
            ConfidenceOverride: null);

        return Evaluate(context);
    }

    public PublishEligibilityResult Evaluate(PublishEligibilityContext context)
    {
        var policy = GetPolicy();
        var confidence = context.ConfidenceOverride ??
            scoring.Score(context.Candidate, context.DedupeMatchScore, context.ReviewConfidence);

        var blockers = new List<PublishBlocker>();
        var notes = new List<string>();

        var completeness = EvaluateFieldCompleteness(context, blockers);
        ApplyConfidenceRules(context, confidence, policy, blockers, notes);
        ApplyStateRules(context, blockers);

        var confidenceDecision = BuildConfidenceDecision(confidence, policy, blockers, notes);
        var hasHardBlockers = blockers.Any(b => b.IsHardBlock);
        var hasPendingManualReview = blockers.Any(b => b.Code == PublishBlockerCode.ManualReviewPending);

        var eligible = !hasHardBlockers && !hasPendingManualReview &&
            (confidenceDecision.Band == PublishEligibilityBand.AutoPublishable || context.ReviewState == PublishReviewState.Approved);

        var eligibilityBand = hasHardBlockers
            ? PublishEligibilityBand.Blocked
            : (hasPendingManualReview || confidenceDecision.Band == PublishEligibilityBand.ManualReviewRequired
                ? PublishEligibilityBand.ManualReviewRequired
                : PublishEligibilityBand.AutoPublishable);

        var recommendedAction = eligibilityBand switch
        {
            PublishEligibilityBand.AutoPublishable => PublishRecommendedAction.AutoPublish,
            PublishEligibilityBand.ManualReviewRequired => PublishRecommendedAction.RouteToManualReview,
            _ => PublishRecommendedAction.BlockPublish,
        };

        return new PublishEligibilityResult(
            Eligible: eligible,
            EligibilityBand: eligibilityBand,
            RecommendedNextAction: recommendedAction,
            Blockers: blockers.ToArray(),
            ConfidenceSummary: confidenceDecision,
            FieldCompletenessSummary: completeness,
            Policy: policy,
            Notes: notes.ToArray());
    }

    private static FieldCompletenessResult EvaluateFieldCompleteness(
        PublishEligibilityContext context,
        List<PublishBlocker> blockers)
    {
        var missing = new List<string>();
        var invalid = new List<string>();

        RequireText(context.Candidate.Title, "title", PublishBlockerCode.MissingTitle, blockers, missing);
        RequireText(context.Candidate.VenueName, "venue", PublishBlockerCode.MissingVenue, blockers, missing);
        RequireText(context.Candidate.Address, "address", PublishBlockerCode.MissingAddress, blockers, missing);
        RequireText(context.Candidate.StartUtc, "startTime", PublishBlockerCode.MissingStartTime, blockers, missing);
        RequireText(context.Candidate.Timezone, "timezone", PublishBlockerCode.MissingTimezone, blockers, missing);

        if (!context.GeoValidated)
        {
            missing.Add("geo");
            blockers.Add(new PublishBlocker(
                PublishBlockerCode.MissingGeo,
                "Missing or invalid geo validation.",
                IsHardBlock: true,
                Field: "geo"));
        }

        if (!context.SourceIntegrityValid)
        {
            missing.Add("provenance");
            blockers.Add(new PublishBlocker(
                PublishBlockerCode.MissingProvenance,
                "Source provenance is missing or invalid.",
                IsHardBlock: true,
                Field: "provenance"));
        }

        if (double.IsNaN(context.Candidate.ExtractionConfidence)
            || double.IsNaN(context.Candidate.GeocodeConfidence)
            || double.IsNaN(context.Candidate.TemporalConfidence))
        {
            invalid.Add("confidence");
            blockers.Add(new PublishBlocker(
                PublishBlockerCode.MissingConfidence,
                "Confidence signal is missing or invalid.",
                IsHardBlock: true,
                Field: "confidence"));
        }

        const int requiredCount = 8;
        var satisfiedCount = requiredCount - missing.Count - invalid.Count;
        var completenessRatio = requiredCount == 0 ? 1.0 : (double)satisfiedCount / requiredCount;

        return new FieldCompletenessResult(
            IsComplete: missing.Count == 0 && invalid.Count == 0,
            MissingRequiredFields: missing.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            InvalidRequiredFields: invalid.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            CompletenessRatio: Math.Round(completenessRatio, 4),
            RequiredCount: requiredCount,
            SatisfiedCount: Math.Max(0, satisfiedCount));
    }

    private static void ApplyConfidenceRules(
        PublishEligibilityContext context,
        PublishConfidenceVector confidence,
        AutoPublishPolicy policy,
        List<PublishBlocker> blockers,
        List<string> notes)
    {
        AddDimensionBlocker(
            score: confidence.Extraction,
            key: "extraction",
            warningCode: PublishBlockerCode.LowExtractionConfidence,
            hardMessage: "Extraction confidence is critically low.",
            warningMessage: "Extraction confidence is below the publish threshold.",
            policy,
            blockers);

        AddDimensionBlocker(
            score: confidence.Geocode,
            key: "geocode",
            warningCode: PublishBlockerCode.LowGeocodeConfidence,
            hardMessage: "Geo confidence is critically low.",
            warningMessage: "Geo confidence is below the publish threshold.",
            policy,
            blockers);

        AddDimensionBlocker(
            score: confidence.Temporal,
            key: "temporal",
            warningCode: PublishBlockerCode.LowTemporalConfidence,
            hardMessage: "Temporal confidence is critically low.",
            warningMessage: "Temporal confidence is below the publish threshold.",
            policy,
            blockers);

        AddDimensionBlocker(
            score: confidence.VenueMatch,
            key: "venueMatch",
            warningCode: PublishBlockerCode.LowVenueMatchConfidence,
            hardMessage: "Venue resolution confidence is critically low.",
            warningMessage: "Venue resolution confidence is below preferred minimum.",
            policy,
            blockers);

        if (confidence.Aggregate < policy.BlockPublishThreshold)
        {
            blockers.Add(new PublishBlocker(
                PublishBlockerCode.LowAggregateConfidence,
                $"Aggregate confidence {confidence.Aggregate:F2} is below hard block threshold {policy.BlockPublishThreshold:F2}.",
                IsHardBlock: true,
                Field: "confidence.aggregate"));
        }
        else if (confidence.Aggregate < policy.ManualReviewThreshold)
        {
            blockers.Add(new PublishBlocker(
                PublishBlockerCode.LowAggregateConfidence,
                $"Aggregate confidence {confidence.Aggregate:F2} requires manual review (threshold {policy.ManualReviewThreshold:F2}).",
                IsHardBlock: false,
                Field: "confidence.aggregate"));
        }

        if (!context.VenueResolved)
        {
            blockers.Add(new PublishBlocker(
                PublishBlockerCode.UnresolvedVenue,
                "Venue is unresolved and must be confirmed before publish.",
                IsHardBlock: false,
                Field: "venue"));
        }

        if (!context.GeoValidated)
        {
            blockers.Add(new PublishBlocker(
                PublishBlockerCode.GeoNotValidated,
                "Geo is not validated.",
                IsHardBlock: true,
                Field: "geo"));
        }

        if (context.HasUnresolvedDedupeConflict)
        {
            blockers.Add(new PublishBlocker(
                PublishBlockerCode.DuplicateConflictUnresolved,
                "Duplicate conflict unresolved.",
                IsHardBlock: true,
                Field: "dedupe",
                Metadata: $"matchScore={context.DedupeMatchScore:F2}"));
        }

        if (!context.EvidenceChainComplete)
        {
            blockers.Add(new PublishBlocker(
                PublishBlockerCode.IncompleteEvidenceChain,
                "Evidence chain is incomplete for this candidate.",
                IsHardBlock: true,
                Field: "evidence"));
        }

        if (!context.SourceIntegrityValid)
        {
            blockers.Add(new PublishBlocker(
                PublishBlockerCode.SourceIntegrityFailed,
                "Source integrity checks failed.",
                IsHardBlock: true,
                Field: "provenance"));
        }

        notes.Add($"Confidence aggregate={confidence.Aggregate:F2}");
        notes.Add($"Thresholds auto={policy.AutoPublishThreshold:F2} review={policy.ManualReviewThreshold:F2} block={policy.BlockPublishThreshold:F2}");
    }

    private static void ApplyStateRules(PublishEligibilityContext context, List<PublishBlocker> blockers)
    {
        if (context.ReviewState == PublishReviewState.Rejected)
        {
            blockers.Add(new PublishBlocker(
                PublishBlockerCode.RejectedReviewState,
                "Review state is REJECTED.",
                IsHardBlock: true,
                Field: "reviewState"));
        }

        if (context.ReviewState == PublishReviewState.NeedsReview || context.ReviewState == PublishReviewState.ChangesRequested)
        {
            blockers.Add(new PublishBlocker(
                PublishBlockerCode.ManualReviewPending,
                "Manual review is still pending.",
                IsHardBlock: false,
                Field: "reviewState"));
        }

        if (string.Equals(context.LifecycleStatus, "REJECTED", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add(new PublishBlocker(
                PublishBlockerCode.LifecycleRejected,
                "Lifecycle status is REJECTED.",
                IsHardBlock: true,
                Field: "lifecycleStatus"));
        }

        if (string.Equals(context.LifecycleStatus, "ARCHIVED", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add(new PublishBlocker(
                PublishBlockerCode.LifecycleArchived,
                "Lifecycle status is ARCHIVED.",
                IsHardBlock: true,
                Field: "lifecycleStatus"));
        }
    }

    private static ConfidenceGateDecision BuildConfidenceDecision(
        PublishConfidenceVector confidence,
        AutoPublishPolicy policy,
        List<PublishBlocker> blockers,
        List<string> notes)
    {
        var aggregate = confidence.Aggregate;

        var band = aggregate >= policy.AutoPublishThreshold
            ? PublishEligibilityBand.AutoPublishable
            : aggregate >= policy.ManualReviewThreshold
                ? PublishEligibilityBand.ManualReviewRequired
                : PublishEligibilityBand.Blocked;

        var isBlocked = blockers.Any(b => b.IsHardBlock) || band == PublishEligibilityBand.Blocked;
        var requiresReview = band == PublishEligibilityBand.ManualReviewRequired || blockers.Any(b => b.Code == PublishBlockerCode.ManualReviewPending);

        var dimensionScores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["extraction"] = confidence.Extraction,
            ["geocode"] = confidence.Geocode,
            ["temporal"] = confidence.Temporal,
            ["venueMatch"] = confidence.VenueMatch,
            ["dupeRisk"] = confidence.DupeRisk,
            ["sourceTrust"] = confidence.SourceTrust,
            ["reviewConfidence"] = confidence.ReviewConfidence,
            ["aggregate"] = aggregate,
        };

        notes.Add($"Confidence band={band}");

        return new ConfidenceGateDecision(
            Band: band,
            Aggregate: aggregate,
            MeetsAutoPublishThreshold: aggregate >= policy.AutoPublishThreshold,
            RequiresManualReview: requiresReview,
            IsBlocked: isBlocked,
            DimensionScores: dimensionScores,
            Explanations: blockers.Select(b => b.Message).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static void AddDimensionBlocker(
        double score,
        string key,
        PublishBlockerCode warningCode,
        string hardMessage,
        string warningMessage,
        AutoPublishPolicy policy,
        List<PublishBlocker> blockers)
    {
        if (score < policy.BlockPublishThreshold)
        {
            blockers.Add(new PublishBlocker(
                warningCode,
                $"{hardMessage} score={score:F2}.",
                IsHardBlock: true,
                Field: key));
            return;
        }

        if (policy.DimensionMinimums.TryGetValue(key, out var min) && score < min)
        {
            blockers.Add(new PublishBlocker(
                warningCode,
                $"{warningMessage} score={score:F2}, minimum={min:F2}.",
                IsHardBlock: false,
                Field: key));
        }
    }

    private static void RequireText(
        string? value,
        string field,
        PublishBlockerCode code,
        List<PublishBlocker> blockers,
        List<string> missing)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        missing.Add(field);
        blockers.Add(new PublishBlocker(
            code,
            $"Required field '{field}' is missing.",
            IsHardBlock: true,
            Field: field));
    }
}
