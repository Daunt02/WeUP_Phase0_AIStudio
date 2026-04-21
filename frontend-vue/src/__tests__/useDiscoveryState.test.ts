import { beforeEach, describe, expect, it } from "vitest";
import { TimeWindowPreset } from "../contracts/time-window.contracts";
import type {
  EventMapFeedQueryDto,
  EventMapItemDto,
} from "../contracts/map-feed.contracts";
import {
  resetDiscoveryStateForTests,
  useDiscoveryState,
} from "../composables/useDiscoveryState";

function createMapQuery(
  overrides: Partial<EventMapFeedQueryDto> = {},
): EventMapFeedQueryDto {
  return {
    bbox: "-96.0,29.0,-95.0,30.0",
    preset: TimeWindowPreset.Tonight,
    timezone: "America/Chicago",
    includeSavedOnly: false,
    ...overrides,
  };
}

function createMapItem(eventId: string): EventMapItemDto {
  return {
    eventId,
    title: `Event ${eventId}`,
    startUtc: "2026-04-20T20:00:00Z",
    endUtc: null,
    latitude: 29.76,
    longitude: -95.36,
    venueName: "Warehouse 9",
    district: "Midtown",
    primaryCategory: "music",
    savedByCurrentUser: false,
    markerState: "default",
  };
}

describe("useDiscoveryState", () => {
  beforeEach(() => {
    resetDiscoveryStateForTests();
  });

  it("derives the shared temporal and filter state from the canonical map query", () => {
    const discovery = useDiscoveryState();

    discovery.reportMapFeedQuery(
      createMapQuery({
        customStartUtc: "2026-04-20T00:00:00Z",
        customEndUtc: "2026-04-21T00:00:00Z",
        district: "Downtown",
        categories: ["food"],
      }),
    );
    discovery.applyFilters({
      district: "Midtown",
      categories: ["music", "community"],
      includeSavedOnly: true,
    });

    expect(discovery.activeTemporalFilter.value).toEqual({
      preset: TimeWindowPreset.Tonight,
      timezone: "America/Chicago",
      customStartUtc: "2026-04-20T00:00:00Z",
      customEndUtc: "2026-04-21T00:00:00Z",
    });
    expect(discovery.calendarFeedQuery.value).toEqual({
      bbox: "-96.0,29.0,-95.0,30.0",
      preset: TimeWindowPreset.Tonight,
      timezone: "America/Chicago",
      customStartUtc: "2026-04-20T00:00:00Z",
      customEndUtc: "2026-04-21T00:00:00Z",
      district: "Midtown",
      categories: ["music", "community"],
      includeSavedOnly: true,
    });
    expect(discovery.stateSummary.value.temporalPreset).toBe(
      TimeWindowPreset.Tonight,
    );
    expect(discovery.stateSummary.value.temporalTimezone).toBe(
      "America/Chicago",
    );
  });

  it("restores the previous overlay layout when a selected event leaves scope", () => {
    const discovery = useDiscoveryState();

    discovery.expandOverlay();
    discovery.selectEvent("evt-001");

    expect(discovery.overlayMode.value).toBe("event-selected");
    expect(discovery.selectedEventId.value).toBe("evt-001");

    discovery.reportVisibleEvents([createMapItem("evt-002")]);

    expect(discovery.selectedEventId.value).toBeNull();
    expect(discovery.overlayMode.value).toBe("expanded");
  });

  it("keeps repeated selection writes idempotent and preserves overlay fallback", () => {
    const discovery = useDiscoveryState();

    discovery.openPartialOverlay();
    discovery.selectEvent("evt-001");
    discovery.selectEvent("evt-001");
    discovery.clearSelection();

    expect(discovery.selectedEventId.value).toBeNull();
    expect(discovery.overlayMode.value).toBe("partial");
  });
});
