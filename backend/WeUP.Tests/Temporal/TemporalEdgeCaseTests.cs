using WeUP.Domain.Temporal;
using Xunit;

namespace WeUP.Tests.Temporal;

// ─────────────────────────────────────────────────────────────────────────────
// M8-P40: Temporal Edge-Case and Boundary Condition Tests
//
// EDGE-CASE TABLE
// ┌──────────────────────────────────────────────────────┬──────────┐
// │ Scenario                                             │ Expected │
// ├──────────────────────────────────────────────────────┼──────────┤
// │ Tonight ref exactly at 03:00 local                   │ PASS – same-day window  │
// │ Tonight ref at 02:59:59 local                        │ PASS – prior-evening window │
// │ Tonight ref at midnight (00:00)                      │ PASS – prior-evening window │
// │ Tonight DST spring-forward (prior evening)           │ PASS – 8 UTC h window   │
// │ Tonight DST spring-forward (same-day evening)        │ PASS – 9 UTC h window   │
// │ Tonight DST fall-back (same-day evening)             │ PASS – 10 UTC h window  │
// │ Today DST spring-forward day                         │ PASS – 23 UTC h window  │
// │ Today DST fall-back day                              │ PASS – 25 UTC h window  │
// │ Weekend Fri exactly at 18:00                         │ PASS – start of window  │
// │ Weekend Fri at 17:59:59                              │ PASS – same weekend     │
// │ Weekend Mon exactly at 00:00                         │ PASS – NEXT weekend     │
// │ Weekend Saturday daytime                             │ PASS – current weekend  │
// │ Weekend Sunday daytime                               │ PASS – current weekend  │
// │ Event at exactly startUtc                            │ INCLUDED                │
// │ Event at exactly endUtc                              │ EXCLUDED                │
// │ Event one tick before startUtc                       │ EXCLUDED                │
// │ Event one tick after endUtc                          │ EXCLUDED                │
// │ Invalid local (spring-forward gap) → ResolveToUtc    │ PASS – advanced to valid│
// │ Ambiguous local (fall-back) → ResolveToUtc           │ PASS – earliest UTC     │
// │ ValidateRequest: blank timezone                      │ FAIL                    │
// │ ValidateRequest: unsupported timezone                │ FAIL                    │
// │ ValidateRequest: CustomRange end before start        │ FAIL                    │
// │ ValidateRequest: CustomRange start == end            │ FAIL                    │
// │ ValidateRequest: CustomRange missing start           │ FAIL                    │
// │ ValidateRequest: CustomRange missing end             │ FAIL                    │
// │ ValidateRequest: CustomRange span > 30 days          │ FAIL                    │
// │ ValidateRequest: non-Custom with bounds              │ FAIL                    │
// │ ValidateRequest: valid Tonight request               │ PASS                    │
// │ ValidateRequest: valid CustomRange request           │ PASS                    │
// └──────────────────────────────────────────────────────┴──────────┘
//
// DST REFERENCE DATES (America/Chicago)
//   Spring forward: 2026-03-08 02:00 CST → 03:00 CDT  (transition at 08:00Z)
//   Fall back:      2026-11-01 02:00 CDT → 01:00 CST  (transition at 07:00Z)
// ─────────────────────────────────────────────────────────────────────────────

public sealed class TemporalEdgeCaseTests
{
    private static readonly ITimeWindowResolver Resolver = new TimeWindowResolver();
    private const string ChicagoTz = "America/Chicago";

    // ── Section 1: Validation edge cases ─────────────────────────────────────

    [Fact]
    public void ValidateRequest_BlankTimezone_Fails()
    {
        var result = TemporalRangeValidator.ValidateRequest("  ", TimeWindowPreset.Tonight);

        Assert.False(result.IsValid);
        Assert.Contains("timezone is required", result.Error);
    }

    [Fact]
    public void ValidateRequest_NullTimezone_Fails()
    {
        var result = TemporalRangeValidator.ValidateRequest(null, TimeWindowPreset.Tonight);

        Assert.False(result.IsValid);
        Assert.Contains("timezone is required", result.Error);
    }

    [Fact]
    public void ValidateRequest_UnsupportedTimezone_Fails()
    {
        var result = TemporalRangeValidator.ValidateRequest("Fake/Zone", TimeWindowPreset.Tonight);

        Assert.False(result.IsValid);
        Assert.Contains("Unsupported timezone", result.Error);
        Assert.Contains("Fake/Zone", result.Error);
    }

    [Fact]
    public void ValidateCustomRange_EndBeforeStart_Fails()
    {
        // RULE 5: end before start → rejected explicitly, not self-corrected
        var start = DateTimeOffset.Parse("2026-05-10T12:00:00Z");
        var end   = DateTimeOffset.Parse("2026-05-08T12:00:00Z");

        var result = TemporalRangeValidator.ValidateCustomRange(start, end);

        Assert.False(result.IsValid);
        Assert.Equal("customStartUtc must be earlier than customEndUtc.", result.Error);
    }

    [Fact]
    public void ValidateCustomRange_StartEqualsEnd_Fails()
    {
        // Zero-length range is meaningless; must fail explicitly.
        var instant = DateTimeOffset.Parse("2026-05-10T12:00:00Z");

        var result = TemporalRangeValidator.ValidateCustomRange(instant, instant);

        Assert.False(result.IsValid);
        Assert.Equal("customStartUtc must be earlier than customEndUtc.", result.Error);
    }

    [Fact]
    public void ValidateCustomRange_MissingStart_Fails()
    {
        var result = TemporalRangeValidator.ValidateCustomRange(null, DateTimeOffset.UtcNow);

        Assert.False(result.IsValid);
        Assert.Contains("required", result.Error);
    }

    [Fact]
    public void ValidateCustomRange_MissingEnd_Fails()
    {
        var result = TemporalRangeValidator.ValidateCustomRange(DateTimeOffset.UtcNow, null);

        Assert.False(result.IsValid);
        Assert.Contains("required", result.Error);
    }

    [Fact]
    public void ValidateCustomRange_MissingBoth_Fails()
    {
        var result = TemporalRangeValidator.ValidateCustomRange(null, null);

        Assert.False(result.IsValid);
        Assert.Contains("required", result.Error);
    }

    [Fact]
    public void ValidateCustomRange_SpanExceeds30Days_Fails()
    {
        var start = DateTimeOffset.Parse("2026-05-01T00:00:00Z");
        var end   = start.AddDays(31);

        var result = TemporalRangeValidator.ValidateCustomRange(start, end);

        Assert.False(result.IsValid);
        Assert.Contains("30 days", result.Error);
    }

    [Fact]
    public void ValidatePresetConsistency_NonCustomPresetWithBounds_Fails()
    {
        // RULE 7: preset=Tonight with custom bounds → ambiguous intent, rejected
        var result = TemporalRangeValidator.ValidatePresetConsistency(
            TimeWindowPreset.Tonight,
            DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-05-02T00:00:00Z"));

        Assert.False(result.IsValid);
        Assert.Contains("Custom bounds must not be supplied", result.Error);
        Assert.Contains("Tonight", result.Error);
    }

    [Fact]
    public void ValidatePresetConsistency_NonCustomPresetWithOnlyStartBound_Fails()
    {
        // Partial custom bounds alongside a non-Custom preset are also rejected.
        var result = TemporalRangeValidator.ValidatePresetConsistency(
            TimeWindowPreset.Tomorrow,
            DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            null);

        Assert.False(result.IsValid);
        Assert.Contains("Custom bounds must not be supplied", result.Error);
    }

    [Fact]
    public void ValidateRequest_ValidTonightRequest_Passes()
    {
        var result = TemporalRangeValidator.ValidateRequest(ChicagoTz, TimeWindowPreset.Tonight);

        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ValidateRequest_ValidCustomRange_Passes()
    {
        var start = DateTimeOffset.Parse("2026-06-01T00:00:00Z");
        var end   = start.AddDays(2);

        var result = TemporalRangeValidator.ValidateRequest(ChicagoTz, TimeWindowPreset.CustomRange, start, end);

        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    // ── Section 2: Tonight boundary semantics ────────────────────────────────

    [Fact]
    public void Tonight_ExactlyAt0300Local_UsesSameDayEveningWindow()
    {
        // Inclusion rule: the 03:00 boundary is exclusive — at exactly 03:00 the prior
        // evening window has closed and the resolver returns tonight's upcoming window.
        //
        // ref: 2026-04-26T08:00:00Z = 03:00:00 AM CDT (UTC-5)
        //   startLocal = 2026-04-26 18:00, endLocal = 2026-04-27 03:00
        var reference = DateTimeOffset.Parse("2026-04-26T08:00:00Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.Tonight,
            ChicagoTz,
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        // Must NOT be the prior evening (Apr 25 23:00Z → Apr 26 08:00Z)
        Assert.NotEqual(DateTimeOffset.Parse("2026-04-25T23:00:00Z"), window.StartUtc);
        // Must be the same-day window
        Assert.Equal(DateTimeOffset.Parse("2026-04-26T23:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-04-27T08:00:00Z"), window.EndUtc);
    }

    [Fact]
    public void Tonight_At025959Local_UsesPriorEveningWindow()
    {
        // ref: 2026-04-26T07:59:59Z = 02:59:59 AM CDT — inside the late-night window
        // Must return the prior evening: Apr 25 18:00 CDT → Apr 26 03:00 CDT
        var reference = DateTimeOffset.Parse("2026-04-26T07:59:59Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.Tonight,
            ChicagoTz,
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        Assert.Equal(DateTimeOffset.Parse("2026-04-25T23:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-04-26T08:00:00Z"), window.EndUtc);
    }

    [Fact]
    public void Tonight_AtMidnightLocal_UsesPriorEveningWindow()
    {
        // ref: 2026-04-26T05:00:00Z = 00:00:00 AM CDT — deep in late-night window
        var reference = DateTimeOffset.Parse("2026-04-26T05:00:00Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.Tonight,
            ChicagoTz,
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        Assert.Equal(DateTimeOffset.Parse("2026-04-25T23:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-04-26T08:00:00Z"), window.EndUtc);
    }

    [Fact]
    public void Tonight_AtLateNight0200Local_UsesPriorEveningWindow()
    {
        // ref: 2026-04-26T07:00:00Z = 02:00:00 AM CDT — still before 03:00 cutoff
        var reference = DateTimeOffset.Parse("2026-04-26T07:00:00Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.Tonight,
            ChicagoTz,
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        Assert.Equal(DateTimeOffset.Parse("2026-04-25T23:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-04-26T08:00:00Z"), window.EndUtc);
    }

    // ── Section 3: DST – Spring forward (2026-03-08, America/Chicago) ─────────
    //
    // 2:00 AM CST → 3:00 AM CDT (clocks spring forward; 02:xx is invalid/gap)
    // UTC transition: 2026-03-08T08:00:00Z

    [Fact]
    public void Tonight_DstSpringForward_PriorEveningCrossingGap_Is8UtcHours()
    {
        // Nominal Tonight span is 9 hours (18:00 → 03:00 local).
        // When the prior evening's end boundary (Mar 8 03:00) falls on the spring-forward
        // date, the CDT offset is UTC-5 so the UTC span is only 8 hours.
        //
        // ref: 2026-03-08T07:30:00Z = 01:30 AM CST (UTC-6, before transition at 08:00Z)
        //   Prior evening: Mar 7 18:00 CST → Mar 8 03:00 CDT
        //   Start: 2026-03-08T00:00:00Z  (18:00 CST, UTC-6)
        //   End:   2026-03-08T08:00:00Z  (03:00 CDT, UTC-5)
        //   Duration: 8 UTC hours
        var reference = DateTimeOffset.Parse("2026-03-08T07:30:00Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.Tonight,
            ChicagoTz,
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        Assert.Equal(DateTimeOffset.Parse("2026-03-08T00:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-03-08T08:00:00Z"), window.EndUtc);
        // Duration is 8 UTC hours, not 9, due to spring-forward shortening the local night.
        Assert.Equal(TimeSpan.FromHours(8), window.EndUtc - window.StartUtc);
    }

    [Fact]
    public void Tonight_DstSpringForward_SameDayEvening_Is9UtcHours()
    {
        // The same-day evening window (18:00 Mar 8 CDT → 03:00 Mar 9 CDT) does not
        // cross a DST transition, so the duration remains the nominal 9 UTC hours.
        //
        // ref: 2026-03-08T18:00:00Z = 1:00 PM CDT (UTC-5, after transition at 08:00Z)
        //   Start: 2026-03-08T23:00:00Z  (18:00 CDT)
        //   End:   2026-03-09T08:00:00Z  (03:00 CDT)
        //   Duration: 9 UTC hours
        var reference = DateTimeOffset.Parse("2026-03-08T18:00:00Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.Tonight,
            ChicagoTz,
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        Assert.Equal(DateTimeOffset.Parse("2026-03-08T23:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-03-09T08:00:00Z"), window.EndUtc);
        Assert.Equal(TimeSpan.FromHours(9), window.EndUtc - window.StartUtc);
    }

    [Fact]
    public void Today_DstSpringForwardDay_Is23UtcHours()
    {
        // The spring-forward date (Mar 8 2026) is only 23 UTC hours because clocks
        // spring from CST (UTC-6) midnight to CDT (UTC-5) midnight.
        //
        // Start: Mar 8 00:00 CST (UTC-6) = 2026-03-08T06:00:00Z
        // End:   Mar 9 00:00 CDT (UTC-5) = 2026-03-09T05:00:00Z
        // Duration: 23 UTC hours
        var reference = DateTimeOffset.Parse("2026-03-08T14:00:00Z"); // 09:00 CDT

        var window = TemporalPresetMapper.GetTimeWindow(
            TemporalPreset.Today,
            reference,
            ChicagoTz);

        var utcDuration = window.EndUtc.ToUniversalTime() - window.StartUtc.ToUniversalTime();
        Assert.Equal(TimeSpan.FromHours(23), utcDuration);
    }

    [Fact]
    public void ResolveLocalBoundaryToUtc_InvalidSpringForwardTime_AdvancesToNextValidInstant()
    {
        // Local time 02:30 on 2026-03-08 does not exist (spring-forward gap 02:00–02:59).
        // The resolver must advance to the next valid instant (03:00 CDT = 08:00Z).
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        var invalidLocal = new DateTime(2026, 3, 8, 2, 30, 0, DateTimeKind.Unspecified);

        Assert.True(tz.IsInvalidTime(invalidLocal), "Pre-condition: 02:30 is invalid on spring-forward day.");

        var resolved = TimeWindowResolver.ResolveLocalBoundaryToUtc(invalidLocal, tz);

        // Must resolve to 03:00 CDT (UTC-5) = 2026-03-08T08:00:00Z
        Assert.Equal(DateTimeOffset.Parse("2026-03-08T08:00:00Z"), resolved);
    }

    // ── Section 4: DST – Fall back (2026-11-01, America/Chicago) ─────────────
    //
    // 2:00 AM CDT → 1:00 AM CST (clocks fall back; 01:xx is ambiguous/repeated)
    // UTC transition: 2026-11-01T07:00:00Z

    [Fact]
    public void Tonight_DstFallBack_SameDayEveningCrossingFallBack_Is10UtcHours()
    {
        // Nominal Tonight span is 9 hours (18:00 → 03:00 local).
        // When the evening crosses the fall-back transition the end boundary 03:00 CST
        // is UTC-6, giving 10 UTC hours total.
        //
        // ref: 2026-10-31T14:30:00Z = 09:30 AM CDT (UTC-5, day before fall back)
        //   Tonight: Oct 31 18:00 CDT → Nov 1 03:00 CST
        //   Start: 2026-10-31T23:00:00Z  (18:00 CDT, UTC-5)
        //   End:   2026-11-01T09:00:00Z  (03:00 CST, UTC-6)
        //   Duration: 10 UTC hours
        var reference = DateTimeOffset.Parse("2026-10-31T14:30:00Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.Tonight,
            ChicagoTz,
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        Assert.Equal(DateTimeOffset.Parse("2026-10-31T23:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-11-01T09:00:00Z"), window.EndUtc);
        Assert.Equal(TimeSpan.FromHours(10), window.EndUtc - window.StartUtc);
    }

    [Fact]
    public void Today_DstFallBackDay_Is25UtcHours()
    {
        // The fall-back date (Nov 1 2026) is 25 UTC hours because clocks fall from
        // CDT (UTC-5) midnight to CST (UTC-6) midnight.
        //
        // Start: Nov 1 00:00 CDT (UTC-5) = 2026-11-01T05:00:00Z
        // End:   Nov 2 00:00 CST (UTC-6) = 2026-11-02T06:00:00Z
        // Duration: 25 UTC hours
        var reference = DateTimeOffset.Parse("2026-11-01T14:00:00Z"); // 08:00 AM CST

        var window = TemporalPresetMapper.GetTimeWindow(
            TemporalPreset.Today,
            reference,
            ChicagoTz);

        var utcDuration = window.EndUtc.ToUniversalTime() - window.StartUtc.ToUniversalTime();
        Assert.Equal(TimeSpan.FromHours(25), utcDuration);
    }

    [Fact]
    public void ResolveLocalBoundaryToUtc_AmbiguousFallBackTime_ResolvesToEarliestUtcInstant()
    {
        // Local time 01:30 on 2026-11-01 occurs twice (CDT then CST).
        // Policy: choose earliest UTC instant → CDT occurrence (UTC-5) = 06:30Z.
        // Implementation: offsets.Max() = TimeSpan(-5,0,0) = CDT.
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        var ambiguousLocal = new DateTime(2026, 11, 1, 1, 30, 0, DateTimeKind.Unspecified);

        Assert.True(tz.IsAmbiguousTime(ambiguousLocal), "Pre-condition: 01:30 is ambiguous on fall-back day.");

        var resolved = TimeWindowResolver.ResolveLocalBoundaryToUtc(ambiguousLocal, tz);

        // CDT occurrence: 01:30 - (-5) = 06:30Z
        Assert.Equal(DateTimeOffset.Parse("2026-11-01T06:30:00Z"), resolved);
    }

    [Fact]
    public void Tonight_FallBack_LateNightRef_UsesPriorEveningAndEndsAt03CstUtc9()
    {
        // ref: 2026-11-01T08:30:00Z = 02:30 AM CST (after fall-back; before 03:00 cutoff)
        // Prior evening: Oct 31 18:00 CDT → Nov 1 03:00 CST → 10h UTC window
        var reference = DateTimeOffset.Parse("2026-11-01T08:30:00Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.Tonight,
            ChicagoTz,
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        Assert.Equal(DateTimeOffset.Parse("2026-10-31T23:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-11-01T09:00:00Z"), window.EndUtc);
        Assert.Equal(TimeSpan.FromHours(10), window.EndUtc - window.StartUtc);
    }

    // ── Section 5: Weekend boundary conditions ────────────────────────────────

    [Fact]
    public void ThisWeekend_FridayExactlyAt1800_StartsImmediately()
    {
        // Inclusion rule: 18:00 Friday is the inclusive start of the weekend window.
        // ref: 2026-04-10T23:00:00Z = 18:00:00 CDT (exactly at start)
        var reference = DateTimeOffset.Parse("2026-04-10T23:00:00Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.ThisWeekend,
            ChicagoTz,
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        // Must return the current weekend (not next), starting at the reference instant.
        Assert.Equal(DateTimeOffset.Parse("2026-04-10T23:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-04-13T05:00:00Z"), window.EndUtc);
    }

    [Fact]
    public void ThisWeekend_FridayBefore1800_ReturnsUpcomingFridayWindow()
    {
        // Before 18:00 Friday, the upcoming weekend is returned (start is 1 min in future).
        // ref: 2026-04-10T22:59:59Z = 17:59:59 CDT (1 second before window start)
        var reference = DateTimeOffset.Parse("2026-04-10T22:59:59Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.ThisWeekend,
            ChicagoTz,
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        // Window starts at 18:00 Friday (1 second after reference).
        Assert.Equal(DateTimeOffset.Parse("2026-04-10T23:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-04-13T05:00:00Z"), window.EndUtc);
    }

    [Fact]
    public void ThisWeekend_SaturdayMidday_ReturnsCurrentWeekend()
    {
        // ref: 2026-04-11T16:00:00Z = 11:00 AM CDT Saturday — inside the weekend window
        var reference = DateTimeOffset.Parse("2026-04-11T16:00:00Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.ThisWeekend,
            ChicagoTz,
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        Assert.Equal(DateTimeOffset.Parse("2026-04-10T23:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-04-13T05:00:00Z"), window.EndUtc);
    }

    [Fact]
    public void ThisWeekend_SundayEvening_ReturnsCurrentWeekend()
    {
        // ref: 2026-04-12T22:00:00Z = 17:00 CDT Sunday — still inside the weekend
        var reference = DateTimeOffset.Parse("2026-04-12T22:00:00Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.ThisWeekend,
            ChicagoTz,
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        Assert.Equal(DateTimeOffset.Parse("2026-04-10T23:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-04-13T05:00:00Z"), window.EndUtc);
    }

    [Fact]
    public void ThisWeekend_MondayExactlyAt0000_ReturnsNextWeekend()
    {
        // Exclusion rule: the Monday 00:00 boundary is exclusive.
        // At exactly Monday 00:00 the prior weekend window is closed.
        // ref: 2026-04-13T05:00:00Z = 00:00:00 CDT Monday (exactly at end boundary)
        var reference = DateTimeOffset.Parse("2026-04-13T05:00:00Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.ThisWeekend,
            ChicagoTz,
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        // Must be the NEXT weekend: Apr 17 18:00 CDT → Apr 20 00:00 CDT
        Assert.Equal(DateTimeOffset.Parse("2026-04-17T23:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-04-20T05:00:00Z"), window.EndUtc);
    }

    [Fact]
    public void ThisWeekend_MondayMorning_ReturnsNextWeekend()
    {
        // ref: 2026-04-13T12:00:00Z = 07:00 AM CDT Monday — well past weekend end
        var reference = DateTimeOffset.Parse("2026-04-13T12:00:00Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.ThisWeekend,
            ChicagoTz,
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        Assert.Equal(DateTimeOffset.Parse("2026-04-17T23:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-04-20T05:00:00Z"), window.EndUtc);
    }

    // ── Section 6: Event boundary inclusion/exclusion rules ───────────────────
    //
    // TimeWindow.Contains(t): INCLUSIVE start, EXCLUSIVE end → [startUtc, endUtc)

    private static readonly TimeWindow TestWindow = new(
        DateTimeOffset.Parse("2026-04-26T20:00:00Z"),  // start = 20:00 UTC
        DateTimeOffset.Parse("2026-04-27T04:00:00Z"),  // end   = 04:00 UTC
        ChicagoTz);

    [Fact]
    public void Contains_EventAtExactlyStartUtc_IsIncluded()
    {
        // Inclusive start: event at the exact opening instant must be included.
        Assert.True(TestWindow.Contains(DateTimeOffset.Parse("2026-04-26T20:00:00Z")));
    }

    [Fact]
    public void Contains_EventOneTickAfterStartUtc_IsIncluded()
    {
        Assert.True(TestWindow.Contains(DateTimeOffset.Parse("2026-04-26T20:00:00.001Z")));
    }

    [Fact]
    public void Contains_EventOneTickBeforeEndUtc_IsIncluded()
    {
        // One millisecond before the exclusive boundary must still be included.
        Assert.True(TestWindow.Contains(DateTimeOffset.Parse("2026-04-27T03:59:59.999Z")));
    }

    [Fact]
    public void Contains_EventAtExactlyEndUtc_IsExcluded()
    {
        // Exclusive end: event exactly at the closing instant must be excluded.
        Assert.False(TestWindow.Contains(DateTimeOffset.Parse("2026-04-27T04:00:00Z")));
    }

    [Fact]
    public void Contains_EventOneTickAfterEndUtc_IsExcluded()
    {
        Assert.False(TestWindow.Contains(DateTimeOffset.Parse("2026-04-27T04:00:00.001Z")));
    }

    [Fact]
    public void Contains_EventOneTickBeforeStartUtc_IsExcluded()
    {
        Assert.False(TestWindow.Contains(DateTimeOffset.Parse("2026-04-26T19:59:59.999Z")));
    }

    [Fact]
    public void Contains_CrossDayEvent_StartsInsideWindow_IsIncluded()
    {
        // A cross-midnight event that begins within the window is included
        // (inclusion is determined by the event's start instant).
        var crossMidnightEventStart = DateTimeOffset.Parse("2026-04-26T23:30:00Z");
        Assert.True(TestWindow.Contains(crossMidnightEventStart));
    }

    [Fact]
    public void Contains_CrossDayEvent_StartsOutsideWindow_IsExcluded()
    {
        // Cross-midnight event whose start is outside the window is excluded,
        // even if its end time falls within the window.
        var priorNightEventStart = DateTimeOffset.Parse("2026-04-26T19:00:00Z");
        Assert.False(TestWindow.Contains(priorNightEventStart));
    }

    // ── Section 7: Rolling presets stay exact across DST boundaries ───────────

    [Fact]
    public void Next24Hours_AcrossSpringForward_RemainsExact24UtcHours()
    {
        // Rolling windows use pure UTC duration math; DST is irrelevant.
        // Spring-forward date: 2026-03-08T07:00:00Z is the transition.
        var reference = DateTimeOffset.Parse("2026-03-08T06:00:00Z"); // 1h before transition

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.Next24Hours,
            ChicagoTz,
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        Assert.Equal(TimeSpan.FromHours(24), window.EndUtc - window.StartUtc);
        Assert.Equal(reference, window.StartUtc);
        Assert.Equal(reference.AddHours(24), window.EndUtc);
    }

    [Fact]
    public void Next48Hours_AcrossFallBack_RemainsExact48UtcHours()
    {
        // Fall-back date: 2026-11-01T07:00:00Z is the transition.
        var reference = DateTimeOffset.Parse("2026-11-01T05:00:00Z"); // 2h before transition

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.Next48Hours,
            ChicagoTz,
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        Assert.Equal(TimeSpan.FromHours(48), window.EndUtc - window.StartUtc);
        Assert.Equal(reference, window.StartUtc);
        Assert.Equal(reference.AddHours(48), window.EndUtc);
    }

    // ── Section 8: Tomorrow boundary ─────────────────────────────────────────

    [Fact]
    public void Tomorrow_AtMidnight_ReturnsNextLocalDay()
    {
        // ref: 2026-04-26T05:00:00Z = 00:00 CDT midnight
        // Tomorrow = Apr 27 00:00 CDT → Apr 28 00:00 CDT
        var reference = DateTimeOffset.Parse("2026-04-26T05:00:00Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.Tomorrow,
            ChicagoTz,
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        Assert.Equal(DateTimeOffset.Parse("2026-04-27T05:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-04-28T05:00:00Z"), window.EndUtc);
        Assert.Equal(TimeSpan.FromDays(1), window.EndUtc - window.StartUtc);
    }

    [Fact]
    public void Tomorrow_LateEvening_ReturnsNextLocalDay()
    {
        // ref: 2026-04-27T04:59:59Z = 23:59:59 CDT on Apr 26 (one second before midnight)
        // CDT = UTC-5: 2026-04-27T04:59:59Z = 2026-04-26T23:59:59 CDT
        // Tomorrow from Apr 26 = Apr 27 00:00 CDT → Apr 28 00:00 CDT
        var reference = DateTimeOffset.Parse("2026-04-27T04:59:59Z");

        var ok = Resolver.TryGetTimeWindow(
            TimeWindowPreset.Tomorrow,
            ChicagoTz,
            out var window,
            out var error,
            referenceTime: reference);

        Assert.True(ok, error);
        Assert.Equal(DateTimeOffset.Parse("2026-04-27T05:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-04-28T05:00:00Z"), window.EndUtc);
    }
}
