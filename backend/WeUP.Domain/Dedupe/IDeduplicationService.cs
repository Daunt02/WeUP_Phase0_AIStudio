using WeUP.Contracts.Dedupe;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Events;

namespace WeUP.Domain.Dedupe;

// ---------------------------------------------------------------------------
// Match outcomes
// ---------------------------------------------------------------------------

public enum DedupeOutcome
{
    NoMatch,
    PossibleDuplicate,
    ProbableDuplicate,
    MergeIntoExisting,
    NeedsManualResolution,
}

// ---------------------------------------------------------------------------
// Scoring
// ---------------------------------------------------------------------------

public record DedupeScore(
    DedupeOutcome Outcome,
    double Score,            // 0–1 composite
    double TitleSimilarity,
    double VenueSimilarity,
    double AddressSimilarity,
    double GeoDistanceMeters,
    double TemporalOverlapMinutes,
    string[] MatchReasons);

// ---------------------------------------------------------------------------
// Field-level merge precedence
// ---------------------------------------------------------------------------

public record MergeDecision(
    string? Title,
    string? VenueName,
    string? Address,
    string? StartUtc,
    string? EndUtc,
    string? Timezone,
    string? Category,
    string? Description,
    string[]? Tags,
    string[] MergedSourceRefs,
    string[] MergeRationale);

// ---------------------------------------------------------------------------
// Dedupe result
// ---------------------------------------------------------------------------

public record DedupeResult(
    string CandidateSourceRef,
    DedupeOutcome Outcome,
    DedupeScore Score,
    string? ExistingEventId,       // null when NoMatch
    MergeDecision? MergeDecision,  // non-null when MergeIntoExisting
    bool RequiresManualReview,
    string[] ReviewReasons);

// ---------------------------------------------------------------------------
// Interfaces
// ---------------------------------------------------------------------------

/// <summary>
/// Compares an incoming candidate against existing events.
/// </summary>
public interface IDeduplicationService
{
    /// <summary>
    /// Deterministically compare a candidate against canonical events and return the best assessment.
    /// </summary>
    Task<DuplicateAssessment> AssessAsync(
        CandidateEvent candidate,
        IReadOnlyCollection<EventAggregate> canonicalEvents)
        => throw new NotSupportedException("This implementation supports only EvaluateCandidateAsync.");

    /// <summary>
    /// Legacy dedupe API retained for compatibility with the existing ingestion pipeline.
    /// </summary>
    Task<DedupeResult> EvaluateCandidateAsync(
        NormalizedEventCandidate candidate,
        CancellationToken ct = default)
        => throw new NotSupportedException("This implementation supports only AssessAsync.");
}

/// <summary>
/// Resolves entity references (venue names, addresses) against known canonical entities.
/// </summary>
public interface IEntityResolutionService
{
    Task<string?> ResolveVenueIdAsync(string venueName, string? address, CancellationToken ct = default);
    Task<string?> ResolveDistrictAsync(double lat, double lng, CancellationToken ct = default);
}

/// <summary>
/// Applies field-level merge rules when two candidates or candidate+existing are merged.
/// </summary>
public interface IMergePolicyEvaluator
{
    MergeDecision Merge(NormalizedEventCandidate incoming, NormalizedEventCandidate existing);
}
