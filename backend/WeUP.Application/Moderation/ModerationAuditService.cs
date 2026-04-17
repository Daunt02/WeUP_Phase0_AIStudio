using WeUP.Contracts.Moderation;
using WeUP.Domain.Moderation;

namespace WeUP.Application.Moderation;

public interface IModerationAuditService
{
    /// <summary>
    /// Appends a new immutable moderation history entry for the supplied queue item.
    /// The service never updates or removes prior rows; callers must append a fresh
    /// record for every moderation action.
    /// </summary>
    Task<ModerationHistoryEntry> AppendAsync(
        WeUP.Domain.Moderation.ModerationQueueItem item,
        string reviewerId,
        string actorId,
        string action,
        ModerationItemStatus previousStatus,
        ModerationItemStatus newStatus,
        string? reasonComment,
        DateTimeOffset? actionTimestampUtc = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<ModerationHistoryEntry>> GetFullHistoryByCandidateAsync(string candidateId, CancellationToken ct = default);
    Task<IReadOnlyList<ModerationHistoryEntry>> GetHistoryByReviewerAsync(string reviewerId, CancellationToken ct = default);
    Task<IReadOnlyList<ModerationHistoryEntry>> GetHistoryByEventAsync(string eventId, CancellationToken ct = default);
}

/// <summary>
/// Moderation audit history is stored in append-only form and always returned in
/// chronological order so reviewers can reconstruct the exact decision sequence.
/// </summary>
public sealed class ModerationAuditService(IModerationAuditRepository repository) : IModerationAuditService
{
    private const string CandidateMarker = "candidate-id:";

    public async Task<ModerationHistoryEntry> AppendAsync(
        WeUP.Domain.Moderation.ModerationQueueItem item,
        string reviewerId,
        string actorId,
        string action,
        ModerationItemStatus previousStatus,
        ModerationItemStatus newStatus,
        string? reasonComment,
        DateTimeOffset? actionTimestampUtc = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        var timestamp = actionTimestampUtc ?? DateTimeOffset.UtcNow;
        var entry = new ModerationHistoryEntry(
            EntryId: Guid.NewGuid().ToString("N"),
            QueueItemId: item.ItemId,
            EventId: item.LinkedEventId,
            CandidateId: ResolveCandidateId(item),
            ReviewerId: reviewerId,
            ActorId: actorId,
            Action: action,
            PreviousStatus: previousStatus,
            NewStatus: newStatus,
            ActionTimestampUtc: timestamp,
            ReasonComment: reasonComment);

        await repository.AppendAsync(entry, ct);
        return entry;
    }

    public async Task<IReadOnlyList<ModerationHistoryEntry>> GetFullHistoryByCandidateAsync(string candidateId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateId);
        var history = await repository.GetByCandidateAsync(candidateId, ct);
        return Order(history);
    }

    public async Task<IReadOnlyList<ModerationHistoryEntry>> GetHistoryByReviewerAsync(string reviewerId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewerId);
        var history = await repository.GetByReviewerAsync(reviewerId, ct);
        return Order(history);
    }

    public async Task<IReadOnlyList<ModerationHistoryEntry>> GetHistoryByEventAsync(string eventId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var history = await repository.GetByEventAsync(eventId, ct);
        return Order(history);
    }

    internal static string ResolveCandidateId(WeUP.Domain.Moderation.ModerationQueueItem item)
    {
        var explicitCandidateId = ExtractReviewReasonValue(item.ReviewReasons, CandidateMarker);
        if (!string.IsNullOrWhiteSpace(explicitCandidateId))
        {
            return explicitCandidateId;
        }

        if (!string.IsNullOrWhiteSpace(item.Candidate?.SourceRef))
        {
            return item.Candidate.SourceRef!;
        }

        if (!string.IsNullOrWhiteSpace(item.Provenance.SourceRef))
        {
            return item.Provenance.SourceRef;
        }

        return item.ItemId;
    }

    private static IReadOnlyList<ModerationHistoryEntry> Order(IReadOnlyList<ModerationHistoryEntry> history) =>
        history
            .OrderBy(h => h.ActionTimestampUtc)
            .ThenBy(h => h.EntryId, StringComparer.Ordinal)
            .ToArray();

    private static string? ExtractReviewReasonValue(IEnumerable<string> reasons, string prefix)
    {
        foreach (var reason in reasons)
        {
            if (reason.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return reason[prefix.Length..];
            }
        }

        return null;
    }
}