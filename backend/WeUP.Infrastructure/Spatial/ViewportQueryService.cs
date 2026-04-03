using WeUP.Contracts.Events;
using WeUP.Domain.Events;
using WeUP.Domain.Spatial;
using System.Linq;

namespace WeUP.Infrastructure.Spatial;

/// <summary>
/// Stub implementation of ViewportQueryService for Phase 0.
/// Later: replace with PostGIS or spatial index queries.
/// </summary>
public class ViewportQueryService : IViewportQueryService
{
    private readonly IEventRepository _eventRepository;

    // Phase 0 stub: in-memory district registry
    // Future: load from database or configuration
    private static readonly Dictionary<string, District> _districtRegistry;

    // Static initializer for districts
    static ViewportQueryService()
    {
        _districtRegistry = new(StringComparer.OrdinalIgnoreCase)
        {
            ["downtown"] = new District(
                districtCode: "downtown",
                displayName: "Downtown",
                marketCode: "sf",
                boundingBoxApproximation: new BoundingBox(37.78, 37.80, -122.415, -122.393),
                sortOrder: 1,
                description: "Downtown SF"),

            ["mission"] = new District(
                districtCode: "mission",
                displayName: "Mission District",
                marketCode: "sf",
                boundingBoxApproximation: new BoundingBox(37.75, 37.77, -122.42, -122.40),
                sortOrder: 2,
                description: "Mission District"),

            ["marina"] = new District(
                districtCode: "marina",
                displayName: "Marina District",
                marketCode: "sf",
                boundingBoxApproximation: new BoundingBox(37.80, 37.81, -122.44, -122.42),
                sortOrder: 3,
                description: "Marina District"),
        };
    }

    public ViewportQueryService(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public async Task<MapFeedResponse> GetEventsInViewportAsync(
        MapFeedRequest request,
        CancellationToken ct = default)
    {
        // Validate the bounding box
        request.Bounds.Validate();

        // For Phase 0: use stub repository to get all events, then filter client-side
        // Future: send bounding box to PostGIS backend query
        var allEventsResponse = await _eventRepository.GetMapFeedAsync(request, ct);

        // Filter events within the bounding box
        var eventsInViewport = allEventsResponse.Events
            .Where(e => request.Bounds.Contains(e.Lat, e.Lng))
            .ToArray();

        return new MapFeedResponse(eventsInViewport, eventsInViewport.Length);
    }

    public async Task<EventMapCardDto[]> GetEventsInDistrictAsync(
        string districtCode,
        TimeWindowRequest window,
        string[]? categories = null,
        double minConfidence = 0.0,
        CancellationToken ct = default)
    {
        var district = await GetDistrictAsync(districtCode, ct);
        if (district == null)
            return Array.Empty<EventMapCardDto>();

        // Convert domain BoundingBox to contract GeoBoundingBox
        var geoBbox = new GeoBoundingBox(
            district.BoundingBoxApproximation.MinLat,
            district.BoundingBoxApproximation.MaxLat,
            district.BoundingBoxApproximation.MinLng,
            district.BoundingBoxApproximation.MaxLng);

        // Query within the district's bounding box approximation
        var request = new MapFeedRequest(geoBbox, window, categories, districtCode, minConfidence);

        var response = await GetEventsInViewportAsync(request, ct);
        return response.Events;
    }

    public Task<District?> GetDistrictAsync(string districtCode, CancellationToken ct = default)
    {
        _districtRegistry.TryGetValue(districtCode, out var district);
        return Task.FromResult(district);
    }

    public Task<District[]> GetDistrictsForMarketAsync(string marketCode, CancellationToken ct = default)
    {
        var districts = _districtRegistry.Values
            .Where(d => d.MarketCode == marketCode && d.IsActive)
            .OrderBy(d => d.SortOrder)
            .ToArray();

        return Task.FromResult(districts);
    }

    public async Task<string?> DetermineDistrictAsync(
        double latitude,
        double longitude,
        string marketCode,
        CancellationToken ct = default)
    {
        var districts = await GetDistrictsForMarketAsync(marketCode, ct);

        // Find the most specific (deepest) district containing the point
        // First, prefer sub-districts (non-null ParentDistrictCode)
        var subDistricts = districts
            .Where(d => d.ParentDistrictCode != null && d.ApproximatelyContains(latitude, longitude))
            .OrderBy(d => d.SortOrder)
            .ToList();

        if (subDistricts.Any())
            return subDistricts.First().DistrictCode;

        // Then, try top-level districts
        var topLevelDistricts = districts
            .Where(d => d.ParentDistrictCode == null && d.ApproximatelyContains(latitude, longitude))
            .OrderBy(d => d.SortOrder)
            .ToList();

        return topLevelDistricts.FirstOrDefault()?.DistrictCode;
    }
}
