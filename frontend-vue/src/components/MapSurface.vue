<!--
  WEUP-SYNTH:
  sources=[
    components/MapCanvas.tsx,
    components/WorldLayer.tsx,
    components/GeoControls.tsx,
    components/InteractionLayer.tsx
  ]
  destination=frontend-vue/src/components/MapSurface.vue
  mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
  notes=AI Studio world-surface behavior synthesized into the existing map
  architecture. Feed contracts stay canonical: events flow exclusively through
  fetchEventMapFeed; selection resolves by eventId only; server/client cluster
  rendering, saved-state rendering, viewport refresh, and the temporal query
  path are preserved. Carried over from the sources: dark basemap grammar,
  GeolocateControl, hover-ring/press ghost interaction affordances, selected
  marker label, and the pulse/zone layers (VenuePulseLayer, ZoneDrawer).
-->
<template>
  <q-card flat bordered class="map-surface">
    <q-card-section class="controls-row">
      <MapTimeFilterPanel
        :preset="preset"
        :timezone="timezone"
        :custom-start-local="customStartLocal"
        :custom-end-local="customEndLocal"
        :include-saved-only="includeSavedOnly"
        :is-loading="isLoading"
        :validation-error="validationError"
        @update:preset="preset = $event"
        @update:timezone="timezone = $event"
        @update:custom-start-local="customStartLocal = $event"
        @update:custom-end-local="customEndLocal = $event"
        @update:include-saved-only="includeSavedOnly = $event"
        @refresh="refreshFromCurrentViewport"
      />
    </q-card-section>

    <q-separator />

    <q-card-section class="map-body">
      <div ref="mapContainer" class="map-canvas" />

      <!-- WEUP-SYNTH world layers: pulses + district zones, both driven by
           canonical markers and the shared discovery filters. -->
      <VenuePulseLayer v-if="map && !tokenMissing" :map="map" :markers="markers" />
      <ZoneDrawer
        v-if="map && !tokenMissing"
        :map="map"
        :active-district="resolvedActiveFilters.district"
        :visible="zonesVisible"
        @zone-selected="onZoneSelected"
        @update:visible="zonesVisible = $event"
      />

      <q-banner v-if="tokenMissing" class="banner error" rounded>
        Missing VITE_MAPBOX_ACCESS_TOKEN. Map cannot render.
      </q-banner>

      <q-banner v-else-if="error" class="banner error" rounded>
        {{ error }}
      </q-banner>

      <q-banner
        v-else-if="!isLoading && markers.length === 0"
        class="banner info"
        rounded
      >
        No approved events found for the current map viewport and filters.
      </q-banner>
    </q-card-section>
  </q-card>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import mapboxgl, { type GeoJSONSource } from "mapbox-gl";
import type { FeatureCollection, Point } from "geojson";
import "mapbox-gl/dist/mapbox-gl.css";

import MapTimeFilterPanel from "./MapTimeFilterPanel.vue";
import VenuePulseLayer from "./VenuePulseLayer.vue";
import ZoneDrawer from "./ZoneDrawer.vue";
import {
  GHOST_LONG_PRESS_MS,
  resolveBasemapStyle,
} from "./mapWorld";
import type {
  EventMapDensityControlDto,
  EventMapFeedClusterDto,
  EventMapFeedQueryDto,
  EventMapItemDto,
  EventMapMarkerViewModel,
} from "../contracts/map-feed.contracts";
import type { DiscoveryFilterState } from "../composables/useDiscoveryState";
import { useMapFeedFilters } from "../composables/useMapFeedFilters";
import { useMapEvents } from "../composables/useMapEvents";

const props = withDefaults(
  defineProps<{
    selectedEventId?: string | null;
    selectedEventSavedState?: boolean | null;
    activeFilters?: DiscoveryFilterState;
  }>(),
  {
    selectedEventId: null,
    selectedEventSavedState: null,
  },
);

const POINT_SOURCE_ID = "weup-events-points";
const CLUSTER_SOURCE_ID = "weup-events-clustered";
const SELECTED_SOURCE_ID = "weup-events-selected";
const SERVER_CLUSTER_SOURCE_ID = "weup-events-server-clusters";

const PLAIN_MARKER_LAYER_ID = "weup-event-markers";
const CLIENT_SINGLE_LAYER_ID = "weup-event-markers-client-single";
const CLIENT_CLUSTER_LAYER_ID = "weup-event-clusters";
const CLIENT_CLUSTER_COUNT_LAYER_ID = "weup-event-cluster-count";
const SERVER_CLUSTER_LAYER_ID = "weup-event-server-clusters";
const SERVER_CLUSTER_COUNT_LAYER_ID = "weup-event-server-cluster-count";
const SELECTED_MARKER_LAYER_ID = "weup-selected-event-marker";
// WEUP-SYNTH (InteractionLayer): hover ring + selected label affordances on
// the existing sources via mapbox feature rendering (no second state store).
const HOVER_SOURCE_ID = "weup-events-hover";
const HOVER_RING_LAYER_ID = "weup-marker-hover-ring";
const SELECTED_LABEL_LAYER_ID = "weup-selected-event-label";

const mapContainer = ref<HTMLElement | null>(null);
const map = ref<any>(null);
const currentZoom = ref(11);
const clusterSourceConfigKey = ref<string | null>(null);

// WEUP-SYNTH (InteractionLayer): transient interaction state only. Hover and
// ghost previews never write to any store; selection happens exclusively
// through emitSelection() → selectEvent().
const hoveredEventId = ref<string | null>(null);
const zonesVisible = ref(true);

const {
  preset,
  timezone,
  customStartLocal,
  customEndLocal,
  includeSavedOnly,
  validationError,
  requestSignature,
  buildMapFeedQuery,
} = useMapFeedFilters();

const {
  events,
  markers,
  clusters,
  densityControl,
  clusterStrategy,
  isLoading,
  error,
  loadEvents,
  setSavedStateByEventId,
} = useMapEvents(computed(() => props.selectedEventId ?? null));

const selectedEventId = computed(() => props.selectedEventId ?? null);
const resolvedActiveFilters = computed<DiscoveryFilterState>(() => ({
  district: props.activeFilters?.district,
  categories: props.activeFilters?.categories,
  includeSavedOnly:
    props.activeFilters?.includeSavedOnly ?? includeSavedOnly.value,
}));

const token =
  (import.meta.env.VITE_MAPBOX_ACCESS_TOKEN as string | undefined) ?? "";
const tokenMissing = computed(() => token.trim().length === 0);

const emits = defineEmits<{
  (event: "event-selected", eventId: string): void;
  (event: "update:selectedEventId", eventId: string | null): void;
  (event: "map-feed-query-updated", query: EventMapFeedQueryDto): void;
  (event: "map-items-updated", items: EventMapItemDto[]): void;
  (event: "filters-updated", filters: Partial<DiscoveryFilterState>): void;
}>();

type MarkerFeatureProperties = {
  eventId: string;
  title: string;
  venueName: string;
  markerState: EventMapMarkerViewModel["markerState"];
  effectiveMarkerState: EventMapMarkerViewModel["effectiveMarkerState"];
};

type ServerClusterFeatureProperties = {
  clusterId: string;
  count: number;
  savedCount: number;
  eventIdsJson: string;
};

type ClusterSource = GeoJSONSource & {
  getClusterExpansionZoom: (
    clusterId: number,
    callback: (error: Error | null, zoom: number) => void,
  ) => void;
  getClusterLeaves: (
    clusterId: number,
    limit: number,
    offset: number,
    callback: (error: Error | null, leaves: Array<any>) => void,
  ) => void;
};

type ResolvedServerCluster = {
  clusterId: string;
  centerLat: number;
  centerLng: number;
  count: number;
  eventIds: string[];
  savedCount: number;
};

function toBboxString(currentMap: any): string {
  const bounds = currentMap.getBounds();
  if (!bounds) {
    throw new Error("Map bounds are unavailable.");
  }

  // Contract invariant: bbox is minLng,minLat,maxLng,maxLat.
  return [
    bounds.getWest(),
    bounds.getSouth(),
    bounds.getEast(),
    bounds.getNorth(),
  ].join(",");
}

function toFeatureCollection(
  items: EventMapMarkerViewModel[],
): FeatureCollection<Point, MarkerFeatureProperties> {
  return {
    type: "FeatureCollection",
    features: items.map((item) => ({
      type: "Feature",
      id: item.eventId,
      geometry: {
        type: "Point",
        coordinates: [item.longitude, item.latitude],
      },
      properties: {
        eventId: item.eventId,
        title: item.title,
        venueName: item.venueName,
        markerState: item.markerState,
        effectiveMarkerState: item.effectiveMarkerState,
      },
    })),
  };
}

function toServerClusterFeatureCollection(
  items: ResolvedServerCluster[],
): FeatureCollection<Point, ServerClusterFeatureProperties> {
  return {
    type: "FeatureCollection",
    features: items.map((item) => ({
      type: "Feature",
      id: item.clusterId,
      geometry: {
        type: "Point",
        coordinates: [item.centerLng, item.centerLat],
      },
      properties: {
        clusterId: item.clusterId,
        count: item.count,
        savedCount: item.savedCount,
        eventIdsJson: JSON.stringify(item.eventIds),
      },
    })),
  };
}

function getClusterSourceConfigKey(control: EventMapDensityControlDto): string {
  return [control.clusterRadiusPixels, control.clusterMaxZoomInclusive].join(
    ":",
  );
}

function removeLayerIfExists(currentMap: any, layerId: string): void {
  if (currentMap.getLayer(layerId)) {
    currentMap.removeLayer(layerId);
  }
}

function removeSourceIfExists(currentMap: any, sourceId: string): void {
  if (currentMap.getSource(sourceId)) {
    currentMap.removeSource(sourceId);
  }
}

function markerCirclePaint(): Record<string, unknown> {
  return {
    "circle-radius": [
      "case",
      ["==", ["get", "effectiveMarkerState"], "saved"],
      7,
      6,
    ],
    "circle-color": [
      "case",
      ["==", ["get", "effectiveMarkerState"], "saved"],
      "#C44536",
      "#2563EB",
    ],
    "circle-stroke-color": "#FFFFFF",
    "circle-stroke-width": 1.5,
  };
}

function ensureSources(currentMap: any): void {
  if (!currentMap.getSource(POINT_SOURCE_ID)) {
    currentMap.addSource(POINT_SOURCE_ID, {
      type: "geojson",
      data: toFeatureCollection([]),
    });
  }

  if (!currentMap.getSource(SELECTED_SOURCE_ID)) {
    currentMap.addSource(SELECTED_SOURCE_ID, {
      type: "geojson",
      data: toFeatureCollection([]),
    });
  }

  if (!currentMap.getSource(HOVER_SOURCE_ID)) {
    currentMap.addSource(HOVER_SOURCE_ID, {
      type: "geojson",
      data: toFeatureCollection([]),
    });
  }

  if (!currentMap.getSource(SERVER_CLUSTER_SOURCE_ID)) {
    currentMap.addSource(SERVER_CLUSTER_SOURCE_ID, {
      type: "geojson",
      data: toServerClusterFeatureCollection([]),
    });
  }

  const nextConfigKey = getClusterSourceConfigKey(densityControl.value);
  if (clusterSourceConfigKey.value !== nextConfigKey) {
    removeLayerIfExists(currentMap, CLIENT_CLUSTER_COUNT_LAYER_ID);
    removeLayerIfExists(currentMap, CLIENT_CLUSTER_LAYER_ID);
    removeLayerIfExists(currentMap, CLIENT_SINGLE_LAYER_ID);
    removeSourceIfExists(currentMap, CLUSTER_SOURCE_ID);
    clusterSourceConfigKey.value = null;
  }

  if (!currentMap.getSource(CLUSTER_SOURCE_ID)) {
    currentMap.addSource(CLUSTER_SOURCE_ID, {
      type: "geojson",
      data: toFeatureCollection([]),
      cluster: true,
      clusterMaxZoom: densityControl.value.clusterMaxZoomInclusive,
      clusterRadius: densityControl.value.clusterRadiusPixels,
      clusterProperties: {
        savedCount: [
          "+",
          ["case", ["==", ["get", "effectiveMarkerState"], "saved"], 1, 0],
        ],
      },
    });
    clusterSourceConfigKey.value = nextConfigKey;
  }
}

function ensureLayers(currentMap: any): void {
  if (!currentMap.getLayer(PLAIN_MARKER_LAYER_ID)) {
    currentMap.addLayer({
      id: PLAIN_MARKER_LAYER_ID,
      type: "circle",
      source: POINT_SOURCE_ID,
      paint: markerCirclePaint(),
    });
  }

  if (!currentMap.getLayer(CLIENT_CLUSTER_LAYER_ID)) {
    currentMap.addLayer({
      id: CLIENT_CLUSTER_LAYER_ID,
      type: "circle",
      source: CLUSTER_SOURCE_ID,
      filter: ["has", "point_count"],
      paint: {
        "circle-radius": [
          "step",
          ["get", "point_count"],
          18,
          8,
          24,
          20,
          32,
          40,
          42,
        ],
        "circle-color": [
          "case",
          [">", ["coalesce", ["get", "savedCount"], 0], 0],
          "#C44536",
          "#0F766E",
        ],
        "circle-stroke-color": "#FFFFFF",
        "circle-stroke-width": 2,
      },
    });
  }

  if (!currentMap.getLayer(CLIENT_CLUSTER_COUNT_LAYER_ID)) {
    currentMap.addLayer({
      id: CLIENT_CLUSTER_COUNT_LAYER_ID,
      type: "symbol",
      source: CLUSTER_SOURCE_ID,
      filter: ["has", "point_count"],
      layout: {
        "text-field": ["get", "point_count_abbreviated"],
        "text-size": 12,
        "text-font": ["Open Sans Bold", "Arial Unicode MS Bold"],
      },
      paint: {
        "text-color": "#FFFFFF",
      },
    });
  }

  if (!currentMap.getLayer(CLIENT_SINGLE_LAYER_ID)) {
    currentMap.addLayer({
      id: CLIENT_SINGLE_LAYER_ID,
      type: "circle",
      source: CLUSTER_SOURCE_ID,
      filter: ["!", ["has", "point_count"]],
      paint: markerCirclePaint(),
    });
  }

  if (!currentMap.getLayer(SERVER_CLUSTER_LAYER_ID)) {
    currentMap.addLayer({
      id: SERVER_CLUSTER_LAYER_ID,
      type: "circle",
      source: SERVER_CLUSTER_SOURCE_ID,
      paint: {
        "circle-radius": ["step", ["get", "count"], 18, 8, 24, 20, 32, 40, 42],
        "circle-color": [
          "case",
          [">", ["coalesce", ["get", "savedCount"], 0], 0],
          "#C44536",
          "#0F766E",
        ],
        "circle-stroke-color": "#FFFFFF",
        "circle-stroke-width": 2,
      },
    });
  }

  if (!currentMap.getLayer(SERVER_CLUSTER_COUNT_LAYER_ID)) {
    currentMap.addLayer({
      id: SERVER_CLUSTER_COUNT_LAYER_ID,
      type: "symbol",
      source: SERVER_CLUSTER_SOURCE_ID,
      layout: {
        "text-field": ["to-string", ["get", "count"]],
        "text-size": 12,
        "text-font": ["Open Sans Bold", "Arial Unicode MS Bold"],
      },
      paint: {
        "text-color": "#FFFFFF",
      },
    });
  }

  if (!currentMap.getLayer(SELECTED_MARKER_LAYER_ID)) {
    currentMap.addLayer({
      id: SELECTED_MARKER_LAYER_ID,
      type: "circle",
      source: SELECTED_SOURCE_ID,
      paint: {
        "circle-radius": 10,
        "circle-color": "#0B6E4F",
        "circle-stroke-color": "#FFFFFF",
        "circle-stroke-width": 2,
      },
    });
  }

  // WEUP-SYNTH (MapCanvas): selected-event title label under the selected
  // marker, rendered from the same canonical source as the selected ring.
  if (!currentMap.getLayer(SELECTED_LABEL_LAYER_ID)) {
    currentMap.addLayer({
      id: SELECTED_LABEL_LAYER_ID,
      type: "symbol",
      source: SELECTED_SOURCE_ID,
      layout: {
        "text-field": ["get", "title"],
        "text-size": 10,
        "text-transform": "uppercase",
        "text-letter-spacing": 0.2,
        "text-font": ["Open Sans Bold", "Arial Unicode MS Bold"],
        "text-anchor": "top",
        "text-offset": [0, 1.4],
        "text-max-width": 12,
      },
      paint: {
        "text-color": "#FFFFFF",
        "text-halo-color": "rgba(0,0,0,0.9)",
        "text-halo-width": 2,
      },
    });
  }

  // WEUP-SYNTH (InteractionLayer): hover ring affordance. Driven by the
  // transient hoveredEventId ref — never touches selection or feed state.
  if (!currentMap.getLayer(HOVER_RING_LAYER_ID)) {
    currentMap.addLayer({
      id: HOVER_RING_LAYER_ID,
      type: "circle",
      source: HOVER_SOURCE_ID,
      paint: {
        "circle-radius": 9,
        "circle-color": "rgba(0,0,0,0)",
        "circle-stroke-color": "#FFFFFF",
        "circle-stroke-width": 2,
        "circle-stroke-opacity": 0.9,
      },
    });
  }
}

function setSourceData(sourceId: string, data: FeatureCollection<Point>): void {
  const currentMap = map.value;
  if (!currentMap) {
    return;
  }

  const source = currentMap.getSource(sourceId) as GeoJSONSource | undefined;
  if (!source) {
    return;
  }

  source.setData(data);
}

function setLayerVisibility(
  currentMap: any,
  layerId: string,
  visible: boolean,
): void {
  if (!currentMap.getLayer(layerId)) {
    return;
  }

  currentMap.setLayoutProperty(
    layerId,
    "visibility",
    visible ? "visible" : "none",
  );
}

function isDensityClusteringActive(
  control: EventMapDensityControlDto,
): boolean {
  return (
    control.clusteringEnabled &&
    currentZoom.value <= control.activationMaxZoomInclusive
  );
}

function resolveServerClusters(
  items: EventMapFeedClusterDto[],
  allMarkers: EventMapMarkerViewModel[],
  selectedId: string | null,
): ResolvedServerCluster[] {
  const markersById = new Map(
    allMarkers.map((marker: EventMapMarkerViewModel) => [
      marker.eventId,
      marker,
    ]),
  );

  return items
    .map((cluster: EventMapFeedClusterDto) => {
      const memberMarkers = cluster.eventIds
        .filter((eventId: string) => eventId !== selectedId)
        .map((eventId: string) => markersById.get(eventId))
        .filter(
          (marker): marker is EventMapMarkerViewModel => marker !== undefined,
        );

      if (memberMarkers.length <= 1) {
        return null;
      }

      return {
        clusterId: cluster.clusterId,
        centerLat:
          memberMarkers.reduce((sum, marker) => sum + marker.latitude, 0) /
          memberMarkers.length,
        centerLng:
          memberMarkers.reduce((sum, marker) => sum + marker.longitude, 0) /
          memberMarkers.length,
        count: memberMarkers.length,
        eventIds: memberMarkers.map((marker) => marker.eventId),
        savedCount: memberMarkers.filter((marker) => marker.savedByCurrentUser)
          .length,
      } satisfies ResolvedServerCluster;
    })
    .filter((cluster): cluster is ResolvedServerCluster => cluster !== null);
}

function updateMapRendering(): void {
  const currentMap = map.value;
  if (!currentMap || !currentMap.isStyleLoaded()) {
    return;
  }

  ensureSources(currentMap);
  ensureLayers(currentMap);

  const selectedId = selectedEventId.value;
  const selectedMarker =
    selectedId === null
      ? null
      : (markers.value.find((marker) => marker.eventId === selectedId) ?? null);

  const shouldUseDensityClusters = isDensityClusteringActive(
    densityControl.value,
  );
  const serverClustersToRender =
    clusterStrategy.value === "server_v1" && shouldUseDensityClusters
      ? resolveServerClusters(clusters.value, markers.value, selectedId)
      : [];
  const shouldUseServerClusters = serverClustersToRender.length > 0;
  const shouldUseClientClusters =
    clusterStrategy.value === "client_v1" && shouldUseDensityClusters;

  const serverClusteredIds = new Set(
    serverClustersToRender.flatMap((cluster) => cluster.eventIds),
  );

  const unselectedMarkers = markers.value.filter((marker) => {
    if (marker.eventId === selectedId) {
      return false;
    }

    if (shouldUseServerClusters && serverClusteredIds.has(marker.eventId)) {
      return false;
    }

    return true;
  });

  // Identity invariant: the selected event is rendered from its own source so
  // density recomputation never hides the active canonical event.
  setSourceData(POINT_SOURCE_ID, toFeatureCollection(unselectedMarkers));
  setSourceData(CLUSTER_SOURCE_ID, toFeatureCollection(unselectedMarkers));
  setSourceData(
    SELECTED_SOURCE_ID,
    toFeatureCollection(selectedMarker ? [selectedMarker] : []),
  );
  setSourceData(
    SERVER_CLUSTER_SOURCE_ID,
    toServerClusterFeatureCollection(serverClustersToRender),
  );

  // Hover ring follows the transient hover ref; never the selection state.
  const hoveredMarker =
    hoveredEventId.value === null
      ? null
      : (markers.value.find(
          (marker) => marker.eventId === hoveredEventId.value,
        ) ?? null);
  setSourceData(HOVER_SOURCE_ID, toFeatureCollection(hoveredMarker ? [hoveredMarker] : []));

  setLayerVisibility(
    currentMap,
    PLAIN_MARKER_LAYER_ID,
    !shouldUseClientClusters && !shouldUseServerClusters,
  );
  setLayerVisibility(
    currentMap,
    CLIENT_SINGLE_LAYER_ID,
    shouldUseClientClusters,
  );
  setLayerVisibility(
    currentMap,
    CLIENT_CLUSTER_LAYER_ID,
    shouldUseClientClusters,
  );
  setLayerVisibility(
    currentMap,
    CLIENT_CLUSTER_COUNT_LAYER_ID,
    shouldUseClientClusters,
  );
  setLayerVisibility(
    currentMap,
    SERVER_CLUSTER_LAYER_ID,
    shouldUseServerClusters,
  );
  setLayerVisibility(
    currentMap,
    SERVER_CLUSTER_COUNT_LAYER_ID,
    shouldUseServerClusters,
  );
  setLayerVisibility(
    currentMap,
    SELECTED_MARKER_LAYER_ID,
    selectedMarker !== null,
  );
  setLayerVisibility(
    currentMap,
    SELECTED_LABEL_LAYER_ID,
    selectedMarker !== null,
  );
  setLayerVisibility(currentMap, HOVER_RING_LAYER_ID, hoveredMarker !== null);
}

function emitSelection(eventId: string): void {
  emits("update:selectedEventId", eventId);
  emits("event-selected", eventId);
}

function onZoneSelected(district: string | undefined): void {
  // WEUP-SYNTH (ZoneDrawer): zone tap-to-filter routes through the shared
  // discovery filters so zones and the calendar stay in sync. App.vue's
  // filters-updated handler applies this via useDiscoveryState.applyFilters.
  emits("filters-updated", { district });
}

function onSingleMarkerClick(evt: any): void {
  // A long-press that resolved to selection suppresses the synthetic click
  // that may follow it on release.
  if (Date.now() < suppressClickUntil) {
    return;
  }

  const eventId = evt.features?.[0]?.properties?.eventId as string | undefined;
  if (!eventId) {
    return;
  }

  // Selection invariant: resolve marker interaction using canonical eventId only.
  emitSelection(eventId);
}

/*
 * WEUP-SYNTH (InteractionLayer): hover ring + press ghost preview.
 *
 * Hover shows a white ring on the marker under the pointer (desktop).
 * Press-and-hold shows a translucent ghost projection of the canonical
 * marker; releasing resolves to full selection through emitSelection().
 * Ghost previews are visual only — they never write to any store.
 */
const SINGLE_MARKER_LAYER_IDS = [
  PLAIN_MARKER_LAYER_ID,
  CLIENT_SINGLE_LAYER_ID,
  SELECTED_MARKER_LAYER_ID,
];

let pressTimer: ReturnType<typeof setTimeout> | null = null;
let pressStart: {
  x: number;
  y: number;
  eventId: string;
  lngLat: [number, number];
} | null = null;
let ghostMarker: any = null;
let ghostMarkerEventId: string | null = null;
let suppressClickUntil = 0;

function markerFeatureAt(point: {
  x: number;
  y: number;
}): { eventId: string; lngLat: [number, number] } | null {
  const currentMap = map.value;
  if (!currentMap) {
    return null;
  }

  const features = currentMap.queryRenderedFeatures(point, {
    layers: SINGLE_MARKER_LAYER_IDS,
  });
  const feature = features?.[0] as any;
  const eventId = feature?.properties?.eventId as string | undefined;
  const coordinates = feature?.geometry?.coordinates as
    | [number, number]
    | undefined;
  if (!eventId || !coordinates) {
    return null;
  }

  return { eventId, lngLat: coordinates };
}

function hideGhost(): void {
  if (ghostMarker) {
    ghostMarker.remove();
    ghostMarker = null;
  }
  ghostMarkerEventId = null;
}

function showGhost(eventId: string, lngLat: [number, number]): void {
  const currentMap = map.value;
  if (!currentMap) {
    return;
  }

  hideGhost();

  const element = document.createElement("div");
  element.className = "weup-ghost-marker";
  element.setAttribute("aria-hidden", "true");

  ghostMarker = new mapboxgl.Marker({ element, anchor: "bottom" })
    .setLngLat(lngLat)
    .addTo(currentMap);
  ghostMarkerEventId = eventId;
}

function clearPressTimer(): void {
  if (pressTimer !== null) {
    clearTimeout(pressTimer);
    pressTimer = null;
  }
}

function onCanvasPointerDown(evt: PointerEvent): void {
  const currentMap = map.value;
  if (!currentMap) {
    return;
  }

  const rect = currentMap.getCanvas().getBoundingClientRect();
  const hit = markerFeatureAt({
    x: evt.clientX - rect.left,
    y: evt.clientY - rect.top,
  });
  if (!hit) {
    return;
  }

  pressStart = { x: evt.clientX, y: evt.clientY, ...hit };
  clearPressTimer();
  pressTimer = setTimeout(() => {
    pressTimer = null;
    if (pressStart) {
      showGhost(pressStart.eventId, pressStart.lngLat);
    }
  }, GHOST_LONG_PRESS_MS);
}

function onCanvasPointerMove(evt: PointerEvent): void {
  if (!pressStart) {
    return;
  }

  const moved =
    Math.abs(evt.clientX - pressStart.x) > 12 ||
    Math.abs(evt.clientY - pressStart.y) > 12;
  if (moved) {
    // A drag is a viewport gesture, not a press — cancel the ghost.
    clearPressTimer();
    hideGhost();
    pressStart = null;
  }
}

function onCanvasPointerUp(): void {
  clearPressTimer();
  const start = pressStart;
  const ghostEventId = ghostMarkerEventId;
  pressStart = null;
  hideGhost();

  if (ghostEventId !== null && start && start.eventId === ghostEventId) {
    // Ghost preview resolves to full selection on release.
    suppressClickUntil = Date.now() + 350;
    emitSelection(start.eventId);
  }
}

function onCanvasPointerLeave(): void {
  clearPressTimer();
  pressStart = null;
  hideGhost();
}

function bindMarkerHover(currentMap: any): void {
  for (const layerId of SINGLE_MARKER_LAYER_IDS) {
    currentMap.on("mousemove", layerId, (evt: any) => {
      const eventId = evt.features?.[0]?.properties?.eventId as
        | string
        | undefined;
      if (eventId && eventId !== hoveredEventId.value) {
        hoveredEventId.value = eventId;
      }
    });
    currentMap.on("mouseleave", layerId, () => {
      if (hoveredEventId.value !== null) {
        hoveredEventId.value = null;
      }
    });
  }

  const canvas = currentMap.getCanvas() as HTMLElement;
  canvas.addEventListener("pointerdown", onCanvasPointerDown);
  canvas.addEventListener("pointermove", onCanvasPointerMove);
  canvas.addEventListener("pointerup", onCanvasPointerUp);
  canvas.addEventListener("pointerleave", onCanvasPointerLeave);
}

function fitMapToEvents(
  items: EventMapMarkerViewModel[],
  fallbackCenter?: [number, number],
): void {
  const currentMap = map.value;
  if (!currentMap) {
    return;
  }

  if (items.length === 0) {
    if (fallbackCenter) {
      currentMap.easeTo({
        center: fallbackCenter,
        zoom: Math.min(
          densityControl.value.clusterMaxZoomInclusive + 1,
          currentMap.getZoom() + 1,
        ),
      });
    }
    return;
  }

  if (items.length === 1) {
    currentMap.easeTo({
      center: [items[0].longitude, items[0].latitude],
      zoom: Math.min(
        densityControl.value.clusterMaxZoomInclusive + 1,
        currentMap.getZoom() + 1.5,
      ),
    });
    return;
  }

  const bounds = new mapboxgl.LngLatBounds();
  for (const item of items) {
    bounds.extend([item.longitude, item.latitude]);
  }

  currentMap.fitBounds(bounds, {
    padding: 72,
    maxZoom: densityControl.value.clusterMaxZoomInclusive + 1,
  });
}

function onClientClusterClick(evt: any): void {
  const currentMap = map.value;
  if (!currentMap) {
    return;
  }

  const clusterId = evt.features?.[0]?.properties?.cluster_id as
    | number
    | undefined;
  const coordinates = evt.features?.[0]?.geometry?.coordinates as
    | [number, number]
    | undefined;

  if (clusterId === undefined || !coordinates) {
    return;
  }

  const source = currentMap.getSource(CLUSTER_SOURCE_ID) as
    | ClusterSource
    | undefined;
  if (!source) {
    return;
  }

  // Expansion rule: prefer the deterministic cluster expansion zoom, then fall
  // back to fitting cluster leaves when the source is already at that zoom.
  source.getClusterExpansionZoom(clusterId, (error, expansionZoom) => {
    if (error || typeof expansionZoom !== "number") {
      return;
    }

    if (expansionZoom > currentMap.getZoom() + 0.25) {
      currentMap.easeTo({ center: coordinates, zoom: expansionZoom });
      return;
    }

    source.getClusterLeaves(clusterId, 25, 0, (leavesError, leaves) => {
      if (leavesError || !Array.isArray(leaves)) {
        currentMap.easeTo({
          center: coordinates,
          zoom: Math.min(
            densityControl.value.clusterMaxZoomInclusive + 1,
            currentMap.getZoom() + 1,
          ),
        });
        return;
      }

      const leafEventIds = leaves
        .map((leaf) => leaf?.properties?.eventId as string | undefined)
        .filter((eventId): eventId is string => Boolean(eventId));
      const memberMarkers = markers.value.filter((marker) =>
        leafEventIds.includes(marker.eventId),
      );
      fitMapToEvents(memberMarkers, coordinates);
    });
  });
}

function onServerClusterClick(evt: any): void {
  const properties = evt.features?.[0]?.properties as
    | ServerClusterFeatureProperties
    | undefined;
  const coordinates = evt.features?.[0]?.geometry?.coordinates as
    | [number, number]
    | undefined;

  if (!properties) {
    return;
  }

  let eventIds: string[] = [];
  try {
    eventIds = JSON.parse(properties.eventIdsJson) as string[];
  } catch {
    eventIds = [];
  }

  const memberMarkers = markers.value.filter((marker) =>
    eventIds.includes(marker.eventId),
  );

  fitMapToEvents(memberMarkers, coordinates);
}

async function refreshFromCurrentViewport(): Promise<void> {
  const currentMap = map.value;
  if (!currentMap || tokenMissing.value) {
    return;
  }

  if (validationError.value) {
    return;
  }

  const baseQuery: EventMapFeedQueryDto = buildMapFeedQuery(
    toBboxString(currentMap),
  );
  const query: EventMapFeedQueryDto = {
    ...baseQuery,
    district: resolvedActiveFilters.value.district,
    categories: resolvedActiveFilters.value.categories,
    includeSavedOnly: resolvedActiveFilters.value.includeSavedOnly,
  };

  emits("map-feed-query-updated", query);

  await loadEvents(query);
}

onMounted(async () => {
  if (tokenMissing.value || !mapContainer.value) {
    return;
  }

  mapboxgl.accessToken = token;

  // WEUP-SYNTH (WorldLayer): dark basemap style grammar. The token contract
  // (VITE_MAPBOX_ACCESS_TOKEN) and the missing-token banner are unchanged.
  // An optional VITE_MAPBOX_STYLE_URL env override replaces the default.
  const basemapStyle = resolveBasemapStyle(
    import.meta.env.VITE_MAPBOX_STYLE_URL as string | undefined,
  );

  const currentMap = new mapboxgl.Map({
    container: mapContainer.value,
    style: basemapStyle,
    // Garden Vue audit: default viewport must match the shipped seed dataset's
    // market (San Francisco). Houston was showing an empty market on first paint.
    center: [-122.4194, 37.7749],
    zoom: 11,
  });

  currentMap.addControl(
    new mapboxgl.NavigationControl({ showCompass: true }),
    "top-right",
  );

  // WEUP-SYNTH (GeoControls): locate-me joins the existing control stack.
  // Device-local geolocation only; the map moveend handler refetches the
  // viewport feed, so no new data path is introduced.
  currentMap.addControl(
    new mapboxgl.GeolocateControl({
      positionOptions: { enableHighAccuracy: true, timeout: 6000 },
      trackUserLocation: false,
      showUserLocation: true,
      showAccuracyCircle: true,
    }),
    "top-right",
  );

  currentMap.on("load", async () => {
    currentZoom.value = currentMap.getZoom();
    ensureSources(currentMap);
    ensureLayers(currentMap);
    updateMapRendering();
    bindMarkerHover(currentMap);
    await refreshFromCurrentViewport();
  });

  currentMap.on("click", PLAIN_MARKER_LAYER_ID, onSingleMarkerClick);
  currentMap.on("click", CLIENT_SINGLE_LAYER_ID, onSingleMarkerClick);
  currentMap.on("click", SELECTED_MARKER_LAYER_ID, onSingleMarkerClick);
  currentMap.on("click", CLIENT_CLUSTER_LAYER_ID, onClientClusterClick);
  currentMap.on("click", SERVER_CLUSTER_LAYER_ID, onServerClusterClick);

  currentMap.on("moveend", async () => {
    currentZoom.value = currentMap.getZoom();
    updateMapRendering();
    await refreshFromCurrentViewport();
  });

  currentMap.on("zoomend", () => {
    currentZoom.value = currentMap.getZoom();
    updateMapRendering();
  });

  map.value = currentMap;
});

watch(
  () => props.activeFilters?.includeSavedOnly,
  (nextIncludeSavedOnly) => {
    if (
      typeof nextIncludeSavedOnly === "boolean" &&
      nextIncludeSavedOnly !== includeSavedOnly.value
    ) {
      includeSavedOnly.value = nextIncludeSavedOnly;
    }
  },
  { immediate: true },
);

watch(includeSavedOnly, (nextIncludeSavedOnly, previousIncludeSavedOnly) => {
  if (nextIncludeSavedOnly === previousIncludeSavedOnly) {
    return;
  }

  // Anti-loop: this emits a change upward; the parent writes the shared store,
  // then the prop sync above mirrors it back only when the value actually differs.
  emits("filters-updated", { includeSavedOnly: nextIncludeSavedOnly });
});

watch(
  [() => props.selectedEventId, () => props.selectedEventSavedState],
  ([eventId, savedState]) => {
    if (eventId === null || savedState === null) {
      return;
    }

    // Save actions should reflect in the map marker state immediately instead
    // of waiting for the next viewport refresh to round-trip through the API.
    setSavedStateByEventId(eventId, savedState);
  },
  { immediate: true },
);

watch(
  events,
  (nextItems) => {
    // Calendar overlay is a temporal projection of these same canonical events.
    emits("map-items-updated", nextItems);

    if (
      props.selectedEventId !== null &&
      !nextItems.some((item) => item.eventId === props.selectedEventId)
    ) {
      emits("update:selectedEventId", null);
    }
  },
  { deep: true, immediate: true },
);

watch(
  [markers, clusters, densityControl, clusterStrategy, selectedEventId],
  () => {
    updateMapRendering();
  },
  { deep: true },
);

// Hover is transient interaction state; it re-renders the hover ring only
// and never touches selection, feed, or filter state.
watch(hoveredEventId, () => {
  updateMapRendering();
});

watch(
  [
    requestSignature,
    () => resolvedActiveFilters.value.district,
    () => resolvedActiveFilters.value.categories,
  ],
  async () => {
    // Filter changes always produce one canonical request payload. Reload from the
    // current viewport only after that payload is valid. District/category are
    // watched here (not in requestSignature) because they arrive via the
    // discovery filter state, not the temporal filter panel.
    await refreshFromCurrentViewport();
  },
  { deep: true },
);

onBeforeUnmount(() => {
  const currentMap = map.value;
  if (currentMap) {
    const canvas = currentMap.getCanvas() as HTMLElement | undefined;
    if (canvas) {
      canvas.removeEventListener("pointerdown", onCanvasPointerDown);
      canvas.removeEventListener("pointermove", onCanvasPointerMove);
      canvas.removeEventListener("pointerup", onCanvasPointerUp);
      canvas.removeEventListener("pointerleave", onCanvasPointerLeave);
    }
    currentMap.remove();
  }
  clearPressTimer();
  hideGhost();
  map.value = null;
});
</script>

<style scoped>
.map-surface {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
}

.controls-row {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
}

.map-body {
  position: relative;
  flex: 1;
  min-height: 420px;
  padding: 0;
}

.map-canvas {
  width: 100%;
  height: 100%;
  min-height: 420px;
}

.banner {
  position: absolute;
  left: 16px;
  right: 16px;
  bottom: 16px;
  z-index: 10;
}

/*
 * WEUP-SYNTH (GeoControls / G4 control grammar): glass treatment for the
 * native mapbox control stack, matching the dark AI Studio control language.
 */
.map-body :deep(.mapboxgl-ctrl-top-right .mapboxgl-ctrl-group) {
  background: rgba(0, 0, 0, 0.8);
  backdrop-filter: blur(24px);
  -webkit-backdrop-filter: blur(24px);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 16px;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.6);
  overflow: hidden;
}

.map-body :deep(.mapboxgl-ctrl-group button) {
  width: 40px;
  height: 40px;
  background-color: transparent;
}

.map-body :deep(.mapboxgl-ctrl-group button + button) {
  border-top: 1px solid rgba(255, 255, 255, 0.1);
}

/*
 * WEUP-SYNTH (InteractionLayer): ghost preview marker. Translucent dashed
 * projection of a canonical marker shown on press-and-hold; static by
 * design (motion governance: no decorative loops).
 */
.map-body .weup-ghost-marker {
  width: 40px;
  height: 40px;
  border-radius: 9999px;
  background: rgba(255, 255, 255, 0.08);
  border: 2px dashed rgba(255, 255, 255, 0.85);
  box-shadow: 0 0 16px rgba(0, 255, 156, 0.35);
  pointer-events: none;
}

.error {
  background: #ffe5e8;
}

.info {
  background: #ecf4ff;
}

@media (max-width: 768px) {
  .controls-row {
    gap: 8px;
    justify-content: flex-start;
  }

  .map-body,
  .map-canvas {
    min-height: 320px;
  }
}
</style>
