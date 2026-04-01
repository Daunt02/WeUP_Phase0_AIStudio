using WeUP.Contracts.Moderation;
using WeUP.Domain.Moderation;

namespace WeUP.Application.Moderation;

/// <summary>
/// Handles all moderator actions. Every mutation:
///  1. Validates the transition is legal.
///  2. Updates the queue item status.
///  3. Appends an immutable audit trail entry.
/// No moderation state is mutated without a corresponding audit record.
/// </summary>
public sealed class ReviewActionService(
    IModerationQueueRepository queue,
    IAuditTrailService audit) : IReviewActionService, IRollbackService
{
    public Task<ReviewActionResponse> ApproveAsync(string itemId, ApproveRequest req, CancellationToken ct = default) =>
        ExecuteActionAsync(itemId, "approve", req.ActorId, req.Note, ct: ct);

    public Task<ReviewActionResponse> RejectAsync(string itemId, RejectRequest req, CancellationToken ct = default) =>
        ExecuteActionAsync(itemId, "reject", req.ActorId, $"Reason: {req.RejectionReason}. {req.Note}", ct: ct);

    public Task<ReviewActionResponse> RequestChangesAsync(string itemId, RequestChangesRequest req, CancellationToken ct = default) =>
        ExecuteActionAsync(itemId, "request-changes", req.ActorId, req.CorrectionInstructions, ct: ct);

    public Task<ReviewActionResponse> MarkDuplicateAsync(string itemId, MarkDuplicateRequest req, CancellationToken ct = default) =>
        ExecuteActionAsync(itemId, "mark-duplicate", req.ActorId, $"Duplicate of {req.ExistingEventId}. {req.Note}", ct: ct);

    public Task<ReviewActionResponse> MergeAsync(string itemId, MergeRequest req, CancellationToken ct = default) =>
        ExecuteActionAsync(itemId, "merge", req.ActorId, $"Merged into {req.TargetEventId}. {req.Note}", ct: ct);

    public Task<ReviewActionResponse> ArchiveAsync(string itemId, ArchiveRequest req, CancellationToken ct = default) =>
        ExecuteActionAsync(itemId, "archive", req.ActorId, req.Note, ct: ct);

    public Task<ReviewActionResponse> ReopenAsync(string itemId, ReopenRequest req, CancellationToken ct = default) =>
        ExecuteActionAsync(itemId, "reopen", req.ActorId, req.Note, ct: ct);

    /// <summary>
    /// Rollback does not delete anything. It appends a "rollback" audit record
    /// and reopens the linked moderation item (or creates one if none exists).
    /// </summary>
    public async Task<ReviewActionResponse> RollbackEventAsync(string eventId, RollbackRequest req, CancellationToken ct = default)
    {
        // Find a moderation item linked to this event (by LinkedEventId)
        var (items, _) = await queue.QueryAsync(new ModerationQueueQuery(PageSize: 100), ct);
        var linked = items.FirstOrDefault(i => i.LinkedEventId == eventId);

        if (linked is not null)
        {
            // Reopen the existing moderation item
            return await ExecuteActionAsync(linked.ItemId, "reopen", req.ActorId,
                $"Rollback: {req.RollbackReason}. {req.Note}", ct: ct);
        }

        // No linked item — create a new PublishBlocked item representing the rollback
        var rollbackItem = new ModerationQueueItem
        {
            Kind = ModerationItemKind.PublishBlocked,
            Status = ModerationItemStatus.Open,
            Provenance = new ProvenanceSummaryDto(
                "rollback", eventId, null, [eventId],
                DateTimeOffset.UtcNow.ToString("O")),
            Confidence = new ConfidenceSummaryDto(0, 0, 0, 0, 0, 0, ConfidenceBucket.Low, []),
            ReviewReasons = [$"Rollback: {req.RollbackReason}"],
        };

        var newItemId = await queue.AddItemAsync(rollbackItem, ct);

        var auditEntry = BuildAuditEntry(
            newItemId, ModerationItemKind.PublishBlocked,
            "rollback", req.ActorId,
            ModerationItemStatus.Resolved, ModerationItemStatus.Open,
            $"Rollback: {req.RollbackReason}. {req.Note}",
            [eventId]);

        await audit.AppendAsync(auditEntry, ct);

        return new ReviewActionResponse(newItemId, "rollback",
            ModerationItemStatus.Resolved.ToString(), ModerationItemStatus.Open.ToString(),
            true, null);
    }

    // ---------------------------------------------------------------------------

    private async Task<ReviewActionResponse> ExecuteActionAsync(
        string itemId, string action, string actorId, string? note, CancellationToken ct)
    {
        var item = await queue.GetItemAsync(itemId, ct);
        if (item is null)
            return new ReviewActionResponse(itemId, action, "?", "?", false, "Item not found.");

        if (!ModerationTransitions.TryGetNextStatus(action, item.Status, out var nextStatus))
            return new ReviewActionResponse(itemId, action,
                item.Status.ToString(), item.Status.ToString(),
                false, $"Action '{action}' is not permitted when item is in status '{item.Status}'.");

        var prevStatus = item.Status;
        item.Status = nextStatus;
        item.AppendHistory(new ReviewHistoryEntry(action, actorId, note, prevStatus, nextStatus, DateTimeOffset.UtcNow));
        await queue.UpdateItemAsync(item, ct);

        var auditEntry = BuildAuditEntry(itemId, item.Kind, action, actorId,
            prevStatus, nextStatus, note, item.Provenance.EvidenceRefs);
        await audit.AppendAsync(auditEntry, ct);

        return new ReviewActionResponse(itemId, action,
            prevStatus.ToString(), nextStatus.ToString(), true, null);
    }

    private static AuditTrailEntry BuildAuditEntry(
        string itemId, ModerationItemKind kind, string action, string actorId,
        ModerationItemStatus prev, ModerationItemStatus next,
        string? note, string[] evidenceRefs) =>
        new(
            EntryId: Guid.NewGuid().ToString("N"),
            ItemId: itemId,
            ItemKind: kind,
            Action: action,
            ActorId: actorId,
            PreviousStatus: prev,
            NextStatus: next,
            Note: note,
            EvidenceSnapshotRefs: evidenceRefs,
            CorrelationId: Guid.NewGuid().ToString("N"),
            Timestamp: DateTimeOffset.UtcNow);
}
