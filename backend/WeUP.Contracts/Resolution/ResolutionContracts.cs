using WeUP.Contracts.Ingestion;

namespace WeUP.Contracts.Resolution;

public enum ResolutionDecisionType
{
    NoMatch,
    AutoMergeAllowed,
    RequiresManualReview,
}

public enum MatchSignal
{
    TitleSimilarity,
    VenueSimilarity,
    TimeOverlap,
    AddressSimilarity,
    GeoProximity,
    SourceReferenceDuplication,
}

public sealed record DuplicateMatchScore(
    double CompositeScore,
    double TitleSimilarity,
    double VenueSimilarity,
    double TimeOverlap,
    double AddressSimilarity,
    double GeoProximity,
    double SourceReferenceDuplication,
    double GeoDistanceMeters,
    double TimeDeltaMinutes,
    string[] MatchedSignals,
    string[] Explanations);

public sealed record DuplicateMatchCandidate(
    string ComparedRecordId,
    string ComparedRecordType,
    string? CanonicalEventId,
    NormalizedEventCandidate ComparedCandidate,
    DuplicateMatchScore Score,
    string[] ComparedSourceRefs,
    string[] ComparedEvidenceRefs,
    string[] ComparedReviewRefs,
    bool CandidateToCandidateComparison,
    bool CandidateToExistingComparison);

public sealed record FieldConflict(
    string FieldName,
    string? ExistingValue,
    string? IncomingValue,
    bool BlocksAutoMerge,
    string Reason);

public sealed record MergePlan(
    string CanonicalEventId,
    string? Title,
    string? VenueName,
    string? Address,
    string? StartUtc,
    string? EndUtc,
    string? Timezone,
    string? Category,
    string? Description,
    string[] Tags,
    double MergedConfidence,
    bool AutoMergeAllowed,
    string[] MergeRationale,
    string[] ManualReviewReasons,
    FieldConflict[] Conflicts,
    string[] UnionedSourceRefs,
    string[] UnionedEvidenceRefs,
    string[] PreservedReviewRefs);

public sealed record ResolutionDecision(
    string ResolutionId,
    ResolutionDecisionType DecisionType,
    bool AutoMergeAllowed,
    bool RequiresManualReview,
    string[] Reasons,
    DateTimeOffset DecidedAtUtc);

public sealed record EntityResolutionResult(
    string ResolutionId,
    NormalizedEventCandidate Candidate,
    DuplicateMatchCandidate[] Matches,
    DuplicateMatchCandidate? BestMatch,
    ResolutionDecision Decision,
    MergePlan? MergePlan,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? MergedAtUtc,
    string[] AuditTrail,
    ProvenanceEntry[]? ProvenanceEntries = null);

public sealed record EvaluateResolutionRequest(
    NormalizedEventCandidate Candidate,
    int MaxComparisons = 20);

public sealed record MergeResolutionRequest(
    string ResolutionId,
    string? RequestedBy,
    bool AllowUnsafeMerge = false);

public sealed record MergeResolutionResponse(
    string ResolutionId,
    bool Merged,
    bool RequiresManualReview,
    string Message,
    string? CanonicalEventId,
    string[] AuditTrail);
