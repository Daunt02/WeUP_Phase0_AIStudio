using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WeUP.Domain.Moderation;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Moderation;

public sealed class EfAuditTrailRepository(WeUpDbContext db) : IAuditTrailService
{
    public async Task AppendAsync(AuditTrailEntry entry, CancellationToken ct = default)
    {
        var entity = new AuditTrailEntity
        {
            Id = Guid.NewGuid(),
            EntryId = entry.EntryId,
            ItemId = entry.ItemId,
            ItemKind = entry.ItemKind.ToString(),
            Action = entry.Action,
            ActorId = entry.ActorId,
            PreviousStatus = entry.PreviousStatus.ToString(),
            NextStatus = entry.NextStatus.ToString(),
            Note = entry.Note,
            EvidenceSnapshotRefsJson = JsonSerializer.Serialize(entry.EvidenceSnapshotRefs),
            CorrelationId = entry.CorrelationId,
            TimestampUtc = entry.Timestamp
        };

        db.AuditTrails.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<AuditTrailEntry> Entries, int Total)> GetEntriesAsync(
        int pageSize, string? cursor, CancellationToken ct = default)
    {
        var query = db.AuditTrails.AsNoTracking();

        if (cursor is not null && long.TryParse(
            System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor)), out var ticks))
        {
            query = query.Where(e => e.TimestampUtc.UtcTicks < ticks);
        }

        var entities = await query
            .OrderByDescending(e => e.TimestampUtc)
            .Take(pageSize)
            .ToListAsync(ct);

        var total = await db.AuditTrails.CountAsync(ct);

        var entries = entities.Select(e => new AuditTrailEntry(
            e.EntryId,
            e.ItemId,
            Enum.Parse<WeUP.Contracts.Moderation.ModerationItemKind>(e.ItemKind, true),
            e.Action,
            e.ActorId,
            Enum.Parse<WeUP.Contracts.Moderation.ModerationItemStatus>(e.PreviousStatus, true),
            Enum.Parse<WeUP.Contracts.Moderation.ModerationItemStatus>(e.NextStatus, true),
            e.Note,
            JsonSerializer.Deserialize<string[]>(e.EvidenceSnapshotRefsJson) ?? [],
            e.CorrelationId,
            e.TimestampUtc
        )).ToArray();

        return (entries, total);
    }
}
