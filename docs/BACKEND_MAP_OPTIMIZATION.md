// Backend Response Shaping Guidance for Optimized Map Feed
//
// This file contains design guidance and example code for optimizing the
// backend map feed endpoint to work efficiently with the optimized frontend.
//
// ===========================================================================
// KEY PRINCIPLES
// ===========================================================================
//
// 1. RESPONSE FILTERING: Return only visible + non-low-confidence markers
// - Exclude low-confidence events from response (frontend filters these anyway)
// - Include confidence score for client-side filtering if needed
// - Filter by bounding box on database layer for efficiency
//
// 2. REASONABLE LIMITS: Cap marker count to prevent frontend overload
// - Target: <= 250 markers for typical urban viewport
// - If over limit, implement clustering at database level or truncate with "more available" flag
// - Never load unlimited markers and expect frontend to paginate
//
// 3. EFFICIENT SHAPING: Include only fields needed for map rendering
// - Strip unused fields (internal IDs, full descriptions, etc.)
// - Include: id, title, venueName, lat, lng, category, startTime, thumbnail
// - Exclude: long descriptions, complex nested objects, internal audit fields
//
// 4. CACHE HEADERS: Set appropriate cache controls
// - Allow browser caching: "Cache-Control: public, max-age=300"
// - Frontend respects this via query cache layer
// - Backend itself should use query result caching (e.g., Redis)
//
// 5. ASYNC PREFETCH: Support "viewport zoom" queries for UX preview
// - Allow bounding box queries at different zoom levels
// - Pre-compute clusters at different zoom levels (z=10, z=12, z=14, z=18)
// - Return cluster metadata for client-side expansion
//
// ===========================================================================
// IMPLEMENTATION EXAMPLE (C# / .NET)
// ===========================================================================

/\*
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WeUP.Api.Endpoints;

public static class MapFeedOptimizations
{
/// <summary>
/// GetOptimizedMapFeed — Efficient map marker endpoint
///
/// Optimizations:
/// - Early filtering at repository level (bbox + visibility + confidence)
/// - Limit marker count to prevent frontend overload
/// - Return only essential fields for rendering
/// - Add cache headers for browser caching
/// - Support cluster expansion metadata
/// </summary>
public static void MapOptimizedMapFeed(
this IEndpointRouteBuilder app)
{
app.MapGet("/api/events/map-feed/optimized", async (
HttpContext ctx,
IEventRepository repo,
IClusteringService clustering,
string bbox,
int zoom,
string? preset = "tomorrow",
string? timezone = "UTC",
CancellationToken ct = default) =>
{
// ===================================================================
// 1. PARSE & VALIDATE BOUNDS
// ===================================================================

            if (!TryParseBbox(bbox, out var bounds, out var error))
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["bbox"] = [error] },
                    statusCode: 422);

            if (bounds is null)
                return Results.BadRequest("Invalid bounding box");

            // ===================================================================
            // 2. QUERY DATABASE EFFICIENTLY
            // ===================================================================
            //
            // Key optimizations:
            // - Filter at repository level: bbox, visibility, confidence, temporal window
            // - Use spatial indexes (PostGIS, SQL Server Spatial, etc.)
            // - Limit query to MaxMarkers (e.g., 500) to avoid full table scans
            // - Order by relevance (confidence, proximity to viewport center)

            const int MaxMarkers = 250;

            var mapFeedResponse = await repo.GetMapFeedAsync(
                new MapFeedRequest(
                    Bounds: bounds,
                    Window: new TimeWindowRequest(
                        StartUtc: DateTime.UtcNow,
                        EndUtc: DateTime.UtcNow.AddDays(7),
                        Timezone: timezone),
                    Categories: null,
                    DistrictCode: null,
                    MinConfidence: 0.4, // Only include moderate+ confidence
                    Sort: "confidence_desc"),
                ct);

            var markerCandidates = mapFeedResponse.Events
                .Take(MaxMarkers)
                .ToList();

            // ===================================================================
            // 3. CHECK IF CLUSTERING NEEDED (for high-density areas)
            // ===================================================================
            //
            // If marker density is excessive, apply server-side clustering
            // to prevent frontend from rendering 1000+ markers.

            var markersToDraw = markerCandidates;
            var clusterMetadata = new Dictionary<string, object>();

            if (markerCandidates.Count > 150 && zoom < 14)
            {
                // Density is high at this zoom level; apply clustering
                var clusters = clustering.ClusterMarkersForZoom(
                    markerCandidates,
                    zoom: zoom,
                    clusterRadius: 50); // pixels

                markersToDraw = clusters.CenterPoints.ToList();
                clusterMetadata["clusters"] = clusters.Groups.Select(g => new
                {
                    g.ClusterId,
                    MarkerCount = g.Markers.Count,
                    CenterLat = g.CenterLat,
                    CenterLng = g.CenterLng,
                    Confidence = g.AverageConfidence,
                }).ToList();
            }

            // ===================================================================
            // 4. SHAPE RESPONSE (remove unnecessary fields)
            // ===================================================================

            var optimizedMarkers = markersToDraw.Select(card => new
            {
                // Essential for rendering
                id = card.Id,
                title = card.Title,
                venueName = card.VenueName,
                lat = card.Latitude,
                lng = card.Longitude,
                category = card.Category?.ToLowerInvariant() ?? "other",
                startTime = card.StartTime.ToString("O"),

                // Optional for UX polish
                thumbnailUrl = card.ThumbnailUrl,
                confidence = card.Confidence,

                // Cluster info (if applicable)
                clusterType = markersToDraw.Count != markerCandidates.Count
                    ? "clustered"
                    : null,
            }).ToList();

            // ===================================================================
            // 5. BUILD RESPONSE
            // ===================================================================

            var response = new
            {
                events = optimizedMarkers,
                totalCount = markersToDraw.Count,
                hasMore = markerCandidates.Count > MaxMarkers,
                clusterMetadata,
                _links = new
                {
                    self = $"/api/events/map-feed/optimized?bbox={bbox}&zoom={zoom}&preset={preset}"
                }
            };

            // ===================================================================
            // 6. SET CACHE HEADERS (5 minute TTL)
            // ===================================================================

            ctx.Response.Headers["Cache-Control"] = "public, max-age=300";
            ctx.Response.Headers["ETag"] = GenerateETag(response);

            return Results.Ok(response);
        })
        .WithName("GetOptimizedMapFeed")
        .Produces<OptimizedMapFeedResponse>()
        .WithOpenApi(op =>
        {
            op.Summary = "Get optimized map feed with clustering support";
            op.Description =
                "Returns map markers optimized for rendering performance. " +
                "Automatically applies clustering for high-density areas. " +
                "Maximum 250 markers per response.";
            return op;
        });
    }

    /// <summary>
    /// GetClusterExpansion — Fetch individual markers within a cluster
    ///
    /// When user clicks a cluster, this endpoint provides the underlying markers.
    /// </summary>
    public static void MapClusterExpansionEndpoint(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events/clusters/{clusterId}/markers", async (
            string clusterId,
            IEventRepository repo,
            IClusteringService clustering,
            CancellationToken ct) =>
        {
            // Fetch original markers for this cluster
            var clusterMarkers = await clustering.GetMarkersInClusterAsync(clusterId, ct);

            var response = new
            {
                clusterId,
                markers = clusterMarkers.Select(m => new
                {
                    id = m.Id,
                    title = m.Title,
                    venueName = m.VenueName,
                    lat = m.Latitude,
                    lng = m.Longitude,
                    category = m.Category?.ToLowerInvariant() ?? "other",
                    startTime = m.StartTime.ToString("O"),
                    thumbnailUrl = m.ThumbnailUrl,
                }).ToList(),
            };

            return Results.Ok(response);
        })
        .WithName("GetClusterExpansion")
        .WithOpenApi();
    }

}

// ===========================================================================
// RESPONSE CONTRACT EXAMPLE
// ===========================================================================

public class OptimizedMapFeedResponse
{
public List<MapMarkerDto> Events { get; set; } = new();
public int TotalCount { get; set; }
public bool HasMore { get; set; }
public Dictionary<string, object> ClusterMetadata { get; set; } = new();
}

public class MapMarkerDto
{
public string Id { get; set; }
public string Title { get; set; }
public string VenueName { get; set; }
public double Lat { get; set; }
public double Lng { get; set; }
public string Category { get; set; }
public string StartTime { get; set; }
public string? ThumbnailUrl { get; set; }
public double Confidence { get; set; }
}

// ===========================================================================
// DATABASE QUERY OPTIMIZATION
// ===========================================================================
//
// In IEventRepository.GetMapFeedAsync(), implement these optimizations:
//
// 1. SPATIAL INDEX:
// `sql
//    CREATE SPATIAL INDEX idx_event_location 
//    ON Events (Latitude, Longitude) 
//    USING GEOGRAPHY;
//    `
//
// 2. EFFICIENT BBOX FILTER:
// `csharp
//    var query = _context.Events
//        .Where(e => e.Latitude >= bounds.MinLat
//                 && e.Latitude <= bounds.MaxLat
//                 && e.Longitude >= bounds.MinLng
//                 && e.Longitude <= bounds.MaxLng)
//        .Where(e => e.Confidence >= 0.4)
//        .Where(e => e.Status == EventStatus.Published)
//        .OrderByDescending(e => e.Confidence)
//        .ThenBy(e => e.StartTime)
//        .Take(500)
//        .Select(e => new EventMapCardDto
//        {
//            Id = e.Id,
//            Title = e.Title,
//            Latitude = e.Latitude,
//            Longitude = e.Longitude,
//            // ... only essential fields
//        })
//        .ToListAsync();
//    `
//
// 3. RESULT CACHING:
// `csharp
//    var cacheKey = $"mapfeed:{bbox.MinLng}:{bbox.MinLat}:{bbox.MaxLng}:{bbox.MaxLat}:{startTime:yyyyMMdd}";
//    var cached = await _cache.GetAsync<MapFeedResponse>(cacheKey);
//    if (cached != null) return cached;
//    
//    // ... execute query ...
//    
//    await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));
//    `
//
// 4. PAGINATION PREVENT:
// Do NOT paginate map results. Instead, cap at MaxMarkers and use
// a "load more in cluster" approach for high-density areas.

// ===========================================================================
// PERFORMANCE TARGETS (Backend)
// ===========================================================================
//
// ✓ Query execution: < 200ms
// ✓ Response serialization: < 100ms
// ✓ Total response time: < 500ms
// ✓ Response size: < 1 MB (compressed)
// ✓ Cache hit ratio: > 70% during normal operation
// ✓ P95 latency: < 800ms (including network)
//
// Monitor these with Application Insights or similar:
// - Query duration histogram
// - Cache hit/miss ratio
// - Marker count distribution
// - Clustering overhead (if used)

// ===========================================================================
// DEPLOYMENT CHECKLIST
// ===========================================================================
//
// Before enabling optimized endpoint in production:
//
// [ ] Spatial indexes created on Location columns
// [ ] Query result caching (Redis) configured
// [ ] Response compression enabled (gzip, brotli)
// [ ] Cache headers set correctly
// [ ] Cluster expansion endpoint tested
// [ ] Load test with 10k+ markers in viewport
// [ ] Monitor Application Insights for slow queries
// [ ] Set up alerts for response timeout > 1s
// [ ] Verify ETag generation works (for 304 Not Modified)
// [ ] Test with poor network (slow 3G, high latency)
