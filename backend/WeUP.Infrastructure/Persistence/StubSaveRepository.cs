using WeUP.Contracts.Saves;
using WeUP.Domain.Users;
using WeUP.Infrastructure.Seed;

namespace WeUP.Infrastructure.Persistence;

/// <summary>
/// In-memory stub for saves — replaced by EF Core implementation in P08–P09.
/// </summary>
public sealed class StubSaveRepository : ISaveRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<(string UserId, string EventId), DateTimeOffset> _saves = [];
    private readonly Dictionary<string, (string Title, string VenueName, string? ThumbnailUrl, DateTimeOffset StartUtc)> _eventIndex = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _seedNow = DateTimeOffset.Parse("2026-04-11T18:00:00Z");

    public void Reset(Phase0SeedDataset dataset)
    {
        var venueById = dataset.Venues.ToDictionary(v => v.VenueId, StringComparer.OrdinalIgnoreCase);

        lock (_gate)
        {
            _eventIndex.Clear();
            foreach (var evt in dataset.Events)
            {
                var venue = venueById[evt.VenueId];
                _eventIndex[evt.EventId] = (evt.Title, venue.Name, evt.ImageUrl, DateTimeOffset.Parse(evt.StartsAtUtc));
            }

            _saves.Clear();
            foreach (var save in dataset.Saves)
            {
                _saves[(save.UserId, save.EventId)] = DateTimeOffset.Parse(save.SavedAt);
            }

            _seedNow = DateTimeOffset.Parse(dataset.Meta.FixedNow);
        }
    }

    public Task<SavedEventsResponse> GetSavesAsync(string userId, int page, int pageSize, CancellationToken ct = default)
    {
        SavedEventDto[] items;
        int total;

        lock (_gate)
        {
            var filtered = _saves
                .Where(s => s.Key.UserId == userId)
                .OrderByDescending(s => s.Value)
                .ToArray();

            total = filtered.Length;
            items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s =>
            {
                var metadata = _eventIndex.TryGetValue(s.Key.EventId, out var value)
                    ? value
                    : (s.Key.EventId, "Unknown Venue", (string?)null, _seedNow);
                return new SavedEventDto(s.Key.EventId, metadata.Item1, metadata.Item2, metadata.Item4, metadata.Item3, s.Value);
            })
            .ToArray();
        }

        return Task.FromResult(new SavedEventsResponse(items, total, page, pageSize, false));
    }

    public Task<bool> IsEventSavedAsync(string userId, string eventId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_saves.ContainsKey((userId, eventId)));
        }
    }

    public Task<SaveEventResponseDto> SaveEventAsync(string userId, string eventId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            _saves[(userId, eventId)] = _seedNow.AddMinutes(_saves.Count + 1);
        }

        return Task.FromResult(new SaveEventResponseDto(eventId, true, "Saved"));
    }

    public Task<SaveEventResponseDto> UnsaveEventAsync(string userId, string eventId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            _saves.Remove((userId, eventId));
        }

        return Task.FromResult(new SaveEventResponseDto(eventId, false, "Unsaved"));
    }
}
