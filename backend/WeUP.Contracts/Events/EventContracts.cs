namespace WeUP.Contracts.Events;

// ---------------------------------------------------------------------------
// Shared primitive types
// ---------------------------------------------------------------------------

public record GeoBoundingBox(
    double MinLat,
    double MaxLat,
    double MinLng,
    double MaxLng);

public record TimeWindowRequest(
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Timezone);

// ---------------------------------------------------------------------------
// Map feed
// ---------------------------------------------------------------------------

public record MapFeedRequest(
    GeoBoundingBox Bounds,
    TimeWindowRequest Window,
    string[]? Categories = null,
    string? DistrictCode = null,
    double MinConfidence = 0.0,
    string Sort = "start_time_asc");

public record EventMapCardDto(
    string Id,
    string Title,
    string VenueName,
    string Category,
    double Lat,
    double Lng,
    string? ThumbnailUrl,
    string Status,
    double Confidence);

public record MapFeedResponse(
    EventMapCardDto[] Events,
    int TotalCount);

// ---------------------------------------------------------------------------
// Calendar feed
// ---------------------------------------------------------------------------

public record CalendarFeedRequest(
    TimeWindowRequest Window,
    string[]? Categories = null,
    string? DistrictCode = null,
    double MinConfidence = 0.0,
    string Sort = "start_time_asc",
    int Page = 1,
    int PageSize = 50);

public record EventCalendarDto(
    string Id,
    string Title,
    string VenueName,
    string Category,
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
    string Timezone,
    string? ThumbnailUrl,
    string Status);

public record CalendarFeedResponse(
    EventCalendarDto[] Items,
    int TotalCount,
    int Page,
    int PageSize,
    bool HasNextPage);

// ---------------------------------------------------------------------------
// Event detail
// ---------------------------------------------------------------------------

public record EventDetailDto(
    string Id,
    string Title,
    string? Description,
    string VenueName,
    string Address,
    double Lat,
    double Lng,
    string Category,
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
    string Timezone,
    MediaRefDto[] MediaRefs,
    string[] Tags,
    string Status,
    double Confidence,
    string SourceKind);

public record MediaRefDto(string Url, string Kind);

public record EventDetailResponse(EventDetailDto? Event);

// ---------------------------------------------------------------------------
// Event submission
// ---------------------------------------------------------------------------

public record EventSubmissionRequest(
    string Title,
    string VenueName,
    string Address,
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
    string Timezone,
    string Category,
    string? Description = null,
    string[]? Tags = null);

public record EventSubmissionResponse(
    string SubmissionId,
    string Status,
    string Message);
