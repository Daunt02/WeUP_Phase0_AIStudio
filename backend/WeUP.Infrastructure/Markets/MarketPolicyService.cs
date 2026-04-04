using WeUP.Domain.Markets;
using WeUP.Domain.Spatial;

namespace WeUP.Infrastructure.Markets;

/// <summary>
/// Stub implementation of market policy service for Phase 0.
/// Future: load from database; enhance with complex freeze rule evaluation.
/// </summary>
public class MarketPolicyService : IMarketPolicyService
{
    // Phase 0 stub: in-memory market registry
    private static readonly Dictionary<string, Market> _marketRegistry;

    // The launch market for Phase 0 (only one active)
    private const string LaunchMarketCode = "sf";

    static MarketPolicyService()
    {
        _marketRegistry = new(StringComparer.OrdinalIgnoreCase)
        {
            ["sf"] = new Market(
                code: "sf",
                displayName: "San Francisco",
                timezone: "America/Los_Angeles",
                centerLat: 37.7749,
                centerLng: -122.4194,
                boundingBox: new BoundingBox(37.70, 37.85, -122.52, -122.37),
                description: "San Francisco - Phase 0 Launch Market")
            {
                Status = MarketStatus.Active,
                IsAcceptingSubmissions = true,
                LaunchedAt = DateTimeOffset.UtcNow.AddMonths(-1),
            },
        };
    }

    public Task<Market?> GetMarketAsync(string marketCode, CancellationToken ct = default)
    {
        _marketRegistry.TryGetValue(marketCode, out var market);
        return Task.FromResult(market);
    }

    public Task<Market[]> GetActiveMarketsAsync(CancellationToken ct = default)
    {
        var active = _marketRegistry.Values
            .Where(m => m.Status == MarketStatus.Active)
            .ToArray();
        return Task.FromResult(active);
    }

    public async Task<Market?> GetLaunchMarketAsync(CancellationToken ct = default)
    {
        return await GetMarketAsync(LaunchMarketCode, ct);
    }

    public async Task<string?> DetermineMarketAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        var activeMarkets = await GetActiveMarketsAsync(ct);

        // Find the first market that approximately contains this coordinate
        var market = activeMarkets.FirstOrDefault(m => m.ApproximatelyContains(latitude, longitude));
        return market?.Code;
    }

    public async Task<bool> IsMarketAcceptingSubmissionsAsync(string marketCode, CancellationToken ct = default)
    {
        var market = await GetMarketAsync(marketCode, ct);
        if (market == null) return false;

        // Market must be active AND explicitly accepting submissions
        return market.Status == MarketStatus.Active && market.IsAcceptingSubmissions;
    }

    public async Task<string[]> GetMarketPublishBlockersAsync(
        string marketCode,
        double latitude,
        double longitude,
        CancellationToken ct = default)
    {
        var blockers = new List<string>();

        var market = await GetMarketAsync(marketCode, ct);
        if (market == null)
        {
            blockers.Add($"Market '{marketCode}' not found");
            return blockers.ToArray();
        }

        // Check market status
        if (market.Status != MarketStatus.Active)
        {
            blockers.Add($"Market is {market.Status}, not Active");
        }

        // Check submission acceptance
        if (!market.IsAcceptingSubmissions)
        {
            blockers.Add($"Market {marketCode} is not accepting submissions (freeze rules active)");
        }

        // Check geographic containment
        if (!market.ApproximatelyContains(latitude, longitude))
        {
            blockers.Add($"Coordinates ({latitude:F4}, {longitude:F4}) not within market {marketCode} boundaries");
        }

        return blockers.ToArray();
    }

    public async Task<string?> AssignEventToMarketAsync(
        double latitude,
        double longitude,
        CancellationToken ct = default)
    {
        return await DetermineMarketAsync(latitude, longitude, ct);
    }
}
