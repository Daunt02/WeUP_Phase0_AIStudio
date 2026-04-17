using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Resolution;

namespace WeUP.Domain.Resolution;

public sealed record ResolutionComparisonRecord(
    string RecordId,
    string RecordType,
    string? CanonicalEventId,
    NormalizedEventCandidate Candidate,
    string[] SourceRefs,
    string[] EvidenceRefs,
    string[] ReviewRefs,
    double ExistingConfidence,
    DateTimeOffset UpdatedAtUtc,
    bool IsCandidateRecord,
    bool IsExistingEventRecord);

public sealed record MergeCommitCommand(
    string ResolutionId,
    NormalizedEventCandidate Candidate,
    DuplicateMatchCandidate BestMatch,
    MergePlan Plan,
    ResolutionDecision Decision,
    string[] MatchReasons,
    string? RequestedBy,
    DateTimeOffset RequestedAtUtc);

public sealed record MergeCommitResult(
    bool Success,
    bool RequiresManualReview,
    string Message,
    string? CanonicalEventId,
    string[] AuditTrail);

public interface IEventDuplicateDetector
{
    Task<DuplicateMatchCandidate[]> CompareAsync(
        NormalizedEventCandidate candidate,
        int maxComparisons,
        CancellationToken ct = default);
}

public interface IMergePlanner
{
    /// <summary>
    /// Generate a comprehensive, auditable merge plan for a single (candidate, canonical) pair.
    /// 
    /// This interface has been extended in WeUP.Domain.Dedupe.IMergePlanner with the full
    /// MergePlanDetail contract. This version here is legacy; use the Domain version for
    /// new integrations with complete conflict detection and field-level decision tracking.
    /// </summary>
    MergePlan CreatePlan(
        NormalizedEventCandidate incoming,
        DuplicateMatchCandidate bestMatch,
        ResolutionDecision decision);
}

public interface IEntityResolutionService
{
    Task<EntityResolutionResult> EvaluateAsync(EvaluateResolutionRequest request, CancellationToken ct = default);
    Task<MergeResolutionResponse> MergeAsync(MergeResolutionRequest request, CancellationToken ct = default);
    Task<EntityResolutionResult?> GetAsync(string resolutionId, CancellationToken ct = default);
}

public interface IEntityResolutionRepository
{
    Task<ResolutionComparisonRecord[]> GetComparisonRecordsAsync(
        NormalizedEventCandidate candidate,
        int maxComparisons,
        CancellationToken ct = default);

    Task SaveResultAsync(EntityResolutionResult result, CancellationToken ct = default);
    Task<EntityResolutionResult?> GetResultAsync(string resolutionId, CancellationToken ct = default);
    Task<MergeCommitResult> CommitMergeAsync(MergeCommitCommand command, CancellationToken ct = default);
}
