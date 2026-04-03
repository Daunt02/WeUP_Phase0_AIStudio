namespace WeUP.Domain.Spatial;

/// <summary>
/// Canonical bounding box for geographic queries.
/// Represents a rectangular region on the map bounded by latitude/longitude coordinates.
/// </summary>
public record BoundingBox(
    double MinLat,
    double MaxLat,
    double MinLng,
    double MaxLng)
{
    /// <summary>
    /// Validates the bounding box for correctness.
    /// Throws InvalidOperationException if invalid.
    /// </summary>
    public void Validate()
    {
        // Latitude must be in [-90, 90]
        if (MinLat < -90 || MinLat > 90)
            throw new InvalidOperationException($"MinLat {MinLat} must be between -90 and 90");
        if (MaxLat < -90 || MaxLat > 90)
            throw new InvalidOperationException($"MaxLat {MaxLat} must be between -90 and 90");

        // Longitude must be in [-180, 180]
        if (MinLng < -180 || MinLng > 180)
            throw new InvalidOperationException($"MinLng {MinLng} must be between -180 and 180");
        if (MaxLng < -180 || MaxLng > 180)
            throw new InvalidOperationException($"MaxLng {MaxLng} must be between -180 and 180");

        // Min must be less than Max for latitude
        if (MinLat >= MaxLat)
            throw new InvalidOperationException($"MinLat ({MinLat}) must be less than MaxLat ({MaxLat})");

        // Min must be less than Max for longitude (can wrap around dateline in future, but not for Phase 0)
        if (MinLng >= MaxLng)
            throw new InvalidOperationException($"MinLng ({MinLng}) must be less than MaxLng ({MaxLng})");
    }

    /// <summary>
    /// Checks if a coordinate point is within this bounding box.
    /// </summary>
    public bool Contains(double latitude, double longitude)
    {
        return latitude >= MinLat && latitude <= MaxLat &&
               longitude >= MinLng && longitude <= MaxLng;
    }

    /// <summary>
    /// Calculates the center point of the bounding box.
    /// </summary>
    public (double Latitude, double Longitude) Center =>
        (
            (MinLat + MaxLat) / 2,
            (MinLng + MaxLng) / 2
        );

    /// <summary>
    /// Calculates approximate area in square degrees (not precise but useful for comparison).
    /// </summary>
    public double ApproximateAreaInSquareDegrees =>
        (MaxLat - MinLat) * (MaxLng - MinLng);
}
