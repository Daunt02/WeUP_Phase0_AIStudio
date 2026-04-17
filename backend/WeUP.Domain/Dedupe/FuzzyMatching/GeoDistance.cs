namespace WeUP.Domain.Dedupe.FuzzyMatching;

/// <summary>
/// Distance seam for future database-native implementations (e.g., PostGIS ST_Distance).
/// Current default uses deterministic in-process Haversine math.
/// </summary>
public interface IGeoDistanceCalculator
{
    double CalculateMeters(double latitudeA, double longitudeA, double latitudeB, double longitudeB);
}

/// <summary>
/// Deterministic great-circle distance calculator using the Haversine formula.
/// </summary>
public sealed class HaversineGeoDistanceCalculator : IGeoDistanceCalculator
{
    public const double EarthRadiusMeters = 6_371_000.0;

    public double CalculateMeters(double latitudeA, double longitudeA, double latitudeB, double longitudeB)
    {
        var deltaLat = DegreesToRadians(latitudeB - latitudeA);
        var deltaLon = DegreesToRadians(longitudeB - longitudeA);
        var a =
            Math.Sin(deltaLat / 2.0) * Math.Sin(deltaLat / 2.0) +
            Math.Cos(DegreesToRadians(latitudeA)) *
            Math.Cos(DegreesToRadians(latitudeB)) *
            Math.Sin(deltaLon / 2.0) * Math.Sin(deltaLon / 2.0);

        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return EarthRadiusMeters * c;
    }

    private static double DegreesToRadians(double value) => value * (Math.PI / 180.0);
}