using System.Collections.Concurrent;
using WeUP.Contracts.Users;
using WeUP.Domain.Users;

namespace WeUP.Infrastructure.Auth;

/// <summary>
/// Phase 0 in-memory itinerary store.
/// Items are stored per user, ordered by Position ascending.
/// </summary>
public sealed class InMemoryItineraryRepository : IItineraryRepository
{
    // userId → list of items (mutable, ordered by Position)
    private readonly ConcurrentDictionary<string, List<ItineraryRecord>> _store = new();

    public Task<ItineraryResponse> GetAsync(string userId, CancellationToken ct = default)
    {
        var items = GetOrCreate(userId)
            .OrderBy(i => i.Position)
            .Select(i => i.ToDto())
            .ToArray();
        return Task.FromResult(new ItineraryResponse(items, items.Length));
    }

    public Task<ItineraryItemResponse> AddAsync(string userId, AddToItineraryRequest request, CancellationToken ct = default)
    {
        var list = GetOrCreate(userId);
        lock (list)
        {
            // Compute position — append to end by default
            var maxPos = list.Count > 0 ? list.Max(i => i.Position) : 0;
            var position = request.Position ?? maxPos + 1;

            // Shift down items at or after the insertion point
            foreach (var item in list.Where(i => i.Position >= position))
                item.Position += 1;

            var record = new ItineraryRecord(
                ItemId: Guid.NewGuid().ToString("N"),
                EventId: request.EventId,
                Note: request.Note,
                Position: position,
                AddedAt: DateTimeOffset.UtcNow);

            list.Add(record);
            return Task.FromResult(new ItineraryItemResponse(record.ItemId, request.EventId, true, "Added to itinerary."));
        }
    }

    public Task<ItineraryItemResponse> UpdateItemAsync(string userId, string itemId, UpdateItineraryItemRequest request, CancellationToken ct = default)
    {
        var list = GetOrCreate(userId);
        lock (list)
        {
            var item = list.FirstOrDefault(i => i.ItemId == itemId);
            if (item is null)
                return Task.FromResult(new ItineraryItemResponse(itemId, "", false, "Item not found."));

            if (request.Note is not null) item.Note = request.Note;
            if (request.Position.HasValue) item.Position = request.Position.Value;

            return Task.FromResult(new ItineraryItemResponse(itemId, item.EventId, true, "Updated."));
        }
    }

    public Task<ItineraryItemResponse> RemoveAsync(string userId, string itemId, CancellationToken ct = default)
    {
        var list = GetOrCreate(userId);
        lock (list)
        {
            var item = list.FirstOrDefault(i => i.ItemId == itemId);
            if (item is null)
                return Task.FromResult(new ItineraryItemResponse(itemId, "", false, "Item not found."));

            list.Remove(item);
            return Task.FromResult(new ItineraryItemResponse(itemId, item.EventId, false, "Removed from itinerary."));
        }
    }

    private List<ItineraryRecord> GetOrCreate(string userId) =>
        _store.GetOrAdd(userId, _ => []);

    private sealed class ItineraryRecord(
        string ItemId, string EventId, string? Note, int Position, DateTimeOffset AddedAt)
    {
        public string ItemId   { get; } = ItemId;
        public string EventId  { get; } = EventId;
        public string? Note    { get; set; } = Note;
        public int Position    { get; set; } = Position;
        public DateTimeOffset AddedAt { get; } = AddedAt;

        public ItineraryItemDto ToDto() =>
            new(ItemId, EventId, null, null, null, Position, Note, AddedAt);
    }
}
