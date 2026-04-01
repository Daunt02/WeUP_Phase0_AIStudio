using WeUP.Contracts.Moderation;

namespace WeUP.Domain.Moderation;

// ---------------------------------------------------------------------------
// Legal transitions for moderation items
// ---------------------------------------------------------------------------

public static class ModerationTransitions
{
    /// <summary>
    /// Maps (action, currentStatus) → nextStatus.
    /// Any absent combination is an illegal transition.
    /// </summary>
    private static readonly Dictionary<(string Action, ModerationItemStatus From), ModerationItemStatus> _table = new()
    {
        [("approve",          ModerationItemStatus.Open)]     = ModerationItemStatus.Resolved,
        [("approve",          ModerationItemStatus.InReview)] = ModerationItemStatus.Resolved,
        [("reject",           ModerationItemStatus.Open)]     = ModerationItemStatus.Resolved,
        [("reject",           ModerationItemStatus.InReview)] = ModerationItemStatus.Resolved,
        [("request-changes",  ModerationItemStatus.Open)]     = ModerationItemStatus.InReview,
        [("request-changes",  ModerationItemStatus.InReview)] = ModerationItemStatus.InReview,
        [("mark-duplicate",   ModerationItemStatus.Open)]     = ModerationItemStatus.Resolved,
        [("mark-duplicate",   ModerationItemStatus.InReview)] = ModerationItemStatus.Resolved,
        [("merge",            ModerationItemStatus.Open)]     = ModerationItemStatus.Resolved,
        [("merge",            ModerationItemStatus.InReview)] = ModerationItemStatus.Resolved,
        [("archive",          ModerationItemStatus.Open)]     = ModerationItemStatus.Closed,
        [("archive",          ModerationItemStatus.InReview)] = ModerationItemStatus.Closed,
        [("archive",          ModerationItemStatus.Resolved)] = ModerationItemStatus.Closed,
        [("reopen",           ModerationItemStatus.Resolved)] = ModerationItemStatus.Open,
        [("reopen",           ModerationItemStatus.Closed)]   = ModerationItemStatus.Open,
    };

    public static bool TryGetNextStatus(
        string action,
        ModerationItemStatus current,
        out ModerationItemStatus next)
    {
        return _table.TryGetValue((action, current), out next);
    }
}

// ---------------------------------------------------------------------------
// Audit trail entity (immutable log entry)
// ---------------------------------------------------------------------------

public sealed record AuditTrailEntry(
    string EntryId,
    string ItemId,
    ModerationItemKind ItemKind,
    string Action,
    string ActorId,
    ModerationItemStatus PreviousStatus,
    ModerationItemStatus NextStatus,
    string? Note,
    string[] EvidenceSnapshotRefs,
    string CorrelationId,
    DateTimeOffset Timestamp);

// ---------------------------------------------------------------------------
// Service interfaces
// ---------------------------------------------------------------------------

public interface IReviewActionService
{
    Task<ReviewActionResponse> ApproveAsync(string itemId, ApproveRequest request, CancellationToken ct = default);
    Task<ReviewActionResponse> RejectAsync(string itemId, RejectRequest request, CancellationToken ct = default);
    Task<ReviewActionResponse> RequestChangesAsync(string itemId, RequestChangesRequest request, CancellationToken ct = default);
    Task<ReviewActionResponse> MarkDuplicateAsync(string itemId, MarkDuplicateRequest request, CancellationToken ct = default);
    Task<ReviewActionResponse> MergeAsync(string itemId, MergeRequest request, CancellationToken ct = default);
    Task<ReviewActionResponse> ArchiveAsync(string itemId, ArchiveRequest request, CancellationToken ct = default);
    Task<ReviewActionResponse> ReopenAsync(string itemId, ReopenRequest request, CancellationToken ct = default);
}

public interface IRollbackService
{
    /// <summary>
    /// Rolls back a published/resolved event back to review pending.
    /// Does NOT delete — appends to audit trail and reopens moderation item.
    /// </summary>
    Task<ReviewActionResponse> RollbackEventAsync(string eventId, RollbackRequest request, CancellationToken ct = default);
}

public interface IAuditTrailService
{
    Task AppendAsync(AuditTrailEntry entry, CancellationToken ct = default);
    Task<(IReadOnlyList<AuditTrailEntry> Entries, int Total)> GetEntriesAsync(
        int pageSize, string? cursor, CancellationToken ct = default);
}
