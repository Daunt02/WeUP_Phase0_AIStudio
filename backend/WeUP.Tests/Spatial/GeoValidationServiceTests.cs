using WeUP.Contracts.Events;
using WeUP.Infrastructure.Spatial;
using Xunit;

namespace WeUP.Tests.Spatial;

public sealed class GeoValidationServiceTests
{
    private readonly GeoValidationService _service = new();

    [Fact]
    public void ValidateEventCoordinates_ReturnsRenderable_ForHighConfidencePoint()
    {
        var result = _service.ValidateEventCoordinates(29.7604, -95.3698, 0.91, "901 Bagby St, Houston, TX");

        Assert.Equal(GeoIntegrityCategory.Valid, result.Category);
        Assert.True(result.IsRenderableOnMap);
        Assert.Equal(GeoRenderingEligibility.Render, result.RenderingEligibility);
    }

    [Fact]
    public void ValidateEventCoordinates_ReturnsModeration_ForLowConfidencePoint()
    {
        var result = _service.ValidateEventCoordinates(29.7604, -95.3698, 0.32, "901 Bagby St, Houston, TX");

        Assert.Equal(GeoIntegrityCategory.ValidLowConfidence, result.Category);
        Assert.False(result.IsRenderableOnMap);
        Assert.True(result.RequiresModeration);
        Assert.Contains(result.Issues, issue => issue.Code == "LOW_LOCATION_CONFIDENCE");
    }

    [Fact]
    public void ValidateEventCoordinates_ReturnsFallback_ForMissingCoordinates()
    {
        var result = _service.ValidateEventCoordinates(null, null, 0.8, "901 Bagby St, Houston, TX");

        Assert.Equal(GeoIntegrityCategory.Missing, result.Category);
        Assert.True(result.RequiresFallbackTreatment);
        Assert.Contains(result.Issues, issue => issue.Code == "MISSING_COORDINATES");
    }

    [Fact]
    public void ValidateEventCoordinates_ReturnsBlocked_ForNullIsland()
    {
        var result = _service.ValidateEventCoordinates(0, 0, 0.95, "Unknown");

        Assert.Equal(GeoIntegrityCategory.Invalid, result.Category);
        Assert.Equal(GeoRenderingEligibility.Block, result.RenderingEligibility);
        Assert.Contains(result.Issues, issue => issue.Code == "NULL_ISLAND_COORDINATES");
    }

    [Fact]
    public void ValidateBoundingBoxQuery_RejectsInvalidShape()
    {
        var result = _service.ValidateBoundingBoxQuery("-95.4,29.7,-95.2");

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "INVALID_BBOX_SHAPE");
    }

    [Fact]
    public void ValidateBoundingBox_RejectsWideViewport()
    {
        var result = _service.ValidateBoundingBox(new GeoBoundingBox(29, 35, -96, -95));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "BBOX_LATITUDE_SPAN_TOO_WIDE");
    }
}
