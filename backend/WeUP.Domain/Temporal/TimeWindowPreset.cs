namespace WeUP.Domain.Temporal;

// ─────────────────────────────────────────────────────────────────────────────
// M8-P36: Canonical Time Window Semantics v1.0
//
// INVARIANTS
//   • Every relative preset is resolved against an explicit, timezone-aware
//     reference instant (never implicitly from the server clock alone).
//   • All returned UTC bounds have Kind == Utc (DateTimeOffset.ToUniversalTime).
//   • The frontend NEVER expands presets; it sends preset + timezone and the
//     backend returns fully-resolved UTC bounds.
//   • CustomRange always produces explicit UTC bounds; no local-timezone
//     expansion is applied.
//   • Window values are deterministic: given the same preset, timezone, and
//     reference instant the result is always identical.
//
// TIMEZONE CONTRACT
//   • Callers supply an IANA timezone string (e.g. "America/Chicago").
//   • The mapper also accepts common Windows identifiers ("Central Standard Time")
//     as a fallback on Windows hosts.
//   • Phase-0 default market: "America/Chicago" (Houston).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Canonical map-discovery time-window presets.
/// Integer values are stable across releases; do not renumber existing entries.
/// New entries must be added at the end to preserve wire-format compatibility.
///
/// Semantics (all times local to the supplied market timezone unless noted):
///   Now          – Rolling 4-hour window: [ref, ref+4h)  (UTC-relative, no day boundary)
///   Tonight      – Nightlife window: local 18:00 → local 03:00+1day (9h).
///                  If ref &lt; 03:00 the prior evening's window is used.
///   Tomorrow     – Next local calendar day: [midnight+1, midnight+2) (24h)
///   ThisWeekend  – Friday 18:00 → Monday 00:00 (54h).
///                  If ref is already inside the window, returns the current weekend.
///   Next24Hours  – Rolling 24-hour window: [ref, ref+24h)  (UTC-relative)
///   Next48Hours  – Rolling 48-hour window: [ref, ref+48h)  (UTC-relative)
///   CustomRange  – Caller-supplied UTC bounds.
///                  Requires: startUtc &lt; endUtc; max span 30 days.
/// </summary>
public enum TimeWindowPreset
{
    Now          = 0,
    Tonight      = 1,
    Tomorrow     = 2,
    ThisWeekend  = 3,
    Custom       = 4,  // legacy alias kept for backward compat; prefer CustomRange
    Next24Hours  = 5,
    Next48Hours  = 6,
    CustomRange  = 7,
}

/// <summary>
/// The fully-resolved output of a time-window computation.
///
/// Carries both the UTC bounds used for event filtering AND the authoritative
/// metadata that describes how those bounds were derived, so the frontend can
/// display labels and audit resolution without re-implementing any time logic.
///
/// INVARIANTS
///   • StartUtc &lt; EndUtc (enforced by TimeWindowPresetMapper; consumers may rely on this).
///   • StartUtc and EndUtc are always UTC (UtcTicks are authoritative).
///   • Timezone is the canonical IANA string used during resolution, never empty.
///   • ResolutionInstantUtc is the exact reference moment that drove preset expansion;
///     stored for tracing and reproducibility (useful in tests and audit logs).
///   • For CustomRange, SourcePreset == TimeWindowPreset.CustomRange and PresetLabel
///     is set to "Custom".
///
/// EXAMPLE (Now, ref=2026-04-26T14:30:00Z, tz=America/Chicago):
///   StartUtc            = 2026-04-26T14:30:00Z
///   EndUtc              = 2026-04-26T18:30:00Z
///   Timezone            = "America/Chicago"
///   SourcePreset        = TimeWindowPreset.Now
///   PresetLabel         = "Now"
///   ResolutionInstantUtc= 2026-04-26T14:30:00Z
///
/// EXAMPLE (Tonight, ref=2026-04-26T14:30:00Z, tz=America/Chicago — local 09:30 CDT):
///   StartUtc            = 2026-04-26T23:00:00Z  (18:00 CDT = 23:00 UTC)
///   EndUtc              = 2026-04-27T08:00:00Z  (03:00 CDT+1 = 08:00 UTC)
///   Timezone            = "America/Chicago"
///   SourcePreset        = TimeWindowPreset.Tonight
///   PresetLabel         = "Tonight"
///   ResolutionInstantUtc= 2026-04-26T14:30:00Z
///
/// EXAMPLE (CustomRange):
///   StartUtc            = caller-supplied UTC
///   EndUtc              = caller-supplied UTC
///   Timezone            = caller-supplied IANA id
///   SourcePreset        = TimeWindowPreset.CustomRange
///   PresetLabel         = "Custom"
///   ResolutionInstantUtc= moment TryResolve was called
/// </summary>
public sealed record ResolvedTimeWindow(
    /// <summary>Window open boundary, inclusive. Always UTC.</summary>
    DateTimeOffset StartUtc,
    /// <summary>Window close boundary, exclusive. Always UTC. Guaranteed > StartUtc.</summary>
    DateTimeOffset EndUtc,
    /// <summary>IANA timezone that governed preset resolution (e.g. "America/Chicago").</summary>
    string Timezone,
    /// <summary>The preset that produced this window.</summary>
    TimeWindowPreset SourcePreset,
    /// <summary>Human-readable label suitable for display (e.g. "Tonight", "Next 24 Hours").</summary>
    string PresetLabel,
    /// <summary>
    /// The UTC instant used as "now" when the preset was expanded.
    /// Stored for tracing, testing, and audit log reproducibility.
    /// </summary>
    DateTimeOffset ResolutionInstantUtc)
{
    /// <summary>Duration of the window. Convenience property; derived from EndUtc - StartUtc.</summary>
    public TimeSpan Duration => EndUtc - StartUtc;
}

/// <summary>
/// Computes concrete time windows for canonical map discovery presets.
///
/// DESIGN CONTRACT
///   • The frontend NEVER expands presets locally.  It sends (preset, timezone,
///     optional customStartUtc, optional customEndUtc) and receives resolved UTC bounds.
///   • All relative presets (Now, Tonight, …) are resolved against an explicit
///     referenceTime so results are deterministic and unit-testable.
///   • TryResolve is the primary entry point; it returns a ResolvedTimeWindow that
///     carries semantic metadata alongside the UTC bounds.
///   • TryGetTimeWindow is kept for backward compatibility with existing callers.
/// </summary>
public static class TimeWindowPresetMapper
{
    private const int NowWindowHours     = 4;
    private const int Next24WindowHours  = 24;
    private const int Next48WindowHours  = 48;
    private const int CustomRangeMaxDays = 30;

    // ── Primary API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Resolve a preset to a <see cref="ResolvedTimeWindow"/> carrying both UTC bounds
    /// and the semantic metadata used to produce them.
    ///
    /// <paramref name="referenceTime"/> is the authoritative "now"; pass
    /// <c>DateTimeOffset.UtcNow</c> for live requests and a fixed instant in tests.
    /// </summary>
    public static bool TryResolve(
        TimeWindowPreset preset,
        string timezone,
        out ResolvedTimeWindow resolved,
        out string error,
        DateTimeOffset? referenceTime  = null,
        DateTimeOffset? customStartUtc = null,
        DateTimeOffset? customEndUtc   = null)
    {
        var resolutionInstant = (referenceTime ?? DateTimeOffset.UtcNow).ToUniversalTime();
        resolved = new ResolvedTimeWindow(
            resolutionInstant, resolutionInstant, "UTC",
            preset, string.Empty, resolutionInstant);

        if (!TryGetTimeWindow(preset, timezone, out var window, out error,
                referenceTime, customStartUtc, customEndUtc))
            return false;

        resolved = new ResolvedTimeWindow(
            window.StartUtc,
            window.EndUtc,
            window.Timezone,
            preset,
            GetPresetLabel(preset),
            resolutionInstant);
        return true;
    }

    // ── Backward-compatible API ───────────────────────────────────────────────

    /// <summary>
    /// Resolve a preset to a <see cref="TimeWindow"/>.
    /// Prefer <see cref="TryResolve"/> for new code; it returns richer metadata.
    /// </summary>
    public static bool TryGetTimeWindow(
        TimeWindowPreset preset,
        string timezone,
        out TimeWindow window,
        out string error,
        DateTimeOffset? referenceTime  = null,
        DateTimeOffset? customStartUtc = null,
        DateTimeOffset? customEndUtc   = null)
    {
        error  = string.Empty;
        window = new TimeWindow(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "UTC");

        if (string.IsNullOrWhiteSpace(timezone))
        {
            error = "timezone is required and must be an IANA or Windows timezone identifier.";
            return false;
        }

        // ── CustomRange / Custom (legacy alias) ───────────────────────────────
        if (preset == TimeWindowPreset.CustomRange || preset == TimeWindowPreset.Custom)
        {
            if (!customStartUtc.HasValue || !customEndUtc.HasValue)
            {
                error = "customStartUtc and customEndUtc are required when preset=CustomRange.";
                return false;
            }

            if (customStartUtc.Value >= customEndUtc.Value)
            {
                error = "customStartUtc must be earlier than customEndUtc.";
                return false;
            }

            if ((customEndUtc.Value - customStartUtc.Value).TotalDays > CustomRangeMaxDays)
            {
                error = $"CustomRange span must not exceed {CustomRangeMaxDays} days.";
                return false;
            }

            window = new TimeWindow(
                customStartUtc.Value.ToUniversalTime(),
                customEndUtc.Value.ToUniversalTime(),
                timezone.Trim());
            return true;
        }

        if (!TryResolveTimezone(timezone, out var resolvedTimezone, out var contractTimezone))
        {
            error = $"Unsupported timezone '{timezone}'. Use a supported IANA or Windows timezone identifier.";
            return false;
        }

        var utcReference   = (referenceTime ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var localReference = TimeZoneInfo.ConvertTime(utcReference, resolvedTimezone);

        // ── UTC-relative rolling presets (no local day boundary) ─────────────
        if (preset == TimeWindowPreset.Now)
        {
            // [ref, ref+4h) — rolling, UTC-relative
            window = new TimeWindow(utcReference, utcReference.AddHours(NowWindowHours), contractTimezone);
            return true;
        }

        if (preset == TimeWindowPreset.Next24Hours)
        {
            // [ref, ref+24h) — rolling, UTC-relative
            window = new TimeWindow(utcReference, utcReference.AddHours(Next24WindowHours), contractTimezone);
            return true;
        }

        if (preset == TimeWindowPreset.Next48Hours)
        {
            // [ref, ref+48h) — rolling, UTC-relative
            window = new TimeWindow(utcReference, utcReference.AddHours(Next48WindowHours), contractTimezone);
            return true;
        }

        // ── Market-local day-boundary presets ────────────────────────────────
        DateTime localStart;
        DateTime localEnd;

        switch (preset)
        {
            case TimeWindowPreset.Tonight:
                // Cross-midnight nightlife window: local 18:00 → local 03:00+1day (9h).
                // If ref < 03:00, continues the prior evening's window to avoid a gap.
                (localStart, localEnd) = GetTonightRange(localReference);
                break;

            case TimeWindowPreset.Tomorrow:
                // Full next local calendar day: [midnight+1, midnight+2) — always 24h.
                localStart = localReference.Date.AddDays(1);
                localEnd   = localStart.AddDays(1);
                break;

            case TimeWindowPreset.ThisWeekend:
                // Friday local 18:00 → Monday local 00:00 (54h).
                // Uses the containing weekend when ref is already inside the window.
                (localStart, localEnd) = GetThisWeekendRange(localReference);
                break;

            default:
                error = $"Unsupported preset '{preset}'.";
                return false;
        }

        var startOffset = new DateTimeOffset(localStart, resolvedTimezone.GetUtcOffset(localStart));
        var endOffset   = new DateTimeOffset(localEnd,   resolvedTimezone.GetUtcOffset(localEnd));
        window = new TimeWindow(startOffset.ToUniversalTime(), endOffset.ToUniversalTime(), contractTimezone);
        return true;
    }

    // ── Label helper ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the canonical UI label for a preset.
    /// Labels are stable across releases; do not change without a frontend release.
    /// </summary>
    public static string GetPresetLabel(TimeWindowPreset preset) => preset switch
    {
        TimeWindowPreset.Now         => "Now",
        TimeWindowPreset.Tonight     => "Tonight",
        TimeWindowPreset.Tomorrow    => "Tomorrow",
        TimeWindowPreset.ThisWeekend => "This Weekend",
        TimeWindowPreset.Next24Hours => "Next 24 Hours",
        TimeWindowPreset.Next48Hours => "Next 48 Hours",
        TimeWindowPreset.Custom      => "Custom",
        TimeWindowPreset.CustomRange => "Custom",
        _ => preset.ToString(),
    };

    // ── Private helpers ──────────────────────────────────────────────────────

    private static (DateTime startLocal, DateTime endLocal) GetTonightRange(DateTimeOffset localReference)
    {
        var todayAtSixPm  = localReference.Date.AddHours(18);
        var todayAtThreeAm = localReference.Date.AddHours(3);

        // Before 03:00 → still inside the previous evening's window
        if (localReference.DateTime < todayAtThreeAm)
        {
            var priorEvening = localReference.Date.AddDays(-1).AddHours(18);
            return (priorEvening, localReference.Date.AddHours(3));
        }

        // 03:00–18:00 or ≥18:00 → tonight's window starts at 18:00
        return (todayAtSixPm, todayAtSixPm.AddHours(9));
    }

    private static (DateTime startLocal, DateTime endLocal) GetThisWeekendRange(DateTimeOffset localReference)
    {
        var daysSinceFriday          = ((int)localReference.DayOfWeek - (int)DayOfWeek.Friday + 7) % 7;
        var currentWeekFriday        = localReference.Date.AddDays(-daysSinceFriday).AddHours(18);
        var currentWeekMondayBoundary = currentWeekFriday.AddHours(54); // Fri 18:00 + 54h = Mon 00:00

        if (localReference.DateTime < currentWeekMondayBoundary)
        {
            // Before or inside the current weekend window
            return (currentWeekFriday, currentWeekMondayBoundary);
        }

        // Past Monday 00:00 → advance to next weekend
        var nextWeekFriday = currentWeekFriday.AddDays(7);
        return (nextWeekFriday, nextWeekFriday.AddHours(54));
    }

    private static bool TryResolveTimezone(
        string timezone,
        out TimeZoneInfo resolvedTimezone,
        out string contractTimezone)
    {
        resolvedTimezone = TimeZoneInfo.Utc;
        contractTimezone = timezone.Trim();

        if (string.IsNullOrWhiteSpace(contractTimezone))
            return false;

        try
        {
            resolvedTimezone = TimeZoneInfo.FindSystemTimeZoneById(contractTimezone);
            return true;
        }
        catch
        {
            // Map common IANA ids → Windows ids for Windows host compatibility
            var windowsId = contractTimezone.ToLowerInvariant() switch
            {
                "america/los_angeles" => "Pacific Standard Time",
                "america/new_york"    => "Eastern Standard Time",
                "america/chicago"     => "Central Standard Time",
                "america/denver"      => "Mountain Standard Time",
                "america/phoenix"     => "US Mountain Standard Time",
                "europe/london"       => "GMT Standard Time",
                "europe/paris"        => "Romance Standard Time",
                "asia/tokyo"          => "Tokyo Standard Time",
                _ => null,
            };

            if (windowsId is null)
                return false;

            try
            {
                resolvedTimezone = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}