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
    private readonly Dictionary<string, (string Title, string VenueName, string? District, string Category, double Latitude, double Longitude, DateTimeOffset StartUtc)> _eventIndex = new(StringComparer.OrdinalIgnoreCase);
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
                _eventIndex[evt.EventId] = (
                    evt.Title,
                    venue.Name,
                    venue.DistrictCode,
                    evt.Category,
                    venue.Latitude,
                    venue.Longitude,
                    DateTimeOffset.Parse(evt.StartsAtUtc));
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
                if (_eventIndex.TryGetValue(s.Key.EventId, out var value))
                {
                    return new SavedEventDto(
                        EventId: s.Key.EventId,
                        SavedAt: s.Value,
                        ResolutionStatus: "resolved",
                        ResolutionMessage: null,
                        CanonicalEvent: new WeUP.Contracts.Events.EventMapItemDto(
                            EventId: s.Key.EventId,
                            Title: value.Title,
                            StartUtc: value.StartUtc,
                            EndUtc: null,
                            Latitude: value.Latitude,
                            Longitude: value.Longitude,
                            VenueName: value.VenueName,
                            District: value.District,
                            PrimaryCategory: value.Category,
                            SavedByCurrentUser: true,
                            MarkerState: "saved"));
                }

                return new SavedEventDto(
                    EventId: s.Key.EventId,
                    SavedAt: s.Value,
                    ResolutionStatus: "missing-or-deleted",
                    ResolutionMessage: "Saved reference no longer resolves to an active canonical event.",
                    CanonicalEvent: null);
            })
            .ToArray();
        }

        var resolvedCount = items.Count(item => item.CanonicalEvent is not null);
        var missingOrDeletedCount = items.Length - resolvedCount;

        return Task.FromResult(new SavedEventsResponse(
            items,
            total,
            page,
            pageSize,
            false,
            resolvedCount,
            missingOrDeletedCount,
            _seedNow));
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
            // Keep stub behavior aligned with EF persistence:
            // only canonical seeded event ids may enter authenticated ownership.
            if (!_eventIndex.ContainsKey(eventId))
            {
                return Task.FromResult(new SaveEventResponseDto(eventId, false, "Invalid userId or eventId"));
            }

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
