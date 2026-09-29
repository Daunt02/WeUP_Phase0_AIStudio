/*
 * WEUP-SYNTH:
 * sources=[
 *   components/MapCanvas.tsx,
 *   components/WorldLayer.tsx,
 *   components/VenuePulse.tsx,
 *   components/ZoneDrawer.tsx
 * ]
 * destination=frontend-vue/src/__tests__/mapWorldSurface.test.ts
 * mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
 * notes=Deterministic unit tests for the G5 world-surface derivations.
 * Map rendering itself is not unit-testable here; every behavior with a
 * deterministic contract (basemap resolution, pulse derivation, zone
 * taxonomy/provenance) is covered.
 */
import { describe, expect, it } from "vitest";

import {
  clampPulseIntensity,
  derivePulseVenues,
  resolveBasemapStyle,
  toZoneFeatureCollection,
  WEUP_DARK_BASEMAP_STYLE_URL,
  ZONE_PROTOTYPE_POLYGONS,
  ZONE_PROTOTYPE_PROVENANCE_NOTE,
  type EventMapMarkerViewModel,
} from "../components/mapWorld";
import { DISCOVERY_CANONICAL_DISTRICT_CODES } from "../contracts/user-context.contracts";

let markerCounter = 0;

function makeMarker(
  overrides: Partial<EventMapMarkerViewModel> = {},
): EventMapMarkerViewModel {
  markerCounter += 1;
  return {
    eventId: `test-event-${markerCounter}`,
    title: "Test Event",
    startUtc: "2026-09-29T20:00:00Z",
    endUtc: null,
    latitude: 37.7749,
    longitude: -122.4194,
    venueName: "Test Venue",
    district: "mission",
    primaryCategory: "nightlife",
    savedByCurrentUser: false,
    markerState: "default",
    effectiveMarkerState: "default",
    ...overrides,
  };
}

describe("resolveBasemapStyle", () => {
  it("resolves the canonical AI Studio dark basemap by default", () => {
    expect(resolveBasemapStyle()).toBe(WEUP_DARK_BASEMAP_STYLE_URL);
    expect(WEUP_DARK_BASEMAP_STYLE_URL).toBe("mapbox://styles/mapbox/dark-v11");
  });

  it("honors an explicit style URL override", () => {
    expect(resolveBasemapStyle("mapbox://styles/mapbox/streets-v12")).toBe(
      "mapbox://styles/mapbox/streets-v12",
    );
  });

  it("falls back to the dark basemap on blank overrides", () => {
    expect(resolveBasemapStyle("   ")).toBe(WEUP_DARK_BASEMAP_STYLE_URL);
    expect(resolveBasemapStyle("")).toBe(WEUP_DARK_BASEMAP_STYLE_URL);
  });
});

describe("clampPulseIntensity", () => {
  it("clamps into [0.1, 1.0]", () => {
    expect(clampPulseIntensity(-5)).toBe(0.1);
    expect(clampPulseIntensity(0)).toBe(0.1);
    expect(clampPulseIntensity(0.5)).toBe(0.5);
    expect(clampPulseIntensity(1)).toBe(1.0);
    expect(clampPulseIntensity(99)).toBe(1.0);
  });

  it("maps NaN to the minimum intensity", () => {
    expect(clampPulseIntensity(Number.NaN)).toBe(0.1);
  });
});

describe("derivePulseVenues", () => {
  it("groups canonical markers by venue and derives centers", () => {
    const markers = [
      makeMarker({ venueName: "Club Nova", latitude: 37.76, longitude: -122.41 }),
      makeMarker({ venueName: "Club Nova", latitude: 37.78, longitude: -122.43 }),
      makeMarker({ venueName: "Solo Spot" }),
    ];

    const venues = derivePulseVenues(markers, { minEventCount: 1 });

    expect(venues).toHaveLength(2);
    const nova = venues.find((v) => v.venueName === "Club Nova");
    expect(nova).toBeDefined();
    expect(nova?.eventCount).toBe(2);
    expect(nova?.centerLat).toBeCloseTo(37.77, 5);
    expect(nova?.centerLng).toBeCloseTo(-122.42, 5);
  });

  it("skips single-event venues without saved or selected state", () => {
    const markers = [makeMarker({ venueName: "Solo Spot" })];
    expect(derivePulseVenues(markers)).toEqual([]);
  });

  it("keeps single-event venues that carry saved or selected state", () => {
    const saved = makeMarker({
      venueName: "Saved Spot",
      savedByCurrentUser: true,
      effectiveMarkerState: "saved",
    });
    const selected = makeMarker({
      venueName: "Selected Spot",
      effectiveMarkerState: "selected",
    });

    const venues = derivePulseVenues([saved, selected], { minEventCount: 2 });
    expect(venues.map((v) => v.venueName).sort()).toEqual([
      "Saved Spot",
      "Selected Spot",
    ]);
    expect(venues.every((v) => v.intensity >= 0.1 && v.intensity <= 1.0)).toBe(
      true,
    );
  });

  it("derives intensity from real fields only: count, saved, selected", () => {
    const plain = Array.from({ length: 6 }, () =>
      makeMarker({ venueName: "Busy" }),
    );
    const savedHeavy = Array.from({ length: 6 }, () =>
      makeMarker({ venueName: "SavedBusy", savedByCurrentUser: true }),
    );

    const [busy] = derivePulseVenues(plain);
    const [savedBusy] = derivePulseVenues(savedHeavy);

    // 6 events, no saved/selected: 0.15 + 0.35 * 1 = 0.5
    expect(busy.intensity).toBeCloseTo(0.5, 5);
    // Same count plus saved: 0.5 + 0.35 = 0.85
    expect(savedBusy.intensity).toBeCloseTo(0.85, 5);
    expect(savedBusy.intensity).toBeGreaterThan(busy.intensity);
  });

  it("caps the venue count and sorts by intensity", () => {
    const markers: EventMapMarkerViewModel[] = [];
    for (let i = 0; i < 20; i += 1) {
      markers.push(
        makeMarker({
          venueName: `Venue ${i}`,
          savedByCurrentUser: i % 2 === 0,
        }),
      );
    }

    const venues = derivePulseVenues(markers, {
      minEventCount: 1,
      maxVenues: 5,
    });
    expect(venues).toHaveLength(5);
    for (let i = 1; i < venues.length; i += 1) {
      expect(venues[i - 1].intensity).toBeGreaterThanOrEqual(
        venues[i].intensity,
      );
    }
  });

  it("returns an empty list for empty input", () => {
    expect(derivePulseVenues([])).toEqual([]);
  });

  it("ignores markers with blank venue names", () => {
    const markers = [makeMarker({ venueName: "   " })];
    expect(derivePulseVenues(markers, { minEventCount: 1 })).toEqual([]);
  });
});

describe("zone prototype geometry", () => {
  it("covers every canonical district code", () => {
    const districts = new Set(ZONE_PROTOTYPE_POLYGONS.map((z) => z.district));
    for (const code of DISCOVERY_CANONICAL_DISTRICT_CODES) {
      expect(districts.has(code)).toBe(true);
    }
  });

  it("labels every polygon as prototype geometry", () => {
    expect(ZONE_PROTOTYPE_POLYGONS.length).toBeGreaterThan(0);
    for (const polygon of ZONE_PROTOTYPE_POLYGONS) {
      expect(polygon.provenance).toBe("prototype");
      // Closed ring.
      const ring = polygon.ring;
      expect(ring.length).toBeGreaterThanOrEqual(4);
      expect(ring[0]).toEqual(ring[ring.length - 1]);
    }
  });

  it("carries an explicit provenance note for the UI legend", () => {
    expect(ZONE_PROTOTYPE_PROVENANCE_NOTE.length).toBeGreaterThan(0);
    expect(ZONE_PROTOTYPE_PROVENANCE_NOTE.toLowerCase()).toContain("prototype");
  });

  it("builds a feature collection keyed by canonical district codes", () => {
    const collection = toZoneFeatureCollection(
      ZONE_PROTOTYPE_POLYGONS,
      "mission",
    );

    expect(collection.type).toBe("FeatureCollection");
    expect(collection.features).toHaveLength(ZONE_PROTOTYPE_POLYGONS.length);

    const mission = collection.features.find(
      (feature) => feature.properties.district === "mission",
    );
    expect(mission?.properties.active).toBe(true);
    expect(mission?.properties.provenance).toBe("prototype");
    expect(mission?.geometry.type).toBe("Polygon");

    const soma = collection.features.find(
      (feature) => feature.properties.district === "soma",
    );
    expect(soma?.properties.active).toBe(false);
  });
});
