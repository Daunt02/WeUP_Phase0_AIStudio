using System.Text.Json.Serialization;

namespace WeUP.Contracts.Spatial;

/// <summary>
/// Discriminator for spatial response items.
/// DESIGN: Explicit discriminator prevents accidental polymorphism and ensures
/// type-safe rendering on the frontend. Never infer type from field presence.
/// 
/// UPGRADE PATH:
/// - v1.0 (current): Frontend receives SingleEvent or Cluster responses from simple client-side clustering.
/// - v2.0 (future): Backend computes clusters server-side using spatial partitioning.
///   Response structure remains identical; frontend detects server clusters via ClusteringProvider field.
/// - v3.0 (future): Dynamic cluster aggregation with sub-cluster expansion.
///   New response type added (e.g., NestedCluster) while SingleEvent remains unchanged.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SpatialItemType
{
    /// <summary>
    /// Single event marker. Represents one canonical event.
    /// Frontend renders as individual pin; clicking shows event details.
    /// Saved/selected state applies directly to the event ID.
    /// </summary>
    SingleEvent = 0,

    /// <summary>
    /// Cluster envelope containing multiple event references.
    /// Frontend renders as aggregate marker; clicking expands to member events.
    /// No direct saved/selected state; applies to constituent events.
    /// </summary>
    Cluster = 1,

    // Reserved for future clustering strategies without breaking v1.0 clients:
    // NestedCluster = 2,     // Multi-level cluster hierarchy (v3.0+)
    // ProximityCluster = 3,  // Proximity-based aggregation (v2.5+)
    // DensityCluster = 4,    // Density-peak clustering (v2.5+)
}

/// <summary>
/// Clustering provider signal for future server-side clustering upgrade.
/// Allows frontend to detect whether clustering happened client-side or server-side
/// without changing response structure.
///
/// USAGE:
/// - v1.0: ClientSide only (frontend computed clusters from event list)
/// - v2.0+: ServerSide indicates backend provided pre-computed clusters
/// - Future: Mix indicates hybrid clustering (both server and client contributions)
///
/// RATIONALE: Frontend can adapt rendering/UX based on clustering provider
/// without blocking on new response types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClusteringProvider
{
    /// <summary>
    /// Clustering computed client-side (frontend responsible).
    /// Indicates v1.0 response; backend sent individual events only.
    /// </summary>
    ClientSide = 0,

    /// <summary>
    /// Clustering computed server-side (backend responsible).
    /// Indicates v2.0+ response; backend pre-grouped events into clusters.
    /// Frontend should trust server cluster boundaries and not re-cluster.
    /// </summary>
    ServerSide = 1,

    /// <summary>
    /// Hybrid clustering: server provided initial clusters;
    /// frontend may compute sub-clusters for expanded views.
    /// Reserved for v3.0+ multi-level hierarchies.
    /// </summary>
    Hybrid = 2
}

/// <summary>
/// Canonical single-event spatial marker for map feed and discovery surfaces.
/// 
/// DESIGN PRINCIPLES:
/// - Preserves ALL canonical event identity (EventId, StartUtc, Title).
/// - Non-nullable fields guarantee frontend can render without nullability checks.
/// - Optional fields (ThumbnailUrl, District) gracefully render as empty/default.
/// - State fields (SavedByCurrentUser, MarkerState) always present; backend ensures accuracy.
/// - SpatialConfidence indicates resolution quality for UI filtering (e.g., hide low-confidence).
/// 
/// CLUSTER-READY PROPERTIES:
/// - When ItemType=SingleEvent, this is the authoritative marker.
/// - Frontend selection state binds to EventId (never to a computed hash or proxy).
/// - When ItemType=Cluster, member event IDs are in ClusterSpatialItemDto.memberEventIds;
///   single-event properties are not used (to prevent confusion).
/// 
/// SAVED/SELECTED STATE UNDER CLUSTERING:
/// - Selection state (MarkerState=Selected) remains canonical per event.
/// - Multiple events from same cluster can be Selected independently.
/// - When cluster member is Saved, frontend must track by eventId, not cluster.
/// - Backend must ensure consistency: if event is Saved, it appears in both
///   individual-event responses AND cluster member lists.
///
/// UPGRADE PATH (v1.0 → v2.0):
/// - v1.0: Frontend receives EventSpatialItemDto array + empty/null ClusterResult field.
/// - v2.0: Backend computes clusters server-side.
///   EventSpatialItemDto remains identical; response wraps both events and clusters.
/// - No breaking changes to EventSpatialItemDto structure.
/// </summary>
public sealed record EventSpatialItemDto : SpatialItemDto
{
    /// <summary>
    /// Canonical event identifier (primary key in domain).
    /// Immutable; used for state persistence (saved, selected, moderation).
    /// Never changes across API versions or clustering strategies.
    /// </summary>
    public string EventId { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable event title.
    /// May be truncated for display on small screens.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Canonical event start instant (UTC).
    /// Backend-authoritative; never interpreted as local wall-clock input.
    /// Used for temporal ordering and filtering.
    /// </summary>
    public DateTimeOffset StartUtc { get; init; }

    /// <summary>
    /// Event end instant (UTC), if available.
    /// Null for open-ended events (e.g., ongoing exhibitions).
    /// </summary>
    public DateTimeOffset? EndUtc { get; init; }

    /// <summary>
    /// Venue name or location descriptor.
    /// Displayed in map card and detail view.
    /// </summary>
    public string VenueName { get; init; } = string.Empty;

    /// <summary>
    /// Geographic latitude (WGS84 degrees).
    /// Must be within [-90, 90].
    /// </summary>
    public double Latitude { get; init; }

    /// <summary>
    /// Geographic longitude (WGS84 degrees).
    /// Must be within [-180, 180].
    /// </summary>
    public double Longitude { get; init; }

    /// <summary>
    /// Primary event category (e.g., "concert", "theater", "food").
    /// Backend ensures this aligns with domain taxonomy.
    /// </summary>
    public string PrimaryCategory { get; init; } = string.Empty;

    /// <summary>
    /// Administrative district name (display-friendly).
    /// Null if event could not be spatially resolved to a district.
    /// Never used for filtering (canonical IDs are in backend query layer).
    /// </summary>
    public string? District { get; init; }

    /// <summary>
    /// Spatial resolution confidence [0.0, 1.0].
    /// Indicates quality of geocoding or district assignment.
    /// Frontend may hide low-confidence events (e.g., &lt; 0.5) unless explicitly shown.
    /// Used for MinSpatialConfidence filtering in queries.
    /// </summary>
    public double SpatialConfidence { get; init; }

    /// <summary>
    /// URL to event thumbnail image.
    /// Null if no image available.
    /// Frontend gracefully renders placeholder when null.
    /// </summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>
    /// Whether current authenticated user has saved this event.
    /// Null for anonymous requests; false if not saved.
    /// Frontend uses to render save button state.
    /// </summary>
    public bool SavedByCurrentUser { get; init; }

    /// <summary>
    /// Current visual state of marker on map.
    /// Serialized as uppercase string (e.g., "SELECTED", "SAVED", "DEFAULT").
    /// Backend computes based on: selection, save status, confidence filtering.
    /// </summary>
    public string MarkerState { get; init; } = "DEFAULT";

    /// <summary>
    /// Discriminator signal: always SpatialItemType.SingleEvent for this type.
    /// Frontend type guard: if ItemType != SingleEvent, do not render as individual event.
    /// </summary>
    [JsonIgnore]
    public override SpatialItemType ItemType => SpatialItemType.SingleEvent;
}

/// <summary>
/// Cluster envelope for map feed and discovery surfaces.
/// 
/// DESIGN PRINCIPLES:
/// - Explicit cluster identity (ClusterId) independent from constituent event IDs.
/// - Cluster ID is stable within a single API response (v-tree based on cell ID).
/// - Cluster ID is NOT stable across multiple API calls (re-querying may produce different clusters).
/// - Member event IDs are canonical and always retrievable by separate API call.
/// - Cluster geometry (CenterLat, CenterLng, Radius) optional; for future expansion.
/// 
/// CLUSTER-READY PROPERTIES:
/// - Count indicates how many distinct events are in this cluster.
/// - MemberEventIds list is explicit and complete (no approximation).
/// - Clustering algorithm signal (provider) tells frontend if this is v1.0 or v2.0+.
/// - Cluster bounds (optional) enable client-side sub-clustering or zoom-to-bounds.
/// 
/// SAVED/SELECTED STATE UNDER CLUSTERING:
/// - Cluster itself has no selection state (ItemType=Cluster prevents confusion).
/// - Individual members retain their selection state (track by eventId).
/// - Frontend must not mark entire cluster as "saved"; only constituent events.
/// - When expanding cluster, render member events with their individual save states.
/// 
/// UPGRADE PATH (v1.0 → v2.0):
/// - v1.0: ClusteringProvider=ClientSide (frontend computed).
///   Cluster structure fully populated; backend may not include Radius/BoundsBox.
/// - v2.0: ClusteringProvider=ServerSide (backend computed).
///   All fields populated by backend; frontend trusts cluster boundaries.
/// - v3.0: ClusteringProvider=Hybrid.
///   Server clusters may contain sub-clusters; MemberEventIds represents expanded leaf events.
///
/// RESPONSE SEMANTICS:
/// When response contains both SingleEvent and Cluster items:
///   - Iterate ItemType to dispatch: SingleEvent → render pin; Cluster → render aggregate.
///   - Never render a Cluster as individual event (ItemType prevents this).
///   - Member event IDs in Cluster may be requested via separate batch API if needed.
/// </summary>
public sealed record ClusterSpatialItemDto : SpatialItemDto
{
    /// <summary>
    /// Stable cluster identifier within this API response.
    /// Derived from spatial partitioning cell (e.g., H3 or QuadTree cell ID).
    /// Used for cluster-level interactions (expand, zoom, drill-down).
    /// NOT stable across multiple API calls; client must not persist this ID.
    /// </summary>
    public string ClusterId { get; init; } = string.Empty;

    /// <summary>
    /// Cluster center latitude (WGS84 degrees).
    /// Computed as centroid of member event locations.
    /// Used to position aggregate marker on map.
    /// </summary>
    public double CenterLat { get; init; }

    /// <summary>
    /// Cluster center longitude (WGS84 degrees).
    /// Computed as centroid of member event locations.
    /// </summary>
    public double CenterLng { get; init; }

    /// <summary>
    /// Cluster radius in kilometers.
    /// Indicates spatial extent of cluster (optional; for UI/UX hints).
    /// Null if cluster span is negligible or algorithm doesn't compute radius.
    /// Frontend uses for "zoom-to-cluster" bounds calculation.
    /// </summary>
    public double? RadiusKm { get; init; }

    /// <summary>
    /// Bounding box of all member events [minLng, minLat, maxLng, maxLat].
    /// Optional; provided if server pre-computes for efficient client-side rendering.
    /// Format: matches bbox contract used in SpatialQueryDto and map pan/zoom.
    /// </summary>
    public string? BoundsBox { get; init; }

    /// <summary>
    /// Count of distinct events in this cluster.
    /// Always &gt; 1 (single events use EventSpatialItemDto, not ClusterSpatialItemDto).
    /// Backend guarantees MemberEventIds.Length == Count.
    /// Frontend uses for display label (e.g., "15 events").
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// Complete list of canonical event IDs in this cluster.
    /// Sorted for determinism (helps with client-side caching and comparison).
    /// Frontend uses to:
    ///   - Fetch full event details if cluster is expanded.
    ///   - Track which events are Saved/Selected within cluster.
    ///   - Compute sub-clusters if needed (v3.0 expansion).
    /// Never approximated; always complete list.
    /// </summary>
    public string[] MemberEventIds { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Clustering algorithm provider and version.
    /// Signals to frontend whether clustering is v1.0 (client) or v2.0+ (server).
    /// Default: ClientSide (v1.0 compatibility).
    /// When ServerSide, frontend should not re-cluster member events.
    /// </summary>
    public ClusteringProvider ClusteringProvider { get; init; } = ClusteringProvider.ClientSide;

    /// <summary>
    /// Discriminator signal: always SpatialItemType.Cluster for this type.
    /// Frontend type guard: if ItemType != Cluster, do not render as aggregate marker.
    /// </summary>
    [JsonIgnore]
    public override SpatialItemType ItemType => SpatialItemType.Cluster;

    /// <summary>
    /// Optional metadata about cluster composition (for future analytics/debugging).
    /// Reserved for v2.0+ server-side clustering implementations.
    /// Format: JSON object or key-value pairs (TBD per backend strategy).
    /// Examples: algorithm name, partitioning cell ID, min/max event confidence.
    /// </summary>
    public string? ClusterMetadata { get; init; }
}

/// <summary>
/// Visible area summary metadata (optional response envelope augmentation).
/// 
/// RATIONALE:
/// When frontend is stationary (exploring map), backend can provide aggregate statistics
/// about visible events without breaking response shape. Useful for:
/// - Summary labels ("15 events visible")
/// - Confidence distribution (e.g., "60% high-confidence, 40% low-confidence")
/// - Category breakdown (e.g., "8 concerts, 4 exhibitions, 3 food")
/// - Optimal zoom level hints (e.g., "zoom to level 14 for clearer separation")
///
/// UPGRADE PATH:
/// - v1.0: VisibleAreaSummary is always null/omitted (not computed by backend).
/// - v2.0+: Backend optionally computes and includes summary when clustering is enabled.
/// - Frontend should not assume presence; gracefully ignore if omitted.
/// </summary>
public sealed record VisibleAreaSummary
{
    /// <summary>
    /// Total number of events within the query bounds and temporal window.
    /// Includes both single events and cluster members (expanded count).
    /// </summary>
    public int TotalEventCount { get; init; }

    /// <summary>
    /// Number of distinct clusters in visible area.
    /// Only populated if clustering is enabled (ClusteringProvider != None).
    /// </summary>
    public int? ClusterCount { get; init; }

    /// <summary>
    /// Average spatial confidence of visible events.
    /// Hints to frontend whether geocoding quality is high or degraded.
    /// </summary>
    public double AverageSpatialConfidence { get; init; }

    /// <summary>
    /// Distribution of events by primary category.
    /// Format: { "concert": 8, "theater": 4, "food": 3, ... }
    /// Omitted if category breakdown is not computed.
    /// </summary>
    public Dictionary<string, int>? CategoryDistribution { get; init; }

    /// <summary>
    /// Recommended zoom level for balanced cluster visibility.
    /// Hint for "zoom-to-fit" behavior; TBD per GIS library (H3, etc.).
    /// Null if backend doesn't compute or zoom level is not applicable.
    /// </summary>
    public int? OptimalZoomLevel { get; init; }
}

/// <summary>
/// Canonical spatial feed response envelope for map discovery and event surfaces.
/// 
/// DESIGN PRINCIPLES:
/// - Single response type for all event discovery surfaces (map, calendar, saved).
/// - Items array contains explicit SpatialItemType discriminator for each entry.
/// - No polymorphic guessing; type guards enforce strict differentiation.
/// - Cluster-ready while preserving v1.0 compatibility.
/// - Metadata fields provide context for frontend rendering and UX hints.
///
/// RESPONSE SEMANTICS:
/// 1. Items array contains SpatialItemType.SingleEvent or SpatialItemType.Cluster.
/// 2. Frontend iterates items and dispatches: SingleEvent → pin, Cluster → aggregate.
/// 3. Saved/selected state always resolves to canonical eventId (never cluster ID).
/// 4. VisibleAreaSummary provides aggregate stats (optional; may be null in v1.0).
///
/// QUERY ALIGNMENT:
/// - Response honors spatial composition mode from SpatialQueryDto.
///   (BboxOnly, TaxonomyOnly, BboxWithTaxonomy all produce SpatialFeedResponseDto)
/// - Response honors temporal window and timezone from query.
/// - Response honors content filters (categories, minConfidence).
/// - Response includes only events/clusters matching all filters.
///
/// SAVED/SELECTED STATE CONSISTENCY:
/// - Each EventSpatialItemDto.SavedByCurrentUser reflects current save status.
/// - Each EventSpatialItemDto.MarkerState reflects selection/visual state.
/// - Cluster members retain individual state; cluster itself has no state.
/// - Backend ensures consistency: if event is Saved, it does NOT appear in
///   non-Saved queries (if filtering by saved-only); but it IS listed in
///   cluster.memberEventIds (cluster membership is independent of save status).
///
/// CLUSTERING UPGRADE PATH:
/// - v1.0 (current): Items contains SingleEvent only; clusters computed client-side.
///   Clusters field is null/empty. ClusteringProvider is not used.
/// - v2.0 (future): Items contains SingleEvent + Cluster items mixed.
///   Backend computes clusters server-side. ClusteringProvider = ServerSide.
///   Frontend trusts server boundaries; may optionally sub-cluster on expansion.
/// - v3.0+ (future): New SpatialItemType variants added (e.g., NestedCluster).
///   Existing clients ignore unknown types (forward compatibility).
///   SpatialFeedResponseDto structure unchanged.
///
/// PAGINATION & PARTIAL RESULTS:
/// - TotalCount indicates complete set size (for pagination UI).
/// - Items may be paginated; frontend should load more via offset/cursor.
/// - VisibleAreaSummary reflects ALL events in bounds (not just current page).
///
/// BACKWARD COMPATIBILITY:
/// - Clients expecting EventMapCardDto array can safely map EventSpatialItemDto subset.
/// - Clusters field (in old MapFeedResponse) is replaced by Items with Cluster type.
/// - No breaking changes to canonical EventId, Title, Lat, Lng, VenueName fields.
/// </summary>
public sealed record SpatialFeedResponseDto
{
    /// <summary>
    /// Mixed array of single events and clusters.
    /// Frontend MUST inspect ItemType for each entry before rendering.
    /// Never assume all entries are same type.
    /// Rendered in order returned; frontend may re-sort by time, confidence, or distance.
    /// </summary>
    public SpatialItemDto[] Items { get; init; } = Array.Empty<SpatialItemDto>();

    /// <summary>
    /// Total count of all distinct events matching query filters.
    /// Used for pagination UI ("Showing 1-50 of 500 events").
    /// When clustering is enabled, this is the expanded member count (not cluster count).
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Optional aggregate statistics about visible area.
    /// Null in v1.0 responses; populated when clustering is enabled (v2.0+).
    /// Frontend may use for summary labels or zoom-level hints.
    /// </summary>
    public VisibleAreaSummary? VisibleAreaSummary { get; init; }

    /// <summary>
    /// Query composition mode that was applied.
    /// Echoed from SpatialQueryDto.ResolvedMode for transparency.
    /// Helps frontend understand filtering logic (bbox-only vs taxonomy-only vs hybrid).
    /// </summary>
    public SpatialCompositionMode? CompositionMode { get; init; }

    /// <summary>
    /// Clustering provider used for Items in this response.
    /// Default: ClientSide (v1.0 compatibility; frontend computed clusters).
    /// When ServerSide: backend pre-computed clusters; frontend should not re-cluster.
    /// </summary>
    public ClusteringProvider ClusteringProvider { get; init; } = ClusteringProvider.ClientSide;

    /// <summary>
    /// Diagnostic field: query correlation ID for tracing/debugging.
    /// Helps match frontend requests to backend logs.
    /// </summary>
    public string? QueryCorrelationId { get; init; }
}

/// <summary>
/// Discriminated union type for SpatialFeedResponseDto.Items.
/// Frontend type guards use ItemType field to dispatch to correct renderer.
/// 
/// C# DESIGN:
/// This discriminated union is represented as a sealed record base class
/// that cannot be directly instantiated. Consumers must check ItemType and cast
/// to EventSpatialItemDto or ClusterSpatialItemDto.
/// 
/// RATIONALE (vs explicit union):
/// - JSON serialization uses type discriminator (ItemType field).
/// - Deserialization is single-step (no custom converter needed).
/// - Type safety at compile-time via sealed hierarchy.
/// - Frontend can safely cast after checking ItemType.
///
/// Note: In C#, we represent this as:
///   base class SpatialItemDto (not instantiable, ItemType is abstract)
///   derived EventSpatialItemDto : SpatialItemDto
///   derived ClusterSpatialItemDto : SpatialItemDto
///
/// However, for JSON serialization simplicity, we use direct records above.
/// If type hierarchy is needed later, add abstract base SpatialItemDto.
/// </summary>
// This base type makes the mixed items collection explicit while preserving a stable JSON shape.
// Server-side clustering can add new derived types later without changing the SpatialFeedResponseDto envelope.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "itemType")]
[JsonDerivedType(typeof(EventSpatialItemDto), nameof(SpatialItemType.SingleEvent))]
[JsonDerivedType(typeof(ClusterSpatialItemDto), nameof(SpatialItemType.Cluster))]
public abstract record SpatialItemDto
{
    /// <summary>
    /// Explicit response discriminator. Single-event items preserve canonical EventId;
    /// cluster items expose only aggregate identity plus canonical member event IDs.
    /// </summary>
    public abstract SpatialItemType ItemType { get; }
}
/// 