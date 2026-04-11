using WeUP.Domain.Markets;
using WeUP.Domain.Spatial;
using WeUP.Infrastructure.Seed;

namespace WeUP.Infrastructure.Markets;

/// <summary>
/// Stub implementation of market policy service for Phase 0.
/// Future: load from database; enhance with complex freeze rule evaluation.
/// </summary>
public class MarketPolicyService : IMarketPolicyService
{
    private readonly Dictionary<string, Market> _marketRegistry = new(StringComparer.OrdinalIgnoreCase);
    private string _launchMarketCode = "sf";

    public void Reset(Phase0SeedDataset dataset)
    {
        _marketRegistry.Clear();
        _launchMarketCode = dataset.Meta.LaunchMarketCode;

        foreach (var market in dataset.Markets)
        {
            var seeded = new Market(
                code: market.Code,
                displayName: market.DisplayName,
                timezone: market.Timezone,
                centerLat: market.CenterLat,
                centerLng: market.CenterLng,
                boundingBox: new BoundingBox(market.BoundingBox.MinLat, market.BoundingBox.MaxLat, market.BoundingBox.MinLng, market.BoundingBox.MaxLng),
                description: market.Description)
            {
                Status = ParseStatus(market.Status),
                IsAcceptingSubmissions = market.IsAcceptingSubmissions,
                LaunchedAt = string.IsNullOrWhiteSpace(market.LaunchedAt) ? null : DateTimeOffset.Parse(market.LaunchedAt),
            };

            _marketRegistry[seeded.Code] = seeded;
        }
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
        return await GetMarketAsync(_launchMarketCode, ct);
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

        private static MarketStatus ParseStatus(string raw) =>
            raw.Equals("ComingSoon", StringComparison.OrdinalIgnoreCase)
                ? MarketStatus.Planned
                : Enum.Parse<MarketStatus>(raw, true);
}
