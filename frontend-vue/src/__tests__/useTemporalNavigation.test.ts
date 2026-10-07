/**
 * WEUP-SYNTH (G7): temporal authority tests.
 * Covers the mode inventory (NOW/TODAY/TONIGHT/TOMORROW/WEEKEND/CUSTOM),
 * TimelineControl label arbitration, singleton authority, selection
 * notification, and the URL/query-state projection. Deterministic: every
 * day-boundary test passes an explicit reference instant.
 */
import { beforeEach, describe, expect, it, vi } from "vitest";
import { TimeWindowPreset } from "../contracts/time-window.contracts";
import {
  TIMELINE_SCRUB_LABELS,
  resetTemporalNavigationForTests,
  useTemporalNavigation,
} from "../composables/useTemporalNavigation";

const REF = "2026-09-29T12:00:00Z"; // Tue 2026-09-29 07:00 CDT

describe("useTemporalNavigation (G7 temporal authority)", () => {
  beforeEach(() => {
    resetTemporalNavigationForTests();
    vi.useFakeTimers();
    vi.setSystemTime(new Date(REF));
  });

  it("is one authoritative state shared by every caller", () => {
    const a = useTemporalNavigation();
    const b = useTemporalNavigation();
    a.selectTemporalMode("TONIGHT");
    expect(b.preset.value).toBe(TimeWindowPreset.Tonight);
    expect(b.activeTemporalMode.value).toBe("TONIGHT");
  });

  it("defaults the market timezone to Houston (mission section 17)", () => {
    const temporal = useTemporalNavigation();
    expect(temporal.timezone.value).toBe("America/Chicago");
  });

  it("exposes the full TimelineControl label inventory", () => {
    expect([...TIMELINE_SCRUB_LABELS]).toEqual([
      "NOW",
      "6PM",
      "9PM",
      "MIDNIGHT",
      "3AM",
      "FRI",
      "SAT",
      "SUN",
    ]);
  });

  it("maps UI modes to wire presets", () => {
    const temporal = useTemporalNavigation();
    temporal.selectTemporalMode("TONIGHT");
    expect(temporal.preset.value).toBe(TimeWindowPreset.Tonight);
    expect(temporal.activeTemporalMode.value).toBe("TONIGHT");
    temporal.selectTemporalMode("TOMORROW");
    expect(temporal.preset.value).toBe(TimeWindowPreset.Tomorrow);
    temporal.selectTemporalMode("WEEKEND");
    expect(temporal.preset.value).toBe(TimeWindowPreset.ThisWeekend);
    temporal.selectTemporalMode("NOW");
    expect(temporal.preset.value).toBe(TimeWindowPreset.Now);
    expect(temporal.activeTemporalMode.value).toBe("NOW");
  });

  it("selects TODAY as an explicit CustomRange over the Houston day", () => {
    const temporal = useTemporalNavigation();
    temporal.selectTemporalMode("TODAY", REF);
    expect(temporal.activeTemporalMode.value).toBe("TODAY");
    expect(temporal.preset.value).toBe(TimeWindowPreset.CustomRange);
    expect(temporal.customStartLocal.value).toBe("2026-09-29T05:00:00.000Z");
    expect(temporal.customEndLocal.value).toBe("2026-09-30T05:00:00.000Z");
    expect(temporal.activeDayKey.value).toBe("2026-09-29");
    // The effective query window carries explicit UTC bounds, never a
    // frontend-computed preset expansion.
    const query = temporal.effectiveQueryWindow.value;
    expect(query.fromUtc).toBe("2026-09-29T05:00:00.000Z");
    expect(query.toUtc).toBe("2026-09-30T05:00:00.000Z");
    expect(query.marketTimezone).toBe("America/Chicago");
  });

  it("selects an arbitrary Houston day from the calendar strip", () => {
    const temporal = useTemporalNavigation();
    temporal.selectHoustonDay("2026-10-02");
    expect(temporal.preset.value).toBe(TimeWindowPreset.CustomRange);
    expect(temporal.customStartLocal.value).toBe("2026-10-02T05:00:00.000Z");
    expect(temporal.customEndLocal.value).toBe("2026-10-03T05:00:00.000Z");
    expect(temporal.activeDayKey.value).toBe("2026-10-02");
    expect(temporal.activeTemporalMode.value).toBe("CUSTOM");
  });

  it("arbitrates scrub labels: NOW returns to now", () => {
    const temporal = useTemporalNavigation();
    temporal.selectTemporalMode("TONIGHT");
    temporal.scrubToLabel("NOW");
    expect(temporal.activeTemporalMode.value).toBe("NOW");
    expect(temporal.preset.value).toBe(TimeWindowPreset.Now);
  });

  it("arbitrates scrub labels: hour labels move the cursor inside Tonight", () => {
    const temporal = useTemporalNavigation({ refInstant: REF });
    temporal.scrubToLabel("9PM", REF);
    expect(temporal.activeTemporalMode.value).toBe("TONIGHT");
    expect(temporal.preset.value).toBe(TimeWindowPreset.Tonight);
    expect(temporal.scrubPosition.value).toBeCloseTo(1 / 3, 5);
    expect(temporal.scrubCursorLabel.value).toBe("9PM");
    // Cursor instant lands inside the canonical 18:00->03:00 window.
    expect(temporal.scrubCursorUtc.value).toBe("2026-09-30T02:00:00.000Z");
    temporal.scrubToLabel("3AM", REF);
    expect(temporal.scrubPosition.value).toBeCloseTo(1, 5);
    expect(temporal.scrubCursorUtc.value).toBe("2026-09-30T08:00:00.000Z");
  });

  it("arbitrates scrub labels: FRI/SAT/SUN step to that weekday's Houston day", () => {
    const temporal = useTemporalNavigation({ refInstant: REF });
    // REF is a Tuesday; FRI is +3 days -> 2026-10-02.
    temporal.scrubToLabel("FRI", REF);
    expect(temporal.activeDayKey.value).toBe("2026-10-02");
    expect(temporal.customStartLocal.value).toBe("2026-10-02T05:00:00.000Z");
    expect(temporal.customEndLocal.value).toBe("2026-10-03T05:00:00.000Z");
    expect(temporal.stepOffset.value).toBe(3);
  });

  it("steps presets to Houston day ranges (no device-local math)", () => {
    const temporal = useTemporalNavigation({ refInstant: REF });
    temporal.stepDateTime(1, "day", REF);
    expect(temporal.activeDayKey.value).toBe("2026-09-30");
    expect(temporal.customStartLocal.value).toBe("2026-09-30T05:00:00.000Z");
    expect(temporal.customEndLocal.value).toBe("2026-10-01T05:00:00.000Z");
    expect(temporal.stepOffset.value).toBe(1);
  });

  it("notifies selection listeners when the window shift makes selection stale", () => {
    const seen: Array<string | null> = [];
    useTemporalNavigation({
      refInstant: REF,
      onSelectedEventChange: (id) => seen.push(id),
    });
    const temporal = useTemporalNavigation();
    // Small steps preserve selection (no notification).
    temporal.stepDateTime(1, "day", REF);
    expect(seen).toEqual([]);
    // Large steps clear it.
    temporal.stepDateTime(5, "day", REF);
    expect(seen).toEqual([null]);
  });

  it("serializes and restores the temporal query state", () => {
    const temporal = useTemporalNavigation();
    temporal.selectTemporalMode("TODAY", REF);
    const params = temporal.toQueryParams();
    expect(params.get("preset")).toBe("customRange");
    expect(params.get("timezone")).toBe("America/Chicago");
    expect(params.get("fromUtc")).toBe("2026-09-29T05:00:00.000Z");
    expect(params.get("toUtc")).toBe("2026-09-30T05:00:00.000Z");

    resetTemporalNavigationForTests();
    const restored = useTemporalNavigation();
    expect(restored.fromQueryParams(params)).toBe(true);
    expect(restored.preset.value).toBe(TimeWindowPreset.CustomRange);
    expect(restored.customStartLocal.value).toBe("2026-09-29T05:00:00.000Z");
    expect(restored.activeTemporalMode.value).toBe("CUSTOM");
  });

  it("fail-closes on unknown URL presets", () => {
    const temporal = useTemporalNavigation();
    const params = new URLSearchParams({ preset: "someday" });
    expect(temporal.fromQueryParams(params)).toBe(false);
    expect(temporal.preset.value).toBe(TimeWindowPreset.Now);
  });

  it("resetToNow restores the canonical mode", () => {
    const temporal = useTemporalNavigation();
    temporal.selectTemporalMode("WEEKEND");
    temporal.resetToNow();
    expect(temporal.activeTemporalMode.value).toBe("NOW");
    expect(temporal.preset.value).toBe(TimeWindowPreset.Now);
    expect(temporal.activeDayKey.value).toBeNull();
    expect(temporal.stepOffset.value).toBe(0);
  });
});
