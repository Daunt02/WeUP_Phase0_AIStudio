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
    // Each UserState holds its own lock; avoids lock contention across users.
    private readonly ConcurrentDictionary<string, UserState> _store = new();

    public Task<ItineraryResponse> GetAsync(string userId, CancellationToken ct = default)
    {
        var state = GetOrCreate(userId);
        lock (state)
        {
            var items = state.Items
                .OrderBy(i => i.Position)
                .Select(i => i.ToDto())
                .ToArray();
            return Task.FromResult(new ItineraryResponse(items, items.Length));
        }
    }

    public Task<ItineraryItemResponse> AddAsync(string userId, AddToItineraryRequest request, CancellationToken ct = default)
    {
        var state = GetOrCreate(userId);
        lock (state)
        {
            var position = request.Position ?? state.NextPosition;

            foreach (var item in state.Items.Where(i => i.Position >= position))
                item.Position += 1;

            var record = new ItineraryRecord(
                Guid.NewGuid().ToString("N"),
                request.EventId,
                request.Note,
                position,
                DateTimeOffset.UtcNow);

            state.Items.Add(record);
            if (position >= state.NextPosition)
                state.NextPosition = position + 1;

            return Task.FromResult(new ItineraryItemResponse(record.ItemId, request.EventId, true, "Added to itinerary."));
        }
    }

    public Task<ItineraryItemResponse> UpdateItemAsync(string userId, string itemId, UpdateItineraryItemRequest request, CancellationToken ct = default)
    {
        var state = GetOrCreate(userId);
        lock (state)
        {
            var item = state.Items.FirstOrDefault(i => i.ItemId == itemId);
            if (item is null)
                return Task.FromResult(new ItineraryItemResponse(itemId, "", false, "Item not found."));

            if (request.Note is not null) item.Note = request.Note;
            if (request.Position.HasValue) item.Position = request.Position.Value;

            return Task.FromResult(new ItineraryItemResponse(itemId, item.EventId, true, "Updated."));
        }
    }

    public Task<ItineraryItemResponse> RemoveAsync(string userId, string itemId, CancellationToken ct = default)
    {
        var state = GetOrCreate(userId);
        lock (state)
        {
            var item = state.Items.FirstOrDefault(i => i.ItemId == itemId);
            if (item is null)
                return Task.FromResult(new ItineraryItemResponse(itemId, "", false, "Item not found."));

            state.Items.Remove(item);
            return Task.FromResult(new ItineraryItemResponse(itemId, item.EventId, false, "Removed from itinerary."));
        }
    }

    private UserState GetOrCreate(string userId) =>
        _store.GetOrAdd(userId, _ => new UserState());

    // Per-user state: items list + cached next position counter (avoids O(n) Max scan).
    private sealed class UserState
    {
        public List<ItineraryRecord> Items { get; } = [];
        public int NextPosition { get; set; } = 1;
    }

    private sealed class ItineraryRecord(
        string itemId, string eventId, string? note, int position, DateTimeOffset addedAt)
    {
        public string ItemId       { get; } = itemId;
        public string EventId      { get; } = eventId;
        public string? Note        { get; set; } = note;
        public int Position        { get; set; } = position;
        public DateTimeOffset AddedAt { get; } = addedAt;

        // Event title/venue/startUtc are not available in this layer (Phase 0: no event join).
        public ItineraryItemDto ToDto() =>
            new(ItemId, EventId, null, null, null, Position, Note, AddedAt);
    }
}
