using WeUP.Domain.Temporal;
using Xunit;

namespace WeUP.Tests.Temporal;

public sealed class TimeWindowResolverTests
{
    private static readonly ITimeWindowResolver Resolver = new TimeWindowResolver();

    [Fact]
    public void TryGetTimeWindow_Tonight_Before3AmLocal_UsesPreviousEveningWindow()
    {
        // 2026-04-26T07:30:00Z -> 02:30 local America/Chicago (CDT)
        var reference = DateTimeOffset.Parse("2026-04-26T07:30:00Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.Tonight,
            "America/Chicago",
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        Assert.Equal(DateTimeOffset.Parse("2026-04-25T23:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-04-26T08:00:00Z"), window.EndUtc);
    }

    [Fact]
    public void TryGetTimeWindow_Tonight_DaytimeLocal_UsesSameDay6PmTo3Am()
    {
        // 2026-04-26T14:30:00Z -> 09:30 local America/Chicago (CDT)
        var reference = DateTimeOffset.Parse("2026-04-26T14:30:00Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.Tonight,
            "America/Chicago",
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        Assert.Equal(DateTimeOffset.Parse("2026-04-26T23:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-04-27T08:00:00Z"), window.EndUtc);
    }

    [Fact]
    public void TryGetTimeWindow_ThisWeekend_WhenInsideWeekend_UsesContainingWeekend()
    {
        // Saturday in Houston
        var reference = DateTimeOffset.Parse("2026-04-11T14:00:00Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.ThisWeekend,
            "America/Chicago",
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        Assert.Equal(DateTimeOffset.Parse("2026-04-10T23:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-04-13T05:00:00Z"), window.EndUtc);
    }

    [Fact]
    public void TryGetTimeWindow_ThisWeekend_AfterBoundary_UsesNextWeekend()
    {
        // Monday morning after the previous weekend closed
        var reference = DateTimeOffset.Parse("2026-04-13T12:00:00Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.ThisWeekend,
            "America/Chicago",
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        Assert.Equal(DateTimeOffset.Parse("2026-04-17T23:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-04-20T05:00:00Z"), window.EndUtc);
    }

    [Fact]
    public void TryGetTimeWindow_Next24Hours_UsesExactDurationAcrossDstSeason()
    {
        // Around spring DST transition season in Houston. Rolling window stays exact.
        var reference = DateTimeOffset.Parse("2026-03-08T07:30:00Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.Next24Hours,
            "America/Chicago",
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        Assert.Equal(reference, window.StartUtc);
        Assert.Equal(reference.AddHours(24), window.EndUtc);
        Assert.Equal(TimeSpan.FromHours(24), window.EndUtc - window.StartUtc);
    }

    [Fact]
    public void TryGetTimeWindow_Next48Hours_UsesExactDurationMath()
    {
        var reference = DateTimeOffset.Parse("2026-11-01T05:30:00Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.Next48Hours,
            "America/Chicago",
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        Assert.Equal(reference, window.StartUtc);
        Assert.Equal(reference.AddHours(48), window.EndUtc);
        Assert.Equal(TimeSpan.FromHours(48), window.EndUtc - window.StartUtc);
    }

    [Fact]
    public void TryGetTimeWindow_InvalidTimezone_ReturnsValidationError()
    {
        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.Tonight,
            "Invalid/TZ",
            out _ ,
            out var error,
            referenceTime: DateTimeOffset.Parse("2026-04-26T14:30:00Z"));

        Assert.False(ok);
        Assert.Contains("Unsupported timezone", error);
    }

    [Fact]
    public void ResolveTonightLocalRange_Helper_Before3Am_MapsToPreviousEvening()
    {
        var localReference = new DateTimeOffset(2026, 04, 26, 02, 00, 00, TimeSpan.FromHours(-5));

        var (startLocal, endLocal) = TimeWindowResolver.ResolveTonightLocalRange(localReference);

        Assert.Equal(new DateTime(2026, 04, 25, 18, 00, 00), startLocal);
        Assert.Equal(new DateTime(2026, 04, 26, 03, 00, 00), endLocal);
    }

    [Fact]
    public void ResolveThisWeekendLocalRange_Helper_AfterWeekend_ReturnsNextFridayStart()
    {
        var localReference = new DateTimeOffset(2026, 04, 13, 07, 00, 00, TimeSpan.FromHours(-5));

        var (startLocal, endLocal) = TimeWindowResolver.ResolveThisWeekendLocalRange(localReference);

        Assert.Equal(new DateTime(2026, 04, 17, 18, 00, 00), startLocal);
        Assert.Equal(new DateTime(2026, 04, 20, 00, 00, 00), endLocal);
    }
}
