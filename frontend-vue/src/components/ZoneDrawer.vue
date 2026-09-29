<template>
  <!--
    WEUP-SYNTH:
    source=components/ZoneDrawer.tsx
    destination=frontend-vue/src/components/ZoneDrawer.vue
    mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
    notes=District zone overlay as a map layer inside MapSurface. Zone identity
    is the canonical district code; tap-to-filter routes through the shared
    discovery filters (zones and calendar stay in sync). Zone GEOMETRY is
    PROTOTYPE (no zone-geometry backend observed) and is labeled as such in
    code and in the legend. No fabricated event data is rendered.
  -->
  <div class="zone-legend" :class="{ 'is-collapsed': !visible }">
    <button
      type="button"
      class="zone-legend-toggle"
      :aria-expanded="visible"
      :aria-label="visible ? 'Hide district zones' : 'Show district zones'"
      @click="emits('update:visible', !visible)"
    >
      <span class="zone-legend-dot" />
      Zones
      <span class="zone-prototype-badge">prototype</span>
    </button>

    <div v-if="visible" class="zone-chips" role="group" aria-label="District zones">
      <button
        v-for="zone in zones"
        :key="zone.district"
        type="button"
        class="zone-chip"
        :class="{ 'is-active': zone.district === activeDistrict }"
        :aria-pressed="zone.district === activeDistrict"
        @click="onZoneTap(zone.district)"
      >
        {{ zone.district }}
      </button>
    </div>

    <p v-if="visible" class="zone-provenance-note">
      {{ provenanceNote }} — tap a zone to filter by district.
    </p>
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, watch } from "vue";
import {
  WEUP_ACCENT,
  ZONE_PROTOTYPE_POLYGONS,
  ZONE_PROTOTYPE_PROVENANCE_NOTE,
  toZoneFeatureCollection,
} from "./mapWorld";

const ZONE_FILL_SOURCE_ID = "weup-zone-polygons";
const ZONE_FILL_LAYER_ID = "weup-zone-fill";
const ZONE_LINE_LAYER_ID = "weup-zone-boundary";
const ZONE_LABEL_LAYER_ID = "weup-zone-label";

const props = withDefaults(
  defineProps<{
    /** Live mapboxgl.Map instance; zone layers attach once it exists. */
    map?: unknown;
    /** Canonical district code currently applied as a filter. */
    activeDistrict?: string;
    /** Zone layer visibility. */
    visible?: boolean;
  }>(),
  {
    map: undefined,
    activeDistrict: undefined,
    visible: true,
  },
);

const emits = defineEmits<{
  /** Tap-to-filter: district code, or undefined to clear the district filter. */
  (event: "zone-selected", district: string | undefined): void;
  (event: "update:visible", visible: boolean): void;
}>();

const zones = ZONE_PROTOTYPE_POLYGONS;
const provenanceNote = ZONE_PROTOTYPE_PROVENANCE_NOTE;

function getMap(): any {
  const currentMap = props.map as any;
  if (
    !currentMap ||
    typeof currentMap.getLayer !== "function" ||
    typeof currentMap.isStyleLoaded !== "function" ||
    !currentMap.isStyleLoaded()
  ) {
    return null;
  }
  return currentMap;
}

function layerAnchorId(currentMap: any): string | undefined {
  // Zones render above the basemap but below markers.
  return currentMap.getLayer("weup-event-markers")
    ? "weup-event-markers"
    : undefined;
}

function ensureZoneLayers(): void {
  const currentMap = getMap();
  if (!currentMap) {
    return;
  }

  if (!currentMap.getSource(ZONE_FILL_SOURCE_ID)) {
    currentMap.addSource(ZONE_FILL_SOURCE_ID, {
      type: "geojson",
      data: toZoneFeatureCollection(zones, props.activeDistrict),
    });
  }

  const anchor = layerAnchorId(currentMap);

  if (!currentMap.getLayer(ZONE_FILL_LAYER_ID)) {
    currentMap.addLayer(
      {
        id: ZONE_FILL_LAYER_ID,
        type: "fill",
        source: ZONE_FILL_SOURCE_ID,
        paint: {
          "fill-color": [
            "case",
            ["get", "active"],
            WEUP_ACCENT,
            "#FFFFFF",
          ],
          "fill-opacity": ["case", ["get", "active"], 0.14, 0.04],
        },
      },
      anchor,
    );
  }

  if (!currentMap.getLayer(ZONE_LINE_LAYER_ID)) {
    currentMap.addLayer(
      {
        id: ZONE_LINE_LAYER_ID,
        type: "line",
        source: ZONE_FILL_SOURCE_ID,
        paint: {
          "line-color": ["case", ["get", "active"], WEUP_ACCENT, "#FFFFFF"],
          "line-opacity": ["case", ["get", "active"], 0.9, 0.25],
          "line-width": ["case", ["get", "active"], 2, 1],
        },
      },
      anchor,
    );
  }

  if (!currentMap.getLayer(ZONE_LABEL_LAYER_ID)) {
    currentMap.addLayer(
      {
        id: ZONE_LABEL_LAYER_ID,
        type: "symbol",
        source: ZONE_FILL_SOURCE_ID,
        layout: {
          "text-field": ["get", "district"],
          "text-size": 10,
          "text-transform": "uppercase",
          "text-letter-spacing": 0.2,
          "text-font": ["Open Sans Bold", "Arial Unicode MS Bold"],
          "text-anchor": "center",
          "symbol-placement": "point",
        },
        paint: {
          "text-color": "#FFFFFF",
          "text-opacity": 0.65,
          "text-halo-color": "#000000",
          "text-halo-width": 1.5,
        },
      },
      anchor,
    );
  }

  applyVisibility();
  wireZoneClicks();
}

function removeZoneLayers(): void {
  const currentMap = props.map as any;
  if (!currentMap || typeof currentMap.getLayer !== "function") {
    return;
  }
  for (const layerId of [
    ZONE_LABEL_LAYER_ID,
    ZONE_LINE_LAYER_ID,
    ZONE_FILL_LAYER_ID,
  ]) {
    if (currentMap.getLayer(layerId)) {
      currentMap.removeLayer(layerId);
    }
  }
  if (currentMap.getSource(ZONE_FILL_SOURCE_ID)) {
    currentMap.removeSource(ZONE_FILL_SOURCE_ID);
  }
}

function applyVisibility(): void {
  const currentMap = getMap();
  if (!currentMap) {
    return;
  }
  for (const layerId of [
    ZONE_FILL_LAYER_ID,
    ZONE_LINE_LAYER_ID,
    ZONE_LABEL_LAYER_ID,
  ]) {
    if (currentMap.getLayer(layerId)) {
      currentMap.setLayoutProperty(
        layerId,
        "visibility",
        props.visible ? "visible" : "none",
      );
    }
  }
}

function refreshZoneData(): void {
  const currentMap = getMap();
  if (!currentMap) {
    return;
  }
  const source = currentMap.getSource(ZONE_FILL_SOURCE_ID);
  if (source && typeof source.setData === "function") {
    source.setData(toZoneFeatureCollection(zones, props.activeDistrict));
  }
}

let clicksWired = false;

function onZoneFeatureClick(evt: any): void {
  const district = evt.features?.[0]?.properties?.district as
    | string
    | undefined;
  if (!district) {
    return;
  }
  // Toggle: tapping the active zone clears the district filter.
  emits("zone-selected", district === props.activeDistrict ? undefined : district);
}

function wireZoneClicks(): void {
  const currentMap = getMap();
  if (!currentMap || clicksWired) {
    return;
  }
  clicksWired = true;
  currentMap.on("click", ZONE_FILL_LAYER_ID, onZoneFeatureClick);
  currentMap.on("mouseenter", ZONE_FILL_LAYER_ID, () => {
    currentMap.getCanvas().style.cursor = "pointer";
  });
  currentMap.on("mouseleave", ZONE_FILL_LAYER_ID, () => {
    currentMap.getCanvas().style.cursor = "";
  });
}

function onZoneTap(district: string): void {
  // Legend chips share the same toggle semantics as the map polygons.
  emits("zone-selected", district === props.activeDistrict ? undefined : district);
}

watch(
  () => props.map,
  () => {
    clicksWired = false;
    ensureZoneLayers();
  },
);

watch(
  () => props.activeDistrict,
  () => {
    refreshZoneData();
  },
);

watch(
  () => props.visible,
  () => {
    applyVisibility();
  },
);

onBeforeUnmount(() => {
  removeZoneLayers();
  clicksWired = false;
});
</script>

<style scoped>
.zone-legend {
  position: absolute;
  top: 12px;
  left: 12px;
  z-index: 5;
  max-width: 240px;
  background: rgba(0, 0, 0, 0.8);
  backdrop-filter: blur(24px);
  -webkit-backdrop-filter: blur(24px);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 16px;
  padding: 8px;
}

.zone-legend-toggle {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  min-height: 36px;
  padding: 4px 10px;
  background: transparent;
  border: none;
  border-radius: 12px;
  color: #ffffff;
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  cursor: pointer;
}

.zone-legend-toggle:hover {
  background: rgba(255, 255, 255, 0.08);
}

.zone-legend-dot {
  width: 8px;
  height: 8px;
  border-radius: 9999px;
  background: #00ff9c;
  box-shadow: 0 0 8px rgba(0, 255, 156, 0.8);
  flex-shrink: 0;
}

.zone-prototype-badge {
  margin-left: auto;
  padding: 2px 8px;
  border-radius: 9999px;
  background: rgba(251, 191, 36, 0.15);
  border: 1px solid rgba(251, 191, 36, 0.4);
  color: #fbbf24;
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.1em;
  text-transform: uppercase;
}

.zone-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  padding: 8px 4px 4px;
}

.zone-chip {
  padding: 6px 12px;
  min-height: 32px;
  border-radius: 9999px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  color: rgba(255, 255, 255, 0.6);
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  cursor: pointer;
  transition:
    background-color 200ms,
    color 200ms,
    border-color 200ms;
}

.zone-chip:hover {
  color: #ffffff;
  background: rgba(255, 255, 255, 0.1);
}

.zone-chip.is-active {
  background: #00ff9c;
  border-color: #00ff9c;
  color: #000000;
  box-shadow: 0 0 16px rgba(0, 255, 156, 0.5);
}

.zone-provenance-note {
  margin: 6px 4px 2px;
  color: rgba(255, 255, 255, 0.4);
  font-size: 10px;
  line-height: 1.5;
}

.is-collapsed .zone-legend-toggle {
  margin-bottom: 0;
}
</style>
