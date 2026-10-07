import { describe, expect, it } from "vitest";

import { useMapFeedFilters } from "../composables/useMapFeedFilters";
import { TimeWindowPreset } from "../contracts/time-window.contracts";

describe("useMapFeedFilters", () => {
  it("builds a canonical preset query without deriving window bounds", () => {
    const filters = useMapFeedFilters({
      preset: TimeWindowPreset.Tomorrow,
      timezone: "America/Chicago",
      includeSavedOnly: true,
    });

    expect(filters.validationError.value).toBeNull();
    // Garden G-04: stale-test repair — the temporal contract now carries
    // marketTimezone/referenceInstantUtc (implementation is authoritative).
    expect(filters.toCalendarOverlayTemporalQuery()).toEqual({
      preset: TimeWindowPreset.Tomorrow,
      timezone: "America/Chicago",
      marketTimezone: "America/Chicago",
      referenceInstantUtc: undefined,
    });

    expect(filters.buildMapFeedQuery("-95.4,29.6,-95.1,29.9")).toEqual({
      bbox: "-95.4,29.6,-95.1,29.9",
      preset: TimeWindowPreset.Tomorrow,
      timezone: "America/Chicago",
      marketTimezone: "America/Chicago",
      fromUtc: undefined,
      toUtc: undefined,
      referenceInstantUtc: undefined,
      includeSavedOnly: true,
    });
  });

  it("rejects invalid custom ranges before request dispatch", () => {
    const filters = useMapFeedFilters({
      preset: TimeWindowPreset.Custom,
      timezone: "America/Chicago",
      customStartLocal: "2026-04-13T11:00",
      customEndLocal: "2026-04-13T09:00",
    });

    expect(filters.validationError.value).toBe(
      "Custom range start must be earlier than custom range end.",
    );
    expect(() => filters.buildMapFeedQuery("-95.4,29.6,-95.1,29.9")).toThrow(
      "Custom range start must be earlier than custom range end.",
    );
  });
});
