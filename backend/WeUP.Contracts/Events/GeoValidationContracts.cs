using System.Globalization;

namespace WeUP.Contracts.Events;

/// <summary>
/// Canonical point contract used when validating event, viewport, and query coordinates.
/// This is an explicit lat/lng pair only; validation never implies reverse geocoding.
/// </summary>
public sealed record GeoPoint(
    double Latitude,
    double Longitude);

/// <summary>
/// Deterministic integrity category for a spatial point.
/// ValidLowConfidence means the point is structurally valid but not trusted enough for public map rendering.
/// </summary>
public enum GeoIntegrityCategory
{
    Valid = 0,
    ValidLowConfidence = 1,
    Invalid = 2,
    Missing = 3,
}

/// <summary>
/// Explicit downstream rendering rule for a validated location.
/// Render: safe for public map use.
/// RequireModeration: coordinates exist but need moderator review before map use.
/// UseFallback: do not render on map; non-map surfaces may still use address/text fallback.
/// Block: coordinates are invalid and must be suppressed.
/// </summary>
public enum GeoRenderingEligibility
{
    Render = 0,
    RequireModeration = 1,
    UseFallback = 2,
    Block = 3,
}

public sealed record GeoValidationIssue(
    string Code,
    string Message,
    string? Field = null);

/// <summary>
/// Result for validating a single coordinate pair.
/// </summary>
public sealed record GeoValidationResult(
    GeoIntegrityCategory Category,
    GeoRenderingEligibility RenderingEligibility,
    GeoPoint? Point,
    double? Confidence,
    IReadOnlyList<GeoValidationIssue> Issues)
{
    public bool IsValid => Category is GeoIntegrityCategory.Valid or GeoIntegrityCategory.ValidLowConfidence;

    public bool IsRenderableOnMap => RenderingEligibility == GeoRenderingEligibility.Render;

    public bool RequiresModeration => RenderingEligibility == GeoRenderingEligibility.RequireModeration;

    public bool RequiresFallbackTreatment => RenderingEligibility == GeoRenderingEligibility.UseFallback;
}

/// <summary>
/// Result for validating bbox query parameters or viewport bounds.
/// Validation is explicit and deterministic; callers must inspect errors rather than infer fallback behavior.
/// </summary>
public sealed record GeoBoundingBoxValidationResult(
    bool IsValid,
    GeoBoundingBox? Bounds,
    IReadOnlyList<GeoValidationIssue> Issues);

/// <summary>
/// Shared geo integrity rules used by backend services and contract guardrails.
/// These rules never geocode, infer a city center, or silently coerce invalid coordinates.
/// </summary>
public static class GeoValidationRules
{
    public const double PublicMapConfidenceThreshold = 0.50;
    public const double MaxBoundingBoxSpanDegrees = 5.0;
    public const double MaxBoundingBoxAreaSquareDegrees = 8.0;

    public static GeoValidationResult ValidatePoint(
        double? latitude,
        double? longitude,
        double? confidence = null,
        string? address = null)
    {
        var issues = new List<GeoValidationIssue>();

        if (!latitude.HasValue && !longitude.HasValue)
        {
            issues.Add(new GeoValidationIssue(
                Code: "MISSING_COORDINATES",
                Message: "Coordinates are missing.",
                Field: "coordinates"));

            return new GeoValidationResult(
                GeoIntegrityCategory.Missing,
                GeoRenderingEligibility.UseFallback,
                Point: null,
                Confidence: confidence,
                Issues: issues);
        }

        if (!latitude.HasValue)
        {
            issues.Add(new GeoValidationIssue(
                Code: "MISSING_LATITUDE",
                Message: "Latitude is missing.",
                Field: "latitude"));
        }

        if (!longitude.HasValue)
        {
            issues.Add(new GeoValidationIssue(
                Code: "MISSING_LONGITUDE",
                Message: "Longitude is missing.",
                Field: "longitude"));
        }

        if (issues.Count > 0)
        {
            return new GeoValidationResult(
                GeoIntegrityCategory.Missing,
                GeoRenderingEligibility.UseFallback,
                Point: null,
                Confidence: confidence,
                Issues: issues);
        }

        var lat = latitude!.Value;
        var lng = longitude!.Value;

        if (double.IsNaN(lat) || double.IsInfinity(lat))
        {
            issues.Add(new GeoValidationIssue(
                Code: "INVALID_LATITUDE",
                Message: $"Latitude must be a finite number in [-90, 90], got {lat}.",
                Field: "latitude"));
        }
        else if (lat < -90 || lat > 90)
        {
            issues.Add(new GeoValidationIssue(
                Code: "INVALID_LATITUDE",
                Message: $"Latitude must be in [-90, 90], got {lat}.",
                Field: "latitude"));
        }

        if (double.IsNaN(lng) || double.IsInfinity(lng))
        {
            issues.Add(new GeoValidationIssue(
                Code: "INVALID_LONGITUDE",
                Message: $"Longitude must be a finite number in [-180, 180], got {lng}.",
                Field: "longitude"));
        }
        else if (lng < -180 || lng > 180)
        {
            issues.Add(new GeoValidationIssue(
                Code: "INVALID_LONGITUDE",
                Message: $"Longitude must be in [-180, 180], got {lng}.",
                Field: "longitude"));
        }

        if (Math.Abs(lat) < 0.0000001 && Math.Abs(lng) < 0.0000001)
        {
            issues.Add(new GeoValidationIssue(
                Code: "NULL_ISLAND_COORDINATES",
                Message: "Coordinates (0, 0) indicate an unresolved null-island location.",
                Field: "latitude"));
        }

        if (confidence.HasValue)
        {
            var value = confidence.Value;
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1)
            {
                issues.Add(new GeoValidationIssue(
                    Code: "INVALID_LOCATION_CONFIDENCE",
                    Message: $"Location confidence must be in [0, 1], got {value}.",
                    Field: "confidence"));
            }
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            issues.Add(new GeoValidationIssue(
                Code: "MISSING_ADDRESS",
                Message: "Address is missing; non-map fallback may rely on text-only location details.",
                Field: "address"));
        }

        var structuralErrors = issues.Where(issue => issue.Code is
            "INVALID_LATITUDE" or
            "INVALID_LONGITUDE" or
            "NULL_ISLAND_COORDINATES" or
            "INVALID_LOCATION_CONFIDENCE").ToArray();

        if (structuralErrors.Length > 0)
        {
            return new GeoValidationResult(
                GeoIntegrityCategory.Invalid,
                GeoRenderingEligibility.Block,
                Point: null,
                Confidence: confidence,
                Issues: issues);
        }

        var point = new GeoPoint(lat, lng);
        if (confidence.HasValue && confidence.Value < PublicMapConfidenceThreshold)
        {
            issues.Add(new GeoValidationIssue(
                Code: "LOW_LOCATION_CONFIDENCE",
                Message: $"Location confidence {confidence.Value.ToString("F2", CultureInfo.InvariantCulture)} is below the public map threshold {PublicMapConfidenceThreshold.ToString("F2", CultureInfo.InvariantCulture)}.",
                Field: "confidence"));

            return new GeoValidationResult(
                GeoIntegrityCategory.ValidLowConfidence,
                GeoRenderingEligibility.RequireModeration,
                Point: point,
                Confidence: confidence,
                Issues: issues);
        }

        return new GeoValidationResult(
            GeoIntegrityCategory.Valid,
            GeoRenderingEligibility.Render,
            Point: point,
            Confidence: confidence,
            Issues: issues);
    }

    public static GeoBoundingBoxValidationResult ValidateBoundingBox(GeoBoundingBox bbox)
    {
        var issues = new List<GeoValidationIssue>();

        AppendPointIssues(issues, ValidatePoint(bbox.MinLat, bbox.MinLng), "minLat", "minLng");
        AppendPointIssues(issues, ValidatePoint(bbox.MaxLat, bbox.MaxLng), "maxLat", "maxLng");

        if (bbox.MinLat >= bbox.MaxLat)
        {
            issues.Add(new GeoValidationIssue(
                Code: "INVALID_BBOX_LATITUDE_ORDER",
                Message: $"MinLat ({bbox.MinLat}) must be less than MaxLat ({bbox.MaxLat}).",
                Field: "minLat"));
        }

        if (bbox.MinLng >= bbox.MaxLng)
        {
            issues.Add(new GeoValidationIssue(
                Code: "INVALID_BBOX_LONGITUDE_ORDER",
                Message: $"MinLng ({bbox.MinLng}) must be less than MaxLng ({bbox.MaxLng}).",
                Field: "minLng"));
        }

        var latSpan = bbox.MaxLat - bbox.MinLat;
        var lngSpan = bbox.MaxLng - bbox.MinLng;

        if (latSpan > MaxBoundingBoxSpanDegrees)
        {
            issues.Add(new GeoValidationIssue(
                Code: "BBOX_LATITUDE_SPAN_TOO_WIDE",
                Message: $"Latitude span ({latSpan}) exceeds max allowed span ({MaxBoundingBoxSpanDegrees}).",
                Field: "maxLat"));
        }

        if (lngSpan > MaxBoundingBoxSpanDegrees)
        {
            issues.Add(new GeoValidationIssue(
                Code: "BBOX_LONGITUDE_SPAN_TOO_WIDE",
                Message: $"Longitude span ({lngSpan}) exceeds max allowed span ({MaxBoundingBoxSpanDegrees}).",
                Field: "maxLng"));
        }

        var area = latSpan * lngSpan;
        if (area > MaxBoundingBoxAreaSquareDegrees)
        {
            issues.Add(new GeoValidationIssue(
                Code: "BBOX_AREA_TOO_WIDE",
                Message: $"Bounding box area ({area}) exceeds max allowed area ({MaxBoundingBoxAreaSquareDegrees} square degrees).",
                Field: "bounds"));
        }

        return new GeoBoundingBoxValidationResult(
            IsValid: issues.Count == 0,
            Bounds: issues.Count == 0 ? bbox : null,
            Issues: issues);
    }

    public static GeoBoundingBoxValidationResult ParseBoundingBox(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new GeoBoundingBoxValidationResult(
                IsValid: false,
                Bounds: null,
                Issues:
                [
                    new GeoValidationIssue(
                        Code: "MISSING_BBOX",
                        Message: "bbox is required and must use format 'minLng,minLat,maxLng,maxLat'.",
                        Field: "bbox")
                ]);
        }

        var tokens = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length != 4)
        {
            return new GeoBoundingBoxValidationResult(
                IsValid: false,
                Bounds: null,
                Issues:
                [
                    new GeoValidationIssue(
                        Code: "INVALID_BBOX_SHAPE",
                        Message: "bbox must include exactly 4 comma-separated numeric values: minLng,minLat,maxLng,maxLat.",
                        Field: "bbox")
                ]);
        }

        if (!double.TryParse(tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var minLng) ||
            !double.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var minLat) ||
            !double.TryParse(tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var maxLng) ||
            !double.TryParse(tokens[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var maxLat))
        {
            return new GeoBoundingBoxValidationResult(
                IsValid: false,
                Bounds: null,
                Issues:
                [
                    new GeoValidationIssue(
                        Code: "INVALID_BBOX_NUMBER",
                        Message: "bbox values must be valid numbers in format minLng,minLat,maxLng,maxLat.",
                        Field: "bbox")
                ]);
        }

        return ValidateBoundingBox(new GeoBoundingBox(minLat, maxLat, minLng, maxLng));
    }

    private static void AppendPointIssues(
        List<GeoValidationIssue> destination,
        GeoValidationResult result,
        string latitudeField,
        string longitudeField)
    {
        foreach (var issue in result.Issues)
        {
            if (issue.Code == "MISSING_ADDRESS")
            {
                continue;
            }

            destination.Add(issue.Field switch
            {
                "latitude" => issue with { Field = latitudeField },
                "longitude" => issue with { Field = longitudeField },
                _ => issue,
            });
        }
    }
}
