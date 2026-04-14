using WeUP.Contracts.Ingestion;

namespace WeUP.Domain.Moderation;

public enum PublishReviewState
{
    NeedsReview,
    Approved,
    Rejected,
    ChangesRequested,
}

public enum PublishEligibilityBand
{
    AutoPublishable,
    ManualReviewRequired,
    Blocked,
}

public enum PublishRecommendedAction
{
    AutoPublish,
    RouteToManualReview,
    BlockPublish,
}

public enum PublishBlockerCode
{
    MissingTitle,
    MissingVenue,
    MissingAddress,
    MissingGeo,
    MissingStartTime,
    MissingTimezone,
    MissingProvenance,
    MissingConfidence,
    LowExtractionConfidence,
    LowTemporalConfidence,
    LowGeocodeConfidence,
    LowVenueMatchConfidence,
    LowAggregateConfidence,
    UnresolvedVenue,
    GeoNotValidated,
    DuplicateConflictUnresolved,
    ManualReviewPending,
    RejectedReviewState,
    LifecycleRejected,
    LifecycleArchived,
    IncompleteEvidenceChain,
    SourceIntegrityFailed,
}

public record PublishBlocker(
    PublishBlockerCode Code,
    string Message,
    bool IsHardBlock,
    string? Field = null,
    string? Metadata = null);

public record AutoPublishPolicy(
    string PolicyId,
    string Description,
    double AutoPublishThreshold,
    double ManualReviewThreshold,
    double BlockPublishThreshold,
    double MinimumDimensionThreshold,
    IReadOnlyDictionary<string, double> DimensionMinimums,
    IReadOnlyDictionary<string, double> DimensionWeights);

public record PublishConfidenceVector(
    double Extraction,
    double Geocode,
    double Temporal,
    double VenueMatch,
    double DupeRisk,
    double SourceTrust,
    double ReviewConfidence,
    IReadOnlyDictionary<string, double>? WeightsOverride = null)
{
    public double Aggregate
    {
        get
        {
            var weights = WeightsOverride ?? DefaultWeights;

            return Extraction * weights["extraction"]
                + Geocode * weights["geocode"]
                + Temporal * weights["temporal"]
                + VenueMatch * weights["venueMatch"]
                + DupeRisk * weights["dupeRisk"]
                + SourceTrust * weights["sourceTrust"]
                + ReviewConfidence * weights["reviewConfidence"];
        }
    }

    private static readonly IReadOnlyDictionary<string, double> DefaultWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
    {
        ["extraction"] = 0.25,
        ["geocode"] = 0.20,
        ["temporal"] = 0.15,
        ["venueMatch"] = 0.10,
        ["dupeRisk"] = 0.15,
        ["sourceTrust"] = 0.10,
        ["reviewConfidence"] = 0.05,
    };
}

public record FieldCompletenessResult(
    bool IsComplete,
    string[] MissingRequiredFields,
    string[] InvalidRequiredFields,
    double CompletenessRatio,
    int RequiredCount,
    int SatisfiedCount);

public record ConfidenceGateDecision(
    PublishEligibilityBand Band,
    double Aggregate,
    bool MeetsAutoPublishThreshold,
    bool RequiresManualReview,
    bool IsBlocked,
    IReadOnlyDictionary<string, double> DimensionScores,
    string[] Explanations);

public record PublishEligibilityContext(
    NormalizedEventCandidate Candidate,
    PublishReviewState ReviewState,
    string LifecycleStatus,
    bool HasUnresolvedDedupeConflict,
    bool SourceIntegrityValid,
    bool EvidenceChainComplete,
    bool VenueResolved,
    bool GeoValidated,
    double DedupeMatchScore = 0.0,
    double ReviewConfidence = 0.0,
    PublishConfidenceVector? ConfidenceOverride = null);

public record PublishEligibilityResult(
    bool Eligible,
    PublishEligibilityBand EligibilityBand,
    PublishRecommendedAction RecommendedNextAction,
    PublishBlocker[] Blockers,
    ConfidenceGateDecision ConfidenceSummary,
    FieldCompletenessResult FieldCompletenessSummary,
    AutoPublishPolicy Policy,
    string[] Notes);

public interface IConfidenceScoringService
{
    PublishConfidenceVector Score(
        NormalizedEventCandidate candidate,
        double dedupeMatchScore = 0.0,
        double reviewConfidence = 0.0);
}

public interface IPublishEligibilityService
{
    AutoPublishPolicy GetPolicy();
    PublishEligibilityResult Evaluate(PublishEligibilityContext context);
    PublishEligibilityResult Evaluate(NormalizedEventCandidate candidate, double dedupeMatchScore = 0.0);
}
