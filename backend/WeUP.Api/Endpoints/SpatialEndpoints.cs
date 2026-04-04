using WeUP.Contracts.Events;
using WeUP.Domain.Spatial;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace WeUP.Api.Endpoints;

/// <summary>
/// Spatial query endpoints for Phase 0.
/// P19: Bounding Box, Cluster, and District Query Semantics
/// </summary>
public static class SpatialEndpoints
{
    public static void MapSpatialEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/spatial")
            .WithName("Spatial")
            .WithOpenApi();

        // POST /api/spatial/map-feed — Viewport query with bounding box and filters
        group.MapPost("/map-feed", GetMapFeed)
            .WithName("GetMapFeed")
            .WithOpenApi()
            .WithDescription("Query events within a bounding box with optional district and category filters");

        // GET /api/spatial/districts/{marketCode} — List districts for a market
        group.MapGet("/districts/{marketCode}", GetDistrictsList)
            .WithName("GetDistrictsForMarket")
            .WithOpenApi()
            .WithDescription("List all districts for a specific market");

        // GET /api/spatial/district/{districtCode} — Get district metadata
        group.MapGet("/district/{districtCode}", GetDistrict)
            .WithName("GetDistrict")
            .WithOpenApi()
            .WithDescription("Get metadata for a specific district");

        // POST /api/spatial/determine-district — Determine which district a coordinate belongs to
        group.MapPost("/determine-district", DetermineDistrict)
            .WithName("DetermineDistrict")
            .WithOpenApi()
            .WithDescription("Determine which district a coordinate point belongs to");
    }

    /// <summary>
    /// Query events within a bounding box.
    /// Integrates with ViewportQueryService to filter by geography and metadata.
    /// </summary>
    private static async Task<MapFeedResponse> GetMapFeed(
        MapFeedRequest request,
        IViewportQueryService viewportService,
        CancellationToken ct)
    {
        try
        {
            // Validate the bounding box early
            request.Bounds.Validate();

            // Query events within viewport
            var response = await viewportService.GetEventsInViewportAsync(request, ct);

            return response;
        }
        catch (InvalidOperationException)
        {
            // Validation error — return empty result
            return new MapFeedResponse(Array.Empty<EventMapCardDto>(), 0);
        }
    }

    /// <summary>
    /// Get all active districts for a market.
    /// </summary>
    private static async Task<DistrictListResponse> GetDistrictsList(
        string marketCode,
        IViewportQueryService viewportService,
        CancellationToken ct)
    {
        var districts = await viewportService.GetDistrictsForMarketAsync(marketCode, ct);

        var dtos = districts.Select(d => new DistrictDto(
            d.DistrictCode,
            d.DisplayName,
            d.Description ?? "",
            d.MarketCode,
            d.SortOrder,
            d.ParentDistrictCode
        )).ToArray();

        return new DistrictListResponse(dtos, dtos.Length);
    }

    /// <summary>
    /// Get a specific district's metadata.
    /// </summary>
    private static async Task<DistrictDetailResponse?> GetDistrict(
        string districtCode,
        IViewportQueryService viewportService,
        CancellationToken ct)
    {
        var district = await viewportService.GetDistrictAsync(districtCode, ct);

        if (district == null)
            return null;

        var dto = new DistrictDto(
            district.DistrictCode,
            district.DisplayName,
            district.Description ?? "",
            district.MarketCode,
            district.SortOrder,
            district.ParentDistrictCode
        );

        return new DistrictDetailResponse(dto);
    }

    /// <summary>
    /// Determine which district a coordinate belongs to.
    /// </summary>
    private static async Task<DetermineDistrictResponse> DetermineDistrict(
        DetermineDistrictRequest request,
        IViewportQueryService viewportService,
        CancellationToken ct)
    {
        var districtCode = await viewportService.DetermineDistrictAsync(
            request.Latitude,
            request.Longitude,
            request.MarketCode,
            ct);

        return new DetermineDistrictResponse(districtCode ?? null);
    }
}

/// <summary>
/// Response DTOs for spatial queries
/// </summary>

public record DistrictDto(
    string Code,
    string DisplayName,
    string Description,
    string MarketCode,
    int SortOrder,
    string? ParentCode);

public record DistrictListResponse(DistrictDto[] Districts, int TotalCount);

public record DistrictDetailResponse(DistrictDto District);

public record DetermineDistrictRequest(
    double Latitude,
    double Longitude,
    string MarketCode);

public record DetermineDistrictResponse(string? DistrictCode);
