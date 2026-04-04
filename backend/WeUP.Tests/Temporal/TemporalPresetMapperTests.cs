using Xunit;
using WeUP.Domain.Temporal;

namespace WeUP.Tests.Temporal;

/// <summary>
/// Tests for TemporalPresetMapper — P21 Temporal Query Logic
/// </summary>
public class TemporalPresetMapperTests
{
    [Fact]
    public void GetTimeWindow_WithNOWPreset_ReturnsOneHourWindow()
    {
        // Arrange
        var preset = TemporalPreset.NOW;
        var referenceTime = new DateTimeOffset(2026, 4, 4, 12, 0, 0, TimeSpan.Zero);

        // Act
        var window = TemporalPresetMapper.GetTimeWindow(preset, referenceTime, "America/Los_Angeles");

        // Assert
        Assert.NotNull(window);
        Assert.Equal(referenceTime, window.StartUtc);
        Assert.Equal(referenceTime.AddHours(1), window.EndUtc);
    }

    [Fact]
    public void GetTimeWindow_ReturnsValidTimeWindow()
    {
        // Arrange
        var preset = TemporalPreset.Evening6PM;
        var referenceTime = new DateTimeOffset(2026, 4, 4, 12, 0, 0, TimeSpan.Zero);

        // Act
        var window = TemporalPresetMapper.GetTimeWindow(preset, referenceTime, "America/Los_Angeles");

        // Assert
        Assert.NotNull(window);
        // Validate should not throw
        window.Validate();
    }

    [Fact]
    public void GetPresetLabel_WithNOWPreset_ReturnsNOW()
    {
        // Act
        var label = TemporalPresetMapper.GetPresetLabel(TemporalPreset.NOW);

        // Assert
        Assert.Equal("NOW", label);
    }

    [Fact]
    public void GetPresetLabel_WithEvening6PM_Returns6PM()
    {
        // Act
        var label = TemporalPresetMapper.GetPresetLabel(TemporalPreset.Evening6PM);

        // Assert
        Assert.Equal("6PM", label);
    }

    [Fact]
    public void GetPresetLabel_ReturnsLabelForAllPresets()
    {
        // Arrange
        var presets = new[]
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
        foreach (var preset in presets)
        {
            var label = TemporalPresetMapper.GetPresetLabel(preset);
            Assert.NotNull(label);
            Assert.NotEmpty(label);
        }
    }

    [Fact]
    public void TimeWindow_Validate_WithValidWindow_DoesNotThrow()
    {
        // Arrange
        var window = new TimeWindow(
            new DateTimeOffset(2026, 4, 4, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 4, 13, 0, 0, TimeSpan.Zero),
            "America/Los_Angeles");

        // Act & Assert
        window.Validate(); // Should not throw
    }

    [Fact]
    public void TimeWindow_Contains_WithTimeInWindow_ReturnsTrue()
    {
        // Arrange
        var startTime = new DateTimeOffset(2026, 4, 4, 12, 0, 0, TimeSpan.Zero);
        var endTime = new DateTimeOffset(2026, 4, 4, 13, 0, 0, TimeSpan.Zero);
        var window = new TimeWindow(startTime, endTime, "America/Los_Angeles");
        var timeToCheck = new DateTimeOffset(2026, 4, 4, 12, 30, 0, TimeSpan.Zero);

        // Act
        var contains = window.Contains(timeToCheck);

        // Assert
        Assert.True(contains);
    }

    [Fact]
    public void TimeWindow_Contains_WithTimeOutsideWindow_ReturnsFalse()
    {
        // Arrange
        var startTime = new DateTimeOffset(2026, 4, 4, 12, 0, 0, TimeSpan.Zero);
        var endTime = new DateTimeOffset(2026, 4, 4, 13, 0, 0, TimeSpan.Zero);
        var window = new TimeWindow(startTime, endTime, "America/Los_Angeles");
        var timeToCheck = new DateTimeOffset(2026, 4, 4, 14, 0, 0, TimeSpan.Zero);

        // Act
        var contains = window.Contains(timeToCheck);

        // Assert
        Assert.False(contains);
    }
}
