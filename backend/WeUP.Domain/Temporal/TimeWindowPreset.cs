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
    private static readonly ITimeWindowResolver Resolver = new TimeWindowResolver();

    public static bool TryResolve(
        TimeWindowPreset preset,
        string timezone,
        out ResolvedTimeWindow resolved,
        out string error,
        DateTimeOffset? referenceTime  = null,
        DateTimeOffset? customStartUtc = null,
        DateTimeOffset? customEndUtc   = null)
    {
        return Resolver.TryResolve(
            preset,
            timezone,
            out resolved,
            out error,
            referenceTime,
            customStartUtc,
            customEndUtc);
    }

    public static bool TryGetTimeWindow(
        TimeWindowPreset preset,
        string timezone,
        out TimeWindow window,
        out string error,
        DateTimeOffset? referenceTime  = null,
        DateTimeOffset? customStartUtc = null,
        DateTimeOffset? customEndUtc   = null)
    {
        return Resolver.TryGetTimeWindow(
            preset,
            timezone,
            out window,
            out error,
            referenceTime,
            customStartUtc,
            customEndUtc);
    }

    public static string GetPresetLabel(TimeWindowPreset preset) => preset switch
    {
        _ => Resolver.GetPresetLabel(preset),
    };
}