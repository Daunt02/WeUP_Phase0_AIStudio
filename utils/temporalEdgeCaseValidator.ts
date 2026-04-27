/**
 * M8-P40: Temporal Edge-Case Validator
 *
 * Frontend contract-awareness helpers that mirror the backend's validation rules.
 * These validators exist to give the frontend early, deterministic feedback before
 * a request reaches the backend.
 *
 * The backend remains the authoritative resolver.  These functions MUST NOT
 * expand presets or compute UTC windows; they only validate the *shape* of the
 * request.
 *
 * VALIDATION RULES (mirrored from TemporalRangeValidator.cs)
 *   1. Timezone must be non-blank.
 *   2. Timezone must look like a valid IANA identifier (e.g. "Region/City").
 *   3. CustomRange requires both customStartUtc and customEndUtc.
 *   4. CustomRange: start must be strictly before end.
 *   5. CustomRange: span must not exceed 30 days.
 *   6. Non-Custom preset with custom bounds → ambiguous intent, rejected.
 *
 * BOUNDARY SEMANTICS
 *   • startUtc is INCLUSIVE — [startUtc, endUtc)
 *   • endUtc is EXCLUSIVE
 *   • Midnight rollover is fully handled server-side; the client never derives
 *     UTC bounds from a preset itself.
 *
 * DST NOTES (informational; not enforced on the frontend)
 *   • Tonight crossing spring-forward: 8 UTC hours instead of 9.
 *   • Tonight crossing fall-back:      10 UTC hours instead of 9.
 *   • Today on spring-forward date:    23 UTC hours.
 *   • Today on fall-back date:         25 UTC hours.
 *   These are backend-resolved; the frontend should display the backend-returned
 *   startUtc / endUtc without re-computing them.
 */

// ─────────────────────────────────────────────────────────────────────────────
// Types
// ─────────────────────────────────────────────────────────────────────────────

export interface TemporalValidationResult {
  readonly valid: boolean;
  readonly errors: string[];
}

/** Presets that require explicit custom UTC bounds. */
const CUSTOM_PRESETS = new Set([
  "custom",
  "customrange",
  "CUSTOM",
  "CustomRange",
]);

/** Maximum allowed span for a custom range, in milliseconds. */
const CUSTOM_RANGE_MAX_MS = 30 * 24 * 60 * 60 * 1000;

// ─────────────────────────────────────────────────────────────────────────────
// Internal helpers
// ─────────────────────────────────────────────────────────────────────────────

function fail(...errors: string[]): TemporalValidationResult {
  return { valid: false, errors };
}

function ok(): TemporalValidationResult {
  return { valid: true, errors: [] };
}

/**
 * Returns true if the timezone is recognised by the runtime Intl engine or
 * looks like a Windows-style identifier.
 *
 * Uses Intl.DateTimeFormat as the authoritative runtime check where available,
 * then falls back to a format check for Windows-style ids (e.g.
 * "Central Standard Time") which may not be in the IANA database.
 */
function looksLikeIanaTimezone(tz: string): boolean {
  // Primary: let the runtime validate the timezone directly.
  try {
    new Intl.DateTimeFormat(undefined, { timeZone: tz });
    return true;
  } catch {
    // Not a valid IANA tz — fall through to Windows-style alias check.
  }
  // Windows-style identifiers contain spaces and end with "Time".
  // These are accepted as a fallback on Windows hosts (backend validates them
  // against TimeZoneInfo; the frontend defers authoritative lookup to backend).
  if (/^[A-Za-z ]+Time$/.test(tz)) return true;
  return false;
}

function isCustomPreset(preset: string): boolean {
  return CUSTOM_PRESETS.has(preset);
}

// ─────────────────────────────────────────────────────────────────────────────
// Public validators
// ─────────────────────────────────────────────────────────────────────────────

/**
 * Validates that `timezone` is non-blank and has a recognisable IANA format.
 *
 * PASS: "America/Chicago", "Europe/London", "Central Standard Time"
 * FAIL: null, "", "  ", "not_a_timezone", "UTC+5" (use "Etc/GMT-5" instead)
 */
export function validateTimezone(
  timezone: string | null | undefined,
): TemporalValidationResult {
  if (!timezone || !timezone.trim()) {
    return fail(
      "timezone is required and must be a valid IANA timezone identifier.",
    );
  }

  const trimmed = timezone.trim();

  if (!looksLikeIanaTimezone(trimmed)) {
    return fail(
      `Unsupported timezone '${trimmed}'. ` +
        'Use a valid IANA identifier (e.g. "America/Chicago") or Windows identifier (e.g. "Central Standard Time").',
    );
  }

  return ok();
}

/**
 * Validates custom range bounds.
 *
 * PASS: start="2026-05-01T00:00:00Z", end="2026-05-03T00:00:00Z"
 * FAIL: either null, start >= end, span > 30 days
 */
export function validateCustomRange(
  customStartUtc: string | null | undefined,
  customEndUtc: string | null | undefined,
): TemporalValidationResult {
  if (!customStartUtc || !customEndUtc) {
    return fail(
      "customStartUtc and customEndUtc are required when preset=CustomRange.",
    );
  }

  const start = new Date(customStartUtc);
  const end = new Date(customEndUtc);

  if (isNaN(start.getTime())) {
    return fail(
      `customStartUtc is not a valid ISO 8601 timestamp: '${customStartUtc}'.`,
    );
  }
  if (isNaN(end.getTime())) {
    return fail(
      `customEndUtc is not a valid ISO 8601 timestamp: '${customEndUtc}'.`,
    );
  }

  if (start.getTime() >= end.getTime()) {
    // end < start → rejected explicitly; no silent inversion
    return fail("customStartUtc must be earlier than customEndUtc.");
  }

  const spanMs = end.getTime() - start.getTime();
  if (spanMs > CUSTOM_RANGE_MAX_MS) {
    return fail("CustomRange span must not exceed 30 days.");
  }

  return ok();
}

/**
 * Validates that a non-Custom preset is not paired with explicit custom bounds.
 *
 * Supplying custom bounds alongside a non-Custom preset is ambiguous intent
 * and is rejected explicitly — not silently ignored.
 *
 * PASS: preset="tonight", no bounds
 * PASS: preset="CustomRange", both bounds present
 * FAIL: preset="tonight", customStartUtc or customEndUtc also present
 */
export function validatePresetConsistency(
  preset: string,
  customStartUtc?: string | null,
  customEndUtc?: string | null,
): TemporalValidationResult {
  const hasCustomBounds = !!(customStartUtc || customEndUtc);

  if (!isCustomPreset(preset) && hasCustomBounds) {
    return fail(
      `Custom bounds must not be supplied with a non-Custom preset (preset='${preset}'). ` +
        "Use preset='CustomRange' to supply explicit bounds.",
    );
  }

  return ok();
}

/**
 * Full request validation: timezone → preset consistency → range bounds.
 *
 * Returns the first failure; errors do not accumulate across categories.
 *
 * PASS: { preset: "tonight", timezone: "America/Chicago" }
 * PASS: { preset: "CustomRange", timezone: "America/Chicago",
 *          customStartUtc: "2026-06-01T00:00:00Z", customEndUtc: "2026-06-03T00:00:00Z" }
 * FAIL: { preset: "tonight", customStartUtc: "...", customEndUtc: "..." }  ← ambiguous
 * FAIL: { preset: "CustomRange", timezone: "America/Chicago" }              ← missing bounds
 * FAIL: { preset: "CustomRange", timezone: "",  ... }                       ← blank timezone
 */
export function validateTimeWindowRequest(params: {
  preset: string;
  timezone: string | null | undefined;
  customStartUtc?: string | null;
  customEndUtc?: string | null;
}): TemporalValidationResult {
  const tzResult = validateTimezone(params.timezone);
  if (!tzResult.valid) return tzResult;

  const consistencyResult = validatePresetConsistency(
    params.preset,
    params.customStartUtc,
    params.customEndUtc,
  );
  if (!consistencyResult.valid) return consistencyResult;

  if (isCustomPreset(params.preset)) {
    const rangeResult = validateCustomRange(
      params.customStartUtc,
      params.customEndUtc,
    );
    if (!rangeResult.valid) return rangeResult;
  }

  return ok();
}

/**
 * Checks whether a given event start instant falls within a resolved time window.
 *
 * BOUNDARY RULE: [startUtc, endUtc)
 *   • Exactly at startUtc → INCLUDED
 *   • Exactly at endUtc   → EXCLUDED
 *
 * @param eventStartUtc - ISO 8601 UTC timestamp of the event's start instant.
 * @param windowStartUtc - Inclusive lower bound (ISO 8601 UTC).
 * @param windowEndUtc   - Exclusive upper bound (ISO 8601 UTC).
 */
export function isEventInWindow(
  eventStartUtc: string,
  windowStartUtc: string,
  windowEndUtc: string,
): boolean {
  const event = new Date(eventStartUtc).getTime();
  const start = new Date(windowStartUtc).getTime();
  const end = new Date(windowEndUtc).getTime();
  // Inclusive start, exclusive end.
  return event >= start && event < end;
}
