using WeUP.Contracts.Moderation;
using WeUP.Domain.Moderation;
using ContractQueueItem = WeUP.Contracts.Moderation.ModerationQueueItem;
using DomainQueueItem = WeUP.Domain.Moderation.ModerationQueueItem;

namespace WeUP.Application.Moderation;

public interface IModerationQueueService
{
    Task<ModerationQueueResponse> GetQueueAsync(ModerationQueueFilter filter, CancellationToken ct = default);
    Task<ModerationQueueResponse> GetQueueAsync(ModerationQueueQuery query, CancellationToken ct = default);
    Task<ContractQueueItem?> GetItemAsync(string itemId, CancellationToken ct = default);
    Task<ModerationStatsDto> GetStatsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReviewAuditRecord>> GetReviewHistoryAsync(string itemId, int pageSize, string? cursor, CancellationToken ct = default);
}

public sealed class ModerationQueueService(IModerationQueueRepository queue) : IModerationQueueService
{
    public async Task<ModerationQueueResponse> GetQueueAsync(ModerationQueueFilter filter, CancellationToken ct = default)
    {
        var legacy = new ModerationQueueQuery(
            Status: filter.Status,
            Kind: filter.Kind,
            SourceKind: filter.SourceKind,
            ReviewReason: filter.ReviewReason,
            ConfidenceBucket: filter.ConfidenceBucket,
            MinDuplicateSeverity: filter.MinDuplicateSeverity,
            AssignedReviewerId: filter.AssignedReviewerId,
            IngestionJobId: filter.IngestionJobId,
            AfterUtc: filter.AfterUtc,
            BeforeUtc: filter.BeforeUtc,
            PageSize: filter.PageSize,
            Cursor: filter.Cursor);

        var response = await GetQueueAsync(legacy, ct);

        IEnumerable<ContractQueueItem> filtered = response.Items;

        if (filter.ReviewStatus.HasValue)
            filtered = filtered.Where(i => i.ReviewStatus == filter.ReviewStatus.Value);

        if (filter.MinConfidence.HasValue)
            filtered = filtered.Where(i => i.Confidence >= filter.MinConfidence.Value);

        if (filter.MaxConfidence.HasValue)
            filtered = filtered.Where(i => i.Confidence <= filter.MaxConfidence.Value);

        var materialized = filtered.ToArray();
        return new ModerationQueueResponse(materialized, materialized.Length, response.NextCursor, response.PreviousCursor);
    }

    public async Task<ModerationQueueResponse> GetQueueAsync(ModerationQueueQuery query, CancellationToken ct = default)
    {
        var (items, total) = await queue.QueryAsync(query, ct);

        var queueItems = items.Select(ToQueueItem).ToArray();
        var nextCursor = queueItems.Length == query.PageSize && queueItems.Length > 0
            ? EncodeCursor(items[^1].CreatedAt)
            : null;

        return new ModerationQueueResponse(queueItems, total, nextCursor, null);
    }

    public async Task<ContractQueueItem?> GetItemAsync(string itemId, CancellationToken ct = default)
    {
        var item = await queue.GetItemAsync(itemId, ct);
        return item is null ? null : ToQueueItem(item);
    }

    public Task<ModerationStatsDto> GetStatsAsync(CancellationToken ct = default) =>
        queue.GetStatsAsync(ct);

    public async Task<IReadOnlyList<ReviewAuditRecord>> GetReviewHistoryAsync(
        string itemId,
        int pageSize,
        string? cursor,
        CancellationToken ct = default)
    {
        var history = await queue.GetHistoryAsync(itemId, pageSize, cursor, ct);
        var item = await queue.GetItemAsync(itemId, ct);

        if (item is null)
        {
            return [];
        }

        return history
            .OrderByDescending(h => h.Timestamp)
            .Select(h => new ReviewAuditRecord(
                RecordId: h.RecordId,
                QueueItemId: itemId,
                Decision: h.Action,
                ActorId: h.ActorId,
                PreviousReviewStatus: ToReviewStatus(h.Action, h.PreviousStatus),
                NewReviewStatus: ToReviewStatus(h.Action, h.NextStatus),
                Comment: h.Note,
                Reasons: item.ReviewReasons,
                TimestampUtc: h.Timestamp.ToString("O"),
                CorrelationId: null,
                LifecycleFrom: LifecycleFromReviewStatus(ToReviewStatus(h.Action, h.PreviousStatus)),
                LifecycleTo: LifecycleFromReviewStatus(ToReviewStatus(h.Action, h.NextStatus))))
            .ToArray();
    }

    private static ContractQueueItem ToQueueItem(DomainQueueItem item)
    {
        var reviewStatus = ResolveReviewStatus(item);
        var confidence = item.Confidence.Aggregate;
        var blockerReasons = item.ReviewReasons
            .Concat(item.Confidence.ReviewBlockers)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ContractQueueItem(
            ItemId: item.ItemId,
            Kind: item.Kind,
            Status: item.Status,
            ReviewStatus: reviewStatus,
            EventId: item.LinkedEventId,
            CandidateId: item.IngestionJob?.JobId,
            SourceKind: item.Provenance.SourceKind,
            Confidence: confidence,
            BlockerReasons: blockerReasons,
            EvidenceAvailable: item.Provenance.EvidenceRefs.Length > 0,
            Urgency: DetermineUrgency(item, confidence, blockerReasons),
            CreatedAt: item.CreatedAt.ToString("O"),
            UpdatedAt: item.UpdatedAt.ToString("O"),
            Candidate: item.Candidate,
            Provenance: item.Provenance,
            ConfidenceSummary: item.Confidence,
            DedupeMatch: item.DedupeMatch,
            IngestionJob: item.IngestionJob,
            ReviewReasons: item.ReviewReasons,
            AssignedReviewerId: item.AssignedReviewerId);
    }

    private static ModerationReviewUrgency DetermineUrgency(
        WeUP.Domain.Moderation.ModerationQueueItem item,
        double confidence,
        string[] blockerReasons)
    {
        if (item.Kind == ModerationItemKind.IngestionFailure)
            return ModerationReviewUrgency.Critical;
        if (blockerReasons.Any(r => r.Contains("duplicate", StringComparison.OrdinalIgnoreCase)))
            return ModerationReviewUrgency.High;
        if (confidence < 0.55)
            return ModerationReviewUrgency.High;
        if (confidence < 0.75)
            return ModerationReviewUrgency.Normal;
        return ModerationReviewUrgency.Low;
    }

    private static ModerationReviewStatus ResolveReviewStatus(WeUP.Domain.Moderation.ModerationQueueItem item)
    {
        var lastAction = item.History.LastOrDefault()?.Action;
        return ToReviewStatus(lastAction, item.Status);
    }

    private static ModerationReviewStatus ToReviewStatus(string? action, ModerationItemStatus status)
    {
        if (string.Equals(action, "approve", StringComparison.OrdinalIgnoreCase))
            return ModerationReviewStatus.APPROVED;
        if (string.Equals(action, "reject", StringComparison.OrdinalIgnoreCase))
            return ModerationReviewStatus.REJECTED;
        if (string.Equals(action, "request-changes", StringComparison.OrdinalIgnoreCase))
            return ModerationReviewStatus.CHANGES_REQUESTED;

        return status switch
        {
            ModerationItemStatus.Resolved => ModerationReviewStatus.APPROVED,
            _ => ModerationReviewStatus.NEEDS_REVIEW,
        };
    }

    internal static string LifecycleFromReviewStatus(ModerationReviewStatus status) => status switch
    {
        ModerationReviewStatus.APPROVED => "APPROVED",
        ModerationReviewStatus.REJECTED => "REJECTED",
        _ => "NEEDS_REVIEW",
    };

    private static string EncodeCursor(DateTimeOffset dt) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dt.UtcTicks.ToString()));
}
