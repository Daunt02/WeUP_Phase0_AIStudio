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

public record LocalityFilterRequest(
    string? MarketCode = null,
    string? DistrictCode = null,
    string? NeighborhoodCode = null);

// ---------------------------------------------------------------------------
// Map feed
// ---------------------------------------------------------------------------

public record MapFeedRequest(
    GeoBoundingBox Bounds,
    TimeWindowRequest Window,
    string[]? Categories = null,
    LocalityFilterRequest? Locality = null,
    string? DistrictCode = null,
    double MinConfidence = 0.0,
    string Sort = "start_time_asc");

/// <summary>
/// Map pin card DTO — lean surface for map viewport feed.
/// Frontend-safe. No provenance, no moderation internals.
/// </summary>
public record EventMapCardDto(
    string Id,
    string Title,
    string VenueName,
    string Category,
    double Lat,
    double Lng,
    string? ThumbnailUrl,
    /// <remarks>Serialized as uppercase string (e.g. "PUBLISHED").</remarks>
    string Status,
    double Confidence);

public record MapFeedResponse(
    EventMapCardDto[] Events,
    int TotalCount,
    EventMapClusterDto[]? Clusters = null,
    string QueryMode = "bounding_box");

public record EventMapClusterDto(
    string ClusterId,
    double CenterLat,
    double CenterLng,
    int Count,
    string[] EventIds);

// ---------------------------------------------------------------------------
// Map feed v1 (canonical map discovery surface)
// ---------------------------------------------------------------------------

/// <summary>
/// Marker state returned by the canonical map feed.
/// Client selection state must still resolve by canonical eventId.
/// </summary>
public enum EventMapMarkerState
{
    Default,
    Selected,
    Saved,
    LowConfidenceHidden
}

/// <summary>
/// Canonical map item DTO for the public map surface.
/// This is a transport contract only and must not leak domain/EF internals.
/// </summary>
public record EventMapItemDto(
    string EventId,
    string Title,
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
    double Latitude,
    double Longitude,
    string VenueName,
    string? District,
    string PrimaryCategory,
    bool SavedByCurrentUser,
    string MarkerState);

/// <summary>
/// Query contract for GET /api/events/map-feed/v1.
/// Bbox format: "minLng,minLat,maxLng,maxLat".
/// </summary>
public record EventMapFeedQueryDto(
    string Bbox,
    string? TimeWindowPreset,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? District,
    string[]? Categories,
    bool IncludeSavedOnly = false);

/// <summary>
/// Optional cluster aggregate for the canonical map feed.
/// EventIds always reference canonical events and never replace event identity.
/// </summary>
public record EventMapFeedClusterDto(
    string ClusterId,
    double CenterLat,
    double CenterLng,
    int Count,
    string[] EventIds,
    int SavedCount);

/// <summary>
/// Density-control metadata shared with the client map renderer.
/// Clustering activation remains deterministic for the same filtered payload.
/// </summary>
public record EventMapDensityControlDto(
    bool ClusteringEnabled,
    int ActivationVisibleEventCountThreshold,
    double ActivationMaxZoomInclusive,
    int ClusterRadiusPixels,
    int ClusterMaxZoomInclusive,
    bool SelectedMarkerBypassEnabled,
    string ExpansionBehavior);

public record EventMapFeedV1ResponseDto(
    EventMapItemDto[] Events,
    int TotalCount,
    EventMapFeedClusterDto[] Clusters,
    EventMapDensityControlDto DensityControl,
    string ClusterStrategy = "client_v1");

// ---------------------------------------------------------------------------
// Calendar feed
// ---------------------------------------------------------------------------

public record CalendarFeedRequest(
    TimeWindowRequest Window,
    string[]? Categories = null,
    LocalityFilterRequest? Locality = null,
    string? DistrictCode = null,
    double MinConfidence = 0.0,
    string Sort = "start_time_asc",
    int Page = 1,
    int PageSize = 50);

/// <summary>
/// Calendar item DTO for list/calendar feed.
/// Prefer <see cref="EventCalendarCardDto"/> for the explicit M4-P17 contract name.
/// </summary>
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
// Calendar card (explicit named alias matching M4-P17 contract inventory)
// ---------------------------------------------------------------------------

/// <summary>
/// Calendar card DTO returned by the calendar feed endpoint.
/// Aliases EventCalendarDto for contract inventory clarity.
/// Frontend-safe fields only — no provenance, no internal moderation state.
/// </summary>
public record EventCalendarCardDto(
    string Id,
    string Title,
    string VenueName,
    string Category,
    /// <remarks>ISO 8601 UTC. Serialize as "yyyy-MM-ddTHH:mm:ssZ".</remarks>
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
    /// <remarks>IANA timezone identifier.</remarks>
    string Timezone,
    string? ThumbnailUrl,
    /// <remarks>Serialized as uppercase string (e.g. "PUBLISHED").</remarks>
    string Status);

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
    /// <remarks>ISO 8601 UTC. Serialize as "yyyy-MM-ddTHH:mm:ssZ".</remarks>
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
    /// <remarks>IANA timezone identifier.</remarks>
    string Timezone,
    MediaRefDto[] MediaRefs,
    string[] Tags,
    /// <remarks>Serialized as uppercase string (e.g. "PUBLISHED").</remarks>
    string Status,
    double Confidence,
    string SourceKind,
    /// <summary>Canonical aggregate version for optimistic client behavior.</summary>
    int Version = 1,
    /// <summary>Last classified change type (MinorMetadataUpdate, MaterialEventChange, StatusTransition, MergeLineageUpdate).</summary>
    string? LastChangeType = null,
    /// <summary>Concurrency token for conditional update calls.</summary>
    string? ConcurrencyToken = null);

public record MediaRefDto(string Url, string Kind);

public record EventDetailResponse(EventDetailDto? Event);

// ---------------------------------------------------------------------------
// Event moderation DTO — moderation-only surface (moderator auth required)
// ---------------------------------------------------------------------------

/// <summary>
/// Moderation view of a canonical event.
/// Exposes moderation and lifecycle state to authorised moderators.
/// INTERNAL/OPERATIONAL only — not for public event endpoints.
/// </summary>
public record EventModerationDto(
    /// <summary>Canonical event identity — aligns with EventMapCardDto.Id and EventDetailDto.Id.</summary>
    string CanonicalEventId,
    string Title,
    string Category,
    string VenueName,
    /// <remarks>ISO 8601 UTC.</remarks>
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
    string Timezone,
    /// <remarks>Serialized as PascalCase string matching EventLifecycleStatus enum names.</remarks>
    string LifecycleStatus,
    /// <remarks>Serialized as PascalCase string matching EventModerationStatus enum names.</remarks>
    string ModerationStatus,
    /// <remarks>Serialized as PascalCase string matching EventPublishStatus enum names.</remarks>
    string PublishStatus,
    /// <remarks>Serialized as PascalCase string matching EventRiskLevel enum names.</remarks>
    string RiskLevel,
    double ConfidenceScore,
    /// <summary>Version of the canonical aggregate — used for optimistic concurrency checks.</summary>
    int Version,
    DateTimeOffset UpdatedAtUtc,
    /// <summary>Latest classified state change type.</summary>
    string? LastChangeType,
    /// <summary>True when last applied change requires moderator confirmation before publish.</summary>
    bool HasPendingReview,
    /// <summary>Concurrency token suitable for conditional mutation requests.</summary>
    string? ConcurrencyToken,
    /// <summary>Merge lineage summary for deduplication audit. Null if not merged.</summary>
    EventMergeLineageSummaryDto? MergeLineage);

/// <summary>
/// Slim merge lineage projection for the moderation view.
/// Full provenance refs are available via the moderation evidence bundle endpoint.
/// </summary>
public record EventMergeLineageSummaryDto(
    string? ParentCanonicalEventId,
    int MergedEventCount,
    DateTimeOffset? LastMergedAtUtc);

// ---------------------------------------------------------------------------
// Publish eligibility DTO — eligibility gate surface
// ---------------------------------------------------------------------------

/// <summary>
/// Publish eligibility summary returned by the eligibility check endpoint.
/// Exposes operational eligibility state — not moderation internals.
/// </summary>
public record EventPublishEligibilityDto(
    string CanonicalEventId,
    bool Eligible,
    /// <remarks>AutoPublishable | ManualReviewRequired | Blocked</remarks>
    string EligibilityBand,
    /// <remarks>AutoPublish | RouteToManualReview | BlockPublish</remarks>
    string RecommendedAction,
    PublishBlockerSummaryDto[] Blockers,
    EligibilityConfidenceSummaryDto ConfidenceSummary,
    EligibilityFieldCompletenessSummaryDto FieldCompleteness,
    string[] Notes);

/// <summary>
/// Simplified publish blocker — safe to expose without internal metadata.
/// </summary>
public record PublishBlockerSummaryDto(
    /// <remarks>Stable code string corresponding to PublishBlockerCode enum name.</remarks>
    string Code,
    string Message,
    bool IsHardBlock,
    string? Field);

/// <summary>
/// Aggregated confidence summary for eligibility view.
/// Dimension scores are included for transparency; internal policy weights are omitted.
/// </summary>
public record EligibilityConfidenceSummaryDto(
    double Aggregate,
    /// <remarks>High | Medium | Low</remarks>
    string Band,
    double Extraction,
    double Geocode,
    double Temporal,
    double VenueMatch,
    double DupeRisk,
    bool MeetsAutoPublishThreshold,
    bool RequiresManualReview);

/// <summary>
/// Field completeness snapshot for the eligibility view.
/// </summary>
public record EligibilityFieldCompletenessSummaryDto(
    bool IsComplete,
    string[] MissingRequiredFields,
    double CompletenessRatio);

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

        // Guardrails for query scale in Phase 0: keep viewports reasonably scoped.
        var latSpan = bbox.MaxLat - bbox.MinLat;
        var lngSpan = bbox.MaxLng - bbox.MinLng;
        if (latSpan > 5)
            throw new InvalidOperationException($"Latitude span ({latSpan}) exceeds max allowed span (5)");
        if (lngSpan > 5)
            throw new InvalidOperationException($"Longitude span ({lngSpan}) exceeds max allowed span (5)");

        var area = latSpan * lngSpan;
        if (area > 8)
            throw new InvalidOperationException($"Bounding box area ({area}) exceeds max allowed area (8 square degrees)");
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
