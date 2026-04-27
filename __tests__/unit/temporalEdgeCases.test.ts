/**
 * M8-P40: Temporal Edge-Case Unit Tests
 *
 * Tests validation helpers in utils/temporalEdgeCaseValidator and
 * documents frontend contract-awareness for the canonical temporal semantics.
 *
 * EDGE-CASE TABLE
 * ┌─────────────────────────────────────────────────────────┬──────────┐
 * │ Scenario                                                │ Expected │
 * ├─────────────────────────────────────────────────────────┼──────────┤
 * │ Blank timezone                                          │ FAIL     │
 * │ Null timezone                                           │ FAIL     │
 * │ Unrecognised timezone format                            │ FAIL     │
 * │ Valid IANA timezone                                     │ PASS     │
 * │ Valid Windows timezone alias                            │ PASS     │
 * │ CustomRange: end before start                          │ FAIL     │
 * │ CustomRange: start == end (zero-length)                 │ FAIL     │
 * │ CustomRange: missing start                              │ FAIL     │
 * │ CustomRange: missing end                                │ FAIL     │
 * │ CustomRange: span exactly 30 days                       │ PASS     │
 * │ CustomRange: span > 30 days                             │ FAIL     │
 * │ CustomRange: invalid ISO start string                   │ FAIL     │
 * │ Non-Custom preset with both custom bounds               │ FAIL     │
 * │ Non-Custom preset with only one custom bound            │ FAIL     │
 * │ Custom preset with no bounds                            │ FAIL     │
 * │ Valid Tonight request                                   │ PASS     │
 * │ Valid CustomRange request                               │ PASS     │
 * │ isEventInWindow: exactly at startUtc                    │ INCLUDED │
 * │ isEventInWindow: exactly at endUtc                      │ EXCLUDED │
 * │ isEventInWindow: one ms before endUtc                   │ INCLUDED │
 * │ isEventInWindow: one ms before startUtc                 │ EXCLUDED │
 * │ isEventInWindow: cross-midnight event inside window     │ INCLUDED │
 * │ isEventInWindow: cross-midnight event outside window    │ EXCLUDED │
 * └─────────────────────────────────────────────────────────┴──────────┘
 *
 * NOTE ON DST CONTRACT TESTS
 *   DST expansion (spring-forward 8h, fall-back 10h, 23h/25h day) is resolved
 *   server-side.  The frontend tests here validate the CLIENT-SIDE contract:
 *   - The client never expands presets itself.
 *   - The client passes the backend-returned startUtc/endUtc through directly.
 *   - isEventInWindow uses those exact UTC bounds for inclusion/exclusion.
 *
 * Backend DST values used as fixtures (America/Chicago):
 *   Spring-forward Tonight (prior evening, 2026-03-08):
 *     startUtc = "2026-03-08T00:00:00Z"   endUtc = "2026-03-08T08:00:00Z"  (8 h)
 *   Fall-back Tonight (crossing 2026-11-01):
 *     startUtc = "2026-10-31T23:00:00Z"   endUtc = "2026-11-01T09:00:00Z"  (10 h)
 */

import {
  validateTimezone,
  validateCustomRange,
  validatePresetConsistency,
  validateTimeWindowRequest,
  isEventInWindow,
} from "@/utils/temporalEdgeCaseValidator";

// ── Timezone validation ──────────────────────────────────────────────────────

describe("validateTimezone", () => {
  it("rejects null timezone", () => {
    const r = validateTimezone(null);
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toMatch(/required/);
  });

  it("rejects empty string timezone", () => {
    const r = validateTimezone("");
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toMatch(/required/);
  });

  it("rejects whitespace-only timezone", () => {
    const r = validateTimezone("   ");
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toMatch(/required/);
  });

  it("rejects unrecognised timezone format", () => {
    const r = validateTimezone("Not_a_timezone");
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toMatch(/Unsupported timezone/);
    expect(r.errors[0]).toContain("Not_a_timezone");
  });

  it("rejects UTC offset notation (not an IANA id)", () => {
    // "UTC+5" is not a valid IANA id; use "Etc/GMT-5"
    const r = validateTimezone("UTC+5");
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toMatch(/Unsupported timezone/);
  });

  it("accepts a valid IANA timezone", () => {
    expect(validateTimezone("America/Chicago").valid).toBe(true);
  });

  it("accepts another IANA timezone (New York)", () => {
    expect(validateTimezone("America/New_York").valid).toBe(true);
  });

  it("accepts a Windows-style timezone alias", () => {
    expect(validateTimezone("Central Standard Time").valid).toBe(true);
  });
});

// ── Custom range validation ──────────────────────────────────────────────────

describe("validateCustomRange", () => {
  const START = "2026-05-01T00:00:00Z";
  const END = "2026-05-03T00:00:00Z";

  it("rejects null start", () => {
    const r = validateCustomRange(null, END);
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toMatch(/required/);
  });

  it("rejects null end", () => {
    const r = validateCustomRange(START, null);
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toMatch(/required/);
  });

  it("rejects both null (no explicit range, null preset)", () => {
    const r = validateCustomRange(null, null);
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toMatch(/required/);
  });

  it("rejects end before start — no silent inversion", () => {
    // RULE: invalid ranges must fail explicitly, not self-correct silently.
    const r = validateCustomRange(
      "2026-05-10T12:00:00Z",
      "2026-05-08T12:00:00Z",
    );
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toBe(
      "customStartUtc must be earlier than customEndUtc.",
    );
  });

  it("rejects start equal to end (zero-length range)", () => {
    const instant = "2026-05-10T12:00:00Z";
    const r = validateCustomRange(instant, instant);
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toBe(
      "customStartUtc must be earlier than customEndUtc.",
    );
  });

  it("rejects span greater than 30 days", () => {
    const start = "2026-05-01T00:00:00Z";
    const end = new Date(
      new Date(start).getTime() + 31 * 24 * 60 * 60 * 1000,
    ).toISOString();
    const r = validateCustomRange(start, end);
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toMatch(/30 days/);
  });

  it("accepts span of exactly 30 days", () => {
    const start = "2026-05-01T00:00:00Z";
    const end = new Date(
      new Date(start).getTime() + 30 * 24 * 60 * 60 * 1000,
    ).toISOString();
    const r = validateCustomRange(start, end);
    expect(r.valid).toBe(true);
  });

  it("rejects an invalid ISO start string", () => {
    const r = validateCustomRange("not-a-date", END);
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toMatch(/not a valid ISO 8601/);
  });

  it("rejects an invalid ISO end string", () => {
    const r = validateCustomRange(START, "also-not-a-date");
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toMatch(/not a valid ISO 8601/);
  });

  it("accepts a valid range", () => {
    expect(validateCustomRange(START, END).valid).toBe(true);
  });
});

// ── Preset consistency validation ────────────────────────────────────────────

describe("validatePresetConsistency", () => {
  const BOUNDS = {
    start: "2026-06-01T00:00:00Z",
    end: "2026-06-02T00:00:00Z",
  };

  it("rejects non-Custom preset with both bounds supplied", () => {
    const r = validatePresetConsistency("tonight", BOUNDS.start, BOUNDS.end);
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toMatch(/Custom bounds must not be supplied/);
    expect(r.errors[0]).toContain("tonight");
  });

  it("rejects non-Custom preset with only start bound", () => {
    const r = validatePresetConsistency("tomorrow", BOUNDS.start, null);
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toMatch(/Custom bounds must not be supplied/);
  });

  it("rejects non-Custom preset with only end bound", () => {
    const r = validatePresetConsistency("thisWeekend", null, BOUNDS.end);
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toMatch(/Custom bounds must not be supplied/);
  });

  it("passes non-Custom preset with no bounds", () => {
    expect(validatePresetConsistency("tonight").valid).toBe(true);
    expect(validatePresetConsistency("tomorrow").valid).toBe(true);
    expect(validatePresetConsistency("now").valid).toBe(true);
  });

  it("passes CustomRange preset with bounds (range validity checked separately)", () => {
    expect(
      validatePresetConsistency("CustomRange", BOUNDS.start, BOUNDS.end).valid,
    ).toBe(true);
  });

  it("passes custom preset (lower-case) with bounds", () => {
    expect(
      validatePresetConsistency("custom", BOUNDS.start, BOUNDS.end).valid,
    ).toBe(true);
  });
});

// ── Full request validation ───────────────────────────────────────────────────

describe("validateTimeWindowRequest", () => {
  const TZ = "America/Chicago";
  const START = "2026-06-01T00:00:00Z";
  const END = "2026-06-03T00:00:00Z";

  it("accepts a valid Tonight request", () => {
    const r = validateTimeWindowRequest({ preset: "tonight", timezone: TZ });
    expect(r.valid).toBe(true);
  });

  it("accepts a valid CustomRange request", () => {
    const r = validateTimeWindowRequest({
      preset: "CustomRange",
      timezone: TZ,
      customStartUtc: START,
      customEndUtc: END,
    });
    expect(r.valid).toBe(true);
  });

  it("rejects blank timezone (first rule)", () => {
    const r = validateTimeWindowRequest({ preset: "tonight", timezone: "" });
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toMatch(/required/);
  });

  it("rejects unsupported timezone", () => {
    const r = validateTimeWindowRequest({
      preset: "tonight",
      timezone: "Fake/Tz",
    });
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toMatch(/Unsupported timezone/);
  });

  it("rejects non-Custom preset with conflicting custom bounds", () => {
    // preset=tonight + custom bounds → ambiguous intent; fail explicitly
    const r = validateTimeWindowRequest({
      preset: "tonight",
      timezone: TZ,
      customStartUtc: START,
      customEndUtc: END,
    });
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toMatch(/Custom bounds must not be supplied/);
  });

  it("rejects CustomRange with no bounds", () => {
    const r = validateTimeWindowRequest({
      preset: "CustomRange",
      timezone: TZ,
    });
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toMatch(/required/);
  });

  it("rejects CustomRange with end before start", () => {
    const r = validateTimeWindowRequest({
      preset: "CustomRange",
      timezone: TZ,
      customStartUtc: END,
      customEndUtc: START,
    });
    expect(r.valid).toBe(false);
    expect(r.errors[0]).toBe(
      "customStartUtc must be earlier than customEndUtc.",
    );
  });
});

// ── Event boundary inclusion/exclusion ──────────────────────────────────────

describe("isEventInWindow", () => {
  // Cross-midnight window: Apr 26 20:00Z → Apr 27 04:00Z
  const WIN_START = "2026-04-26T20:00:00.000Z";
  const WIN_END = "2026-04-27T04:00:00.000Z";

  it("includes event at exactly startUtc (inclusive start)", () => {
    expect(isEventInWindow(WIN_START, WIN_START, WIN_END)).toBe(true);
  });

  it("includes event 1 ms after startUtc", () => {
    expect(
      isEventInWindow("2026-04-26T20:00:00.001Z", WIN_START, WIN_END),
    ).toBe(true);
  });

  it("includes event 1 ms before endUtc", () => {
    expect(
      isEventInWindow("2026-04-27T03:59:59.999Z", WIN_START, WIN_END),
    ).toBe(true);
  });

  it("excludes event at exactly endUtc (exclusive end)", () => {
    expect(isEventInWindow(WIN_END, WIN_START, WIN_END)).toBe(false);
  });

  it("excludes event 1 ms after endUtc", () => {
    expect(
      isEventInWindow("2026-04-27T04:00:00.001Z", WIN_START, WIN_END),
    ).toBe(false);
  });

  it("excludes event 1 ms before startUtc", () => {
    expect(
      isEventInWindow("2026-04-26T19:59:59.999Z", WIN_START, WIN_END),
    ).toBe(false);
  });

  it("includes cross-midnight event that starts inside the window", () => {
    // Event starts at 23:30 (inside), ends at 01:30+1 (outside window) — start determines inclusion.
    expect(
      isEventInWindow("2026-04-26T23:30:00.000Z", WIN_START, WIN_END),
    ).toBe(true);
  });

  it("excludes cross-midnight event that starts before the window", () => {
    // Event starts at 19:00 (outside), ends at 21:00 (inside) — start determines inclusion.
    expect(
      isEventInWindow("2026-04-26T19:00:00.000Z", WIN_START, WIN_END),
    ).toBe(false);
  });
});

// ── DST contract awareness tests ─────────────────────────────────────────────
// These tests use backend-resolved UTC fixtures to confirm the frontend's
// inclusion/exclusion logic behaves correctly with DST-affected window sizes.

describe("isEventInWindow — DST fixture windows", () => {
  // Spring-forward prior evening: 8 UTC hours (backend resolved)
  const SPRING_FWD_START = "2026-03-08T00:00:00Z";
  const SPRING_FWD_END = "2026-03-08T08:00:00Z";

  // Fall-back same-day evening: 10 UTC hours (backend resolved)
  const FALL_BACK_START = "2026-10-31T23:00:00Z";
  const FALL_BACK_END = "2026-11-01T09:00:00Z";

  it("spring-forward window (8h): event at start is included", () => {
    expect(
      isEventInWindow(SPRING_FWD_START, SPRING_FWD_START, SPRING_FWD_END),
    ).toBe(true);
  });

  it("spring-forward window (8h): event at end is excluded", () => {
    expect(
      isEventInWindow(SPRING_FWD_END, SPRING_FWD_START, SPRING_FWD_END),
    ).toBe(false);
  });

  it("spring-forward window (8h): event 1 ms before end is included", () => {
    const almostEnd = new Date(
      new Date(SPRING_FWD_END).getTime() - 1,
    ).toISOString();
    expect(isEventInWindow(almostEnd, SPRING_FWD_START, SPRING_FWD_END)).toBe(
      true,
    );
  });

  it("fall-back window (10h): event at start is included", () => {
    expect(
      isEventInWindow(FALL_BACK_START, FALL_BACK_START, FALL_BACK_END),
    ).toBe(true);
  });

  it("fall-back window (10h): event at end is excluded", () => {
    expect(isEventInWindow(FALL_BACK_END, FALL_BACK_START, FALL_BACK_END)).toBe(
      false,
    );
  });

  it("fall-back window (10h): event during ambiguous hour (01:30 CDT = 06:30Z) is included", () => {
    // 2026-11-01T06:30:00Z is the CDT occurrence of the ambiguous 01:30 local.
    // The backend resolves it to the earliest UTC instant. It falls inside the window.
    expect(
      isEventInWindow("2026-11-01T06:30:00Z", FALL_BACK_START, FALL_BACK_END),
    ).toBe(true);
  });
});
