// ─────────────────────────────────────────────────────────────────────────────
// M8-P36: Canonical Time Window Semantics v1.0
//
// DESIGN CONTRACT
//   • The frontend NEVER computes time window bounds.
//     It sends (preset + timezone + optional custom UTC bounds) and receives
//     fully-resolved UTC bounds from the backend.
//   • String values must stay in sync with the backend TimeWindowPreset enum
//     integer assignments and GetPresetLabel() output.
//   • No browser-local timezone assumptions.  The IANA timezone string is always
//     supplied explicitly by the caller (e.g. from the user's detected locale or
//     the market default "America/Chicago").
//
// PRESET SEMANTICS (all relative to the market timezone unless noted)
//   Now          – Rolling 4-hour window from request instant  [ref, ref+4h)
//   Tonight      – Nightlife window: local 18:00 → local 03:00+1day  (9h)
//                  If current time < 03:00, uses prior evening's window.
//   Tomorrow     – Next full local calendar day 00:00 → 00:00+1  (24h)
//   ThisWeekend  – Friday local 18:00 → Monday local 00:00  (54h)
//                  Uses the containing weekend if already inside the window.
//   Next24Hours  – Rolling 24-hour window from request instant  [ref, ref+24h)
//   Next48Hours  – Rolling 48-hour window from request instant  [ref, ref+48h)
//   CustomRange  – Caller-supplied explicit UTC bounds.
//                  Requires: customStartUtc < customEndUtc; max span 30 days.
//
// VALIDATION RULES
//   • timezone must be a valid IANA string (e.g. "America/Chicago")
//   • CustomRange requires both customStartUtc and customEndUtc (ISO 8601 UTC)
//   • CustomRange span must not exceed 30 days
//   • customStartUtc must be strictly before customEndUtc
// ─────────────────────────────────────────────────────────────────────────────

/**
 * Canonical map-discovery time-window presets.
 *
 * String values are the wire format sent to the backend API.
 * They must remain aligned with the backend TimeWindowPreset enum.
 *
 * Do NOT add frontend-only entries here; all new presets require a corresponding
 * backend implementation before the frontend value may be used.
 */
export enum TimeWindowPreset {
  /** Rolling 4-hour window from the request instant. UTC-relative. */
  Now = "now",
  /** Nightlife window: local 18:00 → local 03:00+1day (9h). */
  Tonight = "tonight",
  /** Next full local calendar day: 00:00 → 00:00+1 (24h). */
  Tomorrow = "tomorrow",
  /** Friday local 18:00 → Monday local 00:00 (54h). */
  ThisWeekend = "thisWeekend",
  /** Rolling 24-hour window from the request instant. UTC-relative. */
  Next24Hours = "next24Hours",
  /** Rolling 48-hour window from the request instant. UTC-relative. */
  Next48Hours = "next48Hours",
  /**
   * Caller-supplied explicit UTC bounds.
   * Requires customStartUtc + customEndUtc in the request.
   * Validated server-side: startUtc < endUtc, max span 30 days.
   */
  CustomRange = "customRange",
  /**
   * @deprecated Use CustomRange. Kept for backward compatibility with existing
   * serialised state; the backend treats Custom and CustomRange identically.
   */
  Custom = "custom",
}

/**
 * The canonical request contract for time-window filtering.
 *
 * The frontend sends this DTO; the backend resolves it to concrete UTC bounds.
 * The frontend must never compute StartUtc/EndUtc from a preset on its own.
 *
 * INVARIANTS
 *   • preset is always present.
 *   • timezone is always present and must be a valid IANA identifier.
 *   • customStartUtc and customEndUtc are required if and only if
 *     preset === TimeWindowPreset.CustomRange (or legacy Custom).
 */
export interface TimeWindowFilterDto {
  readonly preset: TimeWindowPreset;
  /** IANA timezone string, e.g. "America/Chicago". Never empty. */
  readonly timezone: string;
  /** Required when preset=CustomRange. ISO 8601 UTC string, e.g. "2026-05-01T00:00:00Z". */
  readonly customStartUtc?: string;
  /** Required when preset=CustomRange. ISO 8601 UTC string. Must be after customStartUtc. */
  readonly customEndUtc?: string;
}

/**
 * The response shape the backend returns after resolving a TimeWindowFilterDto.
 *
 * Consumers should use startUtc/endUtc for all event filtering and display.
 * Do not re-derive these from the preset on the frontend.
 *
 * EXAMPLE (Now, ref=2026-04-26T14:30:00Z, tz=America/Chicago):
 *   startUtc            = "2026-04-26T14:30:00Z"
 *   endUtc              = "2026-04-26T18:30:00Z"
 *   timezone            = "America/Chicago"
 *   sourcePreset        = "now"
 *   presetLabel         = "Now"
 *   resolutionInstantUtc= "2026-04-26T14:30:00Z"
 *
 * EXAMPLE (Tonight, ref=2026-04-26T14:30:00Z CDT → local 09:30):
 *   startUtc            = "2026-04-26T23:00:00Z"   // 18:00 CDT
 *   endUtc              = "2026-04-27T08:00:00Z"   // 03:00 CDT next day
 *   timezone            = "America/Chicago"
 *   sourcePreset        = "tonight"
 *   presetLabel         = "Tonight"
 *   resolutionInstantUtc= "2026-04-26T14:30:00Z"
 *
 * EXAMPLE (Next48Hours, ref=2026-04-26T14:30:00Z):
 *   startUtc            = "2026-04-26T14:30:00Z"
 *   endUtc              = "2026-04-28T14:30:00Z"
 *   timezone            = "America/Chicago"
 *   sourcePreset        = "next48Hours"
 *   presetLabel         = "Next 48 Hours"
 *   resolutionInstantUtc= "2026-04-26T14:30:00Z"
 */
export interface ResolvedTimeWindowDto {
  /** Window open boundary, inclusive. ISO 8601 UTC. */
  readonly startUtc: string;
  /** Window close boundary, exclusive. ISO 8601 UTC. Always after startUtc. */
  readonly endUtc: string;
  /** IANA timezone that governed preset resolution. */
  readonly timezone: string;
  /** The preset that produced this window. */
  readonly sourcePreset: TimeWindowPreset;
  /** Human-readable label, e.g. "Tonight", "Next 24 Hours". */
  readonly presetLabel: string;
  /**
   * The UTC instant used as "now" during resolution.
   * ISO 8601 UTC. Useful for debugging and audit traces.
   */
  readonly resolutionInstantUtc: string;
}

/**
 * Local mutable state held by the map-feed filter composable.
 * This is NOT sent over the wire; it is converted to TimeWindowFilterDto before dispatch.
 *
 * customStartLocal / customEndLocal are ISO 8601 strings in the market timezone —
 * they are for local display and input only; the composable converts them to UTC
 * before constructing TimeWindowFilterDto.
 */
export interface MapFeedFilterState {
  preset: TimeWindowPreset;
  timezone: string;
  customStartLocal: string;
  customEndLocal: string;
  includeSavedOnly: boolean;
}

/** Calendar overlay reuses the same temporal contract. No additional fields needed. */
export interface CalendarOverlayTemporalQueryDto extends TimeWindowFilterDto {}
