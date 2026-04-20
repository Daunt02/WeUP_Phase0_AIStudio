using WeUP.Domain.Temporal;
using Xunit;

namespace WeUP.Tests.Temporal;

public sealed class TimeWindowPresetMapperTests
{
    [Fact]
    public void TryGetTimeWindow_WithNowPreset_ReturnsFourHourRollingWindow()
    {
        var referenceTime = DateTimeOffset.Parse("2026-04-10T16:30:00Z");

        var ok = TimeWindowPresetMapper.TryGetTimeWindow(
            TimeWindowPreset.Now,
            "America/Chicago",
            out var window,
            out var error,
            referenceTime: referenceTime);

        Assert.True(ok, error);
        Assert.Equal(TimeSpan.FromHours(4), window.EndUtc - window.StartUtc);
        Assert.Equal(referenceTime, window.StartUtc);
        Assert.Equal("America/Chicago", window.Timezone);
    }

    [Fact]
    public void TryGetTimeWindow_WithTomorrowPreset_ReturnsNextLocalDay()
    {
        var referenceTime = DateTimeOffset.Parse("2026-04-10T16:30:00Z");

        var ok = TimeWindowPresetMapper.TryGetTimeWindow(
            TimeWindowPreset.Tomorrow,
            "America/Chicago",
            out var window,
            out var error,
            referenceTime: referenceTime);

        Assert.True(ok, error);
        Assert.Equal(TimeSpan.FromDays(1), window.EndUtc - window.StartUtc);
        Assert.Equal("America/Chicago", window.Timezone);
        Assert.Equal(DateTimeOffset.Parse("2026-04-11T05:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-04-12T05:00:00Z"), window.EndUtc);
    }

    [Fact]
    public void TryGetTimeWindow_WithThisWeekendPreset_UsesContainingWeekend()
    {
        var referenceTime = DateTimeOffset.Parse("2026-04-11T14:00:00Z");

        var ok = TimeWindowPresetMapper.TryGetTimeWindow(
            TimeWindowPreset.ThisWeekend,
            "America/Chicago",
            out var window,
            out var error,
            referenceTime: referenceTime);

        Assert.True(ok, error);
        Assert.Equal(TimeSpan.FromHours(54), window.EndUtc - window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-04-10T23:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-04-13T05:00:00Z"), window.EndUtc);
    }

    [Fact]
    public void TryGetTimeWindow_WithCustomPreset_RequiresOrderedBounds()
    {
        var ok = TimeWindowPresetMapper.TryGetTimeWindow(
            TimeWindowPreset.Custom,
            "America/Chicago",
            out _,
            out var error,
            customStartUtc: DateTimeOffset.Parse("2026-04-13T00:00:00Z"),
            customEndUtc: DateTimeOffset.Parse("2026-04-11T00:00:00Z"));

        Assert.False(ok);
        Assert.Equal("customStartUtc must be earlier than customEndUtc.", error);
    }

    [Fact]
    public void TryGetTimeWindow_WithUnsupportedTimezone_ReturnsValidationError()
    {
        var ok = TimeWindowPresetMapper.TryGetTimeWindow(
            TimeWindowPreset.Now,
            "Unknown/Timezone",
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("Unsupported timezone", error);
    }
}