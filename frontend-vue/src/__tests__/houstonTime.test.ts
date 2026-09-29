/**
 * WEUP-SYNTH (G7): Houston-time boundary tests.
 * Every case uses explicit reference instants — no device clock, no device
 * timezone. America/Chicago is UTC-5 (CDT) in September 2026 and UTC-6 (CST)
 * in January.
 */
import { describe, expect, it } from "vitest";
import {
  formatHoustonDayLabel,
  formatHoustonTimeShort,
  getHoustonTimeBucket,
  getUpcomingWeek,
  houstonDayBoundsUtc,
  houstonDayEndUtc,
  houstonDayKey,
  houstonDayStartUtc,
  houstonHourOfDay,
  tonightCursorUtc,
  tonightFractionLabel,
} from "../utils/houstonTime";

describe("houstonTime boundary", () => {
  it("derives the Houston day key across the UTC midnight boundary", () => {
    // 2026-09-29T04:59:59Z = 2026-09-28 23:59:59 CDT
    expect(houstonDayKey("2026-09-29T04:59:59Z")).toBe("2026-09-28");
    // 2026-09-29T05:00:00Z = 2026-09-29 00:00:00 CDT
    expect(houstonDayKey("2026-09-29T05:00:00Z")).toBe("2026-09-29");
  });

  it("computes Houston day bounds as UTC instants (CDT, UTC-5)", () => {
    expect(houstonDayStartUtc("2026-09-29T12:00:00Z")).toBe(
      "2026-09-29T05:00:00.000Z",
    );
    expect(houstonDayEndUtc("2026-09-29T12:00:00Z")).toBe(
      "2026-09-30T05:00:00.000Z",
    );
  });

  it("computes Houston day bounds under standard time (CST, UTC-6)", () => {
    expect(houstonDayStartUtc("2026-01-15T12:00:00Z")).toBe(
      "2026-01-15T06:00:00.000Z",
    );
    expect(houstonDayEndUtc("2026-01-15T12:00:00Z")).toBe(
      "2026-01-16T06:00:00.000Z",
    );
  });

  it("resolves explicit day-key bounds", () => {
    expect(houstonDayBoundsUtc("2026-09-29")).toEqual({
      startUtc: "2026-09-29T05:00:00.000Z",
      endUtc: "2026-09-30T05:00:00.000Z",
    });
  });

  it("rejects malformed day keys", () => {
    expect(() => houstonDayBoundsUtc("09/29/2026")).toThrow();
    expect(() => houstonDayKey("not-a-date")).toThrow();
  });

  it("builds a deterministic 7-day strip", () => {
    const week = getUpcomingWeek("2026-09-29T12:00:00Z");
    expect(week).toHaveLength(7);
    expect(week[0].key).toBe("2026-09-29");
    expect(week[0].dayName).toBe("TUE");
    expect(week[0].label).toBe("SEP 29");
    expect(week[0].dayNumber).toBe(29);
    expect(week[6].key).toBe("2026-10-05");
    // Keys are unique even across DST transitions.
    expect(new Set(week.map((d) => d.key)).size).toBe(7);
  });

  it("classifies time-of-day buckets in Houston time", () => {
    // 09:00 CDT -> MORNING
    expect(getHoustonTimeBucket("2026-09-29T14:00:00Z")).toBe("MORNING");
    // 14:00 CDT -> DAY
    expect(getHoustonTimeBucket("2026-09-29T19:00:00Z")).toBe("DAY");
    // 19:00 CDT -> EVENING
    expect(getHoustonTimeBucket("2026-09-30T00:00:00Z")).toBe("EVENING");
    // 22:00 CDT -> NIGHT
    expect(getHoustonTimeBucket("2026-09-30T03:00:00Z")).toBe("NIGHT");
    // 03:30 CDT -> LATE
    expect(getHoustonTimeBucket("2026-09-29T08:30:00Z")).toBe("LATE");
  });

  it("formats display labels in Houston time", () => {
    const date = new Date("2026-09-29T12:00:00Z");
    expect(formatHoustonDayLabel(date)).toBe("SEP 29");
    // 12:00Z = 07:00 CDT
    expect(formatHoustonTimeShort(date)).toBe("07:00");
    expect(houstonHourOfDay("2026-09-29T23:30:00Z")).toBe(18);
  });

  it("places the scrub cursor inside the canonical Tonight window", () => {
    // 09:00 CDT reference: tonight = today 18:00 -> tomorrow 03:00 CDT.
    expect(tonightCursorUtc("2026-09-29T14:00:00Z", 0)).toBe(
      "2026-09-29T23:00:00.000Z",
    );
    expect(tonightCursorUtc("2026-09-29T14:00:00Z", 1)).toBe(
      "2026-09-30T08:00:00.000Z",
    );
    // 02:00 CDT reference: containing window = prior evening 18:00 -> 03:00.
    expect(tonightCursorUtc("2026-09-29T07:00:00Z", 1)).toBe(
      "2026-09-29T08:00:00.000Z",
    );
  });

  it("maps scrub fractions to hour labels", () => {
    expect(tonightFractionLabel(0)).toBe("6PM");
    expect(tonightFractionLabel(1 / 3)).toBe("9PM");
    expect(tonightFractionLabel(2 / 3)).toBe("MIDNIGHT");
    expect(tonightFractionLabel(1)).toBe("3AM");
    expect(tonightFractionLabel(0.5)).toBe("MIDNIGHT");
  });
});
