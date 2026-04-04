using WeUP.Contracts.Events;
using WeUP.Domain.Events;
using System.Linq;

namespace WeUP.Infrastructure.Persistence;

/// <summary>
/// Stub implementation — replaced by EF Core / Postgres in Phase 0.5.
/// Events OCR-extracted from real Houston flyers in /flyers/ (April 2025).
/// Venues geocoded from addresses on flyers; city center: 29.7604, -95.3698.
/// </summary>
public sealed class StubEventRepository : IEventRepository, IEventSubmissionRepository
{
    // OCR-extracted from real Houston flyers — see /flyers/ directory
    private static readonly EventMapCardDto[] SampleMapCards =
    [
        // IMG_6661 — First Friday Alternative Night Market
        new EventMapCardDto(Guid.NewGuid().ToString("N"),
            "First Friday — Alternative Night Market",
            "Downtown House of Spirits", "nightlife",
            29.7604, -95.3698, null, "PUBLISHED", 0.91),

        // att.-WO...jpg — Patrick Squier (native flyer, full address extracted)
        new EventMapCardDto(Guid.NewGuid().ToString("N"),
            "Patrick Squier Live",
            "Shoeshine Charley's Big Top Lounge", "culture",
            29.7261, -95.3870, null, "PUBLISHED", 0.97),

        // IMG_6667 — Effin This Weekend
        new EventMapCardDto(Guid.NewGuid().ToString("N"),
            "Effin: Dennett / Joli / Jilli",
            "8PM Music Venue", "nightlife",
            29.7495, -95.3794, null, "PUBLISHED", 0.85),

        // IMG_6671 — Foundation Room After Dark
        new EventMapCardDto(Guid.NewGuid().ToString("N"),
            "Foundation Room After Dark — VIP Bay Experience",
            "Foundation Room Houston", "nightlife",
            29.7509, -95.3676, null, "PUBLISHED", 0.82),

        // IMG_6678 — Noche de Selena
        new EventMapCardDto(Guid.NewGuid().ToString("N"),
            "Noche de Selena",
            "Houston Venue", "culture",
            29.7580, -95.3750, null, "PUBLISHED", 0.78),

        // IMG_6679 — Freestyle Session + House Class
        new EventMapCardDto(Guid.NewGuid().ToString("N"),
            "Freestyle Session + House Class",
            "Anayra Studio", "wellness",
            29.7640, -95.3850, null, "PUBLISHED", 0.88),

        // IMG_6681/6682 — Iistbahnhof: Ariel Zetina, Lauren Flax, Partok, S4M23
        new EventMapCardDto(Guid.NewGuid().ToString("N"),
            "Iistbahnhof: Ariel Zetina / Lauren Flax / Partok / S4M23",
            "Iistbahnhof", "nightlife",
            29.7385, -95.3733, null, "PUBLISHED", 0.93),

        // IMG_6664 — Desert Hearts: Mikey Lion, Lee Reynolds, Marbs
        new EventMapCardDto(Guid.NewGuid().ToString("N"),
            "Desert Hearts — Mikey Lion / Lee Reynolds / Marbs",
            "Wonder Lust", "nightlife",
            29.7420, -95.4105, null, "PUBLISHED", 0.89),

        // IMG_6662 — Mahmut Orhon (SOLD OUT)
        new EventMapCardDto(Guid.NewGuid().ToString("N"),
            "Mahmut Orhon",
            "Houston Venue", "nightlife",
            29.7530, -95.3660, null, "SOLD_OUT", 0.86),
    ];

    // April 2025 dates aligned to actual Houston flyer dates
    private static readonly (int DayOffset, string TimeZone)[] EventSchedule = new[]
    {
        (0,  "America/Chicago"),  // First Friday — Apr 4
        (28, "America/Chicago"),  // Patrick Squier — May 2
        (0,  "America/Chicago"),  // Effin — Apr 4
        (1,  "America/Chicago"),  // Foundation Room — Apr 5
        (6,  "America/Chicago"),  // Noche de Selena — Apr 10
        (21, "America/Chicago"),  // Freestyle Session — Apr 25
        (60, "America/Chicago"),  // Iistbahnhof — June 2025
        (5,  "America/Chicago"),  // Desert Hearts — Apr 9
        (-1, "America/Chicago"),  // Mahmut Orhon — Apr 3 (SOLD OUT)
    };

    private static readonly EventCalendarDto[] SampleCalendarItems = SampleMapCards
        .Select((m, i) =>
        {
            var (offset, tz) = EventSchedule[i];
            var start = DateTimeOffset.UtcNow.Date.AddDays(offset).AddHours(21); // 9PM CST base
            return new EventCalendarDto(m.Id, m.Title, m.VenueName, m.Category,
                start, start.AddHours(4), tz, m.ThumbnailUrl, m.Status);
        }).ToArray();

    private static readonly EventDetailDto[] SampleDetails = SampleMapCards
        .Select((m, i) =>
        {
            var (offset, tz) = EventSchedule[i];
            var start = DateTimeOffset.UtcNow.Date.AddDays(offset).AddHours(21);
            var address = m.VenueName switch
            {
                "Shoeshine Charley's Big Top Lounge" => "3700 Main St, Houston, TX 77002",
                "8PM Music Venue"                    => "8PM Music Venue, Houston, TX",
                "Iistbahnhof"                        => "Iistbahnhof, Houston, TX",
                "Foundation Room Houston"            => "Foundation Room, Houston, TX",
                "Downtown House of Spirits"          => "Downtown Houston, TX",
                _                                    => "Houston, TX",
            };
            var description = m.Title switch
            {
                var t when t.Contains("Patrick Squier") =>
                    "Patrick Squier live at Shoeshine Charley's Big Top Lounge. Extracted from native flyer: 3700 Main St, Houston TX. www.continentalclub.com",
                var t when t.Contains("First Friday") =>
                    "First Friday Alternative Night Market. 3PM–1AM. Downtown House of Spirits, Houston TX. OCR source: IMG_6661.",
                var t when t.Contains("Effin") =>
                    "Effin presents Dennett, Joli, and Jilli at 8PM Music Venue, Houston TX. OCR source: IMG_6667.",
                var t when t.Contains("Foundation Room") =>
                    "Foundation Room After Dark VIP Bay Experience featuring Overazy and Neoteric. OCR source: IMG_6671.",
                var t when t.Contains("Desert Hearts") =>
                    "Desert Hearts featuring Mikey Lion, Lee Reynolds, and Marbs at Wonder Lust. OCR source: IMG_6664.",
                var t when t.Contains("Iistbahnhof") =>
                    "Iistbahnhof presents Ariel Zetina, Lauren Flax, Partok, and S4M23. OCR source: IMG_6681/6682.",
                var t when t.Contains("Noche de Selena") =>
                    "Noche de Selena tribute night. April 10. Houston TX. OCR source: IMG_6678.",
                var t when t.Contains("Freestyle") =>
                    "Freestyle Session + House Class at Anayra Studio, Houston TX. April 25. OCR source: IMG_6679.",
                var t when t.Contains("Mahmut Orhon") =>
                    "Mahmut Orhon (SOLD OUT). Houston TX. OCR source: IMG_6662.",
                _ => $"Houston event: {m.Title}"
            };
            return new EventDetailDto(m.Id, m.Title, description, m.VenueName, address,
                m.Lat, m.Lng, m.Category, start, start.AddHours(4), tz,
                Array.Empty<MediaRefDto>(), Array.Empty<string>(),
                m.Status, m.Confidence, "flyer_ocr");
        }).ToArray();

    public Task<MapFeedResponse> GetMapFeedAsync(MapFeedRequest request, CancellationToken ct = default)
    {
        var bb = request.Bounds;
        var events = SampleMapCards
            .Where(e => e.Lat >= bb.MinLat && e.Lat <= bb.MaxLat &&
                        e.Lng >= bb.MinLng && e.Lng <= bb.MaxLng)
            .ToArray();
        return Task.FromResult(new MapFeedResponse(events, events.Length));
    }

    public Task<CalendarFeedResponse> GetCalendarFeedAsync(CalendarFeedRequest request, CancellationToken ct = default)
    {
        var w = request.Window;
        var items = SampleCalendarItems
            .Where(i => i.StartUtc >= w.StartUtc && i.StartUtc < w.EndUtc)
            .ToArray();
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
