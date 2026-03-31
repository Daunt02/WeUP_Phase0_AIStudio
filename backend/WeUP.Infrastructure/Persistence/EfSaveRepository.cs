using Microsoft.EntityFrameworkCore;
using WeUP.Contracts.Saves;
using WeUP.Domain.Users;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Persistence;

/// <summary>
/// EF Core + Postgres implementation of ISaveRepository.
/// Save/unsave operations are idempotent — safe to call multiple times.
/// </summary>
public sealed class EfSaveRepository(WeUpDbContext db) : ISaveRepository
{
    public async Task<SavedEventsResponse> GetSavesAsync(string userId, int page, int pageSize, CancellationToken ct = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return new SavedEventsResponse([], 0, page, pageSize, false);

        var totalCount = await db.SavedEvents
            .AsNoTracking()
            .CountAsync(s => s.UserId == userGuid, ct);

        var clampedPage = Math.Max(1, page);
        var clampedSize = Math.Clamp(pageSize, 1, 200);

        var items = await db.SavedEvents
            .AsNoTracking()
            .Where(s => s.UserId == userGuid)
            .OrderByDescending(s => s.SavedAt)
            .Skip((clampedPage - 1) * clampedSize)
            .Take(clampedSize)
            .Join(db.Events.Include(e => e.Media),
                  save => save.EventId,
                  evt  => evt.Id,
                  (save, evt) => new SavedEventDto(
                      evt.Id.ToString(),
                      evt.CanonicalTitle,
                      evt.VenueName,
                      evt.StartUtc,
                      evt.Media.FirstOrDefault(m => m.Kind == "poster" || m.Kind == "image") != null
                          ? evt.Media.First(m => m.Kind == "poster" || m.Kind == "image").Url
                          : null,
                      save.SavedAt))
            .ToListAsync(ct);

        return new SavedEventsResponse(
            [.. items],
            totalCount,
            clampedPage,
            clampedSize,
            (clampedPage - 1) * clampedSize + items.Count < totalCount);
    }

    public async Task<SaveEventResponse> SaveEventAsync(string userId, string eventId, CancellationToken ct = default)
    {
        if (!Guid.TryParse(userId, out var userGuid) || !Guid.TryParse(eventId, out var eventGuid))
            return new SaveEventResponse(eventId, false, "Invalid userId or eventId");

        var exists = await db.SavedEvents
            .AnyAsync(s => s.UserId == userGuid && s.EventId == eventGuid, ct);

        if (!exists)
        {
            db.SavedEvents.Add(new SavedEventEntity
            {
                UserId = userGuid,
                EventId = eventGuid,
                SavedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }

        return new SaveEventResponse(eventId, true, "Saved");
    }

    public async Task<SaveEventResponse> UnsaveEventAsync(string userId, string eventId, CancellationToken ct = default)
    {
        if (!Guid.TryParse(userId, out var userGuid) || !Guid.TryParse(eventId, out var eventGuid))
            return new SaveEventResponse(eventId, false, "Invalid userId or eventId");

        var entity = await db.SavedEvents
            .FirstOrDefaultAsync(s => s.UserId == userGuid && s.EventId == eventGuid, ct);

        if (entity is not null)
        {
            db.SavedEvents.Remove(entity);
            await db.SaveChangesAsync(ct);
        }

        return new SaveEventResponse(eventId, false, "Unsaved");
    }
}
