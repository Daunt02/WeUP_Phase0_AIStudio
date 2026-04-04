using WeUP.Domain.Markets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace WeUP.Api.Endpoints;

public record MarketDto(
    string Code,
    string DisplayName,
    string Timezone,
    double CenterLat,
    double CenterLng,
    string Status,
    bool IsAcceptingSubmissions);

public record MarketListResponse(MarketDto[] Markets, int TotalCount);
public record MarketDetailResponse(MarketDto? Market);
public record DetermineMarketRequest(double Latitude, double Longitude);
public record DetermineMarketResponse(string? MarketCode);
public record AssignMarketRequest(double Latitude, double Longitude);
public record AssignMarketResponse(string? MarketCode);

/// <summary>
/// Market endpoints for Phase 0.
/// P20: City Partitioning, Neighborhood Taxonomy, and Market Freeze Rules
/// </summary>
public static class MarketEndpoints
{
    public static void MapMarketEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/markets")
            .WithName("Markets")
            .WithOpenApi();

        // GET /api/markets/active — List all active markets
        group.MapGet("/active", GetActiveMarkets)
            .WithName("GetActiveMarkets")
            .WithOpenApi()
            .WithDescription("Get all active markets");

        // GET /api/markets/launch — Get the Phase 0 launch market
        group.MapGet("/launch", GetLaunchMarket)
            .WithName("GetLaunchMarket")
            .WithOpenApi()
            .WithDescription("Get the Phase 0 launch market");

        // GET /api/markets/{marketCode} — Get market metadata
        group.MapGet("/{marketCode}", GetMarket)
            .WithName("GetMarket")
            .WithOpenApi()
            .WithDescription("Get market metadata by code");

        // POST /api/markets/determine — Determine which market a coordinate belongs to
        group.MapPost("/determine", DetermineMarket)
            .WithName("DetermineMarket")
            .WithOpenApi()
            .WithDescription("Determine which market a coordinate belongs to");

        // POST /api/markets/assign — Assign coordinate to market
        group.MapPost("/assign", AssignMarket)
            .WithName("AssignMarket")
            .WithOpenApi()
            .WithDescription("Assign coordinate to a market");

        // POST /api/markets/{marketCode}/validate-publish — Check if event can be published in market
        group.MapPost("/{marketCode}/validate-publish", ValidateMarketPublish)
            .WithName("ValidateMarketPublish")
            .WithOpenApi()
            .WithDescription("Validate if event can be published in market");
    }

    private static async Task<MarketListResponse> GetActiveMarkets(
        IMarketPolicyService policyService,
        CancellationToken ct)
    {
        var markets = await policyService.GetActiveMarketsAsync(ct);
        var dtos = markets.Select(MarketToDto).ToArray();
        return new MarketListResponse(dtos, dtos.Length);
    }

    private static async Task<MarketDetailResponse> GetLaunchMarket(
        IMarketPolicyService policyService,
        CancellationToken ct)
    {
        var market = await policyService.GetLaunchMarketAsync(ct);
        return new MarketDetailResponse(market == null ? null : MarketToDto(market));
    }

    private static async Task<MarketDetailResponse?> GetMarket(
        string marketCode,
        IMarketPolicyService policyService,
        CancellationToken ct)
    {
        var market = await policyService.GetMarketAsync(marketCode, ct);
        return market == null ? null : new MarketDetailResponse(MarketToDto(market));
    }

    private static async Task<DetermineMarketResponse> DetermineMarket(
        DetermineMarketRequest request,
        IMarketPolicyService policyService,
        CancellationToken ct)
    {
        var marketCode = await policyService.DetermineMarketAsync(
            request.Latitude,
            request.Longitude,
            ct);
        return new DetermineMarketResponse(marketCode);
    }

    private static async Task<AssignMarketResponse> AssignMarket(
        AssignMarketRequest request,
        IMarketPolicyService policyService,
        CancellationToken ct)
    {
        var marketCode = await policyService.AssignEventToMarketAsync(
            request.Latitude,
            request.Longitude,
            ct);
        return new AssignMarketResponse(marketCode);
    }

    private static async Task<IResult> ValidateMarketPublish(
        string marketCode,
        ValidatePublishRequest request,
        IMarketPolicyService policyService,
        CancellationToken ct)
    {
        var blockers = await policyService.GetMarketPublishBlockersAsync(
            marketCode,
            request.Latitude,
            request.Longitude,
            ct);

        if (blockers.Length == 0)
            return Results.Ok(new { valid = true, blockers = Array.Empty<string>() });

        return Results.BadRequest(new { valid = false, blockers });
    }

    private static MarketDto MarketToDto(Market market)
    {
        return new MarketDto(
            market.Code,
            market.DisplayName,
            market.Timezone,
            market.CenterLat,
            market.CenterLng,
            market.Status.ToString(),
            market.IsAcceptingSubmissions
        );
    }
}

public record ValidatePublishRequest(double Latitude, double Longitude);
