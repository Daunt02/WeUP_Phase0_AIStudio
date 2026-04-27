using WeUP.Contracts.Events;

namespace WeUP.Domain.Spatial;

/// <summary>
/// Shared geospatial validation seam for ingestion, API, and map guardrails.
/// Validation is deterministic and does not infer or recover coordinates.
/// </summary>
public interface IGeoValidationService
{
    GeoValidationResult ValidateEventCoordinates(
        double? latitude,
        double? longitude,
        double? locationConfidence = null,
        string? address = null);

    GeoValidationResult ValidateViewportCoordinate(
        double? latitude,
        double? longitude);

    GeoBoundingBoxValidationResult ValidateBoundingBox(GeoBoundingBox bounds);

    GeoBoundingBoxValidationResult ValidateBoundingBoxQuery(string? rawBbox);
}
