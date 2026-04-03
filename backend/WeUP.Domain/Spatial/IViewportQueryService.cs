using WeUP.Contracts.Events;

namespace WeUP.Domain.Spatial;

/// <summary>
/// Service for querying events by spatial region (viewport/bounding box).
/// Responsible for converting spatial queries into event result sets.
/// </summary>
public interface IViewportQueryService
{
    /// <summary>
    /// Query events within a bounding box.
    /// </summary>
    /// <param name="request">Map feed request with bounding box and optional filters</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Map feed response with events and count</returns>
    Task<MapFeedResponse> GetEventsInViewportAsync(MapFeedRequest request, CancellationToken ct = default);

    /// <summary>
    /// Query events within a specific district.
    /// </summary>
    /// <param name="districtCode">The district code to filter by</param>
    /// <param name="window">Time window for events</param>
    /// <param name="categories">Optional category filter</param>
    /// <param name="minConfidence">Minimum confidence threshold</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of events in the district</returns>
    Task<EventMapCardDto[]> GetEventsInDistrictAsync(
        string districtCode,
        TimeWindowRequest window,
        string[]? categories = null,
        double minConfidence = 0.0,
        CancellationToken ct = default);

    /// <summary>
    /// Get a district by its code.
    /// </summary>
    /// <param name="districtCode">The district code</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>District metadata, or null if not found</returns>
    Task<District?> GetDistrictAsync(string districtCode, CancellationToken ct = default);

    /// <summary>
    /// Get all districts for a specific market.
    /// </summary>
    /// <param name="marketCode">The market code (e.g., "sf", "oakland")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of active districts in the market</returns>
    Task<District[]> GetDistrictsForMarketAsync(string marketCode, CancellationToken ct = default);

    /// <summary>
    /// Determine which district(s) a coordinate point belongs to.
    /// Returns the most specific (deepest) district match.
    /// </summary>
    /// <param name="latitude">Geographic latitude</param>
    /// <param name="longitude">Geographic longitude</param>
    /// <param name="marketCode">Market to search within</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>District code if found, null otherwise</returns>
    Task<string?> DetermineDistrictAsync(
        double latitude,
        double longitude,
        string marketCode,
        CancellationToken ct = default);
}
