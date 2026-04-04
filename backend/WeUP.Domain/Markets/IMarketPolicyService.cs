namespace WeUP.Domain.Markets;

/// <summary>
/// Service for enforcing market-specific policies: freeze rules, assignment, eligibility.
/// P20: City Partitioning, Neighborhood Taxonomy, and Market Freeze Rules
/// </summary>
public interface IMarketPolicyService
{
    /// <summary>
    /// Get a market by its code.
    /// </summary>
    Task<Market?> GetMarketAsync(string marketCode, CancellationToken ct = default);

    /// <summary>
    /// Get all active markets.
    /// </summary>
    Task<Market[]> GetActiveMarketsAsync(CancellationToken ct = default);

    /// <summary>
    /// Get the current launch market (there is only one during Phase 0).
    /// </summary>
    Task<Market?> GetLaunchMarketAsync(CancellationToken ct = default);

    /// <summary>
    /// Determine which market a coordinate belongs to, based on geographic boundaries.
    /// Returns the most specific (highest priority) market match.
    /// </summary>
    Task<string?> DetermineMarketAsync(double latitude, double longitude, CancellationToken ct = default);

    /// <summary>
    /// Check if event submission is allowed in a market.
    /// Returns true if the market accepts submissions; false if frozen.
    /// </summary>
    Task<bool> IsMarketAcceptingSubmissionsAsync(string marketCode, CancellationToken ct = default);

    /// <summary>
    /// Validate that an event can be published in a specific market.
    /// Returns a list of policy violations; empty array = valid.
    /// </summary>
    Task<string[]> GetMarketPublishBlockersAsync(
        string marketCode,
        double latitude,
        double longitude,
        CancellationToken ct = default);

    /// <summary>
    /// Assign an event to a market based on its geographic coordinates.
    /// If no market matches, returns null.
    /// </summary>
    Task<string?> AssignEventToMarketAsync(
        double latitude,
        double longitude,
        CancellationToken ct = default);
}
