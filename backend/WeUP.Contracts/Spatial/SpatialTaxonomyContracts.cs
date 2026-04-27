namespace WeUP.Contracts.Spatial;

/// <summary>
/// Internal geometry pointer. API consumers must not treat this as a display label.
/// </summary>
public sealed record GeometryBoundaryRefDto(
    string Provider,
    string DatasetId,
    string FeatureId,
    string Version,
    string? Hash = null);

/// <summary>
/// Canonical market contract.
/// DisplayName is user-facing only; MarketId/Slug are the source of truth.
/// </summary>
public sealed record SpatialMarketDto(
    string MarketId,
    string Slug,
    string DisplayName,
    string Timezone,
    GeometryBoundaryRefDto BoundaryRef,
    bool IsActive,
    string[] AliasSlugs);

/// <summary>
/// Canonical district contract under a market.
/// </summary>
public sealed record SpatialDistrictDto(
    string DistrictId,
    string MarketId,
    string Slug,
    string DisplayName,
    GeometryBoundaryRefDto BoundaryRef,
    int SortOrder,
    bool IsActive,
    string? ParentDistrictId = null);

/// <summary>
/// Canonical neighborhood contract under a district.
/// </summary>
public sealed record SpatialNeighborhoodDto(
    string NeighborhoodId,
    string MarketId,
    string DistrictId,
    string Slug,
    string DisplayName,
    GeometryBoundaryRefDto BoundaryRef,
    int SortOrder,
    bool IsActive);

/// <summary>
/// Canonical spatial linkage for one event.
/// One event must resolve to one market and at most one district/neighborhood.
/// </summary>
public sealed record SpatialEventReferenceDto(
    string MarketId,
    string? DistrictId,
    string? NeighborhoodId,
    double ResolutionConfidence,
    string ResolutionSource,
    DateTimeOffset ResolvedAtUtc,
    string? MarketDisplayLabel = null,
    string? DistrictDisplayLabel = null,
    string? NeighborhoodDisplayLabel = null);

/// <summary>
/// Canonical filter contract for map/feed/calendar queries.
/// Filters are canonical IDs/slugs, not freeform display text.
/// </summary>
public sealed record SpatialFilterDto(
    string[]? MarketIds = null,
    string[]? DistrictIds = null,
    string[]? NeighborhoodIds = null,
    string[]? MarketSlugs = null,
    string[]? DistrictSlugs = null,
    string[]? NeighborhoodSlugs = null,
    bool IncludeDescendants = true);

/// <summary>
/// Versioned spatial taxonomy payload.
/// </summary>
public sealed record SpatialTaxonomySnapshotDto(
    string TaxonomyVersion,
    DateTimeOffset EffectiveAtUtc,
    SpatialMarketDto[] Markets,
    SpatialDistrictDto[] Districts,
    SpatialNeighborhoodDto[] Neighborhoods);
