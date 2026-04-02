using WeUP.Contracts.Events;
using WeUP.Domain.Events;
using System.Linq;

namespace WeUP.Infrastructure.Persistence;

/// <summary>
/// Stub implementation — replaced by EF Core / Postgres implementation in P08–P09.
/// Returns empty collections with correct shapes so the API surface can be tested.
/// </summary>
public sealed class StubEventRepository : IEventRepository, IEventSubmissionRepository
{
    private static readonly EventMapCardDto[] SampleMapCards = new[]
    {
        new EventMapCardDto(Guid.NewGuid().ToString("N"), "Evening Street Market", "Central Plaza", "Market", 47.608013, -122.335167, null, "PUBLISHED", 0.92),
        new EventMapCardDto(Guid.NewGuid().ToString("N"), "Indie Music Night", "The Warehouse", "Music", 47.610100, -122.342500, null, "PUBLISHED", 0.87),
        new EventMapCardDto(Guid.NewGuid().ToString("N"), "Community Yoga", "Riverside Park", "Fitness", 47.605000, -122.330000, null, "PUBLISHED", 0.75),
    };

    private static readonly EventCalendarDto[] SampleCalendarItems = SampleMapCards.Select((m, i) => new EventCalendarDto(
        m.Id,
        m.Title,
        m.VenueName,
        m.Category,
        DateTimeOffset.UtcNow.AddDays(i),
        DateTimeOffset.UtcNow.AddDays(i).AddHours(2),
        TimeZoneInfo.Utc.Id,
        m.ThumbnailUrl,
        m.Status
    )).ToArray();

    private static readonly EventDetailDto[] SampleDetails = SampleMapCards.Select((m, i) => new EventDetailDto(
        m.Id,
        m.Title,
        $"A sample description for {m.Title}.",
        m.VenueName,
        "123 Example St",
        m.Lat,
        m.Lng,
        m.Category,
        DateTimeOffset.UtcNow.AddDays(i),
        DateTimeOffset.UtcNow.AddDays(i).AddHours(2),
        TimeZoneInfo.Utc.Id,
        Array.Empty<MediaRefDto>(),
        Array.Empty<string>(),
        m.Status,
        m.Confidence,
        "stub"
    )).ToArray();

    public Task<MapFeedResponse> GetMapFeedAsync(MapFeedRequest request, CancellationToken ct = default)
    {
        var bb = request.Bounds;
        var events = SampleMapCards.Where(e => e.Lat >= bb.MinLat && e.Lat <= bb.MaxLat && e.Lng >= bb.MinLng && e.Lng <= bb.MaxLng).ToArray();
        return Task.FromResult(new MapFeedResponse(events, events.Length));
    }

    public Task<CalendarFeedResponse> GetCalendarFeedAsync(CalendarFeedRequest request, CancellationToken ct = default)
    {
        var w = request.Window;
        var items = SampleCalendarItems.Where(i => i.StartUtc >= w.StartUtc && i.StartUtc < w.EndUtc).ToArray();
        return Task.FromResult(new CalendarFeedResponse(items, items.Length, request.Page, request.PageSize, false));
    }

    public Task<EventDetailResponse> GetEventDetailAsync(string eventId, CancellationToken ct = default)
    {
        var detail = SampleDetails.FirstOrDefault(d => d.Id == eventId);
        return Task.FromResult(new EventDetailResponse(detail));
    }

    public Task<string> CreateSubmissionAsync(string userId, EventSubmissionRequest request, CancellationToken ct = default)
        => Task.FromResult(Guid.NewGuid().ToString("N"));

    public Task<string?> GetSubmissionStatusAsync(string submissionId, CancellationToken ct = default)
        => Task.FromResult<string?>("DRAFT");
}
