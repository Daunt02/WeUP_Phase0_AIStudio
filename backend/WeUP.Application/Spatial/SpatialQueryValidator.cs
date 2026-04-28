namespace WeUP.Application.Spatial;

using Microsoft.Extensions.Logging;
using WeUP.Contracts.Spatial;

/// <summary>
/// Comprehensive spatial query validation and normalization service.
/// Implements all filter precedence, composition, and conflict resolution rules.
///
/// RESPONSIBILITIES:
/// - Validate spatial query well-formedness.
/// - Resolve slugs to canonical IDs (taxonomy service dependency).
/// - Normalize filter combinations.
/// - Infer market scope from district/neighborhood filters.
/// - Detect and reject invalid combinations.
/// - Apply composition mode semantics.
/// - Return actionable validation errors.
///
/// PRECEDENCE HIERARCHY:
/// 1. BboxOnly: Bbox overrides all taxonomy (bbox is the sole filter).
/// 2. TaxonomyOnly: marketId > districtId > neighborhoodId (market is most restrictive).
/// 3. BboxWithTaxonomy: Bbox applies first, taxonomy filters within bbox.
/// 4. All slug-based filters are resolved to IDs (or fail if slug not found).
/// </summary>
public interface ISpatialQueryValidator
{
    /// <summary>
    /// Validate a raw spatial query and resolve all dimensions.
    /// Returns detailed errors or the normalized query ready for backend execution.
    /// </summary>
    Task<SpatialQueryValidationResult> ValidateAsync(SpatialQueryDto query);

    /// <summary>
    /// Resolve market slugs to market IDs via taxonomy service.
    /// Throws InvalidOperationException if any slug not found.
    /// </summary>
    Task<string[]> ResolveMarketSlugsAsync(string[] slugs);

    /// <summary>
    /// Resolve district slugs to district IDs.
    /// Requires marketIds to be context for disambiguation.
    /// </summary>
    Task<string[]> ResolveDistrictSlugsAsync(string[] slugs, string[]? marketIds = null);

    /// <summary>
    /// Resolve neighborhood slugs to neighborhood IDs.
    /// </summary>
    Task<string[]> ResolveNeighborhoodSlugsAsync(string[] slugs, string[]? districtIds = null);

    /// <summary>
    /// Infer market IDs from district IDs (all districts must belong to specified markets).
    /// Used when districtIds are provided without marketIds.
    /// </summary>
    Task<string[]> InferMarketsFromDistrictsAsync(string[] districtIds);

    /// <summary>
    /// Infer district and market IDs from neighborhood IDs.
    /// Used when only neighborhoodIds are provided.
    /// </summary>
    Task<(string[] marketIds, string[] districtIds)> InferFromNeighborhoodsAsync(
        string[] neighborhoodIds);
}

/// <summary>
/// Default implementation of spatial query validation.
/// Stateless, injectable validator that coordinates with taxonomy service.
/// </summary>
public sealed class SpatialQueryValidator : ISpatialQueryValidator
{
    private readonly ISpatialTaxonomyService _taxonomyService;
    private readonly ILogger<SpatialQueryValidator> _logger;

    public SpatialQueryValidator(
        ISpatialTaxonomyService taxonomyService,
        ILogger<SpatialQueryValidator> logger)
    {
        _taxonomyService = taxonomyService ?? throw new ArgumentNullException(nameof(taxonomyService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SpatialQueryValidationResult> ValidateAsync(SpatialQueryDto query)
    {
        try
        {
            // Step 1: Validate basic well-formedness (mutual exclusivity, bounds)
            var basicResult = query.Validate();
            if (!basicResult.IsValid())
            {
                _logger.LogDebug("Spatial query validation failed: {Errors}", basicResult.Errors);
                return basicResult;
            }

            var normalized = basicResult.NormalizedQuery!;

            // Step 2: Resolve all slug-based filters to IDs
            if ((normalized.MarketSlugs?.Length ?? 0) > 0)
            {
                try
                {
                    normalized = normalized with
                    {
                        MarketIds = await ResolveMarketSlugsAsync(normalized.MarketSlugs!),
                        MarketSlugs = null
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to resolve market slugs");
                    return new SpatialQueryValidationResult(
                        Mode: null,
                        Errors: new[] { $"Market slug resolution failed: {ex.Message}" },
                        NormalizedQuery: null);
                }
            }

            if ((normalized.DistrictSlugs?.Length ?? 0) > 0)
            {
                try
                {
                    normalized = normalized with
                    {
                        DistrictIds = await ResolveDistrictSlugsAsync(normalized.DistrictSlugs!, normalized.MarketIds),
                        DistrictSlugs = null
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to resolve district slugs");
                    return new SpatialQueryValidationResult(
                        Mode: null,
                        Errors: new[] { $"District slug resolution failed: {ex.Message}" },
                        NormalizedQuery: null);
                }
            }

            if ((normalized.NeighborhoodSlugs?.Length ?? 0) > 0)
            {
                try
                {
                    normalized = normalized with
                    {
                        NeighborhoodIds = await ResolveNeighborhoodslugsAsync(normalized.NeighborhoodSlugs!, normalized.DistrictIds),
                        NeighborhoodSlugs = null
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to resolve neighborhood slugs");
                    return new SpatialQueryValidationResult(
                        Mode: null,
                        Errors: new[] { $"Neighborhood slug resolution failed: {ex.Message}" },
                        NormalizedQuery: null);
                }
            }

            // Step 3: Apply composition-mode-specific normalization

            // TaxonomyOnly: Infer missing market/district from neighborhood/district chains
            if (basicResult.Mode == SpatialCompositionMode.TaxonomyOnly)
            {
                // If only neighborhoods provided, infer districts and markets
                if ((normalized.NeighborhoodIds?.Length ?? 0) > 0 &&
                    (normalized.DistrictIds?.Length ?? 0) == 0 &&
                    (normalized.MarketIds?.Length ?? 0) == 0)
                {
                    try
                    {
                        var (inferredMarkets, inferredDistricts) =
                            await InferFromNeighborhoodsAsync(normalized.NeighborhoodIds!);
                        normalized = normalized with
                        {
                            MarketIds = inferredMarkets,
                            DistrictIds = inferredDistricts
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to infer markets/districts from neighborhoods");
                        return new SpatialQueryValidationResult(
                            Mode: null,
                            Errors: new[] { $"Neighborhood inference failed: {ex.Message}" },
                            NormalizedQuery: null);
                    }
                }

                // If only districts provided, infer markets
                else if ((normalized.DistrictIds?.Length ?? 0) > 0 &&
                         (normalized.MarketIds?.Length ?? 0) == 0)
                {
                    try
                    {
                        normalized = normalized with
                        {
                            MarketIds = await InferMarketsFromDistrictsAsync(normalized.DistrictIds!)
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to infer markets from districts");
                        return new SpatialQueryValidationResult(
                            Mode: null,
                            Errors: new[] { $"District inference failed: {ex.Message}" },
                            NormalizedQuery: null);
                    }
                }
            }

            // Step 4: Final validation after normalization
            var finalResult = normalized.Validate();
            if (!finalResult.IsValid())
            {
                return finalResult;
            }

            _logger.LogDebug("Spatial query validated successfully: Mode={Mode}", basicResult.Mode);
            return finalResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during spatial query validation");
            return new SpatialQueryValidationResult(
                Mode: null,
                Errors: new[] { "Internal validation error." },
                NormalizedQuery: null);
        }
    }

    public async Task<string[]> ResolveMarketSlugsAsync(string[] slugs)
    {
        if (slugs == null || slugs.Length == 0)
            return Array.Empty<string>();

        var taxonomy = await _taxonomyService.GetCurrentTaxonomyAsync();
        var marketIds = new List<string>(slugs.Length);

        foreach (var slug in slugs.Distinct())
        {
            var market = taxonomy.Markets.FirstOrDefault(m =>
                m.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase) ||
                (m.AliasSlugs?.Contains(slug, StringComparer.OrdinalIgnoreCase) ?? false));

            if (market == null)
            {
                throw new InvalidOperationException($"Market slug not found: '{slug}'");
            }

            marketIds.Add(market.MarketId);
        }

        return marketIds.Distinct().ToArray();
    }

    public async Task<string[]> ResolveDistrictSlugsAsync(string[] slugs, string[]? marketIds = null)
    {
        if (slugs == null || slugs.Length == 0)
            return Array.Empty<string>();

        var taxonomy = await _taxonomyService.GetCurrentTaxonomyAsync();
        var districtIds = new List<string>(slugs.Length);

        foreach (var slug in slugs.Distinct())
        {
            var district = taxonomy.Districts.FirstOrDefault(d =>
                d.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase) &&
                (marketIds == null || marketIds.Length == 0 || marketIds.Contains(d.MarketId)));

            if (district == null)
            {
                var contextMsg = marketIds != null && marketIds.Length > 0
                    ? $" in markets [{string.Join(", ", marketIds)}]"
                    : "";
                throw new InvalidOperationException(
                    $"District slug not found: '{slug}'{contextMsg}");
            }

            districtIds.Add(district.DistrictId);
        }

        return districtIds.Distinct().ToArray();
    }

    public async Task<string[]> ResolveNeighborhoodSlugsAsync(string[] slugs, string[]? districtIds = null)
    {
        if (slugs == null || slugs.Length == 0)
            return Array.Empty<string>();

        var taxonomy = await _taxonomyService.GetCurrentTaxonomyAsync();
        var neighborhoodIds = new List<string>(slugs.Length);

        foreach (var slug in slugs.Distinct())
        {
            var neighborhood = taxonomy.Neighborhoods.FirstOrDefault(n =>
                n.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase) &&
                (districtIds == null || districtIds.Length == 0 || districtIds.Contains(n.DistrictId)));

            if (neighborhood == null)
            {
                var contextMsg = districtIds != null && districtIds.Length > 0
                    ? $" in districts [{string.Join(", ", districtIds)}]"
                    : "";
                throw new InvalidOperationException(
                    $"Neighborhood slug not found: '{slug}'{contextMsg}");
            }

            neighborhoodIds.Add(neighborhood.NeighborhoodId);
        }

        return neighborhoodIds.Distinct().ToArray();
    }

    public async Task<string[]> InferMarketsFromDistrictsAsync(string[] districtIds)
    {
        if (districtIds == null || districtIds.Length == 0)
            return Array.Empty<string>();

        var taxonomy = await _taxonomyService.GetCurrentTaxonomyAsync();
        var marketIds = new HashSet<string>();

        foreach (var districtId in districtIds)
        {
            var district = taxonomy.Districts.FirstOrDefault(d => d.DistrictId == districtId);
            if (district == null)
            {
                throw new InvalidOperationException($"District not found: '{districtId}'");
            }

            marketIds.Add(district.MarketId);
        }

        return marketIds.ToArray();
    }

    public async Task<(string[] marketIds, string[] districtIds)> InferFromNeighborhoodsAsync(
        string[] neighborhoodIds)
    {
        if (neighborhoodIds == null || neighborhoodIds.Length == 0)
            return (Array.Empty<string>(), Array.Empty<string>());

        var taxonomy = await _taxonomyService.GetCurrentTaxonomyAsync();
        var marketIds = new HashSet<string>();
        var districtIds = new HashSet<string>();

        foreach (var neighborhoodId in neighborhoodIds)
        {
            var neighborhood = taxonomy.Neighborhoods.FirstOrDefault(n => n.NeighborhoodId == neighborhoodId);
            if (neighborhood == null)
            {
                throw new InvalidOperationException($"Neighborhood not found: '{neighborhoodId}'");
            }

            marketIds.Add(neighborhood.MarketId);
            districtIds.Add(neighborhood.DistrictId);
        }

        return (marketIds.ToArray(), districtIds.ToArray());
    }

    private async Task<string[]> ResolveNeighborhoodslugsAsync(string[] slugs, string[]? districtIds = null)
    {
        // Typo fix needed in interface
        return await ResolveNeighborhoodSlugsAsync(slugs, districtIds);
    }
}

/// <summary>
/// Marker interface for taxonomy service.
/// Implementations provide access to canonical spatial taxonomy (markets, districts, neighborhoods).
/// </summary>
public interface ISpatialTaxonomyService
{
    Task<SpatialTaxonomySnapshotDto> GetCurrentTaxonomyAsync();
}
