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

// ---------------------------------------------------------------------------
// Spatial query extensions
// ---------------------------------------------------------------------------

/// <summary>
/// Extension methods for bounding box validation and spatial queries.
/// </summary>
public static class GeoBoundingBoxExtensions
{
    /// <summary>
    /// Validates a bounding box for correctness.
    /// Throws InvalidOperationException if invalid.
    /// </summary>
    public static void Validate(this GeoBoundingBox bbox)
    {
        if (bbox.MinLat < -90 || bbox.MinLat > 90)
            throw new InvalidOperationException($"MinLat {bbox.MinLat} must be between -90 and 90");
        if (bbox.MaxLat < -90 || bbox.MaxLat > 90)
            throw new InvalidOperationException($"MaxLat {bbox.MaxLat} must be between -90 and 90");
        if (bbox.MinLng < -180 || bbox.MinLng > 180)
            throw new InvalidOperationException($"MinLng {bbox.MinLng} must be between -180 and 180");
        if (bbox.MaxLng < -180 || bbox.MaxLng > 180)
            throw new InvalidOperationException($"MaxLng {bbox.MaxLng} must be between -180 and 180");
        if (bbox.MinLat >= bbox.MaxLat)
            throw new InvalidOperationException($"MinLat ({bbox.MinLat}) must be less than MaxLat ({bbox.MaxLat})");
        if (bbox.MinLng >= bbox.MaxLng)
            throw new InvalidOperationException($"MinLng ({bbox.MinLng}) must be less than MaxLng ({bbox.MaxLng})");
    }

    /// <summary>
    /// Checks if a coordinate point is within the bounding box.
    /// </summary>
    public static bool Contains(this GeoBoundingBox bbox, double latitude, double longitude)
    {
        return latitude >= bbox.MinLat && latitude <= bbox.MaxLat &&
               longitude >= bbox.MinLng && longitude <= bbox.MaxLng;
    }

    /// <summary>
    /// Calculates the center point of the bounding box.
    /// </summary>
    public static (double Latitude, double Longitude) GetCenter(this GeoBoundingBox bbox)
    {
        return (
            (bbox.MinLat + bbox.MaxLat) / 2,
            (bbox.MinLng + bbox.MaxLng) / 2
        );
    }
}
