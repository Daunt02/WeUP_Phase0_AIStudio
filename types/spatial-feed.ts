/**
 * SPATIAL FEED CONTRACTS v1.0
 * ============================
 *
 * Cluster-ready spatial response structures for production-grade map-first event discovery.
 *
 * DESIGN PRINCIPLES:
 * - Explicit discriminator (ItemType) prevents accidental polymorphism.
 * - Single canonical event identity (eventId) never obscured by clustering.
 * - Type guards enforce safe casting and render dispatch.
 * - Upgrade-ready: v2.0 server-side clustering requires no breaking changes.
 * - Saved/selected state remains interpretable under all clustering strategies.
 *
 * UPGRADE PATH (v1.0 → v2.0 → v3.0):
 * - v1.0: Frontend clusters; ClusteringProvider = "ClientSide"; items array has SingleEvent only.
 * - v2.0: Backend clusters; ClusteringProvider = "ServerSide"; items array has both SingleEvent and Cluster.
 * - v3.0: Hierarchical clusters; new SpatialItemType variants; old clients ignore unknown types.
 *
 * RESPONSE SEMANTICS:
 * 1. Items is discriminated union: iterate ItemType, dispatch to renderer.
 * 2. Every EventSpatialItemDto has canonical eventId (never null).
 * 3. Every ClusterSpatialItemDto has MemberEventIds (complete list, not approximated).
 * 4. Saved/Selected state always resolves by eventId (never by cluster ID).
 * 5. VisibleAreaSummary optional; provides aggregate stats for UI hints.
 *
 * TYPE SAFETY:
 * - Use isSingleEventItem() / isClusterItem() type guards BEFORE rendering.
 * - TypeScript will narrow type correctly after guard; no unsafe casts.
 * - Render functions accept narrowed types, preventing property access errors.
 */

// ============================================================================
// ENUMS
// ============================================================================

/**
 * Discriminator for spatial response items.
 * CRITICAL: Do NOT infer type from field presence (e.g., "if count exists, it's a cluster").
 * Always check ItemType explicitly.
 */
export enum SpatialItemType {
  /**
   * Single event marker. Frontend renders as individual pin.
   * Clicking shows event detail. Save/select state applies directly to eventId.
   */
  SingleEvent = "SingleEvent",

  /**
   * Cluster envelope. Frontend renders as aggregate marker.
   * Clicking expands to member events. No direct save/select on cluster; applies to members.
   */
  Cluster = "Cluster",

  // Reserved for future clustering strategies (v3.0+):
  // NestedCluster = "NestedCluster",
  // ProximityCluster = "ProximityCluster",
  // DensityCluster = "DensityCluster",
}

/**
 * Clustering provider signal (v2.0+ feature flag).
 * Allows frontend to detect whether clustering is client-side (v1.0) or server-side (v2.0+).
 */
export enum ClusteringProvider {
  /**
   * Frontend computed clusters from individual events.
   * Indicates v1.0 response; backend sent events only.
   * Frontend should apply own clustering algorithm (e.g., K-means, grid-based).
   */
  ClientSide = "ClientSide",

  /**
   * Backend computed clusters.
   * Indicates v2.0+ response; clusters pre-computed by server.
   * Frontend should trust cluster boundaries; avoid re-clustering.
   */
  ServerSide = "ServerSide",

  /**
   * Hybrid: backend pre-clusters; frontend may compute sub-clusters on expansion.
   * Reserved for v3.0+ multi-level hierarchies.
   */
  Hybrid = "Hybrid",
}

/**
 * Spatial query composition mode (echoed from backend request).
 * Tells frontend which spatial filters were applied.
 */
export enum SpatialCompositionMode {
  /**
   * Bounding box only; no taxonomy filters.
   * Used for: Freeform map exploration.
   */
  BboxOnly = "BboxOnly",

  /**
   * Taxonomy only (market/district/neighborhood); no bounding box.
   * Used for: Curated feeds, administrative boundaries.
   */
  TaxonomyOnly = "TaxonomyOnly",

  /**
   * Both bbox and taxonomy applied together.
   * Events must satisfy BOTH filters.
   * Used for: Refined searches ("downtown events in this map view").
   */
  BboxWithTaxonomy = "BboxWithTaxonomy",

  /**
   * Proximity-based queries (v2.0+, not yet supported in v1.0).
   */
  ProximityReady = "ProximityReady",
}

/**
 * Marker visual state on map.
 * Reflects selection, save status, or confidence filtering.
 */
export enum EventMarkerState {
  /**
   * Default appearance; no special styling.
   */
  Default = "DEFAULT",

  /**
   * Currently selected by user; highlight or glow effect.
   */
  Selected = "SELECTED",

  /**
   * Saved by current user; star or bookmark icon.
   */
  Saved = "SAVED",

  /**
   * Hidden due to low spatial confidence (MinSpatialConfidence filter).
   * Frontend may show in muted view or behind toggle.
   */
  LowConfidenceHidden = "LOW_CONFIDENCE_HIDDEN",
}

// ============================================================================
// INTERFACES (TypeScript contracts)
// ============================================================================

/**
 * Canonical single-event spatial marker.
 *
 * INVARIANTS:
 * - eventId is never null/empty (canonical event identity).
 * - startUtc is always present (temporal ordering).
 * - latitude and longitude are always valid WGS84 coordinates.
 * - ItemType is always SpatialItemType.SingleEvent.
 *
 * CLUSTER-READY:
 * - When rendering cluster expansion, each member event is EventSpatialItemDto.
 * - Frontend can pre-fetch and cache full event details using eventId.
 * - Saved/selected state applies directly to eventId (never derived from cluster).
 *
 * UPGRADE COMPATIBILITY:
 * - v1.0 → v2.0: Structure unchanged; no breaking changes.
 * - If used in cluster member list, treat same as top-level item.
 */
export interface EventSpatialItemDto {
  /**
   * Canonical event identifier (primary key).
   * Immutable across all API versions and clustering strategies.
   * Used for: state persistence (saved, selected, moderation), deep linking.
   */
  eventId: string;

  /**
   * Human-readable event title.
   * May be truncated on small screens.
   */
  title: string;

  /**
   * Canonical event start instant (UTC).
   * Backend-authoritative; never interpreted as local input.
   * Used for temporal ordering, filtering, and schedule display.
   */
  startUtc: string; // ISO 8601 format

  /**
   * Event end instant (UTC), if available.
   * Null for open-ended events (e.g., ongoing exhibitions).
   */
  endUtc?: string | null;

  /**
   * Venue name or location descriptor.
   * Displayed in map card and detail view.
   */
  venueName: string;

  /**
   * Latitude in WGS84 degrees ([-90, 90]).
   * Used for map marker positioning.
   */
  latitude: number;

  /**
   * Longitude in WGS84 degrees ([-180, 180]).
   * Used for map marker positioning.
   */
  longitude: number;

  /**
   * Primary event category (e.g., "concert", "theater", "food").
   * Backend ensures alignment with domain taxonomy.
   * Frontend uses for filtering, grouping, icon assignment.
   */
  primaryCategory: string;

  /**
   * Administrative district name (display-friendly, informational).
   * Null if event could not be spatially resolved to a district.
   * IMPORTANT: Never use for filtering; canonical IDs are in backend query layer only.
   */
  district?: string | null;

  /**
   * Spatial resolution confidence [0.0, 1.0].
   * Indicates quality of geocoding or district assignment.
   * Frontend may:
   *   - Hide low-confidence events (e.g., < 0.5) unless explicitly enabled.
   *   - Show confidence badge in detail view.
   *   - Use for map layer opacity/styling.
   */
  spatialConfidence: number;

  /**
   * URL to event thumbnail image.
   * Null if no image available.
   * Frontend renders placeholder when null.
   */
  thumbnailUrl?: string | null;

  /**
   * Whether current authenticated user has saved this event.
   * Null for anonymous requests.
   * Frontend uses to render save button state (filled vs outline).
   */
  savedByCurrentUser: boolean;

  /**
   * Current visual state of marker on map (serialized as uppercase string).
   * Backend computes based on: selection, save status, confidence filtering.
   * Frontend uses for styling (color, glow, opacity).
   */
  markerState: string; // e.g., "DEFAULT", "SELECTED", "SAVED"

  /**
   * Discriminator: always SpatialItemType.SingleEvent for this type.
   * Frontend type guard: if itemType !== SpatialItemType.SingleEvent, do NOT render as single event.
   */
  itemType: SpatialItemType.SingleEvent;
}

/**
 * Cluster envelope for map feed and discovery surfaces.
 *
 * INVARIANTS:
 * - clusterId is stable within a single API response (derived from spatial cell).
 * - clusterId is NOT stable across multiple API calls (re-querying may produce different clusters).
 * - count is always > 1 (single events use EventSpatialItemDto).
 * - memberEventIds length == count.
 * - ItemType is always SpatialItemType.Cluster.
 *
 * CLUSTER-READY:
 * - When expanding cluster, fetch full events for memberEventIds.
 * - Member event save/select state is independent of cluster membership.
 * - Multiple members can be Selected/Saved simultaneously.
 *
 * UPGRADE COMPATIBILITY:
 * - v1.0: ClusteringProvider = ClientSide (frontend computed); structure fully populated.
 * - v2.0: ClusteringProvider = ServerSide (backend computed); all fields populated.
 * - v3.0: Supports NestedCluster SpatialItemType (new variant); memberEventIds expands to leaf events.
 */
export interface ClusterSpatialItemDto {
  /**
   * Stable cluster identifier within this API response only.
   * Derived from spatial partitioning cell (e.g., H3 or QuadTree).
   * Used for: cluster-level interactions (expand, zoom, drill-down).
   * IMPORTANT: Do NOT persist this ID across multiple API calls.
   */
  clusterId: string;

  /**
   * Cluster center latitude (WGS84 degrees).
   * Computed as centroid of member event locations.
   * Used to position aggregate marker on map.
   */
  centerLat: number;

  /**
   * Cluster center longitude (WGS84 degrees).
   * Computed as centroid of member event locations.
   */
  centerLng: number;

  /**
   * Cluster radius in kilometers (optional).
   * Indicates spatial extent of cluster; used for "zoom-to-cluster" bounds.
   * Null if cluster span is negligible or algorithm doesn't compute radius.
   */
  radiusKm?: number | null;

  /**
   * Bounding box of all member events [minLng, minLat, maxLng, maxLat] (optional).
   * Provided if server pre-computes for efficient client-side rendering.
   * Format matches bbox contract used in SpatialQueryDto and map pan/zoom.
   * Example: "-95.71,29.75,-95.06,30.22"
   */
  boundsBox?: string | null;

  /**
   * Count of distinct events in this cluster.
   * Always > 1; single events use EventSpatialItemDto.
   * Frontend displays as label (e.g., "15 events").
   */
  count: number;

  /**
   * Complete list of canonical event IDs in this cluster (sorted).
   * Sorted for determinism (helps with caching and comparison).
   * Frontend uses to:
   *   - Fetch full event details on expansion.
   *   - Track which events are Saved/Selected.
   *   - Compute sub-clusters if needed.
   * Never approximated; always complete list.
   */
  memberEventIds: string[];

  /**
   * Clustering algorithm provider version.
   * Signals whether clustering is v1.0 (client) or v2.0+ (server).
   * Frontend may adapt UX based on provider (e.g., show sub-cluster expand icon).
   */
  clusteringProvider: ClusteringProvider;

  /**
   * Discriminator: always SpatialItemType.Cluster for this type.
   * Frontend type guard: if itemType !== SpatialItemType.Cluster, do NOT render as cluster.
   */
  itemType: SpatialItemType.Cluster;

  /**
   * Optional metadata about cluster composition (for analytics/debugging).
   * Reserved for v2.0+ server-side clustering implementations.
   * Format: JSON object or key-value pairs (TBD per backend strategy).
   * Frontend should not rely on this field; for informational/telemetry use only.
   */
  clusterMetadata?: string | null;
}

/**
 * Discriminated union of spatial response items.
 * Frontend MUST check itemType before rendering.
 */
export type SpatialItemDto = EventSpatialItemDto | ClusterSpatialItemDto;

/**
 * Visible area summary metadata (optional response envelope augmentation).
 *
 * RATIONALE:
 * When frontend is stationary (exploring map), backend provides aggregate statistics
 * about visible events. Useful for:
 *   - Summary labels ("15 events visible", "3 clusters")
 *   - Confidence distribution hints
 *   - Category breakdown
 *   - Optimal zoom level suggestions
 *
 * UPGRADE:
 * - v1.0: visibleAreaSummary is always null/omitted.
 * - v2.0+: Backend optionally computes when clustering is enabled.
 * - Frontend should gracefully ignore if omitted.
 */
export interface VisibleAreaSummary {
  /**
   * Total number of events within query bounds and temporal window.
   * Includes both single events and cluster members (expanded count).
   */
  totalEventCount: number;

  /**
   * Number of distinct clusters in visible area.
   * Only populated if clustering is enabled.
   */
  clusterCount?: number | null;

  /**
   * Average spatial confidence of visible events.
   * Hints to frontend whether geocoding quality is high or degraded.
   */
  averageSpatialConfidence: number;

  /**
   * Distribution of events by primary category.
   * Format: { "concert": 8, "theater": 4, "food": 3, ... }
   * Omitted if category breakdown is not computed.
   */
  categoryDistribution?: Record<string, number> | null;

  /**
   * Recommended zoom level for balanced cluster visibility.
   * Hint for "zoom-to-fit" behavior (TBD per GIS library: H3, QuadTree, etc.).
   * Null if backend doesn't compute or zoom level not applicable.
   */
  optimalZoomLevel?: number | null;
}

/**
 * Canonical spatial feed response envelope for map discovery and event surfaces.
 *
 * DESIGN:
 * - Single response type for all event discovery surfaces (map, calendar, saved).
 * - Items array contains explicit SpatialItemType discriminator for each entry.
 * - No polymorphic guessing; type guards enforce strict differentiation.
 * - Cluster-ready while preserving v1.0 compatibility.
 * - Metadata provides context for frontend rendering and UX hints.
 *
 * RESPONSE SEMANTICS:
 * 1. Items contains SpatialItemType.SingleEvent or SpatialItemType.Cluster.
 * 2. Frontend iterates items and dispatches: SingleEvent → pin, Cluster → aggregate.
 * 3. Saved/selected state always resolves by eventId (never by cluster ID).
 * 4. VisibleAreaSummary provides aggregate stats (optional; may be null in v1.0).
 *
 * PAGINATION & PARTIAL RESULTS:
 * - TotalCount indicates complete set size (for pagination UI).
 * - Items may be paginated; frontend loads more via offset/cursor.
 * - VisibleAreaSummary reflects ALL events in bounds (not just current page).
 *
 * BACKWARD COMPATIBILITY:
 * - Clients expecting EventMapCardDto array can safely extract EventSpatialItemDto subset.
 * - Clusters field (in old MapFeedResponse) is replaced by Items with Cluster type.
 * - No breaking changes to canonical eventId, title, lat, lng, venueName.
 */
export interface SpatialFeedResponseDto {
  /**
   * Mixed array of single events and clusters.
   * Frontend MUST inspect itemType for each entry before rendering.
   * Never assume all entries are same type.
   * Rendered in order returned; frontend may re-sort by time, confidence, distance.
   *
   * TYPE SAFETY:
   * - Use isSingleEventItem(item) / isClusterItem(item) guards before accessing type-specific properties.
   * - TypeScript will narrow type correctly after guard; no unsafe casts needed.
   */
  items: SpatialItemDto[];

  /**
   * Total count of all distinct events matching query filters.
   * Used for pagination UI ("Showing 1-50 of 500 events").
   * When clustering enabled: this is the expanded member count (not cluster count).
   */
  totalCount: number;

  /**
   * Optional aggregate statistics about visible area.
   * Null in v1.0 responses; populated when clustering enabled (v2.0+).
   * Frontend may use for summary labels or zoom-level hints.
   */
  visibleAreaSummary?: VisibleAreaSummary | null;

  /**
   * Query composition mode that was applied.
   * Echoed from SpatialQueryDto.ResolvedMode for transparency.
   * Helps frontend understand which spatial filters were active (bbox-only vs taxonomy-only vs hybrid).
   */
  compositionMode?: SpatialCompositionMode | null;

  /**
   * Clustering provider used for items in this response.
   * Default: ClusteringProvider.ClientSide (v1.0 compatibility; frontend computed clusters).
   * When ServerSide: backend pre-computed clusters; frontend should not re-cluster.
   */
  clusteringProvider: ClusteringProvider;

  /**
   * Diagnostic field: query correlation ID for tracing/debugging.
   * Helps match frontend requests to backend logs.
   */
  queryCorrelationId?: string | null;
}

// ============================================================================
// TYPE GUARDS (discriminated union helpers)
// ============================================================================

/**
 * Type guard to check if item is a single event marker.
 *
 * USAGE:
 *   for (const item of response.items) {
 *     if (isSingleEventItem(item)) {
 *       // Type narrowed to EventSpatialItemDto
 *       renderEventPin(item);
 *     }
 *   }
 *
 * CRITICAL: Always use this guard before accessing event-specific properties.
 * Do NOT use `if (item.eventId)` or similar field-presence checks.
 */
export function isSingleEventItem(
  item: SpatialItemDto,
): item is EventSpatialItemDto {
  return item.itemType === SpatialItemType.SingleEvent;
}

/**
 * Type guard to check if item is a cluster.
 *
 * USAGE:
 *   for (const item of response.items) {
 *     if (isClusterItem(item)) {
 *       // Type narrowed to ClusterSpatialItemDto
 *       renderClusterMarker(item);
 *     }
 *   }
 *
 * CRITICAL: Always use this guard before accessing cluster-specific properties.
 */
export function isClusterItem(
  item: SpatialItemDto,
): item is ClusterSpatialItemDto {
  return item.itemType === SpatialItemType.Cluster;
}

// ============================================================================
// RENDERING HELPERS
// ============================================================================

/**
 * Extract event ID from any spatial item (single event or cluster member).
 *
 * USAGE:
 *   const eventId = getEventIdForSaving(item); // Never cluster ID
 *   if (eventId) {
 *     saveEvent(eventId);
 *   }
 *
 * RATIONALE:
 * - Single events: return eventId directly.
 * - Clusters: return null so callers must expand and act on canonical member event IDs explicitly.
 * - Prevents arbitrary "first member" behavior and accidental saves against non-canonical cluster identity.
 */
export function getEventIdForSaving(item: SpatialItemDto): string | null {
  if (isSingleEventItem(item)) {
    return item.eventId;
  }
  if (isClusterItem(item)) {
    return null;
  }
  return null;
}

/**
 * Get display label for marker on map.
 *
 * USAGE:
 *   const label = getMarkerLabel(item);
 *   // Single event: "Title"
 *   // Cluster: "15 events"
 *
 * RATIONALE:
 * - Single events show title for quick recognition.
 * - Clusters show count and category breakdown hints if available.
 */
export function getMarkerLabel(item: SpatialItemDto): string {
  if (isSingleEventItem(item)) {
    return item.title;
  }
  if (isClusterItem(item)) {
    return `${item.count} events`;
  }
  return "Unknown";
}

/**
 * Get cluster member event IDs, or empty array for single event.
 *
 * USAGE:
 *   const memberIds = getMemberEventIds(item);
 *   if (memberIds.length > 1) {
 *     // It's a cluster; fetch full event details
 *   }
 *
 * RATIONALE:
 * - Simplifies conditional logic in rendering code.
 * - Safe to call on any SpatialItemDto without type checking first.
 */
export function getMemberEventIds(item: SpatialItemDto): string[] {
  if (isClusterItem(item)) {
    return item.memberEventIds;
  }
  // Single event: return array with just this event
  if (isSingleEventItem(item)) {
    return [item.eventId];
  }
  return [];
}

/**
 * Get map marker position (latitude, longitude) from any spatial item.
 *
 * USAGE:
 *   const pos = getMarkerPosition(item);
 *   map.addMarker({ lat: pos.lat, lng: pos.lng });
 *
 * RATIONALE:
 * - Single events: use their exact coordinates.
 * - Clusters: use center coordinates (centroid of members).
 */
export function getMarkerPosition(item: SpatialItemDto): {
  lat: number;
  lng: number;
} {
  if (isSingleEventItem(item)) {
    return { lat: item.latitude, lng: item.longitude };
  }
  if (isClusterItem(item)) {
    return { lat: item.centerLat, lng: item.centerLng };
  }
  return { lat: 0, lng: 0 };
}

/**
 * Get cluster expansion bounds (if available) for "zoom-to-cluster" behavior.
 *
 * USAGE:
 *   const bounds = getClusterBounds(item);
 *   if (bounds) {
 *     map.fitBounds(bounds);
 *   }
 *
 * RATIONALE:
 * - Single events: no expansion bounds; use radiusKm as fallback if needed.
 * - Clusters: prefer boundsBox if available; fall back to center + radiusKm.
 *
 * RETURNS:
 * - boundsBox (string): "[minLng,minLat,maxLng,maxLat]" format if available.
 * - radiusKm (number): cluster radius for buffer calculation.
 * - null: if neither available.
 */
export function getClusterBounds(
  item: SpatialItemDto,
): { boundsBox?: string; radiusKm?: number } | null {
  if (isClusterItem(item)) {
    return {
      boundsBox: item.boundsBox || undefined,
      radiusKm: item.radiusKm || undefined,
    };
  }
  return null;
}

/**
 * Check if an item is "saveable" (has a stable event ID).
 *
 * USAGE:
 *   if (canSaveItem(item)) {
 *     renderSaveButton(item);
 *   }
 *
 * RATIONALE:
 * - Single events: always saveable (eventId is stable).
 * - Clusters: not directly saveable; caller must expand and save members.
 */
export function canSaveItem(item: SpatialItemDto): boolean {
  if (isSingleEventItem(item)) {
    return true;
  }
  // Cluster: can't save cluster itself; must save members
  return false;
}

/**
 * Check if an item is "selectable" (can be visually highlighted on map).
 *
 * USAGE:
 *   if (canSelectItem(item)) {
 *     item.markerState = "SELECTED";
 *     renderMarker(item);
 *   }
 *
 * RATIONALE:
 * - Both single events and clusters can be selected (for highlighting).
 * - Selection state is independent of save state.
 */
export function canSelectItem(item: SpatialItemDto): boolean {
  return true; // Both SingleEvent and Cluster are selectable
}

// ============================================================================
// FILTERING HELPERS (v2.0+ server-side clustering context)
// ============================================================================

/**
 * Filter items by clustering provider (useful for UI toggles or A/B testing).
 *
 * USAGE (future v2.0+):
 *   const serverClusters = filterByClusteringProvider(
 *     response.items,
 *     ClusteringProvider.ServerSide
 *   );
 *
 * RATIONALE:
 * - v1.0: All clusters are ClientSide.
 * - v2.0+: Can mix ClientSide and ServerSide (or all ServerSide).
 * - Frontend may apply different UX/styling based on provider.
 */
export function filterByClusteringProvider(
  items: SpatialItemDto[],
  provider: ClusteringProvider,
): SpatialItemDto[] {
  return items.filter((item) => {
    if (isSingleEventItem(item)) {
      // Single events don't have clusteringProvider; always include
      return true;
    }
    if (isClusterItem(item)) {
      return item.clusteringProvider === provider;
    }
    return false;
  });
}

/**
 * Count events in response, expanding clusters.
 *
 * USAGE:
 *   const totalEvents = countExpandedEvents(response.items);
 *   console.log(`Total events visible: ${totalEvents}`);
 *
 * RATIONALE:
 * - Single events: count as 1.
 * - Clusters: count as cluster.memberEventIds.length.
 * - Useful for UI labels and analytics.
 */
export function countExpandedEvents(items: SpatialItemDto[]): number {
  return items.reduce((sum, item) => {
    if (isSingleEventItem(item)) {
      return sum + 1;
    }
    if (isClusterItem(item)) {
      return sum + item.count;
    }
    return sum;
  }, 0);
}

/**
 * Expand all clusters to individual events in response.
 *
 * USAGE (frontend clustering strategy):
 *   const expanded = expandAllClusters(response);
 *   const reordered = reOrderByDistance(expanded);
 *
 * RATIONALE:
 * - v1.0: Frontend may receive ServerSide clusters and want to re-cluster differently.
 * - This function extracts all event IDs from clusters for re-clustering logic.
 * - Does NOT fetch full event details; only uses eventId and basic geo from cluster centroid.
 *
 * CAUTION:
 * - Cluster member events are only identified by ID; full details must be fetched separately.
 * - Use getMemberEventIds() to get IDs for batch fetch.
 */
export function expandAllClusters(
  items: SpatialItemDto[],
): EventSpatialItemDto[] {
  const expanded: EventSpatialItemDto[] = [];

  for (const item of items) {
    if (isSingleEventItem(item)) {
      expanded.push(item);
    }
    // Clusters are expanded only by ID; full details must be fetched separately
    // Caller should use getMemberEventIds(cluster) to get IDs for batch API call
  }

  return expanded;
}

// ============================================================================
// COMMENTS: CLUSTERING UPGRADE SEMANTICS & IDENTITY PRESERVATION
// ============================================================================

/**
 * CLUSTERING UPGRADE PATH (v1.0 → v2.0 → v3.0)
 * ==============================================
 *
 * V1.0 (CURRENT: CLIENT-SIDE CLUSTERING)
 * -------
 * Backend sends: EventSpatialItemDto[] only (no clusters).
 * Response structure:
 *   {
 *     items: [EventSpatialItemDto, EventSpatialItemDto, ...],
 *     totalCount: 500,
 *     clusteringProvider: "ClientSide",
 *     ...
 *   }
 *
 * Frontend responsibility:
 *   - Receive flat event list.
 *   - Compute clusters client-side (e.g., via Leaflet.markercluster or H3 grid).
 *   - Render: pins for solo events, aggregate markers for clusters.
 *   - Save/select: track by eventId (cluster members have their own save state).
 *
 * Compatibility: Old MapFeedResponse.clusters field replaced by Items.
 * Clients must check itemType before rendering.
 *
 * -------
 *
 * V2.0 (FUTURE: SERVER-SIDE CLUSTERING)
 * ------
 * Backend sends: EventSpatialItemDto[] + ClusterSpatialItemDto[] (mixed).
 * Response structure:
 *   {
 *     items: [
 *       SingleEvent { eventId: "E1", itemType: "SingleEvent", ... },
 *       Cluster { clusterId: "C1", memberEventIds: ["E2", "E3", ...], itemType: "Cluster", ... },
 *       SingleEvent { eventId: "E100", itemType: "SingleEvent", ... },
 *       ...
 *     ],
 *     totalCount: 500,
 *     clusteringProvider: "ServerSide",
 *     visibleAreaSummary: { totalEventCount: 500, clusterCount: 15, ... },
 *     ...
 *   }
 *
 * Backend responsibility:
 *   - Cluster events using spatial partitioning (e.g., H3 cells, QuadTree).
 *   - Send pre-computed clusters in response.
 *   - Compute cluster centroid, member list, and metadata.
 *   - Update clusteringProvider = "ServerSide" in response.
 *
 * Frontend responsibility:
 *   - Check itemType for each entry: SingleEvent vs Cluster.
 *   - Render: pins for SingleEvent, aggregate markers for Cluster.
 *   - Save/select: still track by eventId (no cluster-level save).
 *   - Expand cluster: fetch full event details using memberEventIds for expanded view.
 *   - DO NOT re-cluster server-provided clusters (trust backend boundaries).
 *
 * Compatibility: SpatialFeedResponseDto structure unchanged; no breaking changes.
 * New ClusterSpatialItemDto type added; old clients ignore unknown itemType.
 *
 * -------
 *
 * V3.0 (FUTURE: HIERARCHICAL CLUSTERING)
 * ------
 * Backend sends: SingleEvent + Cluster + NestedCluster items (mixed).
 * New SpatialItemType.NestedCluster represents multi-level hierarchy.
 *
 * Backend responsibility:
 *   - Cluster events hierarchically (e.g., top-level grid cells, sub-cells within zoom).
 *   - Send nested cluster structure.
 *   - Compute sub-cluster centroids and member lists.
 *
 * Frontend responsibility:
 *   - Check itemType for each entry: SingleEvent, Cluster, NestedCluster.
 *   - Render: pins for SingleEvent, aggregate for Cluster, collapsible for NestedCluster.
 *   - Expand NestedCluster: show constituent Cluster or SingleEvent items.
 *   - Save/select: still track by eventId (hierarchical nesting doesn't change identity).
 *
 * Compatibility: SpatialFeedResponseDto structure unchanged.
 * New itemType value added; old clients safely ignore (treat as unknown type).
 *
 * -------
 *
 * IDENTITY PRESERVATION INVARIANTS (ALL VERSIONS)
 * -------
 * 1. EventId is immutable across all versions.
 *    - Never obscured by clustering (even in nested hierarchies).
 *    - Always canonical for state persistence (save, select, moderation).
 *
 * 2. Saved/selected state always resolves by eventId.
 *    - Single event: state applies directly to eventId.
 *    - Cluster: no direct state; members retain individual state.
 *    - Nested cluster: no direct state; leaf events retain individual state.
 *
 * 3. Cluster membership is independent of save status.
 *    - Saved events can appear in clusters (cluster membership ≠ filtering).
 *    - When expanding cluster, all members appear (including saved/selected).
 *    - No hidden members due to save filtering.
 *
 * 4. Response shape remains compatible.
 *    - SpatialFeedResponseDto structure unchanged across versions.
 *    - New itemType values can be added; old clients ignore unknown types.
 *    - No breaking changes to EventSpatialItemDto or ClusterSpatialItemDto fields.
 *
 * -------
 *
 * TEMPORAL FILTERING COMPATIBILITY
 * -------
 * Spatial clustering must remain compatible with temporal filtering (calendar overlays).
 *
 * v1.0: Frontend clusters events within temporal window.
 *   - Calendar renders events from same time window.
 *   - Cluster membership respects temporal bounds (no temporal outliers).
 *
 * v2.0+: Backend clusters events within temporal window.
 *   - Clusters represent spatial grouping within current time view.
 *   - Changing time window may re-cluster (different member set).
 *   - Calendar still renders individual events (not clusters).
 *   - Identity preserved: cluster members have same eventId across time windows.
 *
 * -------
 *
 * RESPONSE VALIDATION RULES (TYPE SAFETY)
 * -------
 * Frontend MUST enforce:
 *
 * 1. Every item in response.items has itemType field set.
 *    ✗ BAD: item.eventId exists → assume single event.
 *    ✓ GOOD: item.itemType === SpatialItemType.SingleEvent → render as single event.
 *
 * 2. SingleEvent items have eventId (non-empty string).
 *    - Used for state persistence, deep links, batch fetches.
 *    - Never null; backend validates before serialization.
 *
 * 3. Cluster items have memberEventIds (non-empty array).
 *    - count == memberEventIds.length (invariant).
 *    - Used for cluster expansion, member queries.
 *    - Never approximated; always complete list.
 *
 * 4. Saved/selected state applies only to eventId (via SingleEvent or cluster member).
 *    - ClusterId is never used for state tracking (unstable across API calls).
 *    - If SavedByCurrentUser = true, state persists across clustering strategy changes.
 */
