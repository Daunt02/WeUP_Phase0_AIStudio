using Microsoft.EntityFrameworkCore;
using WeUP.Contracts.Users;
using WeUP.Domain.Users;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Auth;

public sealed class EfItineraryRepository(WeUpDbContext db) : IItineraryRepository
{
    public async Task<ItineraryResponse> GetAsync(string userId, CancellationToken ct = default)
    {
        var items = await db.Itineraries
            .AsNoTracking()
            .Where(i => i.UserId == userId)
            .OrderBy(i => i.Position)
            .ToListAsync(ct);

        var dtos = items.Select(i => new ItineraryItemDto(
            i.ItemId, i.EventId, null, null, null, i.Position, i.Note, i.AddedAtUtc)).ToArray();

        return new ItineraryResponse(dtos, dtos.Length);
    }

    public async Task<ItineraryItemResponse> AddAsync(string userId, AddToItineraryRequest request, CancellationToken ct = default)
    {
        var items = await db.Itineraries
            .Where(i => i.UserId == userId)
            .ToListAsync(ct);

        var nextPosition = items.Count > 0 ? items.Max(i => i.Position) + 1 : 1;
        var position = request.Position ?? nextPosition;

        foreach (var item in items.Where(i => i.Position >= position))
        {
            item.Position++;
        }

        var entity = new ItineraryEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ItemId = Guid.NewGuid().ToString("N"),
            EventId = request.EventId,
            Note = request.Note,
            Position = position,
            AddedAtUtc = DateTimeOffset.UtcNow
        };

        db.Itineraries.Add(entity);
        await db.SaveChangesAsync(ct);

        return new ItineraryItemResponse(entity.ItemId, request.EventId, true, "Added to itinerary.");
    }

    public async Task<ItineraryItemResponse> UpdateItemAsync(string userId, string itemId, UpdateItineraryItemRequest request, CancellationToken ct = default)
    {
        var item = await db.Itineraries.FirstOrDefaultAsync(i => i.UserId == userId && i.ItemId == itemId, ct);
        if (item is null)
        {
            return new ItineraryItemResponse(itemId, "", false, "Item not found.");
        }

        if (request.Note is not null) item.Note = request.Note;
        if (request.Position.HasValue) item.Position = request.Position.Value;

        await db.SaveChangesAsync(ct);
        return new ItineraryItemResponse(itemId, item.EventId, true, "Updated.");
    }

    public async Task<ItineraryItemResponse> RemoveAsync(string userId, string itemId, CancellationToken ct = default)
    {
        var item = await db.Itineraries.FirstOrDefaultAsync(i => i.UserId == userId && i.ItemId == itemId, ct);
        if (item is null)
        {
            return new ItineraryItemResponse(itemId, "", false, "Item not found.");
        }

        db.Itineraries.Remove(item);
        await db.SaveChangesAsync(ct);
        return new ItineraryItemResponse(itemId, item.EventId, false, "Removed from itinerary.");
    }
}
