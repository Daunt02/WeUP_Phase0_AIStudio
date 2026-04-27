namespace WeUP.Domain.Spatial.Taxonomy;

/// <summary>
/// Internal geometry pointer. This is not user-facing and can evolve independently
/// of canonical IDs/slugs used by APIs and UI filters.
/// </summary>
public sealed record GeometryBoundaryRef(
    string Provider,
    string DatasetId,
    string FeatureId,
    string Version,
    string? Hash = null);

/// <summary>
/// Market is the top-level spatial partition.
/// - Canonical identity: MarketId
/// - Stable URL/query identity: Slug
/// - User-facing label: DisplayName
/// </summary>
public sealed record Market(
    string MarketId,
    string Slug,
    string DisplayName,
    string Timezone,
    GeometryBoundaryRef BoundaryRef,
    string[] AliasSlugs,
    bool IsActive = true);

/// <summary>
/// District is a second-level partition under a market.
/// Districts are canonical taxonomy nodes, never freeform labels.
/// </summary>
public sealed record District(
    string DistrictId,
    string MarketId,
    string Slug,
    string DisplayName,
    GeometryBoundaryRef BoundaryRef,
    int SortOrder,
    bool IsActive = true,
    string? ParentDistrictId = null);

/// <summary>
/// Neighborhood is a third-level partition under one district.
/// Neighborhood can be unresolved for an event when confidence is insufficient.
/// </summary>
public sealed record Neighborhood(
    string NeighborhoodId,
    string MarketId,
    string DistrictId,
    string Slug,
    string DisplayName,
    GeometryBoundaryRef BoundaryRef,
    int SortOrder,
    bool IsActive = true);

/// <summary>
/// Canonical spatial linkage attached to one event.
/// Rules:
/// - Exactly one MarketId
/// - Zero-or-one DistrictId
/// - Zero-or-one NeighborhoodId
/// - Neighborhood requires District
/// </summary>
public sealed record EventSpatialSemantics(
    string MarketId,
    string? DistrictId,
    string? NeighborhoodId,
    double ResolutionConfidence,
    string ResolutionSource,
    DateTimeOffset ResolvedAtUtc)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(MarketId))
            throw new InvalidOperationException("MarketId is required for event spatial semantics.");

        if (!string.IsNullOrWhiteSpace(NeighborhoodId) && string.IsNullOrWhiteSpace(DistrictId))
            throw new InvalidOperationException("NeighborhoodId requires DistrictId.");

        if (ResolutionConfidence < 0 || ResolutionConfidence > 1)
            throw new InvalidOperationException("ResolutionConfidence must be in range [0, 1].");

        if (string.IsNullOrWhiteSpace(ResolutionSource))
            throw new InvalidOperationException("ResolutionSource is required for auditability.");
    }
}

/// <summary>
/// Versioned taxonomy snapshot.
/// Spatial taxonomy must be stable and explicitly versioned.
/// </summary>
public sealed record SpatialTaxonomySnapshot(
    string TaxonomyVersion,
    DateTimeOffset EffectiveAtUtc,
    Market[] Markets,
    District[] Districts,
    Neighborhood[] Neighborhoods)
{
    public void ValidateInvariants()
    {
        if (string.IsNullOrWhiteSpace(TaxonomyVersion))
            throw new InvalidOperationException("TaxonomyVersion is required.");

        var marketById = Markets.ToDictionary(m => m.MarketId, StringComparer.OrdinalIgnoreCase);
        var districtById = Districts.ToDictionary(d => d.DistrictId, StringComparer.OrdinalIgnoreCase);

        foreach (var district in Districts)
        {
            if (!marketById.ContainsKey(district.MarketId))
                throw new InvalidOperationException($"District '{district.DistrictId}' references unknown MarketId '{district.MarketId}'.");

            if (!string.IsNullOrWhiteSpace(district.ParentDistrictId) && !districtById.ContainsKey(district.ParentDistrictId))
                throw new InvalidOperationException($"District '{district.DistrictId}' references unknown ParentDistrictId '{district.ParentDistrictId}'.");
        }

        foreach (var neighborhood in Neighborhoods)
        {
            if (!marketById.ContainsKey(neighborhood.MarketId))
                throw new InvalidOperationException($"Neighborhood '{neighborhood.NeighborhoodId}' references unknown MarketId '{neighborhood.MarketId}'.");

            if (!districtById.TryGetValue(neighborhood.DistrictId, out var district))
                throw new InvalidOperationException($"Neighborhood '{neighborhood.NeighborhoodId}' references unknown DistrictId '{neighborhood.DistrictId}'.");

            if (!string.Equals(district.MarketId, neighborhood.MarketId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Neighborhood '{neighborhood.NeighborhoodId}' market '{neighborhood.MarketId}' does not match parent district market '{district.MarketId}'.");
        }
    }
}
