using WeUP.Domain.Temporal;
using Xunit;

namespace WeUP.Tests.Temporal;

public sealed class TimeWindowPresetMapperTests
{
    // ── Existing presets ─────────────────────────────────────────────────────

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

    // ── M8-P36: New rolling presets ──────────────────────────────────────────

    [Fact]
    public void TryGetTimeWindow_WithNext24HoursPreset_Returns24HourRollingWindow()
    {
        // Arrange: a fixed reference so the test is deterministic
        var referenceTime = DateTimeOffset.Parse("2026-04-26T14:30:00Z");

        // Act
        var ok = TimeWindowPresetMapper.TryGetTimeWindow(
            TimeWindowPreset.Next24Hours,
            "America/Chicago",
            out var window,
            out var error,
            referenceTime: referenceTime);

        // Assert
        Assert.True(ok, error);
        Assert.Equal(TimeSpan.FromHours(24), window.EndUtc - window.StartUtc);
        Assert.Equal(referenceTime, window.StartUtc);
        Assert.Equal(referenceTime.AddHours(24), window.EndUtc);
        Assert.Equal("America/Chicago", window.Timezone);
    }

    [Fact]
    public void TryGetTimeWindow_WithNext48HoursPreset_Returns48HourRollingWindow()
    {
        var referenceTime = DateTimeOffset.Parse("2026-04-26T14:30:00Z");

        var ok = TimeWindowPresetMapper.TryGetTimeWindow(
            TimeWindowPreset.Next48Hours,
            "America/Chicago",
            out var window,
            out var error,
            referenceTime: referenceTime);

        Assert.True(ok, error);
        Assert.Equal(TimeSpan.FromHours(48), window.EndUtc - window.StartUtc);
        Assert.Equal(referenceTime, window.StartUtc);
        Assert.Equal(referenceTime.AddHours(48), window.EndUtc);
        Assert.Equal("America/Chicago", window.Timezone);
    }

    // ── M8-P36: CustomRange preset ────────────────────────────────────────────

    [Fact]
    public void TryGetTimeWindow_WithCustomRangePreset_AcceptsValidBounds()
    {
        var start = DateTimeOffset.Parse("2026-05-01T00:00:00Z");
        var end   = DateTimeOffset.Parse("2026-05-03T00:00:00Z");

        var ok = TimeWindowPresetMapper.TryGetTimeWindow(
            TimeWindowPreset.CustomRange,
            "America/Chicago",
            out var window,
            out var error,
            customStartUtc: start,
            customEndUtc: end);

        Assert.True(ok, error);
        Assert.Equal(start, window.StartUtc);
        Assert.Equal(end, window.EndUtc);
    }

    [Fact]
    public void TryGetTimeWindow_WithCustomRangePreset_RejectsSpanExceeding30Days()
    {
        var start = DateTimeOffset.Parse("2026-05-01T00:00:00Z");
        var end   = start.AddDays(31);

        var ok = TimeWindowPresetMapper.TryGetTimeWindow(
            TimeWindowPreset.CustomRange,
            "America/Chicago",
            out _,
            out var error,
            customStartUtc: start,
            customEndUtc: end);

        Assert.False(ok);
        Assert.Contains("30 days", error);
    }

    [Fact]
    public void TryGetTimeWindow_WithCustomRangePreset_RequiresBothBounds()
    {
        var ok = TimeWindowPresetMapper.TryGetTimeWindow(
            TimeWindowPreset.CustomRange,
            "America/Chicago",
            out _,
            out var error,
            customStartUtc: DateTimeOffset.UtcNow,
            customEndUtc: null);

        Assert.False(ok);
        Assert.Contains("required", error);
    }

    [Fact]
    public void TryGetTimeWindow_WithEmptyTimezone_ReturnsValidationError()
    {
        var ok = TimeWindowPresetMapper.TryGetTimeWindow(
            TimeWindowPreset.Now,
            "   ",
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("timezone is required", error);
    }

    // ── M8-P36: TryResolve returns ResolvedTimeWindow ─────────────────────────

    [Fact]
    public void TryResolve_WithNext24HoursPreset_ReturnsResolvedTimeWindow()
    {
        var referenceTime = DateTimeOffset.Parse("2026-04-26T14:30:00Z");

        var ok = TimeWindowPresetMapper.TryResolve(
            TimeWindowPreset.Next24Hours,
            "America/Chicago",
            out var resolved,
            out var error,
            referenceTime: referenceTime);

        Assert.True(ok, error);
        Assert.Equal(TimeSpan.FromHours(24), resolved.Duration);
        Assert.Equal(referenceTime, resolved.StartUtc);
        Assert.Equal(TimeWindowPreset.Next24Hours, resolved.SourcePreset);
        Assert.Equal("Next 24 Hours", resolved.PresetLabel);
        Assert.Equal(referenceTime, resolved.ResolutionInstantUtc);
        Assert.Equal("America/Chicago", resolved.Timezone);
    }

    [Fact]
    public void TryResolve_WithNext48HoursPreset_ReturnsResolvedTimeWindow()
    {
        var referenceTime = DateTimeOffset.Parse("2026-04-26T14:30:00Z");

        var ok = TimeWindowPresetMapper.TryResolve(
            TimeWindowPreset.Next48Hours,
            "America/Chicago",
            out var resolved,
            out var error,
            referenceTime: referenceTime);

        Assert.True(ok, error);
        Assert.Equal(TimeSpan.FromHours(48), resolved.Duration);
        Assert.Equal(TimeWindowPreset.Next48Hours, resolved.SourcePreset);
        Assert.Equal("Next 48 Hours", resolved.PresetLabel);
    }

    [Fact]
    public void TryResolve_WithNowPreset_RecordsResolutionInstant()
    {
        var referenceTime = DateTimeOffset.Parse("2026-04-26T10:00:00Z");

        var ok = TimeWindowPresetMapper.TryResolve(
            TimeWindowPreset.Now,
            "America/Chicago",
            out var resolved,
            out _,
            referenceTime: referenceTime);

        Assert.True(ok);
        Assert.Equal(referenceTime, resolved.ResolutionInstantUtc);
        Assert.Equal("Now", resolved.PresetLabel);
    }

    [Fact]
    public void TryResolve_WithCustomRangePreset_ReturnsExplicitBounds()
    {
        var start = DateTimeOffset.Parse("2026-05-10T06:00:00Z");
        var end   = DateTimeOffset.Parse("2026-05-12T06:00:00Z");

        var ok = TimeWindowPresetMapper.TryResolve(
            TimeWindowPreset.CustomRange,
            "America/Chicago",
            out var resolved,
            out var error,
            customStartUtc: start,
            customEndUtc: end);

        Assert.True(ok, error);
        Assert.Equal(start, resolved.StartUtc);
        Assert.Equal(end, resolved.EndUtc);
        Assert.Equal(TimeWindowPreset.CustomRange, resolved.SourcePreset);
        Assert.Equal("Custom", resolved.PresetLabel);
    }

    // ── GetPresetLabel coverage ───────────────────────────────────────────────

    [Theory]
    [InlineData(TimeWindowPreset.Now,         "Now")]
    [InlineData(TimeWindowPreset.Tonight,     "Tonight")]
    [InlineData(TimeWindowPreset.Tomorrow,    "Tomorrow")]
    [InlineData(TimeWindowPreset.ThisWeekend, "This Weekend")]
    [InlineData(TimeWindowPreset.Next24Hours, "Next 24 Hours")]
    [InlineData(TimeWindowPreset.Next48Hours, "Next 48 Hours")]
    [InlineData(TimeWindowPreset.CustomRange, "Custom")]
    [InlineData(TimeWindowPreset.Custom,      "Custom")]
    public void GetPresetLabel_ReturnsExpectedLabel(TimeWindowPreset preset, string expectedLabel)
    {
        Assert.Equal(expectedLabel, TimeWindowPresetMapper.GetPresetLabel(preset));
    }
}
