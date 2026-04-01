using WeUP.Contracts.Moderation;
using WeUP.Domain.Moderation;

namespace WeUP.Application.Moderation;

/// <summary>
/// Application service for moderation queue queries, stats, and review history.
/// Mutation (actions) is handled by ReviewActionService.
/// </summary>
public interface IModerationQueueService
{
    Task<ModerationQueueResponse> GetQueueAsync(ModerationQueueQuery query, CancellationToken ct = default);
    Task<ModerationQueueItemDto?> GetItemAsync(string itemId, CancellationToken ct = default);
    Task<ModerationStatsDto> GetStatsAsync(CancellationToken ct = default);
    Task<ReviewHistoryResponse> GetReviewHistoryAsync(int pageSize, string? cursor, CancellationToken ct = default);
}

public sealed class ModerationQueueService(IModerationQueueRepository queue) : IModerationQueueService
{
    public async Task<ModerationQueueResponse> GetQueueAsync(ModerationQueueQuery query, CancellationToken ct = default)
    {
        var (items, total) = await queue.QueryAsync(query, ct);

        var dtos = items.Select(ToDto).ToArray();
        var nextCursor = dtos.Length == query.PageSize && dtos.Length > 0
            ? EncodeCursor(items[^1].CreatedAt)
            : null;

        return new ModerationQueueResponse(dtos, total, nextCursor, null);
    }

    public async Task<ModerationQueueItemDto?> GetItemAsync(string itemId, CancellationToken ct = default)
    {
        var item = await queue.GetItemAsync(itemId, ct);
        return item is null ? null : ToDto(item);
    }

    public Task<ModerationStatsDto> GetStatsAsync(CancellationToken ct = default) =>
        queue.GetStatsAsync(ct);

    public async Task<ReviewHistoryResponse> GetReviewHistoryAsync(int pageSize, string? cursor, CancellationToken ct = default)
    {
        var history = await queue.GetAllHistoryAsync(pageSize, cursor, ct);
        var total = await queue.CountTotalHistoryAsync(ct);

        var items = history.Select(h => new ReviewHistoryItemDto(
            ItemId: "(batch)",
            ItemKind: "unknown",
            Action: h.Action,
            ActorId: h.ActorId,
            Note: h.Note,
            PreviousStatus: h.PreviousStatus.ToString(),
            NextStatus: h.NextStatus.ToString(),
            Timestamp: h.Timestamp.ToString("O"))).ToArray();

        var nextCursor = history.Count == pageSize && history.Count > 0
            ? EncodeCursor(history[^1].Timestamp)
            : null;

        return new ReviewHistoryResponse(items, total, nextCursor);
    }

    private static ModerationQueueItemDto ToDto(ModerationQueueItem i) =>
        new(i.ItemId, i.Kind, i.Status, i.Candidate, i.Provenance, i.Confidence,
            i.DedupeMatch, i.IngestionJob, i.ReviewReasons, i.AssignedReviewerId,
            i.CreatedAt.ToString("O"), i.UpdatedAt.ToString("O"));

    private static string EncodeCursor(DateTimeOffset dt) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dt.UtcTicks.ToString()));
}
