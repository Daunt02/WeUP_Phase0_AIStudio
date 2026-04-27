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
    /// <remarks>
    /// Canonical event start instant in UTC.
    /// This is storage-authoritative and must not be interpreted as local wall-clock input.
    /// </remarks>
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
/// Canonical temporal presets for map discovery and calendar overlays.
/// The backend owns preset expansion so the frontend never derives time windows independently.
/// </summary>
public enum TimeWindowPreset
{
    Now,
    Tonight,
    Tomorrow,
    ThisWeekend,
    Custom,
    Next24Hours,
    Next48Hours,
    CustomRange,
}

/// <summary>
/// Query contract for GET /api/events/map-feed/v1.
/// Bbox format: "minLng,minLat,maxLng,maxLat".
/// Timezone is required for all requests so preset expansion and downstream calendar overlays stay aligned.
/// </summary>
public sealed record EventMapFeedQueryDto
{
    public string Bbox { get; init; } = string.Empty;
    public TimeWindowPreset Preset { get; init; } = TimeWindowPreset.Now;
    public string Timezone { get; init; } = string.Empty;
    /// <summary>
    /// Canonical market timezone alias for cross-surface temporal parity.
    /// Legacy <see cref="Timezone"/> remains for backward compatibility.
    /// </summary>
    public string? MarketTimezone { get; init; }
    /// <summary>
    /// Canonical explicit custom-range start in UTC.
    /// Legacy <see cref="CustomStartUtc"/> remains for backward compatibility.
    /// </summary>
    public DateTimeOffset? FromUtc { get; init; }
    /// <summary>
    /// Canonical explicit custom-range end in UTC.
    /// Legacy <see cref="CustomEndUtc"/> remains for backward compatibility.
    /// </summary>
    public DateTimeOffset? ToUtc { get; init; }
    /// <summary>
    /// Optional deterministic reference instant for server-side preset expansion.
    /// </summary>
    public DateTimeOffset? ReferenceInstantUtc { get; init; }
    public DateTimeOffset? CustomStartUtc { get; init; }
    public DateTimeOffset? CustomEndUtc { get; init; }
    public string? District { get; init; }
    public string[]? Categories { get; init; }
    public bool IncludeSavedOnly { get; init; }
}

/// <summary>
/// Enhanced query contract for GET /api/events/map-feed/v2 (spatial query semantics).
/// Integrates canonical spatial filtering with event discovery.
/// 
/// This DTO wraps SpatialQueryDto and adds temporal/categorical filters.
/// Map, calendar, and saved discovery surfaces all bind to this contract,
/// ensuring consistent spatial semantics across all event feeds.
/// 
/// BINDING EXAMPLE:
///   GET /api/events/map-feed/v2?marketIds=houston,dallas&districtIds=downtown&bbox=...&preset=now&timezone=America/Chicago
///   
/// SPATIAL DIMENSIONS (mutually-exclusive combinations):
/// 1. BboxOnly: bbox only (freeform map exploration).
/// 2. TaxonomyOnly: marketIds/districtIds/neighborhoodIds only (curated feed).
/// 3. BboxWithTaxonomy: both bbox and taxonomy (refined search).
/// 
/// All spatial dimensions resolve to a single composition mode during validation.
/// Backend must reject ambiguous or invalid spatial combinations.
/// </summary>
public sealed record EventMapFeedQueryV2Dto
{
    /// <summary>
    /// Market identity filter (canonical IDs).
    /// Corresponds to SpatialQueryDto.MarketIds.
    /// </summary>
    public string[]? MarketIds { get; init; }

    /// <summary>
    /// Market filter using URL-friendly slugs.
    /// Backend resolves to MarketIds. Mutually exclusive with MarketIds.
    /// </summary>
    public string[]? MarketSlugs { get; init; }

    /// <summary>
    /// District identity filter (canonical IDs).
    /// Corresponds to SpatialQueryDto.DistrictIds.
    /// </summary>
    public string[]? DistrictIds { get; init; }

    /// <summary>
    /// District filter using URL-friendly slugs.
    /// Backend resolves to DistrictIds. Mutually exclusive with DistrictIds.
    /// </summary>
    public string[]? DistrictSlugs { get; init; }

    /// <summary>
    /// Neighborhood identity filter (canonical IDs).
    /// Corresponds to SpatialQueryDto.NeighborhoodIds.
    /// </summary>
    public string[]? NeighborhoodIds { get; init; }

    /// <summary>
    /// Neighborhood filter using URL-friendly slugs.
    /// Backend resolves to NeighborhoodIds. Mutually exclusive with NeighborhoodIds.
    /// </summary>
    public string[]? NeighborhoodSlugs { get; init; }

    /// <summary>
    /// Bounding box geometry: "minLng,minLat,maxLng,maxLat".
    /// When non-empty, filters events within this WGS84 envelope.
    /// </summary>
    public string? Bbox { get; init; }

    /// <summary>
    /// When true, district filters include all descendant neighborhoods.
    /// When false, only direct district members.
    /// Default: true.
    /// </summary>
    public bool IncludeDescendants { get; init; } = true;

    /// <summary>
    /// Confidence threshold for spatial resolution (range [0.0, 1.0]).
    /// Events below this threshold are excluded.
    /// Default: 0.0 (no minimum).
    /// </summary>
    public double MinSpatialConfidence { get; init; } = 0.0;

    // Temporal dimensions (shared with EventMapFeedQueryDto)
    public TimeWindowPreset Preset { get; init; } = TimeWindowPreset.Now;
    public string Timezone { get; init; } = string.Empty;
    public string? MarketTimezone { get; init; }
    public DateTimeOffset? FromUtc { get; init; }
    public DateTimeOffset? ToUtc { get; init; }
    public DateTimeOffset? ReferenceInstantUtc { get; init; }
    public DateTimeOffset? CustomStartUtc { get; init; }
    public DateTimeOffset? CustomEndUtc { get; init; }

    // Categorical/content filters
    public string[]? Categories { get; init; }
    public bool IncludeSavedOnly { get; init; }

    /// <summary>
    /// Convert to canonical SpatialQueryDto for validation and execution.
    /// Backend query handlers should use this method to extract spatial semantics.
    /// </summary>
    public Spatial.SpatialQueryDto ToSpatialQuery()
    {
        return new Spatial.SpatialQueryDto
        {
            MarketIds = this.MarketIds,
            MarketSlugs = this.MarketSlugs,
            DistrictIds = this.DistrictIds,
            DistrictSlugs = this.DistrictSlugs,
            NeighborhoodIds = this.NeighborhoodIds,
            NeighborhoodSlugs = this.NeighborhoodSlugs,
            Bbox = this.Bbox,
            IncludeDescendants = this.IncludeDescendants,
            MinConfidence = this.MinSpatialConfidence
        };
    }
}

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
// Calendar feed v1 (canonical temporal overlay projection)
// ---------------------------------------------------------------------------

/// <summary>
/// Query contract for GET /api/events/calendar-feed/v1.
/// This mirrors EventMapFeedQueryDto so map and calendar remain alternate
/// projections of the same canonical event set and temporal window.
/// </summary>
public sealed record EventCalendarFeedQueryDto
{
    public string Bbox { get; init; } = string.Empty;
    public TimeWindowPreset Preset { get; init; } = TimeWindowPreset.Now;
    public string Timezone { get; init; } = string.Empty;
    /// <summary>
    /// Canonical market timezone alias for cross-surface temporal parity.
    /// Legacy <see cref="Timezone"/> remains for backward compatibility.
    /// </summary>
    public string? MarketTimezone { get; init; }
    /// <summary>
    /// Canonical explicit custom-range start in UTC.
    /// Legacy <see cref="CustomStartUtc"/> remains for backward compatibility.
    /// </summary>
    public DateTimeOffset? FromUtc { get; init; }
    /// <summary>
    /// Canonical explicit custom-range end in UTC.
    /// Legacy <see cref="CustomEndUtc"/> remains for backward compatibility.
    /// </summary>
    public DateTimeOffset? ToUtc { get; init; }
    /// <summary>
    /// Optional deterministic reference instant for server-side preset expansion.
    /// </summary>
    public DateTimeOffset? ReferenceInstantUtc { get; init; }
    public DateTimeOffset? CustomStartUtc { get; init; }
    public DateTimeOffset? CustomEndUtc { get; init; }
    public string? District { get; init; }
    public string[]? Categories { get; init; }
    public bool IncludeSavedOnly { get; init; }
}

/// <summary>
/// Calendar overlay item DTO.
/// Event identity and shared attributes must remain contract-compatible with
/// EventMapItemDto to prevent map/calendar divergence.
/// </summary>
public record CalendarEventItemDto(
    string EventId,
    string Title,
    /// <remarks>
    /// Canonical event start instant in UTC.
    /// Frontend display should project this into the supplied Timezone.
    /// </remarks>
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
    /// <remarks>
    /// IANA timezone used for display projection and market semantics.
    /// It does not change StartUtc/EndUtc storage values.
    /// </remarks>
    string Timezone,
    string VenueName,
    string? District,
    string PrimaryCategory,
    bool SavedByCurrentUser,
    string MarkerState,
    string? ThumbnailUrl);

public record EventCalendarFeedV1ResponseDto(
    CalendarEventItemDto[] Items,
    int TotalCount,
    string Projection = "temporal_grid_v1");

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
    /// <remarks>
    /// ISO 8601 UTC. Serialize as "yyyy-MM-ddTHH:mm:ssZ".
    /// Canonical storage is UTC only; display projection is driven by Timezone.
    /// </remarks>
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

public record EventDetailProvenanceSummaryDto(
    string PrimarySourceKind,
    int SourceCount,
    /// <remarks>ISO 8601 UTC. Serialize as "yyyy-MM-ddTHH:mm:ssZ".</remarks>
    DateTimeOffset FirstObservedAtUtc,
    /// <remarks>ISO 8601 UTC. Serialize as "yyyy-MM-ddTHH:mm:ssZ".</remarks>
    DateTimeOffset LastObservedAtUtc,
    string SummaryLabel);

public record EventDetailDto(
    string Id,
    string Title,
    string? Description,
    string VenueName,
    string Address,
    double Lat,
    double Lng,
    string Category,
    string[] Categories,
    /// <remarks>
    /// ISO 8601 UTC. Serialize as "yyyy-MM-ddTHH:mm:ssZ".
    /// This is the canonical persisted instant and must remain UTC across all layers.
    /// </remarks>
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
    /// <remarks>
    /// IANA timezone identifier used for market/event-local rendering.
    /// This field carries projection context and is not a second storage clock.
    /// </remarks>
    string Timezone,
    string? FlyerImageUrl,
    MediaRefDto[] MediaRefs,
    string[] Tags,
    /// <remarks>Serialized as uppercase string (e.g. "PUBLISHED").</remarks>
    string Status,
    double Confidence,
    string SourceKind,
    EventDetailProvenanceSummaryDto ProvenanceSummary,
    bool SavedByCurrentUser = false,
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
        var validation = GeoValidationRules.ValidateBoundingBox(bbox);
        if (validation.IsValid)
        {
            return;
        }

        throw new InvalidOperationException(validation.Issues[0].Message);
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
