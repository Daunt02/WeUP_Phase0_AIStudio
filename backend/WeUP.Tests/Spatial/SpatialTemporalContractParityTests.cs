using System;
using WeUP.Contracts.Events;
using WeUP.Domain.Temporal;
using Xunit;

namespace WeUP.Tests.Spatial;

public sealed class SpatialTemporalContractParityTests
{
    [Fact]
    public void GeoBoundingBoxValidate_RejectsLatitudeSpanAboveFiveDegrees()
    {
        var bbox = new GeoBoundingBox(10, 16, 20, 21);
        var ex = Assert.Throws<InvalidOperationException>(() => bbox.Validate());
        Assert.Contains("Latitude span", ex.Message);
    }

    [Fact]
    public void GeoBoundingBoxValidate_RejectsAreaAboveEightSquareDegrees()
    {
        var bbox = new GeoBoundingBox(29, 31, -96, -91);
        var ex = Assert.Throws<InvalidOperationException>(() => bbox.Validate());
        Assert.Contains("Bounding box area", ex.Message);
    }

    [Fact]
    public void GeoBoundingBoxValidate_AllowsHoustonScopedViewport()
    {
        var bbox = new GeoBoundingBox(29.68, 29.84, -95.46, -95.25);
        bbox.Validate();
    }

    [Fact]
    public void TemporalPresetMapper_UsesUtcFallbackForUnknownMarketTimezone()
    {
        var window = TemporalPresetMapper.GetTimeWindow(
            TemporalPreset.Today,
            DateTimeOffset.Parse("2026-04-15T12:00:00Z"),
            "Unknown/Timezone");

        Assert.Equal("UTC", window.Timezone);
    }

    [Fact]
    public void TemporalPresetMapper_UsesNineHourTonightWindowInChicago()
    {
        var window = TemporalPresetMapper.GetTimeWindow(
            TemporalPreset.Tonight,
            DateTimeOffset.Parse("2026-04-15T12:00:00Z"),
            "America/Chicago");

        Assert.Equal(TimeSpan.FromHours(9), window.EndUtc - window.StartUtc);
    }
}
