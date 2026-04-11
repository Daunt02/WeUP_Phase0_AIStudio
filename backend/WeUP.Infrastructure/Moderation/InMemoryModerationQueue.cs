using System.Collections.Concurrent;
using WeUP.Contracts.Moderation;
using WeUP.Domain.Moderation;
using WeUP.Infrastructure.Seed;

namespace WeUP.Infrastructure.Moderation;

/// <summary>
/// Thread-safe in-memory moderation queue.
/// Replaced by an EF-backed implementation when the database is provisioned.
/// </summary>
public sealed class InMemoryModerationQueue : IModerationQueueRepository
{
    private readonly ConcurrentDictionary<string, ModerationQueueItem> _items = new();

    public void Reset(Phase0SeedDataset dataset)
    {
        _items.Clear();

        foreach (var item in dataset.ModerationItems)
        {
            var seeded = new ModerationQueueItem
            {
                ItemId = item.ItemId,
                Kind = Enum.Parse<ModerationItemKind>(item.Kind, true),
                Status = Enum.Parse<ModerationItemStatus>(item.Status, true),
                Candidate = item.Candidate is null
                    ? null
                    : new CandidateSnapshotDto(
                        item.Candidate.Title,
                        item.Candidate.VenueName,
                        item.Candidate.Address,
                        item.Candidate.StartUtc,
                        item.Candidate.EndUtc,
                        item.Candidate.Timezone,
                        item.Candidate.Category,
                        item.Candidate.Description,
                        item.Candidate.Tags,
                        item.Candidate.SourceKind,
                        item.Candidate.SourceRef),
                Provenance = new ProvenanceSummaryDto(
                    item.Provenance.SourceKind,
                    item.Provenance.SourceRef,
                    item.Provenance.IngestionJobId,
                    item.Provenance.EvidenceRefs,
                    item.Provenance.SubmittedAt),
                Confidence = new ConfidenceSummaryDto(
                    item.Confidence.Extraction,
                    item.Confidence.Geocode,
                    item.Confidence.Temporal,
                    item.Confidence.VenueMatch,
                    item.Confidence.DupeRisk,
                    item.Confidence.Aggregate,
                    Enum.Parse<ConfidenceBucket>(item.Confidence.Bucket, true),
                    item.Confidence.ReviewBlockers),
                DedupeMatch = item.DedupeMatch is null
                    ? null
                    : new DedupeSummaryDto(
                        item.DedupeMatch.ExistingEventId,
                        item.DedupeMatch.ExistingEventTitle,
                        item.DedupeMatch.MatchScore,
                        Enum.Parse<DuplicateSeverity>(item.DedupeMatch.Severity, true),
                        item.DedupeMatch.MatchReasons),
                IngestionJob = item.IngestionJob is null
                    ? null
                    : new IngestionJobSummaryDto(
                        item.IngestionJob.JobId,
                        item.IngestionJob.Status,
                        item.IngestionJob.FailureReason,
                        item.IngestionJob.CreatedAt,
                        item.IngestionJob.CompletedAt),
                ReviewReasons = item.ReviewReasons,
                AssignedReviewerId = item.AssignedReviewerId,
                CreatedAt = DateTimeOffset.Parse(item.CreatedAt),
                UpdatedAt = DateTimeOffset.Parse(item.UpdatedAt),
                LinkedEventId = item.LinkedEventId,
            };

            foreach (var history in item.History)
            {
                seeded.AppendHistory(new ReviewHistoryEntry(
                    history.Action,
                    history.ActorId,
                    history.Note,
                    Enum.Parse<ModerationItemStatus>(history.PreviousStatus, true),
                    Enum.Parse<ModerationItemStatus>(history.NextStatus, true),
                    DateTimeOffset.Parse(history.Timestamp)));
            }

            _items[seeded.ItemId] = seeded;
        }
    }

    public Task<string> AddItemAsync(ModerationQueueItem item, CancellationToken ct = default)
    {
        _items[item.ItemId] = item;
        return Task.FromResult(item.ItemId);
    }

    public Task<ModerationQueueItem?> GetItemAsync(string itemId, CancellationToken ct = default)
    {
        _items.TryGetValue(itemId, out var item);
        return Task.FromResult(item);
    }

    public Task<(IReadOnlyList<ModerationQueueItem> Items, int Total)> QueryAsync(
        ModerationQueueQuery query, CancellationToken ct = default)
    {
        var filtered = _items.Values.AsEnumerable();

        if (query.Status.HasValue)
            filtered = filtered.Where(i => i.Status == query.Status.Value);
        if (query.Kind.HasValue)
            filtered = filtered.Where(i => i.Kind == query.Kind.Value);
        if (query.SourceKind is not null)
            filtered = filtered.Where(i =>
                i.Provenance.SourceKind.Equals(query.SourceKind, StringComparison.OrdinalIgnoreCase));
        if (query.ReviewReason is not null)
            filtered = filtered.Where(i =>
                i.ReviewReasons.Any(r => r.Contains(query.ReviewReason, StringComparison.OrdinalIgnoreCase)));
        if (query.ConfidenceBucket.HasValue)
            filtered = filtered.Where(i => i.Confidence.Bucket == query.ConfidenceBucket.Value);
        if (query.MinDuplicateSeverity.HasValue)
            filtered = filtered.Where(i =>
                i.DedupeMatch is not null && i.DedupeMatch.Severity >= query.MinDuplicateSeverity.Value);
        if (query.AssignedReviewerId is not null)
            filtered = filtered.Where(i =>
                i.AssignedReviewerId == query.AssignedReviewerId);
        if (query.IngestionJobId is not null)
            filtered = filtered.Where(i =>
                i.IngestionJob?.JobId == query.IngestionJobId);
        if (query.AfterUtc is not null && DateTimeOffset.TryParse(query.AfterUtc, out var after))
            filtered = filtered.Where(i => i.CreatedAt >= after);
        if (query.BeforeUtc is not null && DateTimeOffset.TryParse(query.BeforeUtc, out var before))
            filtered = filtered.Where(i => i.CreatedAt <= before);

        var sorted = filtered.OrderByDescending(i => i.CreatedAt).ToList();
        var total = sorted.Count;

        // Cursor: encode as base64 of the last-seen CreatedAt ticks
        IEnumerable<ModerationQueueItem> paged = sorted;
        if (query.Cursor is not null)
        {
            var ticks = DecodeCursor(query.Cursor);
            paged = sorted.Where(i => i.CreatedAt.UtcTicks < ticks);
        }

        var page = paged.Take(query.PageSize).ToList();
        return Task.FromResult<(IReadOnlyList<ModerationQueueItem>, int)>((page, total));
    }

    public Task UpdateItemAsync(ModerationQueueItem item, CancellationToken ct = default)
    {
        _items[item.ItemId] = item;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReviewHistoryEntry>> GetAllHistoryAsync(
        int pageSize, string? cursor, CancellationToken ct = default)
    {
        var all = _items.Values
            .SelectMany(i => i.History)
            .OrderByDescending(h => h.Timestamp)
            .AsEnumerable();

        if (cursor is not null)
        {
            var ticks = DecodeCursor(cursor);
            all = all.Where(h => h.Timestamp.UtcTicks < ticks);
        }

        IReadOnlyList<ReviewHistoryEntry> result = all.Take(pageSize).ToList();
        return Task.FromResult(result);
    }

    public Task<int> CountTotalHistoryAsync(CancellationToken ct = default)
    {
        var count = _items.Values.Sum(i => i.History.Count);
        return Task.FromResult(count);
    }

    public Task<ModerationStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var all = _items.Values.ToList();

        var stats = new ModerationStatsDto(
            TotalOpen: all.Count(i => i.Status == ModerationItemStatus.Open),
            TotalInReview: all.Count(i => i.Status == ModerationItemStatus.InReview),
            TotalResolved: all.Count(i => i.Status == ModerationItemStatus.Resolved),
            CandidateReviewOpen: all.Count(i => i.Kind == ModerationItemKind.CandidateReview && i.Status == ModerationItemStatus.Open),
            DedupeReviewOpen: all.Count(i => i.Kind == ModerationItemKind.DedupeReview && i.Status == ModerationItemStatus.Open),
            IngestionFailureOpen: all.Count(i => i.Kind == ModerationItemKind.IngestionFailure && i.Status == ModerationItemStatus.Open),
            PublishBlockedOpen: all.Count(i => i.Kind == ModerationItemKind.PublishBlocked && i.Status == ModerationItemStatus.Open),
            HighConfidenceOpen: all.Count(i => i.Status == ModerationItemStatus.Open && i.Confidence.Bucket == ConfidenceBucket.High),
            MediumConfidenceOpen: all.Count(i => i.Status == ModerationItemStatus.Open && i.Confidence.Bucket == ConfidenceBucket.Medium),
            LowConfidenceOpen: all.Count(i => i.Status == ModerationItemStatus.Open && i.Confidence.Bucket == ConfidenceBucket.Low),
            AsOf: DateTimeOffset.UtcNow.ToString("O"));

        return Task.FromResult(stats);
    }

    private static long DecodeCursor(string cursor)
    {
        try { return long.Parse(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor))); }
        catch { return long.MaxValue; }
    }

    internal static string EncodeCursor(DateTimeOffset dt) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dt.UtcTicks.ToString()));
}
