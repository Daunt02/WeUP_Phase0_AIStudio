using Microsoft.EntityFrameworkCore;
using WeUP.Contracts.Moderation;
using WeUP.Domain.Moderation;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Moderation;

/// <summary>
/// EF-backed moderation audit repository.
/// Writes are insert-only and retrieval is index-friendly for candidate, reviewer,
/// and event scoped audit reviews.
/// </summary>
public sealed class EfModerationAuditRepository(WeUpDbContext db) : IModerationAuditRepository
{
    public async Task AppendAsync(ModerationHistoryEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        db.ModerationHistoryEntries.Add(ToEntity(entry));
        await db.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<ModerationHistoryEntry>> GetByCandidateAsync(string candidateId, CancellationToken ct = default) =>
        QueryAsync(db.ModerationHistoryEntries.Where(e => e.CandidateId == candidateId), ct);

    public Task<IReadOnlyList<ModerationHistoryEntry>> GetByReviewerAsync(string reviewerId, CancellationToken ct = default) =>
        QueryAsync(db.ModerationHistoryEntries.Where(e => e.ReviewerId == reviewerId), ct);

    public Task<IReadOnlyList<ModerationHistoryEntry>> GetByEventAsync(string eventId, CancellationToken ct = default) =>
        QueryAsync(db.ModerationHistoryEntries.Where(e => e.EventId == eventId), ct);

    private static ModerationHistoryEntryEntity ToEntity(ModerationHistoryEntry entry) => new()
    {
        Id = Guid.NewGuid(),
        EntryId = entry.EntryId,
        QueueItemId = entry.QueueItemId,
        EventId = entry.EventId,
        CandidateId = entry.CandidateId,
        ReviewerId = entry.ReviewerId,
        ActorId = entry.ActorId,
        Action = entry.Action,
        PreviousStatus = entry.PreviousStatus.ToString(),
        NewStatus = entry.NewStatus.ToString(),
        ActionTimestampUtc = entry.ActionTimestampUtc,
        ReasonComment = entry.ReasonComment,
    };

    private static ModerationHistoryEntry ToDomain(ModerationHistoryEntryEntity entity) => new(
        EntryId: entity.EntryId,
        QueueItemId: entity.QueueItemId,
        EventId: entity.EventId,
        CandidateId: entity.CandidateId,
        ReviewerId: entity.ReviewerId,
        ActorId: entity.ActorId,
        Action: entity.Action,
        PreviousStatus: Enum.Parse<ModerationItemStatus>(entity.PreviousStatus, true),
        NewStatus: Enum.Parse<ModerationItemStatus>(entity.NewStatus, true),
        ActionTimestampUtc: entity.ActionTimestampUtc,
        ReasonComment: entity.ReasonComment);

    private static async Task<IReadOnlyList<ModerationHistoryEntry>> QueryAsync(
        IQueryable<ModerationHistoryEntryEntity> query,
        CancellationToken ct)
    {
        var rows = await query
            .AsNoTracking()
            .OrderBy(e => e.ActionTimestampUtc)
            .ThenBy(e => e.EntryId)
            .ToListAsync(ct);

        return rows.Select(ToDomain).ToArray();
    }
}