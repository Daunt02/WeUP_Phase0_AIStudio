using System.Globalization;
using WeUP.Contracts.Events;
using WeUP.Domain.Events;
using WeUP.Infrastructure.Seed;

namespace WeUP.Infrastructure.Persistence;

/// <summary>
/// Stub implementation — replaced by EF Core / Postgres in Phase 0.5.
/// Events OCR-extracted from real Houston flyers in /flyers/ (April 2025).
/// Venues geocoded from addresses on flyers; city center: 29.7604, -95.3698.
/// </summary>
public sealed class StubEventRepository : IEventRepository, IEventSubmissionRepository, IEventLifecycleRepository
{
    private readonly object _gate = new();
    private EventMapCardDto[] _mapCards = [];
    private EventCalendarDto[] _calendarItems = [];
    private EventDetailDto[] _details = [];
    private readonly Dictionary<string, string> _submissionStatuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _eventStatuses = new(StringComparer.OrdinalIgnoreCase);
    private int _submissionSequence;

    public void Reset(Phase0SeedDataset dataset)
    {
        var venueById = dataset.Venues.ToDictionary(v => v.VenueId, StringComparer.OrdinalIgnoreCase);

        lock (_gate)
        {
            _mapCards = dataset.Events.Select(evt =>
            {
                var venue = venueById[evt.VenueId];
                return new EventMapCardDto(
                    evt.EventId,
                    evt.Title,
                    venue.Name,
                    evt.Category,
                    venue.Latitude,
                    venue.Longitude,
                    evt.ImageUrl,
                    evt.Status,
                    evt.Confidence);
            }).ToArray();

            _calendarItems = dataset.Events.Select(evt =>
            {
                var venue = venueById[evt.VenueId];
                var marketTimezone = dataset.Markets.First(m => m.Code.Equals(venue.MarketCode, StringComparison.OrdinalIgnoreCase)).Timezone;
                return new EventCalendarDto(
                    evt.EventId,
                    evt.Title,
                    venue.Name,
                    evt.Category,
                    ParseDate(evt.StartsAtUtc),
                    ParseDate(evt.EndsAtUtc),
                    marketTimezone,
                    evt.ImageUrl,
                    evt.Status);
            }).OrderBy(item => item.StartUtc).ToArray();

            _details = dataset.Events.Select(evt =>
            {
                var venue = venueById[evt.VenueId];
                var marketTimezone = dataset.Markets.First(m => m.Code.Equals(venue.MarketCode, StringComparison.OrdinalIgnoreCase)).Timezone;
                return new EventDetailDto(
                    evt.EventId,
                    evt.Title,
                    evt.Description,
                    venue.Name,
                    venue.Address,
                    venue.Latitude,
                    venue.Longitude,
                    evt.Category,
                    ParseDate(evt.StartsAtUtc),
                    ParseDate(evt.EndsAtUtc),
                    marketTimezone,
                    [new MediaRefDto(evt.ImageUrl, "image")],
                    evt.Tags,
                    evt.Status,
                    evt.Confidence,
                    evt.SourceKind);
            }).ToArray();

            _eventStatuses.Clear();
            foreach (var evt in dataset.Events)
            {
                _eventStatuses[evt.EventId] = evt.Status;
            }

            _submissionStatuses.Clear();
            _submissionSequence = 0;
        }
    }

    public Task<MapFeedResponse> GetMapFeedAsync(MapFeedRequest request, CancellationToken ct = default)
    {
        var bb = request.Bounds;
        var events = _mapCards
            .Where(e => e.Lat >= bb.MinLat && e.Lat <= bb.MaxLat &&
                        e.Lng >= bb.MinLng && e.Lng <= bb.MaxLng)
            .Where(e => request.Categories is null || request.Categories.Length == 0 || request.Categories.Contains(e.Category, StringComparer.OrdinalIgnoreCase))
            .Where(e => e.Confidence >= request.MinConfidence)
            .ToArray();
        return Task.FromResult(new MapFeedResponse(events, events.Length));
    }

    public Task<CalendarFeedResponse> GetCalendarFeedAsync(CalendarFeedRequest request, CancellationToken ct = default)
    {
        var w = request.Window;
        var items = _calendarItems
            .Where(i => i.StartUtc >= w.StartUtc && i.StartUtc < w.EndUtc)
            .Where(i => request.Categories is null || request.Categories.Length == 0 || request.Categories.Contains(i.Category, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        return Task.FromResult(new CalendarFeedResponse(items, items.Length, request.Page, request.PageSize, false));
    }

    public Task<EventDetailResponse> GetEventDetailAsync(string eventId, CancellationToken ct = default)
    {
        var detail = _details.FirstOrDefault(d => d.Id == eventId);
        return Task.FromResult(new EventDetailResponse(detail));
    }

    public Task<string> CreateSubmissionAsync(string userId, EventSubmissionRequest request, CancellationToken ct = default)
    {
        var id = $"legacy-submission-{Interlocked.Increment(ref _submissionSequence):000}";
        lock (_gate)
        {
            _submissionStatuses[id] = "DRAFT";
        }

        return Task.FromResult(id);
    }

    public Task<string?> GetSubmissionStatusAsync(string submissionId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_submissionStatuses.TryGetValue(submissionId, out var status) ? status : null);
        }
    }

    public IReadOnlyList<EventDetailDto> SnapshotDetails()
    {
        lock (_gate)
        {
            return _details.ToArray();
        }
    }

    public Task<string?> GetLifecycleStatusAsync(string eventId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_eventStatuses.TryGetValue(eventId, out var status) ? status : null);
        }
    }

    public Task<bool> TransitionLifecycleStatusAsync(string eventId, string newStatus, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_eventStatuses.ContainsKey(eventId))
            {
                return Task.FromResult(false);
            }

            _eventStatuses[eventId] = newStatus;

            _mapCards = _mapCards
                .Select(e => e.Id.Equals(eventId, StringComparison.OrdinalIgnoreCase) ? e with { Status = newStatus } : e)
                .ToArray();
            _calendarItems = _calendarItems
                .Select(e => e.Id.Equals(eventId, StringComparison.OrdinalIgnoreCase) ? e with { Status = newStatus } : e)
                .ToArray();
            _details = _details
                .Select(e => e.Id.Equals(eventId, StringComparison.OrdinalIgnoreCase) ? e with { Status = newStatus } : e)
                .ToArray();

            return Task.FromResult(true);
        }
    }

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
}
