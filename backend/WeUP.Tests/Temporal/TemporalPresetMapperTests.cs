using Xunit;
using WeUP.Domain.Temporal;

namespace WeUP.Tests.Temporal;

/// <summary>
/// Tests for TemporalPresetMapper — P21 Temporal Query Logic
/// </summary>
public class TemporalPresetMapperTests
{
    [Fact]
    public void GetTimeWindow_WithTodayPreset_ReturnsLocalDayWindow()
    {
        var referenceTime = DateTimeOffset.Parse("2026-04-10T16:30:00Z"); // 11:30 local CDT
        var window = TemporalPresetMapper.GetTimeWindow(TemporalPreset.Today, referenceTime, "America/Chicago");

        Assert.Equal(TimeSpan.FromDays(1), window.EndUtc - window.StartUtc);
        Assert.Equal("America/Chicago", window.Timezone);
        Assert.Equal(0, window.StartUtc.Hour);
        Assert.Equal(0, window.StartUtc.Minute);
        Assert.Equal(0, window.EndUtc.Hour);
        Assert.Equal(0, window.EndUtc.Minute);
    }

    [Fact]
    public void GetTimeWindow_WithTonightPreset_ReturnsNineHourWindow()
    {
        var referenceTime = DateTimeOffset.Parse("2026-04-10T16:30:00Z");
        var window = TemporalPresetMapper.GetTimeWindow(TemporalPreset.Tonight, referenceTime, "America/Chicago");

        Assert.Equal(TimeSpan.FromHours(9), window.EndUtc - window.StartUtc);
        window.Validate();
    }

    [Fact]
    public void GetTimeWindow_WithWeekendPreset_ReturnsFridayToMondayWindow()
    {
        var referenceTime = DateTimeOffset.Parse("2026-04-08T12:00:00Z"); // Wednesday
        var window = TemporalPresetMapper.GetTimeWindow(TemporalPreset.Weekend, referenceTime, "America/Chicago");

        Assert.Equal(TimeSpan.FromHours(54), window.EndUtc - window.StartUtc);
    }

    [Fact]
    public void GetTimeWindow_WithNext7DaysPreset_ReturnsSevenDayWindow()
    {
        var referenceTime = DateTimeOffset.Parse("2026-04-10T20:30:00Z");
        var window = TemporalPresetMapper.GetTimeWindow(TemporalPreset.Next7Days, referenceTime, "America/Chicago");

        Assert.Equal(TimeSpan.FromDays(7), window.EndUtc - window.StartUtc);
    }

    [Fact]
    public void GetPresetLabel_ReturnsLabelForAllPresets()
    {
        var presets = new[]
        {
            TemporalPreset.Today,
            TemporalPreset.Tonight,
            TemporalPreset.Weekend,
            TemporalPreset.Next7Days,
        };

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
