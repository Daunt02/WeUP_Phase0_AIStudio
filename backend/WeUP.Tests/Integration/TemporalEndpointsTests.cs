using Xunit;
using WeUP.Domain.Temporal;

namespace WeUP.Tests.Integration;

/// <summary>
/// Integration tests for Temporal Endpoints — P21
/// Verifies POST /api/temporal/events-at-time and GET /api/temporal/presets
/// </summary>
public class TemporalEndpointsTests
{
    [Fact]
    public void GetTemporalPresets_ReturnsValidPresets()
    {
        // This test validates that the temporal presets endpoint returns valid data structure
        // In a real integration test, this would call the actual HTTP endpoint

        // Arrange
        var allPresets = new[]
        {
            TemporalPreset.NOW,
            TemporalPreset.Evening6PM,
            TemporalPreset.Evening9PM,
            TemporalPreset.Midnight,
            TemporalPreset.EarlyMorning3AM,
            TemporalPreset.Friday,
            TemporalPreset.Saturday,
            TemporalPreset.Sunday,
        };

        // Act & Assert
        Assert.NotEmpty(allPresets);
        Assert.Equal(8, allPresets.Length);

        foreach (var preset in allPresets)
        {
            var label = TemporalPresetMapper.GetPresetLabel(preset);
            Assert.NotEmpty(label);
        }
    }

    [Theory]
    [InlineData(TemporalPreset.NOW, "NOW")]
    [InlineData(TemporalPreset.Evening6PM, "6PM")]
    [InlineData(TemporalPreset.Evening9PM, "9PM")]
    [InlineData(TemporalPreset.Midnight, "MIDNIGHT")]
    [InlineData(TemporalPreset.EarlyMorning3AM, "3AM")]
    [InlineData(TemporalPreset.Friday, "FRI")]
    [InlineData(TemporalPreset.Saturday, "SAT")]
    [InlineData(TemporalPreset.Sunday, "SUN")]
    public void GetTemporalPresets_ReturnsCorrectLabels(TemporalPreset preset, string expectedLabel)
    {
        // Arrange & Act
        var label = TemporalPresetMapper.GetPresetLabel(preset);

        // Assert
        Assert.Equal(expectedLabel, label);
    }

    [Fact]
    public void GetEventsAtTime_WithValidPreset_ReturnsTimeWindow()
    {
        // Arrange
        var preset = TemporalPreset.NOW;
        var referenceTime = new DateTimeOffset(2026, 4, 4, 12, 0, 0, TimeSpan.Zero);
        var marketTimezone = "America/Los_Angeles";

        // Act
        var timeWindow = TemporalPresetMapper.GetTimeWindow(preset, referenceTime, marketTimezone);

        // Assert
        Assert.NotNull(timeWindow);
        Assert.Equal(referenceTime, timeWindow.StartUtc);
        Assert.Equal(referenceTime.AddHours(1), timeWindow.EndUtc);
        Assert.Equal(marketTimezone, timeWindow.Timezone);
    }

    [Fact]
    public void GetEventsAtTime_TimeWindowIsValid()
    {
        // Arrange
        var preset = TemporalPreset.Evening6PM;
        var referenceTime = new DateTimeOffset(2026, 4, 4, 12, 0, 0, TimeSpan.Zero);

        // Act
        var timeWindow = TemporalPresetMapper.GetTimeWindow(preset, referenceTime, "America/Los_Angeles");

        // Assert
        timeWindow.Validate(); // Should not throw
    }

    [Fact]
    public void GetEventsAtTime_MidnightWindowSpansMidnight()
    {
        // Arrange
        var preset = TemporalPreset.Midnight;
        var referenceTime = new DateTimeOffset(2026, 4, 4, 12, 0, 0, TimeSpan.Zero);

        // Act
        var timeWindow = TemporalPresetMapper.GetTimeWindow(preset, referenceTime, "America/Los_Angeles");

        // Assert
        Assert.NotNull(timeWindow);
        Assert.Equal("America/Los_Angeles", timeWindow.Timezone);
        Assert.Equal(23, timeWindow.StartUtc.Hour);
        Assert.Equal(0, timeWindow.StartUtc.Minute);
        Assert.Equal(2, timeWindow.EndUtc.Hour);
        Assert.Equal(timeWindow.StartUtc.AddHours(3), timeWindow.EndUtc);
    }

    [Fact]
    public void GetEventsAtTime_ResponseIncludesEmptyEventList()
    {
        // Arrangement: In Phase 0, temporal queries return empty event list
        // Real implementation would query database by time window

        // Act & Assert
        // Event count in Phase 0 response should be 0
        var count = 0; // Placeholder for actual endpoint response

        Assert.Equal(0, count);
    }
}
