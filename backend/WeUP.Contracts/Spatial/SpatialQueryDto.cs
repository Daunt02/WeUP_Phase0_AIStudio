namespace WeUP.Contracts.Spatial;

/// <summary>
/// Canonical spatial query composition mode.
/// Defines how spatial filters interact and which filters take precedence.
/// The backend owns authoritative mode interpretation—clients must not guess.
/// </summary>
public enum SpatialCompositionMode
{
    /// <summary>
    /// BboxOnly: Query uses only bounding box geometry. No taxonomy filters.
    /// Precedence: bbox overrides any taxonomy dimension.
    /// Valid: bbox must be present and valid.
    /// Used for: Freeform map exploration, raw geo-spatial discovery.
    /// </summary>
    BboxOnly = 0,

    /// <summary>
    /// TaxonomyOnly: Query uses only market/district/neighborhood IDs. No bbox.
    /// Precedence: marketId > districtIds > neighborhoodIds (market is most restrictive).
    /// Valid: at least one taxonomy dimension must be present and non-empty.
    /// Used for: Curated feeds, administrative boundaries, market/district drill-down.
    /// </summary>
    TaxonomyOnly = 1,

    /// <summary>
    /// BboxWithTaxonomy: Query uses bbox AND taxonomy filters together.
    /// Precedence: bbox applies first (geometric filter), then taxonomy filters events within bbox.
    /// Bbox constrains the spatial envelope; taxonomy further refines.
    /// Valid: bbox present AND at least one taxonomy dimension present.
    /// Used for: Refined searches (e.g., "events in Downtown district within this map view").
    /// </summary>
    BboxWithTaxonomy = 2,

    /// <summary>
    /// ProximityReady: Reserved for proximity-based queries (v2.0+).
    /// Not yet supported in v1.0.
    /// </summary>
    ProximityReady = 99
}

/// <summary>
/// Validation result returned after parsing a spatial query.
/// Contains the resolved composition mode and any validation errors.
/// </summary>
public sealed record SpatialQueryValidationResult(
    /// <summary>
    /// Resolved composition mode after analyzing all spatial dimensions.
    /// Null when validation fails.
    /// </summary>
    SpatialCompositionMode? Mode,

    /// <summary>
    /// Validation errors. Empty when valid.
    /// </summary>
    string[] Errors,

    /// <summary>
    /// Normalized query after applying composition rules.
    /// Null when validation fails.
    /// </summary>
    SpatialQueryDto? NormalizedQuery);

/// <summary>
/// Canonical spatial query contract for map feed, calendar feed, and saved discovery.
/// This is the single source of truth for all spatial filtering across all surfaces.
///
/// DESIGN PRINCIPLES:
/// - All spatial dimensions are explicit (no hidden fallbacks to text matching).
/// - Composition modes define strict precedence rules.
/// - Backend validation must reject or normalize ambiguous combinations.
/// - Taxonomy filters always reference canonical IDs or slugs, never display text.
/// - One market per query (enforced at event entity level).
/// - At most one district and one neighborhood per event.
///
/// FILTER SEMANTICS:
/// - marketIds: Array of market IDs. Filters events to those markets only.
///   When non-empty, districtIds and neighborhoodIds must belong to these markets.
/// - districtIds: Array of district IDs. Filters events to those districts (and their descendants).
///   When non-empty, marketIds can be inferred or must be compatible.
/// - neighborhoodIds: Array of neighborhood IDs. Filters events to those neighborhoods.
///   When non-empty, districtIds can be inferred or must be compatible.
/// - bbox: Bounding box geometry in degrees. Filters to events within the envelope.
///   In BboxWithTaxonomy mode, applied together with taxonomy (events must match BOTH).
/// - IncludeDescendants: When true, districtIds includes all child neighborhoods.
///   When false, only exact district members (not nested neighborhoods).
/// </summary>
public sealed record SpatialQueryDto
{
    /// <summary>
    /// Market identity filter. Canonical market IDs (primary key).
    /// Empty array or null = no market filter.
    /// All markets are applied in OR logic (matches any market).
    /// </summary>
    public string[]? MarketIds { get; init; }

    /// <summary>
    /// Alternative market filter using market slugs (URL-friendly identifiers).
    /// Mutually exclusive with MarketIds for a single query binding.
    /// Backend normalizes to MarketIds after taxonomy lookup.
    /// Empty array or null = no market filter via slugs.
    /// </summary>
    public string[]? MarketSlugs { get; init; }

    /// <summary>
    /// District identity filter. Canonical district IDs.
    /// Empty array or null = no district filter.
    /// All districts are applied in OR logic (matches any district).
    /// When IncludeDescendants=true, automatically includes child neighborhoods.
    /// </summary>
    public string[]? DistrictIds { get; init; }

    /// <summary>
    /// Alternative district filter using district slugs.
    /// Mutually exclusive with DistrictIds for a single query binding.
    /// Backend normalizes to DistrictIds after taxonomy lookup.
    /// Empty array or null = no district filter via slugs.
    /// </summary>
    public string[]? DistrictSlugs { get; init; }

    /// <summary>
    /// Neighborhood identity filter. Canonical neighborhood IDs.
    /// Empty array or null = no neighborhood filter.
    /// All neighborhoods are applied in OR logic (matches any neighborhood).
    /// Can only be used together with compatible DistrictIds.
    /// </summary>
    public string[]? NeighborhoodIds { get; init; }

    /// <summary>
    /// Alternative neighborhood filter using neighborhood slugs.
    /// Mutually exclusive with NeighborhoodIds for a single query binding.
    /// Backend normalizes to NeighborhoodIds after taxonomy lookup.
    /// Empty array or null = no neighborhood filter via slugs.
    /// </summary>
    public string[]? NeighborhoodSlugs { get; init; }

    /// <summary>
    /// Bounding box in WGS84 degrees: minLng,minLat,maxLng,maxLat.
    /// Null or empty = no spatial envelope filter.
    /// When present in BboxWithTaxonomy mode, events must fall within this box AND match taxonomy.
    /// </summary>
    public string? Bbox { get; init; }

    /// <summary>
    /// When true, district filters include all descendant neighborhoods.
    /// When false, only direct district members (neighborhoods that list this district as parent).
    /// Default: true (include descendants).
    /// Only applies when DistrictIds or DistrictSlugs is non-empty.
    /// </summary>
    public bool IncludeDescendants { get; init; } = true;

    /// <summary>
    /// Resolved composition mode after validation.
    /// Not set during query binding; determined by validation logic.
    /// </summary>
    public SpatialCompositionMode? ResolvedMode { get; init; }

    /// <summary>
    /// Quality metadata: confidence threshold for spatial resolution.
    /// Events with resolution confidence below this are excluded.
    /// Default: 0.0 (no minimum).
    /// Range: [0.0, 1.0].
    /// </summary>
    public double MinConfidence { get; init; } = 0.0;

    /// <summary>
    /// Validate if this query is well-formed and non-ambiguous.
    /// Returns composition mode and any errors.
    /// VALIDATION RULES:
    /// 1. Exactly one of {MarketIds, MarketSlugs} must be present (not both, not neither) → ERROR.
    ///    OR BOTH can be null/empty → use districtIds/neighborhoodIds to infer market.
    /// 2. Exactly one of {DistrictIds, DistrictSlugs} must be present (not both) → ERROR.
    /// 3. Exactly one of {NeighborhoodIds, NeighborhoodSlugs} must be present (not both) → ERROR.
    /// 4. If Bbox is present and taxonomy filters are empty → BboxOnly.
    /// 5. If taxonomy filters are present and Bbox is null/empty → TaxonomyOnly.
    /// 6. If BOTH Bbox and taxonomy are present → BboxWithTaxonomy.
    /// 7. If NEITHER Bbox nor taxonomy are present → ERROR (must specify at least one).
    /// 8. If NeighborhoodIds is present without DistrictIds → WARN (infer from neighborhood→district mapping).
    /// 9. MinConfidence must be in [0.0, 1.0] → ERROR if outside.
    /// 10. When slug-based, backend must resolve to IDs before execution.
    /// </summary>
    public SpatialQueryValidationResult Validate()
    {
        var errors = new List<string>();

        // Rule 1: Mutual exclusivity of ID vs slug for each dimension
        if (!string.IsNullOrEmpty(Bbox) || (MarketIds?.Length ?? 0) > 0 || (MarketSlugs?.Length ?? 0) > 0)
        {
            // Only enforce if at least one taxonomy dimension is specified
            if ((MarketIds?.Length ?? 0) > 0 && (MarketSlugs?.Length ?? 0) > 0)
            {
                errors.Add("Cannot specify both MarketIds and MarketSlugs in the same query.");
            }

            if ((DistrictIds?.Length ?? 0) > 0 && (DistrictSlugs?.Length ?? 0) > 0)
            {
                errors.Add("Cannot specify both DistrictIds and DistrictSlugs in the same query.");
            }

            if ((NeighborhoodIds?.Length ?? 0) > 0 && (NeighborhoodSlugs?.Length ?? 0) > 0)
            {
                errors.Add("Cannot specify both NeighborhoodIds and NeighborhoodSlugs in the same query.");
            }
        }

        // Rule 9: MinConfidence bounds check
        if (MinConfidence < 0.0 || MinConfidence > 1.0)
        {
            errors.Add($"MinConfidence must be in [0.0, 1.0], got {MinConfidence}.");
        }

        // Determine if we have any taxonomy filters
        bool hasTaxonomyFilters = (MarketIds?.Length ?? 0) > 0 ||
                                  (MarketSlugs?.Length ?? 0) > 0 ||
                                  (DistrictIds?.Length ?? 0) > 0 ||
                                  (DistrictSlugs?.Length ?? 0) > 0 ||
                                  (NeighborhoodIds?.Length ?? 0) > 0 ||
                                  (NeighborhoodSlugs?.Length ?? 0) > 0;
        bool hasBbox = !string.IsNullOrWhiteSpace(Bbox);

        // Rule 7: At least one spatial dimension must be present
        if (!hasTaxonomyFilters && !hasBbox)
        {
            errors.Add("At least one spatial dimension must be specified: Bbox, MarketIds, DistrictIds, or NeighborhoodIds.");
        }

        // If validation failed, return early
        if (errors.Count > 0)
        {
            return new SpatialQueryValidationResult(
                Mode: null,
                Errors: errors.ToArray(),
                NormalizedQuery: null);
        }

        // Determine composition mode
        SpatialCompositionMode? resolvedMode = null;
        if (hasBbox && !hasTaxonomyFilters)
        {
            resolvedMode = SpatialCompositionMode.BboxOnly;
        }
        else if (!hasBbox && hasTaxonomyFilters)
        {
            resolvedMode = SpatialCompositionMode.TaxonomyOnly;
        }
        else if (hasBbox && hasTaxonomyFilters)
        {
            resolvedMode = SpatialCompositionMode.BboxWithTaxonomy;
        }

        // Create normalized query with resolved mode
        var normalized = new SpatialQueryDto
        {
            MarketIds = this.MarketIds,
            MarketSlugs = this.MarketSlugs,
            DistrictIds = this.DistrictIds,
            DistrictSlugs = this.DistrictSlugs,
            NeighborhoodIds = this.NeighborhoodIds,
            NeighborhoodSlugs = this.NeighborhoodSlugs,
            Bbox = this.Bbox,
            IncludeDescendants = this.IncludeDescendants,
            MinConfidence = this.MinConfidence,
            ResolvedMode = resolvedMode
        };

        return new SpatialQueryValidationResult(
            Mode: resolvedMode,
            Errors: Array.Empty<string>(),
            NormalizedQuery: normalized);
    }
}

/// <summary>
/// Extension helper to determine if a query is valid (has no errors).
/// </summary>
public static class SpatialQueryExtensions
{
    public static bool IsValid(this SpatialQueryValidationResult result)
    {
        return result.Errors.Length == 0 && result.Mode.HasValue && result.NormalizedQuery != null;
    }
}
