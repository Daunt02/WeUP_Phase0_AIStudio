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
        var userGuid = await db.UserProfiles
            .AsNoTracking()
            .Where(u => u.PublicId == userId)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);

        if (userGuid is null)
            return new SavedEventsResponse([], 0, page, pageSize, false);

        var totalCount = await db.SavedEvents
            .AsNoTracking()
            .CountAsync(s => s.UserId == userGuid.Value, ct);

        var clampedPage = Math.Max(1, page);
        var clampedSize = Math.Clamp(pageSize, 1, 200);

        // Fetch saves first, then load matching events separately (EF Core does not support Include inside Join)
        var saves = await db.SavedEvents
            .AsNoTracking()
            .Where(s => s.UserId == userGuid.Value)
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
                    evt.PublicId,
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

    public async Task<bool> IsEventSavedAsync(string userId, string eventId, CancellationToken ct = default)
    {
        var userGuid = await db.UserProfiles
            .AsNoTracking()
            .Where(u => u.PublicId == userId)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);

        if (userGuid is null)
        {
            return false;
        }

        return await db.SavedEvents
            .AsNoTracking()
            .Include(s => s.Event)
            .AnyAsync(
                s => s.UserId == userGuid.Value && s.Event.PublicId == eventId,
                ct);
    }

    public async Task<SaveEventResponseDto> SaveEventAsync(string userId, string eventId, CancellationToken ct = default)
    {
        var user = await db.UserProfiles.FirstOrDefaultAsync(u => u.PublicId == userId, ct);
        var evt = await db.Events.FirstOrDefaultAsync(e => e.PublicId == eventId, ct);
        if (user is null || evt is null)
            return new SaveEventResponseDto(eventId, false, "Invalid userId or eventId");

        var exists = await db.SavedEvents
            .AnyAsync(s => s.UserId == user.Id && s.EventId == evt.Id, ct);

        if (!exists)
        {
            db.SavedEvents.Add(new SavedEventEntity
            {
                UserId = user.Id,
                EventId = evt.Id,
                SavedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }

        return new SaveEventResponseDto(eventId, true, "Saved");
    }

    public async Task<SaveEventResponseDto> UnsaveEventAsync(string userId, string eventId, CancellationToken ct = default)
    {
        var user = await db.UserProfiles.FirstOrDefaultAsync(u => u.PublicId == userId, ct);
        var evt = await db.Events.FirstOrDefaultAsync(e => e.PublicId == eventId, ct);
        if (user is null || evt is null)
            return new SaveEventResponseDto(eventId, false, "Invalid userId or eventId");

        var entity = await db.SavedEvents
            .FirstOrDefaultAsync(s => s.UserId == user.Id && s.EventId == evt.Id, ct);

        if (entity is not null)
        {
            db.SavedEvents.Remove(entity);
            await db.SaveChangesAsync(ct);
        }

        return new SaveEventResponseDto(eventId, false, "Unsaved");
    }
}
