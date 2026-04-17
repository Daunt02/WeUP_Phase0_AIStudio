using WeUP.Contracts.Moderation;

namespace WeUP.Domain.Moderation;

/// <summary>
/// Immutable moderation audit record.
/// Entries are append-only and must never be mutated after persistence so that
/// moderation decisions remain reconstructable for compliance review.
/// </summary>
public sealed record ModerationHistoryEntry(
    string EntryId,
    string QueueItemId,
    string? EventId,
    string CandidateId,
    string ReviewerId,
    string ActorId,
    string Action,
    ModerationItemStatus PreviousStatus,
    ModerationItemStatus NewStatus,
    DateTimeOffset ActionTimestampUtc,
    string? ReasonComment);

/// <summary>
/// Persistence contract for immutable moderation history.
/// Only append and read operations are exposed; update/delete semantics are
/// intentionally absent to preserve a defensible audit trail.
/// </summary>
public interface IModerationAuditRepository
{
    Task AppendAsync(ModerationHistoryEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<ModerationHistoryEntry>> GetByCandidateAsync(string candidateId, CancellationToken ct = default);
    Task<IReadOnlyList<ModerationHistoryEntry>> GetByReviewerAsync(string reviewerId, CancellationToken ct = default);
    Task<IReadOnlyList<ModerationHistoryEntry>> GetByEventAsync(string eventId, CancellationToken ct = default);
}