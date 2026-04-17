using WeUP.Contracts.Moderation;

namespace WeUP.Domain.Moderation;

// ---------------------------------------------------------------------------
// Core moderation domain model
// ---------------------------------------------------------------------------

/// <summary>
/// Internal representation of a moderation queue item.
/// Transport DTOs are in WeUP.Contracts.Moderation.
/// </summary>
public sealed class ModerationQueueItem
{
    public string ItemId { get; init; } = Guid.NewGuid().ToString("N");
    public ModerationItemKind Kind { get; init; }
    public ModerationItemStatus Status { get; set; } = ModerationItemStatus.Open;
    public CandidateSnapshotDto? Candidate { get; set; }
    public ProvenanceSummaryDto Provenance { get; init; } = null!;
    public ConfidenceSummaryDto Confidence { get; set; } = null!;
    public DedupeSummaryDto? DedupeMatch { get; set; }
    public IngestionJobSummaryDto? IngestionJob { get; set; }
    public string[] ReviewReasons { get; set; } = [];
    public string? AssignedReviewerId { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? LinkedEventId { get; set; }

    // Review action history — immutable entries appended only
    private readonly List<ReviewHistoryEntry> _history = [];
    public IReadOnlyList<ReviewHistoryEntry> History => _history;
    public void AppendHistory(ReviewHistoryEntry entry) => _history.Add(entry);
}

public sealed record ReviewHistoryEntry(
    string RecordId,
    string Action,
    string ActorId,
    string? Note,
    ModerationItemStatus PreviousStatus,
    ModerationItemStatus NextStatus,
    DateTimeOffset Timestamp);

// ---------------------------------------------------------------------------
// Repository interfaces
// ---------------------------------------------------------------------------

public interface IModerationQueueRepository
{
    Task<string> AddItemAsync(ModerationQueueItem item, CancellationToken ct = default);
    Task<ModerationQueueItem?> GetItemAsync(string itemId, CancellationToken ct = default);
    Task<(IReadOnlyList<ModerationQueueItem> Items, int Total)> QueryAsync(
        ModerationQueueQuery query, CancellationToken ct = default);
    Task UpdateItemAsync(ModerationQueueItem item, CancellationToken ct = default);
    Task<IReadOnlyList<ReviewHistoryEntry>> GetHistoryAsync(
        string itemId,
        int pageSize,
        string? cursor,
        CancellationToken ct = default);
    Task<IReadOnlyList<ReviewHistoryEntry>> GetAllHistoryAsync(
        int pageSize, string? cursor, CancellationToken ct = default);
    Task<int> CountTotalHistoryAsync(CancellationToken ct = default);
    Task<ModerationStatsDto> GetStatsAsync(CancellationToken ct = default);
}
