/**
 * Canonical spatial query semantics and filtering logic — TypeScript contracts.
 *
 * These interfaces mirror the C# backend contracts exactly, ensuring:
 * - Frontend and backend use identical spatial filter semantics.
 * - Type-safe query composition across map, calendar, and saved surfaces.
 * - No implicit fallbacks or hidden text-matching behavior.
 *
 * All spatial filters reference canonical IDs or slugs, never display text.
 */

/**
 * Composition mode for spatial queries.
 * Determines how spatial filter dimensions interact and apply.
 *
 * This is the single source of truth for spatial query semantics.
 */
export enum SpatialCompositionMode {
  /**
   * BboxOnly: Query uses only bounding box geometry. No taxonomy filters.
   * Precedence: bbox overrides any taxonomy dimension.
   * Used for: Freeform map exploration, raw geo-spatial discovery.
   */
  BboxOnly = 0,

  /**
   * TaxonomyOnly: Query uses only market/district/neighborhood IDs. No bbox.
   * Precedence: marketId > districtIds > neighborhoodIds (market is most restrictive).
   * Used for: Curated feeds, administrative boundaries, market/district drill-down.
   */
  TaxonomyOnly = 1,

  /**
   * BboxWithTaxonomy: Query uses bbox AND taxonomy filters together.
   * Precedence: bbox applies first (geometric filter), then taxonomy filters within bbox.
   * Used for: Refined searches (e.g., "events in Downtown within this map view").
   */
  BboxWithTaxonomy = 2,

  /**
   * ProximityReady: Reserved for proximity-based queries (v2.0+).
   * Not yet supported in v1.0.
   */
  ProximityReady = 99,
}

/**
 * Validation result returned after parsing/validating a spatial query.
 * Contains the resolved composition mode and any validation errors.
 */
export interface SpatialQueryValidationResult {
  /** Resolved composition mode, or null if validation failed. */
  readonly mode: SpatialCompositionMode | null;

  /** Array of validation errors. Empty when valid. */
  readonly errors: readonly string[];

  /** Normalized query after applying composition rules. Null when validation fails. */
  readonly normalizedQuery: SpatialQueryDto | null;
}

/**
 * Canonical spatial query contract for map feed, calendar feed, and saved discovery.
 * This is the single source of truth for all spatial filtering across all surfaces.
 *
 * DESIGN PRINCIPLES:
 * - All spatial dimensions are explicit (no hidden fallbacks to text matching).
 * - Composition modes define strict precedence rules.
 * - Taxonomy filters always reference canonical IDs or slugs, never display text.
 * - One market per query (enforced at event entity level).
 * - At most one district and one neighborhood per event.
 *
 * FILTER SEMANTICS:
 * - marketIds: Array of market IDs. Filters events to those markets only.
 *   When non-empty, districtIds and neighborhoodIds must belong to these markets.
 * - districtIds: Array of district IDs. Filters events to those districts (and their descendants).
 *   When non-empty, marketIds can be inferred or must be compatible.
 * - neighborhoodIds: Array of neighborhood IDs. Filters events to those neighborhoods.
 *   When non-empty, districtIds can be inferred or must be compatible.
 * - bbox: Bounding box geometry in degrees. Filters to events within the envelope.
 *   In BboxWithTaxonomy mode, applied together with taxonomy.
 * - includeDescendants: When true, districtIds includes all child neighborhoods.
 *   When false, only exact district members (not nested neighborhoods).
 */
export interface SpatialQueryDto {
  /**
   * Market identity filter. Canonical market IDs (primary key).
   * Empty array or undefined = no market filter.
   * All markets are applied in OR logic (matches any market).
   */
  readonly marketIds?: readonly string[];

  /**
   * Alternative market filter using market slugs (URL-friendly identifiers).
   * Mutually exclusive with marketIds for a single query binding.
   * Backend normalizes to marketIds after taxonomy lookup.
   * Empty array or undefined = no market filter via slugs.
   */
  readonly marketSlugs?: readonly string[];

  /**
   * District identity filter. Canonical district IDs.
   * Empty array or undefined = no district filter.
   * All districts are applied in OR logic (matches any district).
   * When includeDescendants=true, automatically includes child neighborhoods.
   */
  readonly districtIds?: readonly string[];

  /**
   * Alternative district filter using district slugs.
   * Mutually exclusive with districtIds for a single query binding.
   * Backend normalizes to districtIds after taxonomy lookup.
   * Empty array or undefined = no district filter via slugs.
   */
  readonly districtSlugs?: readonly string[];

  /**
   * Neighborhood identity filter. Canonical neighborhood IDs.
   * Empty array or undefined = no neighborhood filter.
   * All neighborhoods are applied in OR logic (matches any neighborhood).
   * Can only be used together with compatible districtIds.
   */
  readonly neighborhoodIds?: readonly string[];

  /**
   * Alternative neighborhood filter using neighborhood slugs.
   * Mutually exclusive with neighborhoodIds for a single query binding.
   * Backend normalizes to neighborhoodIds after taxonomy lookup.
   * Empty array or undefined = no neighborhood filter via slugs.
   */
  readonly neighborhoodSlugs?: readonly string[];

  /**
   * Bounding box in WGS84 degrees: "minLng,minLat,maxLng,maxLat".
   * Undefined or empty string = no spatial envelope filter.
   * When present in BboxWithTaxonomy mode, events must fall within this box AND match taxonomy.
   */
  readonly bbox?: string;

  /**
   * When true, district filters include all descendant neighborhoods.
   * When false, only direct district members (neighborhoods that list this district as parent).
   * Default: true (include descendants).
   * Only applies when districtIds or districtSlugs is non-empty.
   */
  readonly includeDescendants?: boolean;

  /**
   * Resolved composition mode after validation.
   * Not set during query binding; determined by validation logic.
   * Frontend should not set this value.
   */
  readonly resolvedMode?: SpatialCompositionMode;

  /**
   * Quality metadata: confidence threshold for spatial resolution.
   * Events with resolution confidence below this are excluded.
   * Default: 0.0 (no minimum).
   * Range: [0.0, 1.0].
   */
  readonly minConfidence?: number;
}

/**
 * Enhanced query contract for map feed v2 with spatial query semantics.
 * Integrates canonical spatial filtering with event discovery.
 *
 * This interface wraps SpatialQueryDto and adds temporal/categorical filters.
 * Map, calendar, and saved discovery surfaces all use this contract,
 * ensuring consistent spatial semantics across all event feeds.
 *
 * BINDING EXAMPLE (query string):
 *   marketIds=houston,dallas&districtIds=downtown&bbox=-95.7129,29.7589,-95.0681,30.2271&preset=now&timezone=America/Chicago
 *
 * SPATIAL DIMENSIONS (mutually-exclusive combinations):
 * 1. BboxOnly: bbox only (freeform map exploration).
 * 2. TaxonomyOnly: marketIds/districtIds/neighborhoodIds only (curated feed).
 * 3. BboxWithTaxonomy: both bbox and taxonomy (refined search).
 */
export interface EventMapFeedQueryV2Dto {
  // Spatial dimensions

  /** Market identity filter (canonical IDs). Corresponds to SpatialQueryDto.marketIds. */
  readonly marketIds?: readonly string[];

  /** Market filter using URL-friendly slugs. Backend resolves to marketIds. Mutually exclusive with marketIds. */
  readonly marketSlugs?: readonly string[];

  /** District identity filter (canonical IDs). Corresponds to SpatialQueryDto.districtIds. */
  readonly districtIds?: readonly string[];

  /** District filter using URL-friendly slugs. Backend resolves to districtIds. Mutually exclusive with districtIds. */
  readonly districtSlugs?: readonly string[];

  /** Neighborhood identity filter (canonical IDs). Corresponds to SpatialQueryDto.neighborhoodIds. */
  readonly neighborhoodIds?: readonly string[];

  /** Neighborhood filter using URL-friendly slugs. Backend resolves to neighborhoodIds. Mutually exclusive with neighborhoodIds. */
  readonly neighborhoodSlugs?: readonly string[];

  /**
   * Bounding box geometry: "minLng,minLat,maxLng,maxLat".
   * When non-empty, filters events within this WGS84 envelope.
   */
  readonly bbox?: string;

  /**
   * When true, district filters include all descendant neighborhoods.
   * When false, only direct district members.
   * Default: true.
   */
  readonly includeDescendants?: boolean;

  /**
   * Confidence threshold for spatial resolution (range [0.0, 1.0]).
   * Events below this threshold are excluded.
   * Default: 0.0 (no minimum).
   */
  readonly minSpatialConfidence?: number;

  // Temporal dimensions (shared with EventMapFeedQueryDto)

  /** Temporal preset (e.g., "now", "tonight", "tomorrow", "customRange"). */
  readonly preset?: string;

  /** IANA timezone for temporal projection and market semantics. */
  readonly timezone?: string;

  /** Canonical timezone field shared by map, calendar, and saved temporal filtering. */
  readonly marketTimezone?: string;

  /** ISO 8601 UTC start for custom-range requests. */
  readonly fromUtc?: string | Date;

  /** ISO 8601 UTC end for custom-range requests. */
  readonly toUtc?: string | Date;

  /** Optional deterministic reference instant for backend preset resolution. */
  readonly referenceInstantUtc?: string | Date;

  /** Required when preset=custom. Must be earlier than customEndUtc. */
  readonly customStartUtc?: string | Date;

  /** Required when preset=custom. Must be later than customStartUtc. */
  readonly customEndUtc?: string | Date;

  // Content filters

  /** Category filters (e.g., ["music", "sports"]). */
  readonly categories?: readonly string[];

  /** When true, only include events saved by current user. */
  readonly includeSavedOnly?: boolean;

  /**
   * Convert to canonical SpatialQueryDto for validation and execution.
   * Frontend composables should use this method to extract spatial semantics.
   */
  toSpatialQuery?(): SpatialQueryDto;
}

/**
 * Helper function to create a SpatialQueryDto from EventMapFeedQueryV2Dto.
 * Extracts only the spatial dimensions, ignoring temporal and content filters.
 */
export function toSpatialQuery(query: EventMapFeedQueryV2Dto): SpatialQueryDto {
  return {
    marketIds: query.marketIds,
    marketSlugs: query.marketSlugs,
    districtIds: query.districtIds,
    districtSlugs: query.districtSlugs,
    neighborhoodIds: query.neighborhoodIds,
    neighborhoodSlugs: query.neighborhoodSlugs,
    bbox: query.bbox,
    includeDescendants: query.includeDescendants,
    minConfidence: query.minSpatialConfidence,
  };
}

/**
 * Type guard: check if a query is BboxOnly composition.
 * Events are filtered by bbox geometry only; no taxonomy.
 */
export function isBboxOnly(result: SpatialQueryValidationResult): boolean {
  return result.mode === SpatialCompositionMode.BboxOnly;
}

/**
 * Type guard: check if a query is TaxonomyOnly composition.
 * Events are filtered by market/district/neighborhood taxonomy only; no bbox.
 */
export function isTaxonomyOnly(result: SpatialQueryValidationResult): boolean {
  return result.mode === SpatialCompositionMode.TaxonomyOnly;
}

/**
 * Type guard: check if a query is BboxWithTaxonomy composition.
 * Events are filtered by both bbox AND taxonomy.
 */
export function isBboxWithTaxonomy(
  result: SpatialQueryValidationResult,
): boolean {
  return result.mode === SpatialCompositionMode.BboxWithTaxonomy;
}

/**
 * Check if validation result is valid (no errors, has mode, has normalized query).
 */
export function isValidSpatialQuery(
  result: SpatialQueryValidationResult,
): boolean {
  return (
    result.errors.length === 0 &&
    result.mode != null &&
    result.normalizedQuery != null
  );
}
