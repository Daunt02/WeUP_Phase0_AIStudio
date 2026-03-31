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

        // Fetch saves first, then load matching events separately (EF Core does not support Include inside Join)
        var saves = await db.SavedEvents
            .AsNoTracking()
            .Where(s => s.UserId == userGuid)
            .OrderByDescending(s => s.SavedAt)
            .Skip((clampedPage - 1) * clampedSize)
            .Take(clampedSize)
            .ToListAsync(ct);

        var eventIds = saves.Select(s => s.EventId).ToHashSet();
        var events = await db.Events
            .AsNoTracking()
            .Include(e => e.Media)
            .Where(e => eventIds.Contains(e.Id))
            .ToListAsync(ct);

        var eventsById = events.ToDictionary(e => e.Id);
        var items = saves
            .Where(s => eventsById.ContainsKey(s.EventId))
            .Select(s =>
            {
                var evt = eventsById[s.EventId];
                var thumb = evt.Media.FirstOrDefault(m => m.Kind == "poster")
                         ?? evt.Media.FirstOrDefault(m => m.Kind == "image");
                return new SavedEventDto(
                    evt.Id.ToString(),
                    evt.CanonicalTitle,
                    evt.VenueName,
                    evt.StartUtc,
                    thumb?.Url,
                    s.SavedAt);
            })
            .ToList();

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
