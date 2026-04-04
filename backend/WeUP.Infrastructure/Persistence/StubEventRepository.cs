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
        new EventMapCardDto(Guid.NewGuid().ToString("N"), "Evening Street Market", "Grand Central Market", "nightlife", 34.050000, -118.249000, null, "PUBLISHED", 0.92),
        new EventMapCardDto(Guid.NewGuid().ToString("N"), "Indie Music Night", "The Echoplex", "culture", 34.057500, -118.260000, null, "PUBLISHED", 0.87),
        new EventMapCardDto(Guid.NewGuid().ToString("N"), "Community Yoga", "Echo Park Lake", "wellness", 34.073000, -118.260000, null, "PUBLISHED", 0.75),
        new EventMapCardDto(Guid.NewGuid().ToString("N"), "Tech Meetup", "Row DTLA", "tech", 34.036000, -118.235000, null, "PUBLISHED", 0.88),
        new EventMapCardDto(Guid.NewGuid().ToString("N"), "Rooftop Cinema", "The Lot Studios", "culture", 34.063000, -118.370000, null, "PUBLISHED", 0.91),
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
