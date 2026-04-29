// ------------------------------------------------------------
// File: WeUP.Application/Dedupe/GeoMatcher.cs
// M2-P08: Geo-Spatial Proximity Matching v1.0
// ------------------------------------------------------------
using Microsoft.Extensions.Logging;
using WeUP.Contracts.Dedupe;

namespace WeUP.Application.Dedupe;

/// <summary>
/// Deterministic Haversine-based matcher with fixed similarity buckets.
/// </summary>
public sealed class GeoMatcher : IGeoMatcher
{
    private const double EarthRadiusMeters = 6_371_000d;

    private readonly ILogger<GeoMatcher> _log;

    public GeoMatcher(ILogger<GeoMatcher> log)
    {
        _log = log;
    }

    public Task<GeoMatchResult> ComputeSimilarityAsync(GeoPoint? a, GeoPoint? b)
    {
        try
        {
            if (!a.HasValue || !b.HasValue)
            {
                return Task.FromResult(new GeoMatchResult(0f, double.MaxValue));
            }

            double meters = HaversineMeters(a.Value, b.Value);
            if (!double.IsFinite(meters))
            {
                return Task.FromResult(new GeoMatchResult(0f, double.MaxValue));
            }

            float similarity = meters switch
            {
                < 30d => 1.0f,
                < 100d => 0.9f,
                < 250d => 0.8f,
                < 500d => 0.6f,
                < 1000d => 0.4f,
                < 2000d => 0.2f,
                _ => 0f,
            };

            _log.LogDebug("Geo similarity: {Meters:N0} m -> {Score}", meters, similarity);
            return Task.FromResult(new GeoMatchResult(similarity, meters));
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Geo similarity computation failed.");
            return Task.FromResult(new GeoMatchResult(0f, double.MaxValue));
        }
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;

    private static double HaversineMeters(GeoPoint a, GeoPoint b)
    {
        double dLat = ToRadians(b.Latitude - a.Latitude);
        double dLon = ToRadians(b.Longitude - a.Longitude);
        double lat1 = ToRadians(a.Latitude);
        double lat2 = ToRadians(b.Latitude);

        double sinLat = Math.Sin(dLat / 2d);
        double sinLon = Math.Sin(dLon / 2d);
        double h = (sinLat * sinLat) + (sinLon * sinLon * Math.Cos(lat1) * Math.Cos(lat2));
        double c = 2d * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1d - h));
        return EarthRadiusMeters * c;
    }
}