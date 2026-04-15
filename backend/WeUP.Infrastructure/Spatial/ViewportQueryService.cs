using WeUP.Contracts.Events;
using WeUP.Domain.Events;
using WeUP.Domain.Spatial;
using WeUP.Infrastructure.Seed;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace WeUP.Infrastructure.Spatial;

/// <summary>
/// Stub implementation of ViewportQueryService for Phase 0.
/// Later: replace with PostGIS or spatial index queries.
/// </summary>
public class ViewportQueryService : IViewportQueryService
{
    private readonly IEventRepository _eventRepository;
    private readonly ILogger<ViewportQueryService> _logger;

    private readonly Dictionary<string, District> _districtRegistry;

    public ViewportQueryService(
        IEventRepository eventRepository,
        Phase0SeedLoader seedLoader,
        ILogger<ViewportQueryService> logger)
    {
        _eventRepository = eventRepository;
        _logger = logger;
        _districtRegistry = BuildDistrictRegistry(seedLoader.Load());
    }

    public async Task<MapFeedResponse> GetEventsInViewportAsync(
        MapFeedRequest request,
        CancellationToken ct = default)
    {
        // Validate the bounding box
        request.Bounds.Validate();

        _logger.LogInformation(
            "Viewport query: bounds=[{MinLat},{MaxLat},{MinLng},{MaxLng}] locality.market={Market} locality.district={District} locality.neighborhood={Neighborhood}",
            request.Bounds.MinLat,
            request.Bounds.MaxLat,
            request.Bounds.MinLng,
            request.Bounds.MaxLng,
            request.Locality?.MarketCode ?? "all",
            request.Locality?.DistrictCode ?? request.DistrictCode ?? "all",
            request.Locality?.NeighborhoodCode ?? "all");

        // Phase 0: repository returns event list; spatial index/PostGIS can replace this seam.
        var allEventsResponse = await _eventRepository.GetMapFeedAsync(request, ct);

        // Filter events within the bounding box
        var eventsInViewport = allEventsResponse.Events
            .Where(e => request.Bounds.Contains(e.Lat, e.Lng))
            .ToArray();

        _logger.LogInformation(
            "Viewport query result: {EventCount} events in viewport (total={TotalEvents})",
            eventsInViewport.Length,
            allEventsResponse.Events.Length);

        return new MapFeedResponse(
            eventsInViewport,
            eventsInViewport.Length,
            allEventsResponse.Clusters,
            allEventsResponse.QueryMode);
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
        var request = new MapFeedRequest(
            geoBbox,
            window,
            categories,
            Locality: new LocalityFilterRequest(DistrictCode: districtCode),
            DistrictCode: districtCode,
            MinConfidence: minConfidence);

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

    private static Dictionary<string, District> BuildDistrictRegistry(Phase0SeedDataset dataset)
    {
        var venueByDistrict = dataset.Venues
            .GroupBy(v => (
                MarketCode: v.MarketCode.ToLowerInvariant(),
                DistrictCode: v.DistrictCode.ToLowerInvariant()))
            .ToDictionary(g => g.Key, g => g.ToArray());

        var districts = new Dictionary<string, District>(StringComparer.OrdinalIgnoreCase);

        foreach (var market in dataset.Markets)
        {
            for (var i = 0; i < market.Districts.Length; i++)
            {
                var districtCode = market.Districts[i];
                var key = (market.Code.ToLowerInvariant(), districtCode.ToLowerInvariant());
                if (!venueByDistrict.TryGetValue(key, out var districtVenues) || districtVenues.Length == 0)
                {
                    continue;
                }

                var bbox = BuildDistrictBoundingBox(districtVenues);
                districts[districtCode] = new District(
                    districtCode: districtCode,
                    displayName: HumanizeDistrictName(districtCode),
                    marketCode: market.Code,
                    boundingBoxApproximation: bbox,
                    sortOrder: i + 1,
                    description: $"{HumanizeDistrictName(districtCode)} ({market.DisplayName})");
            }
        }

        return districts;
    }

    private static BoundingBox BuildDistrictBoundingBox(Phase0VenueSeed[] venues)
    {
        const double padding = 0.01;
        var minLat = venues.Min(v => v.Latitude) - padding;
        var maxLat = venues.Max(v => v.Latitude) + padding;
        var minLng = venues.Min(v => v.Longitude) - padding;
        var maxLng = venues.Max(v => v.Longitude) + padding;
        return new BoundingBox(minLat, maxLat, minLng, maxLng);
    }

    private static string HumanizeDistrictName(string districtCode)
    {
        var parts = districtCode.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }
}
