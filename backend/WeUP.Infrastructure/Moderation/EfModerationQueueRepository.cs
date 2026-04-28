using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeUP.Contracts.Moderation;
using WeUP.Domain.Moderation;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;
using DomainQueueItem = WeUP.Domain.Moderation.ModerationQueueItem;

namespace WeUP.Infrastructure.Moderation;

/// <summary>
/// EF Core-backed moderation queue repository for Postgres deployments.
/// Complex nested objects are stored as JSONB; scalar columns are indexed for
/// the common filter operations.
/// </summary>
public sealed class EfModerationQueueRepository : IModerationQueueRepository
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly WeUpDbContext _db;

    public EfModerationQueueRepository(WeUpDbContext db)
        => _db = db;

    // -----------------------------------------------------------------------
    // Write
    // -----------------------------------------------------------------------

    public async Task<string> AddItemAsync(DomainQueueItem item, CancellationToken ct = default)
    {
        var entity = ToEntity(item);
        _db.ModerationQueueItems.Add(entity);
        await _db.SaveChangesAsync(ct);
        return item.ItemId;
    }

    public async Task UpdateItemAsync(DomainQueueItem item, CancellationToken ct = default)
    {
        var entity = await _db.ModerationQueueItems
            .FirstOrDefaultAsync(e => e.ItemId == item.ItemId, ct);

        if (entity is null)
            throw new InvalidOperationException($"Moderation queue item '{item.ItemId}' not found.");

        item.UpdatedAt = DateTimeOffset.UtcNow;
        PatchEntity(entity, item);
        await _db.SaveChangesAsync(ct);
    }

    // -----------------------------------------------------------------------
    // Read
    // -----------------------------------------------------------------------

    public async Task<DomainQueueItem?> GetItemAsync(string itemId, CancellationToken ct = default)
    {
        var entity = await _db.ModerationQueueItems
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ItemId == itemId, ct);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<(IReadOnlyList<DomainQueueItem> Items, int Total)> QueryAsync(
        ModerationQueueQuery query, CancellationToken ct = default)
    {
        // Apply scalar predicates in the database
        IQueryable<ModerationQueueItemEntity> q = _db.ModerationQueueItems.AsNoTracking();

        if (query.Status.HasValue)
            q = q.Where(e => e.Status == query.Status.Value.ToString());
        if (query.Kind.HasValue)
            q = q.Where(e => e.Kind == query.Kind.Value.ToString());
        if (query.AssignedReviewerId is not null)
            q = q.Where(e => e.AssignedReviewerId == query.AssignedReviewerId);
        if (query.AfterUtc is not null && DateTimeOffset.TryParse(query.AfterUtc, out var after))
            q = q.Where(e => e.CreatedAt >= after);
        if (query.BeforeUtc is not null && DateTimeOffset.TryParse(query.BeforeUtc, out var before))
            q = q.Where(e => e.CreatedAt <= before);

        q = q.OrderByDescending(e => e.CreatedAt);

        var entities = await q.ToListAsync(ct);

        // Deserialise and apply remaining (JSON-backed) predicates in memory
        var all = entities.Select(ToDomain).ToList();

        if (query.SourceKind is not null)
            all = all.Where(i =>
                i.Provenance.SourceKind.Equals(query.SourceKind, StringComparison.OrdinalIgnoreCase)).ToList();
        if (query.ReviewReason is not null)
            all = all.Where(i =>
                i.ReviewReasons.Any(r => r.Contains(query.ReviewReason, StringComparison.OrdinalIgnoreCase))).ToList();
        if (query.ConfidenceBucket.HasValue)
            all = all.Where(i => i.Confidence.Bucket == query.ConfidenceBucket.Value).ToList();
        if (query.MinDuplicateSeverity.HasValue)
            all = all.Where(i =>
                i.DedupeMatch is not null && i.DedupeMatch.Severity >= query.MinDuplicateSeverity.Value).ToList();
        if (query.IngestionJobId is not null)
            all = all.Where(i =>
                i.IngestionJob?.JobId == query.IngestionJobId).ToList();

        var total = all.Count;

        IEnumerable<DomainQueueItem> paged = all;
        if (query.Cursor is not null)
        {
            var ticks = DecodeCursor(query.Cursor);
            paged = all.Where(i => i.CreatedAt.UtcTicks < ticks);
        }

        return (paged.Take(query.PageSize).ToList(), total);
    }

    // -----------------------------------------------------------------------
    // History
    // -----------------------------------------------------------------------

    public async Task<IReadOnlyList<ReviewHistoryEntry>> GetHistoryAsync(
        string itemId,
        int pageSize,
        string? cursor,
        CancellationToken ct = default)
    {
        var entity = await _db.ModerationQueueItems
            .AsNoTracking()
            .Where(e => e.ItemId == itemId)
            .Select(e => e.HistoryJson)
            .FirstOrDefaultAsync(ct);

        if (entity is null)
            return [];

        var history = DeserialiseHistory(entity)
            .OrderByDescending(h => h.Timestamp)
            .AsEnumerable();

        if (cursor is not null)
        {
            var ticks = DecodeCursor(cursor);
            history = history.Where(h => h.Timestamp.UtcTicks < ticks);
        }

        return history.Take(pageSize).ToList();
    }

    public async Task<IReadOnlyList<ReviewHistoryEntry>> GetAllHistoryAsync(
        int pageSize, string? cursor, CancellationToken ct = default)
    {
        var allJson = await _db.ModerationQueueItems
            .AsNoTracking()
            .Select(e => e.HistoryJson)
            .ToListAsync(ct);

        var history = allJson
            .SelectMany(j => DeserialiseHistory(j))
            .OrderByDescending(h => h.Timestamp)
            .AsEnumerable();

        if (cursor is not null)
        {
            var ticks = DecodeCursor(cursor);
            history = history.Where(h => h.Timestamp.UtcTicks < ticks);
        }

        return history.Take(pageSize).ToList();
    }

    public async Task<int> CountTotalHistoryAsync(CancellationToken ct = default)
    {
        var allJson = await _db.ModerationQueueItems
            .AsNoTracking()
            .Select(e => e.HistoryJson)
            .ToListAsync(ct);

        return allJson.Sum(j => DeserialiseHistory(j).Count);
    }

    // -----------------------------------------------------------------------
    // Stats
    // -----------------------------------------------------------------------

    public async Task<ModerationStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var rows = await _db.ModerationQueueItems
            .AsNoTracking()
            .Select(e => new { e.Status, e.Kind, e.ConfidenceJson })
            .ToListAsync(ct);

        var openStr = ModerationItemStatus.Open.ToString();
        var inReviewStr = ModerationItemStatus.InReview.ToString();
        var resolvedStr = ModerationItemStatus.Resolved.ToString();

        static ConfidenceBucket GetBucket(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("bucket", out var b)
                    ? Enum.Parse<ConfidenceBucket>(b.GetString() ?? "Low", true)
                    : ConfidenceBucket.Low;
            }
            catch { return ConfidenceBucket.Low; }
        }

        return new ModerationStatsDto(
            TotalOpen: rows.Count(r => r.Status == openStr),
            TotalInReview: rows.Count(r => r.Status == inReviewStr),
            TotalResolved: rows.Count(r => r.Status == resolvedStr),
            CandidateReviewOpen: rows.Count(r => r.Kind == ModerationItemKind.CandidateReview.ToString() && r.Status == openStr),
            DedupeReviewOpen: rows.Count(r => r.Kind == ModerationItemKind.DedupeReview.ToString() && r.Status == openStr),
            IngestionFailureOpen: rows.Count(r => r.Kind == ModerationItemKind.IngestionFailure.ToString() && r.Status == openStr),
            PublishBlockedOpen: rows.Count(r => r.Kind == ModerationItemKind.PublishBlocked.ToString() && r.Status == openStr),
            HighConfidenceOpen: rows.Count(r => r.Status == openStr && GetBucket(r.ConfidenceJson) == ConfidenceBucket.High),
            MediumConfidenceOpen: rows.Count(r => r.Status == openStr && GetBucket(r.ConfidenceJson) == ConfidenceBucket.Medium),
            LowConfidenceOpen: rows.Count(r => r.Status == openStr && GetBucket(r.ConfidenceJson) == ConfidenceBucket.Low),
            AsOf: DateTimeOffset.UtcNow.ToString("O"));
    }

    public async Task<ModerationQueueTelemetrySnapshot> GetTelemetrySnapshotAsync(CancellationToken ct = default)
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var rows = await _db.ModerationQueueItems
            .AsNoTracking()
            .Select(e => new
            {
                e.Status,
                e.CreatedAt,
                e.ReviewReasonsJson,
                e.ConfidenceJson,
                e.HistoryJson,
            })
            .ToListAsync(ct);

        static double ParseAggregateConfidence(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("aggregate", out var aggregate)
                    ? aggregate.GetDouble()
                    : 0d;
            }
            catch
            {
                return 0d;
            }
        }

        var telemetryRows = rows.Select(row =>
        {
            var itemStatus = Enum.Parse<ModerationItemStatus>(row.Status, true);
            var reviewReasons = JsonSerializer.Deserialize<string[]>(row.ReviewReasonsJson, _json) ?? [];
            var history = DeserialiseHistory(row.HistoryJson);
            var workflowStatus = ModerationTelemetryDimensions.ResolveWorkflowStatus(itemStatus, history.LastOrDefault()?.Action);

            return new
            {
                RiskLevel = ModerationTelemetryDimensions.ResolveRiskLevel(reviewReasons, ParseAggregateConfidence(row.ConfidenceJson)),
                ModerationStatus = workflowStatus,
                row.CreatedAt,
            };
        }).ToArray();

        var queueSize = telemetryRows
            .GroupBy(row => new { row.RiskLevel, row.ModerationStatus })
            .Select(group => new ModerationQueueCountSample(group.Key.RiskLevel, group.Key.ModerationStatus, group.Count()))
            .OrderBy(sample => sample.RiskLevel, StringComparer.Ordinal)
            .ThenBy(sample => sample.ModerationStatus, StringComparer.Ordinal)
            .ToArray();

        var pending = telemetryRows
            .Where(row => ModerationTelemetryDimensions.IsPendingStatus(row.ModerationStatus))
            .ToArray();

        var pendingTotals = pending
            .GroupBy(row => new { row.RiskLevel, row.ModerationStatus })
            .Select(group => new ModerationQueueCountSample(group.Key.RiskLevel, group.Key.ModerationStatus, group.Count()))
            .OrderBy(sample => sample.RiskLevel, StringComparer.Ordinal)
            .ThenBy(sample => sample.ModerationStatus, StringComparer.Ordinal)
            .ToArray();

        var backlogAge = pending
            .GroupBy(row => new { row.RiskLevel, row.ModerationStatus })
            .Select(group => new ModerationQueueAgeSample(
                group.Key.RiskLevel,
                group.Key.ModerationStatus,
                group.Max(row => Math.Max(0d, (observedAtUtc - row.CreatedAt).TotalMilliseconds))))
            .OrderBy(sample => sample.RiskLevel, StringComparer.Ordinal)
            .ThenBy(sample => sample.ModerationStatus, StringComparer.Ordinal)
            .ToArray();

        return new ModerationQueueTelemetrySnapshot(observedAtUtc, queueSize, pendingTotals, backlogAge);
    }

    // -----------------------------------------------------------------------
    // Entity <-> domain mapping
    // -----------------------------------------------------------------------

    private static ModerationQueueItemEntity ToEntity(DomainQueueItem item)
    {
        var entity = new ModerationQueueItemEntity
        {
            Id = Guid.NewGuid(),
            ItemId = item.ItemId,
            Kind = item.Kind.ToString(),
            Status = item.Status.ToString(),
            CandidateJson = item.Candidate is null ? null : JsonSerializer.Serialize(item.Candidate, _json),
            ProvenanceJson = JsonSerializer.Serialize(item.Provenance, _json),
            ConfidenceJson = JsonSerializer.Serialize(item.Confidence, _json),
            DedupeMatchJson = item.DedupeMatch is null ? null : JsonSerializer.Serialize(item.DedupeMatch, _json),
            IngestionJobJson = item.IngestionJob is null ? null : JsonSerializer.Serialize(item.IngestionJob, _json),
            ReviewReasonsJson = JsonSerializer.Serialize(item.ReviewReasons, _json),
            HistoryJson = JsonSerializer.Serialize(item.History, _json),
            AssignedReviewerId = item.AssignedReviewerId,
            LinkedEventId = item.LinkedEventId,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
        };
        return entity;
    }

    private static void PatchEntity(ModerationQueueItemEntity entity, DomainQueueItem item)
    {
        entity.Status = item.Status.ToString();
        entity.CandidateJson = item.Candidate is null ? null : JsonSerializer.Serialize(item.Candidate, _json);
        entity.ProvenanceJson = JsonSerializer.Serialize(item.Provenance, _json);
        entity.ConfidenceJson = JsonSerializer.Serialize(item.Confidence, _json);
        entity.DedupeMatchJson = item.DedupeMatch is null ? null : JsonSerializer.Serialize(item.DedupeMatch, _json);
        entity.IngestionJobJson = item.IngestionJob is null ? null : JsonSerializer.Serialize(item.IngestionJob, _json);
        entity.ReviewReasonsJson = JsonSerializer.Serialize(item.ReviewReasons, _json);
        entity.HistoryJson = JsonSerializer.Serialize(item.History, _json);
        entity.AssignedReviewerId = item.AssignedReviewerId;
        entity.LinkedEventId = item.LinkedEventId;
        entity.UpdatedAt = item.UpdatedAt;
    }

    private static DomainQueueItem ToDomain(ModerationQueueItemEntity entity)
    {
        var item = new DomainQueueItem
        {
            ItemId = entity.ItemId,
            Kind = Enum.Parse<ModerationItemKind>(entity.Kind, true),
            Status = Enum.Parse<ModerationItemStatus>(entity.Status, true),
            Candidate = entity.CandidateJson is null
                ? null
                : JsonSerializer.Deserialize<CandidateSnapshotDto>(entity.CandidateJson, _json),
            Provenance = JsonSerializer.Deserialize<ProvenanceSummaryDto>(entity.ProvenanceJson, _json)!,
            Confidence = JsonSerializer.Deserialize<ConfidenceSummaryDto>(entity.ConfidenceJson, _json)!,
            DedupeMatch = entity.DedupeMatchJson is null
                ? null
                : JsonSerializer.Deserialize<DedupeSummaryDto>(entity.DedupeMatchJson, _json),
            IngestionJob = entity.IngestionJobJson is null
                ? null
                : JsonSerializer.Deserialize<IngestionJobSummaryDto>(entity.IngestionJobJson, _json),
            ReviewReasons = JsonSerializer.Deserialize<string[]>(entity.ReviewReasonsJson, _json) ?? [],
            AssignedReviewerId = entity.AssignedReviewerId,
            LinkedEventId = entity.LinkedEventId,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };

        foreach (var entry in DeserialiseHistory(entity.HistoryJson))
            item.AppendHistory(entry);

        return item;
    }

    private static List<ReviewHistoryEntry> DeserialiseHistory(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<ReviewHistoryEntry>>(json, _json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    // -----------------------------------------------------------------------
    // Cursor helpers (matching InMemoryModerationQueue semantics)
    // -----------------------------------------------------------------------

    private static long DecodeCursor(string cursor)
    {
        try { return long.Parse(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor))); }
        catch { return long.MaxValue; }
    }
}
