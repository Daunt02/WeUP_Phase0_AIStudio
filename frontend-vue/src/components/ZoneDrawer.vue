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
  <div
    class="zone-legend weup-animatable"
    :class="{ 'is-collapsed': !visible }"
    :data-state="zoneLegendState"
    :data-plane="visible ? 'z3' : 'z2'"
  >
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
        class="zone-chip weup-animatable"
        :class="{ 'is-active': zone.district === activeDistrict }"
        :aria-pressed="zone.district === activeDistrict"
        data-plane="z2"
        :data-state="zone.district === activeDistrict ? 'selected' : 'visible'"
        @click="onZoneTap(zone.district)"
      >
        {{ zone.district }}
      </button>
    </div>

    <!-- WEUP-2.5D (D08): corridor topology. Directional paths between district
         nodes derived from the prototype zone centroids (see script note).
         Tapping a node reuses the canonical zone-selected emit — corridor
         visuals never create execution paths. -->
    <div v-if="visible" class="zone-corridors">
      <svg
        class="zone-corridors-svg"
        :viewBox="`0 0 ${CORRIDOR_VIEW_W} ${CORRIDOR_VIEW_H}`"
        role="img"
        aria-label="District corridor topology (prototype)"
      >
        <defs>
          <marker
            id="weup-corridor-arrow"
            viewBox="0 0 10 10"
            refX="8"
            refY="5"
            markerWidth="6"
            markerHeight="6"
            orient="auto-start-reverse"
          >
            <path d="M 0 1 L 9 5 L 0 9 z" class="weup-corridor-arrowhead" />
          </marker>
          <marker
            id="weup-corridor-arrow-active"
            viewBox="0 0 10 10"
            refX="8"
            refY="5"
            markerWidth="6"
            markerHeight="6"
            orient="auto-start-reverse"
          >
            <path d="M 0 1 L 9 5 L 0 9 z" class="weup-corridor-arrowhead-active" />
          </marker>
        </defs>
        <g
          v-for="edge in corridorGraph.edges"
          :key="edge.id"
          class="weup-corridor"
          :class="{ 'is-active': corridorEdgeState(edge) === 'active' }"
          :data-state="corridorEdgeState(edge)"
        >
          <path
            :d="edge.path"
            class="weup-corridor-path"
            :marker-end="corridorMarker(edge)"
          />
          <text
            :x="edge.labelX"
            :y="edge.labelY"
            class="weup-corridor-label"
            text-anchor="middle"
          >
            {{ edge.name }}
          </text>
        </g>
        <g
          v-for="node in corridorGraph.nodes"
          :key="node.district"
          class="weup-corridor-node"
          :data-state="node.district === activeDistrict ? 'selected' : 'visible'"
        >
          <circle :cx="node.x" :cy="node.y" r="4" class="weup-corridor-node-dot" />
          <text
            :x="node.x"
            :y="node.y + 13"
            class="weup-corridor-node-label"
            text-anchor="middle"
          >
            {{ node.district }}
          </text>
        </g>
      </svg>
      <p class="zone-corridors-note">
        Corridor topology is illustrative (prototype geometry); corridor names
        are the mission vocabulary mapped deterministically to district pairs.
      </p>
    </div>

    <p v-if="visible" class="zone-provenance-note">
      {{ provenanceNote }} — tap a zone to filter by district.
    </p>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, watch } from "vue";
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

/* ── WEUP-2.5D (D07): legend state hook ─────────────────────────────────────
   Closed = DISCOVERED (topology known, layer hidden); open = VISIBLE;
   an active district filter = SELECTED (spans Z2/Z3 per 03-depth-system). */
const zoneLegendState = computed(() =>
  props.activeDistrict ? "selected" : props.visible ? "visible" : "discovered",
);

/* ── WEUP-2.5D (D08): corridor topology ────────────────────────────────────
   The corridor graph is DERIVED from the prototype zone geometry already in
   mapWorld.ts (labelLng/labelLat centroids): each district connects to its
   two nearest neighbors; unordered pairs are deduplicated and sorted.
   Corridor names are the mission's vocabulary, assigned deterministically
   by sorted pair order (index % 5). Topology is illustrative — the same
   prototype honesty as the zone polygons — while zone identity and
   tap-to-filter stay canonical. No invented data, no new execution paths:
   corridor taps reuse the existing zone-selected emit. */

const CORRIDOR_NAMES = [
  "TECHNO / BASEMENT",
  "RETRO / REWIND",
  "AMBIENT / CHILL",
  "HOUSE / DEEP",
  "EXPERIMENTAL",
] as const;

const CORRIDOR_VIEW_W = 240;
const CORRIDOR_VIEW_H = 150;
const CORRIDOR_PAD = 22;

interface CorridorNode {
  district: string;
  x: number;
  y: number;
}

interface CorridorEdge {
  id: string;
  a: CorridorNode;
  b: CorridorNode;
  name: string;
  path: string;
  labelX: number;
  labelY: number;
}

function projectCorridorPoint(lng: number, lat: number): { x: number; y: number } {
  const lngs = zones.map((zone) => zone.labelLng);
  const lats = zones.map((zone) => zone.labelLat);
  const minLng = Math.min(...lngs);
  const maxLng = Math.max(...lngs);
  const minLat = Math.min(...lats);
  const maxLat = Math.max(...lats);
  const spanLng = Math.max(maxLng - minLng, 1e-9);
  const spanLat = Math.max(maxLat - minLat, 1e-9);
  return {
    x:
      CORRIDOR_PAD +
      ((lng - minLng) / spanLng) * (CORRIDOR_VIEW_W - CORRIDOR_PAD * 2),
    y:
      CORRIDOR_VIEW_H -
      CORRIDOR_PAD -
      ((lat - minLat) / spanLat) * (CORRIDOR_VIEW_H - CORRIDOR_PAD * 2),
  };
}

const corridorGraph = computed<{ nodes: CorridorNode[]; edges: CorridorEdge[] }>(
  () => {
    const nodes: CorridorNode[] = zones.map((zone) => {
      const point = projectCorridorPoint(zone.labelLng, zone.labelLat);
      return { district: zone.district, x: point.x, y: point.y };
    });
    const pairKeys = new Set<string>();
    for (const node of nodes) {
      const nearest = nodes
        .filter((other) => other.district !== node.district)
        .map((other) => ({
          other,
          dist: Math.hypot(other.x - node.x, other.y - node.y),
        }))
        .sort((m, n) => m.dist - n.dist)
        .slice(0, 2);
      for (const { other } of nearest) {
        pairKeys.add([node.district, other.district].sort().join("~"));
      }
    }
    const byDistrict = new Map(nodes.map((node) => [node.district, node]));
    const edges: CorridorEdge[] = [...pairKeys].sort().map((key, index) => {
      const [districtA, districtB] = key.split("~");
      const a = byDistrict.get(districtA ?? "") as CorridorNode;
      const b = byDistrict.get(districtB ?? "") as CorridorNode;
      const midX = (a.x + b.x) / 2;
      const midY = (a.y + b.y) / 2;
      const dx = b.x - a.x;
      const dy = b.y - a.y;
      const len = Math.max(Math.hypot(dx, dy), 1e-9);
      const bow = Math.min(14, len * 0.18);
      const cx = midX + (-dy / len) * bow;
      const cy = midY + (dx / len) * bow;
      return {
        id: `corridor-${districtA}-${districtB}`,
        a,
        b,
        name: CORRIDOR_NAMES[index % CORRIDOR_NAMES.length] as string,
        path: `M ${a.x.toFixed(1)} ${a.y.toFixed(1)} Q ${cx.toFixed(1)} ${cy.toFixed(1)} ${b.x.toFixed(1)} ${b.y.toFixed(1)}`,
        labelX: cx,
        labelY: cy - 4,
      };
    });
    return { nodes, edges };
  },
);

/** Corridor activation state: an edge is active when the canonical district
    filter touches either endpoint. Intensity/activation derive from the
    existing activeDistrict prop — nothing invented. */
function corridorEdgeState(edge: CorridorEdge): "active" | "visible" {
  if (!props.activeDistrict) {
    return "visible";
  }
  return edge.a.district === props.activeDistrict ||
    edge.b.district === props.activeDistrict
    ? "active"
    : "visible";
}

function corridorMarker(edge: CorridorEdge): string {
  return corridorEdgeState(edge) === "active"
    ? "url(#weup-corridor-arrow-active)"
    : "url(#weup-corridor-arrow)";
}

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
  z-index: var(--weup-z-1);
  max-width: 240px;
  background: rgba(0, 0, 0, 0.8);
  backdrop-filter: blur(var(--weup-blur-background));
  -webkit-backdrop-filter: blur(var(--weup-blur-background));
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: var(--weup-radius-surface);
  padding: 8px;
  /* WEUP-2.5D (D07/D08): elevation comes from the global [data-plane] rule
     (z2 closed / z3 open); state glow from the global [data-state] rule. */
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
  border-radius: var(--weup-radius-surface);
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
  border-radius: var(--weup-radius-pill);
  background: var(--weup-glow-signal-color);
  box-shadow: var(--weup-glow-signal);
  flex-shrink: 0;
}

.zone-prototype-badge {
  margin-left: auto;
  padding: 2px 8px;
  border-radius: var(--weup-radius-pill);
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
  border-radius: var(--weup-radius-pill);
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  color: rgba(255, 255, 255, 0.6);
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  cursor: pointer;
  transition:
    background-color var(--weup-motion-focus-duration) var(--weup-motion-easing),
    color var(--weup-motion-focus-duration) var(--weup-motion-easing),
    border-color var(--weup-motion-focus-duration) var(--weup-motion-easing);
}

.zone-chip:hover {
  color: #ffffff;
  background: rgba(255, 255, 255, 0.1);
}

.zone-chip.is-active {
  background: var(--weup-glow-signal-color);
  border-color: var(--weup-glow-signal-color);
  color: #000000;
  box-shadow: var(--weup-glow-signal);
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

/* ── WEUP-2.5D (D08): corridor topology ───────────────────────────────────
   Directional paths between district nodes. Corridor = topology made
   visible: source → connection → destination, with intensity (active vs
   idle), activation (district filter), and selected (node) states — all
   from the token substrate. SVG only; no WebGL. */
.zone-corridors {
  margin-top: 4px;
  padding: 8px 4px 2px;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
}

.zone-corridors-svg {
  display: block;
  width: 100%;
  height: auto;
}

.weup-corridor-path {
  fill: none;
  stroke: rgba(255, 255, 255, 0.35);
  stroke-width: 1.5;
}

.weup-corridor[data-state="active"] .weup-corridor-path {
  stroke: var(--weup-glow-signal-color);
  stroke-width: 2.5;
}

.weup-corridor-arrowhead {
  fill: rgba(255, 255, 255, 0.35);
}

.weup-corridor-arrowhead-active {
  fill: var(--weup-glow-signal-color);
}

.weup-corridor-label {
  font-size: 7px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  fill: rgba(255, 255, 255, 0.55);
}

.weup-corridor[data-state="active"] .weup-corridor-label {
  fill: #ffffff;
  font-weight: 700;
}

.weup-corridor-node-dot {
  fill: rgba(255, 255, 255, 0.5);
}

.weup-corridor-node[data-state="selected"] .weup-corridor-node-dot {
  fill: var(--weup-glow-signal-color);
}

.weup-corridor-node-label {
  font-size: 7.5px;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  fill: rgba(255, 255, 255, 0.6);
}

.zone-corridors-note {
  margin: 4px;
  font-size: 9px;
  line-height: 1.4;
  color: rgba(255, 255, 255, 0.4);
}
</style>
