/*
 * WEUP-SYNTH:
 * sources=[
 *   components/MapCanvas.tsx,
 *   components/WorldLayer.tsx,
 *   components/GeoControls.tsx,
 *   components/InteractionLayer.tsx,
 *   components/VenuePulse.tsx,
 *   components/ZoneDrawer.tsx
 * ]
 * destination=frontend-vue/src/components/mapWorld.ts
 * mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
 * notes=Deterministic world-surface derivations shared by MapSurface,
 * VenuePulseLayer, and ZoneDrawer. Pure functions and constants only —
 * no map instance, no stores, no side effects. Feed contracts remain
 * authoritative; everything here derives from EventMapMarkerViewModel.
 */

import type { CanonicalDistrictCode } from "../contracts/user-context.contracts";
import type { EventMapMarkerViewModel } from "../contracts/map-feed.contracts";

export type { EventMapMarkerViewModel } from "../contracts/map-feed.contracts";

/* ------------------------------------------------------------------ */
/* Basemap (WorldLayer visual grammar)                                 */
/* ------------------------------------------------------------------ */

/**
 * Canonical AI Studio dark basemap style (MapCanvas/WorldLayer grammar).
 * Replaces the previous light-v11 style; the VITE_MAPBOX_ACCESS_TOKEN
 * contract is unchanged and the missing-token banner behavior is unchanged.
 */
export const WEUP_DARK_BASEMAP_STYLE_URL = "mapbox://styles/mapbox/dark-v11";

/**
 * Resolve the active basemap style. An explicit env override wins so the
 * style URL is never hardcoded without an escape hatch (G3).
 */
export function resolveBasemapStyle(styleUrlOverride?: string): string {
  const override = (styleUrlOverride ?? "").trim();
  return override.length > 0 ? override : WEUP_DARK_BASEMAP_STYLE_URL;
}

/* ------------------------------------------------------------------ */
/* Venue pulse derivation (VenuePulse observable contract)             */
/* ------------------------------------------------------------------ */

/**
 * A pulse venue is a deterministic projection of canonical feed markers:
 * centers group by venueName; intensity derives from real fields only
 * (venue event count from the current feed, saved state, selection).
 * No invented energy scores, no mock fields (G3 prototype_isolation).
 */
export interface PulseVenue {
  readonly venueName: string;
  readonly centerLat: number;
  readonly centerLng: number;
  readonly eventCount: number;
  readonly anySaved: boolean;
  readonly anySelected: boolean;
  /** Clamped to [0.1, 1.0]. */
  readonly intensity: number;
}

export interface DerivePulseVenuesOptions {
  /** Max venues to emit (DOM-marker bound). Default 12. */
  readonly maxVenues?: number;
  /**
   * Minimum canonical events at a venue to pulse. Venues below this still
   * pulse when they carry saved or selected state. Default 2.
   */
  readonly minEventCount?: number;
}

export function clampPulseIntensity(value: number): number {
  if (Number.isNaN(value)) {
    return 0.1;
  }
  return Math.min(1.0, Math.max(0.1, value));
}

export function derivePulseVenues(
  markers: readonly EventMapMarkerViewModel[],
  options: DerivePulseVenuesOptions = {},
): PulseVenue[] {
  const maxVenues = options.maxVenues ?? 12;
  const minEventCount = options.minEventCount ?? 2;

  const groups = new Map<string, EventMapMarkerViewModel[]>();
  for (const marker of markers) {
    const name = (marker.venueName ?? "").trim();
    if (name.length === 0) {
      continue;
    }
    const list = groups.get(name);
    if (list) {
      list.push(marker);
    } else {
      groups.set(name, [marker]);
    }
  }

  const venues: PulseVenue[] = [];
  for (const [venueName, items] of groups) {
    const eventCount = items.length;
    const anySaved = items.some((item) => item.savedByCurrentUser);
    const anySelected = items.some(
      (item) => item.effectiveMarkerState === "selected",
    );

    if (eventCount < minEventCount && !anySaved && !anySelected) {
      continue;
    }

    const centerLat =
      items.reduce((sum, item) => sum + item.latitude, 0) / eventCount;
    const centerLng =
      items.reduce((sum, item) => sum + item.longitude, 0) / eventCount;

    const intensity = clampPulseIntensity(
      0.15 +
        0.35 * Math.min(1, eventCount / 6) +
        (anySaved ? 0.35 : 0) +
        (anySelected ? 0.15 : 0),
    );

    venues.push({
      venueName,
      centerLat,
      centerLng,
      eventCount,
      anySaved,
      anySelected,
      intensity,
    });
  }

  venues.sort(
    (a, b) => b.intensity - a.intensity || b.eventCount - a.eventCount,
  );
  return venues.slice(0, Math.max(0, maxVenues));
}

/* ------------------------------------------------------------------ */
/* Zone geometry (ZoneDrawer)                                          */
/* ------------------------------------------------------------------ */

/**
 * Zone identity is the canonical district code
 * (contracts/user-context.contracts.ts DISCOVERY_CANONICAL_DISTRICT_CODES).
 * Zone GEOMETRY is PROTOTYPE: no zone-geometry backend was observed, so these
 * rings are illustrative only and must stay labeled as such in code and in
 * the UI legend until a backend supplies authoritative geometry.
 */
export interface ZonePrototypePolygon {
  readonly district: CanonicalDistrictCode;
  /** Closed ring of [lng, lat]. PROTOTYPE — illustrative only. */
  readonly ring: ReadonlyArray<readonly [number, number]>;
  readonly labelLng: number;
  readonly labelLat: number;
  readonly provenance: "prototype";
}

export const ZONE_PROTOTYPE_PROVENANCE_NOTE =
  "Prototype geometry — illustrative district boundaries only. " +
  "Zone identity is the canonical district code; tap-to-filter is real.";

function rectRing(
  west: number,
  south: number,
  east: number,
  north: number,
): ReadonlyArray<readonly [number, number]> {
  return [
    [west, south],
    [east, south],
    [east, north],
    [west, north],
    [west, south],
  ];
}

export const ZONE_PROTOTYPE_POLYGONS: readonly ZonePrototypePolygon[] = [
  {
    district: "mission",
    ring: rectRing(-122.425, 37.748, -122.405, 37.768),
    labelLng: -122.415,
    labelLat: 37.758,
    provenance: "prototype",
  },
  {
    district: "soma",
    ring: rectRing(-122.415, 37.768, -122.385, 37.792),
    labelLng: -122.4,
    labelLat: 37.78,
    provenance: "prototype",
  },
  {
    district: "north-beach",
    ring: rectRing(-122.418, 37.795, -122.398, 37.812),
    labelLng: -122.408,
    labelLat: 37.8035,
    provenance: "prototype",
  },
  {
    district: "uptown",
    ring: rectRing(-122.435, 37.785, -122.415, 37.805),
    labelLng: -122.425,
    labelLat: 37.795,
    provenance: "prototype",
  },
  {
    district: "jack-london",
    ring: rectRing(-122.345, 37.785, -122.315, 37.8),
    labelLng: -122.33,
    labelLat: 37.7925,
    provenance: "prototype",
  },
  {
    district: "temescal",
    ring: rectRing(-122.275, 37.82, -122.25, 37.84),
    labelLng: -122.2625,
    labelLat: 37.83,
    provenance: "prototype",
  },
];

export function toZoneFeatureCollection(
  polygons: readonly ZonePrototypePolygon[],
  activeDistrict: string | undefined,
): {
  type: "FeatureCollection";
  features: Array<{
    type: "Feature";
    id: string;
    geometry: { type: "Polygon"; coordinates: number[][][] };
    properties: {
      district: string;
      active: boolean;
      provenance: "prototype";
    };
  }>;
} {
  return {
    type: "FeatureCollection",
    features: polygons.map((polygon) => ({
      type: "Feature",
      id: `zone-${polygon.district}`,
      geometry: {
        type: "Polygon",
        coordinates: [polygon.ring.map(([lng, lat]) => [lng, lat])],
      },
      properties: {
        district: polygon.district,
        active: activeDistrict === polygon.district,
        provenance: polygon.provenance,
      },
    })),
  };
}

/* ------------------------------------------------------------------ */
/* Interaction constants (InteractionLayer observable contract)        */
/* ------------------------------------------------------------------ */

/** Press-and-hold duration before a ghost preview appears. */
export const GHOST_LONG_PRESS_MS = 600;

/** Accent used for pulse + active-zone treatment (G4 color.accent). */
export const WEUP_ACCENT = "#00FF9C";
