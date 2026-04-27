using WeUP.Contracts.Events;
using WeUP.Domain.Spatial;

namespace WeUP.Infrastructure.Spatial;

/// <summary>
/// Infrastructure implementation that delegates to the canonical geo validation rules.
/// Kept behind an interface so ingestion, APIs, and moderation flows can share one policy.
/// </summary>
public sealed class GeoValidationService : IGeoValidationService
{
    public GeoValidationResult ValidateEventCoordinates(
        double? latitude,
        double? longitude,
        double? locationConfidence = null,
        string? address = null)
        => GeoValidationRules.ValidatePoint(latitude, longitude, locationConfidence, address);

    public GeoValidationResult ValidateViewportCoordinate(double? latitude, double? longitude)
        => GeoValidationRules.ValidatePoint(latitude, longitude);

    public GeoBoundingBoxValidationResult ValidateBoundingBox(GeoBoundingBox bounds)
        => GeoValidationRules.ValidateBoundingBox(bounds);

    public GeoBoundingBoxValidationResult ValidateBoundingBoxQuery(string? rawBbox)
        => GeoValidationRules.ParseBoundingBox(rawBbox);
}
