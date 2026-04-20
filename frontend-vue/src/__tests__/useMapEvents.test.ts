import { describe, expect, it, vi } from "vitest";

import { useMapEvents } from "../composables/useMapEvents";
import type { EventMapFeedQueryDto } from "../contracts/map-feed.contracts";
import { TimeWindowPreset } from "../contracts/time-window.contracts";
import { fetchEventMapFeed } from "../services/mapFeedService";

vi.mock("../services/mapFeedService", () => ({
  fetchEventMapFeed: vi.fn(),
}));

type Deferred<T> = {
  promise: Promise<T>;
  resolve: (value: T) => void;
};

function createDeferred<T>(): Deferred<T> {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((nextResolve) => {
    resolve = nextResolve;
  });

  return { promise, resolve };
}

function buildQuery(preset: TimeWindowPreset): EventMapFeedQueryDto {
  return {
    bbox: "-95.4,29.6,-95.1,29.9",
    preset,
    timezone: "America/Chicago",
  };
}

describe("useMapEvents", () => {
  it("applies only the latest response when filter changes race", async () => {
    const fetchMock = vi.mocked(fetchEventMapFeed);
    const slowPayload = {
      events: [
        {
          eventId: "evt-old",
          title: "Old",
          startUtc: "2026-04-12T00:00:00Z",
          endUtc: null,
          latitude: 29.76,
          longitude: -95.36,
          venueName: "Venue",
          district: null,
          primaryCategory: "music",
          savedByCurrentUser: false,
          markerState: "default" as const,
        },
      ],
      totalCount: 1,
      clusters: [],
      densityControl: {
        clusteringEnabled: false,
        activationVisibleEventCountThreshold: 24,
        activationMaxZoomInclusive: 13.5,
        clusterRadiusPixels: 56,
        clusterMaxZoomInclusive: 15,
        selectedMarkerBypassEnabled: true,
        expansionBehavior: "zoom_or_expand" as const,
      },
      clusterStrategy: "client_v1" as const,
    };
    const slow = createDeferred({
      ...slowPayload,
    });
    const fast = Promise.resolve({
      events: [
        {
          eventId: "evt-new",
          title: "New",
          startUtc: "2026-04-13T00:00:00Z",
          endUtc: null,
          latitude: 29.77,
          longitude: -95.35,
          venueName: "Venue",
          district: null,
          primaryCategory: "music",
          savedByCurrentUser: true,
          markerState: "saved" as const,
        },
      ],
      totalCount: 1,
      clusters: [],
      densityControl: {
        clusteringEnabled: false,
        activationVisibleEventCountThreshold: 24,
        activationMaxZoomInclusive: 13.5,
        clusterRadiusPixels: 56,
        clusterMaxZoomInclusive: 15,
        selectedMarkerBypassEnabled: true,
        expansionBehavior: "zoom_or_expand" as const,
      },
      clusterStrategy: "client_v1" as const,
    });

    fetchMock.mockImplementationOnce(() => slow.promise);
    fetchMock.mockImplementationOnce(() => fast);

    const mapEvents = useMapEvents();

    const slowRequest = mapEvents.loadEvents(
      buildQuery(TimeWindowPreset.Tonight),
    );
    const fastRequest = mapEvents.loadEvents(
      buildQuery(TimeWindowPreset.Tomorrow),
    );

    await fastRequest;
    slow.resolve(slowPayload);
    await slowRequest;

    expect(mapEvents.events.value).toHaveLength(1);
    expect(mapEvents.events.value[0]?.eventId).toBe("evt-new");
    expect(mapEvents.error.value).toBeNull();
  });

  it("clears selection only after the latest successful payload drops the event", async () => {
    const fetchMock = vi.mocked(fetchEventMapFeed);

    fetchMock.mockResolvedValueOnce({
      events: [
        {
          eventId: "evt-selected",
          title: "Selected",
          startUtc: "2026-04-12T00:00:00Z",
          endUtc: null,
          latitude: 29.76,
          longitude: -95.36,
          venueName: "Venue",
          district: null,
          primaryCategory: "music",
          savedByCurrentUser: false,
          markerState: "default" as const,
        },
      ],
      totalCount: 1,
      clusters: [],
      densityControl: {
        clusteringEnabled: false,
        activationVisibleEventCountThreshold: 24,
        activationMaxZoomInclusive: 13.5,
        clusterRadiusPixels: 56,
        clusterMaxZoomInclusive: 15,
        selectedMarkerBypassEnabled: true,
        expansionBehavior: "zoom_or_expand" as const,
      },
      clusterStrategy: "client_v1" as const,
    });

    fetchMock.mockResolvedValueOnce({
      events: [],
      totalCount: 0,
      clusters: [],
      densityControl: {
        clusteringEnabled: false,
        activationVisibleEventCountThreshold: 24,
        activationMaxZoomInclusive: 13.5,
        clusterRadiusPixels: 56,
        clusterMaxZoomInclusive: 15,
        selectedMarkerBypassEnabled: true,
        expansionBehavior: "zoom_or_expand" as const,
      },
      clusterStrategy: "client_v1" as const,
    });

    const mapEvents = useMapEvents();
    await mapEvents.loadEvents(buildQuery(TimeWindowPreset.Now));
    mapEvents.selectByEventId("evt-selected");

    expect(mapEvents.selectedEventId.value).toBe("evt-selected");

    await mapEvents.loadEvents(buildQuery(TimeWindowPreset.Custom));

    expect(mapEvents.selectedEventId.value).toBeNull();
  });
});
