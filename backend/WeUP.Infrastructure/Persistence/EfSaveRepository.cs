using Microsoft.EntityFrameworkCore;
using WeUP.Contracts.Events;
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
            return new SavedEventsResponse([], 0, page, pageSize, false, 0, 0, DateTimeOffset.UtcNow);

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
            .Select(s =>
            {
                if (!eventsById.TryGetValue(s.EventId, out var evt))
                {
                    return new SavedEventDto(
                        EventId: s.EventId.ToString("N"),
                        SavedAt: s.SavedAt,
                        ResolutionStatus: "missing-or-deleted",
                        ResolutionMessage: "Saved reference no longer resolves to an active canonical event.",
                        CanonicalEvent: null);
                }

                return new SavedEventDto(
                    EventId: evt.PublicId,
                    SavedAt: s.SavedAt,
                    ResolutionStatus: "resolved",
                    ResolutionMessage: null,
                    CanonicalEvent: new EventMapItemDto(
                        EventId: evt.PublicId,
                        Title: evt.CanonicalTitle,
                        StartUtc: evt.StartUtc,
                        EndUtc: evt.EndUtc,
                        Latitude: evt.Latitude,
                        Longitude: evt.Longitude,
                        VenueName: evt.VenueName,
                        District: evt.DistrictCode,
                        PrimaryCategory: evt.Category,
                        SavedByCurrentUser: true,
                        MarkerState: "saved"));
            })
            .ToList();

        var resolvedCount = items.Count(item => item.CanonicalEvent is not null);
        var missingOrDeletedCount = items.Count - resolvedCount;

        return new SavedEventsResponse(
            [.. items],
            totalCount,
            clampedPage,
            clampedSize,
            (clampedPage - 1) * clampedSize + items.Count < totalCount,
            resolvedCount,
            missingOrDeletedCount,
            DateTimeOffset.UtcNow);
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
