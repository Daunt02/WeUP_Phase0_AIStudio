<template>
  <q-card flat bordered class="map-surface">
    <q-card-section class="controls-row">
      <q-btn-toggle
        v-model="timeWindowPreset"
        unelevated
        toggle-color="primary"
        :options="presetOptions"
      />
      <q-toggle v-model="includeSavedOnly" label="Saved only" color="primary" />
      <q-btn
        color="primary"
        :loading="isLoading"
        label="Refresh"
        @click="refreshFromCurrentViewport"
      />
    </q-card-section>

    <q-separator />

    <q-card-section class="map-body">
      <div ref="mapContainer" class="map-canvas" />

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

import type {
  EventMapDensityControlDto,
  EventMapFeedClusterDto,
  EventMapFeedQueryDto,
  EventMapMarkerViewModel,
} from "../contracts/map-feed.contracts";
import { useMapEvents } from "../composables/useMapEvents";

const props = withDefaults(
  defineProps<{
    selectedEventId?: string | null;
    selectedEventSavedState?: boolean | null;
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

const mapContainer = ref<HTMLElement | null>(null);
const map = ref<any>(null);
const currentZoom = ref(11);
const clusterSourceConfigKey = ref<string | null>(null);

const timeWindowPreset =
  ref<EventMapFeedQueryDto["timeWindowPreset"]>("next7days");
const includeSavedOnly = ref(false);

const presetOptions: {
  label: string;
  value: NonNullable<EventMapFeedQueryDto["timeWindowPreset"]>;
}[] = [
  { label: "Today", value: "today" },
  { label: "Tonight", value: "tonight" },
  { label: "Weekend", value: "weekend" },
  { label: "7 Days", value: "next7days" },
];

const {
  markers,
  clusters,
  densityControl,
  clusterStrategy,
  isLoading,
  error,
  selectedEventId,
  loadEvents,
  selectByEventId,
  setSavedStateByEventId,
} = useMapEvents();

const token =
  (import.meta.env.VITE_MAPBOX_ACCESS_TOKEN as string | undefined) ?? "";
const tokenMissing = computed(() => token.trim().length === 0);

const emits = defineEmits<{
  (event: "event-selected", eventId: string): void;
  (event: "update:selectedEventId", eventId: string | null): void;
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
}

function emitSelection(eventId: string): void {
  selectByEventId(eventId);
  emits("update:selectedEventId", eventId);
  emits("event-selected", eventId);
}

function onSingleMarkerClick(evt: any): void {
  const eventId = evt.features?.[0]?.properties?.eventId as string | undefined;
  if (!eventId) {
    return;
  }

  // Selection invariant: resolve marker interaction using canonical eventId only.
  emitSelection(eventId);
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

  const query: EventMapFeedQueryDto = {
    bbox: toBboxString(currentMap),
    timeWindowPreset: timeWindowPreset.value,
    includeSavedOnly: includeSavedOnly.value,
  };

  await loadEvents(query);
}

onMounted(async () => {
  if (tokenMissing.value || !mapContainer.value) {
    return;
  }

  mapboxgl.accessToken = token;

  const currentMap = new mapboxgl.Map({
    container: mapContainer.value,
    style: "mapbox://styles/mapbox/light-v11",
    center: [-95.3698, 29.7604],
    zoom: 11,
  });

  currentMap.addControl(
    new mapboxgl.NavigationControl({ showCompass: true }),
    "top-right",
  );

  currentMap.on("load", async () => {
    currentZoom.value = currentMap.getZoom();
    ensureSources(currentMap);
    ensureLayers(currentMap);
    updateMapRendering();
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
  () => props.selectedEventId,
  (eventId) => {
    if (eventId !== selectedEventId.value) {
      selectByEventId(eventId ?? null);
    }
  },
  { immediate: true },
);

watch(selectedEventId, (eventId) => {
  // The map owns hit-testing, but the parent owns the canonical selection
  // state. When a refresh removes the selected event, propagate the null up.
  if (eventId === null && props.selectedEventId !== null) {
    emits("update:selectedEventId", null);
  }
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
  [markers, clusters, densityControl, clusterStrategy, selectedEventId],
  () => {
    updateMapRendering();
  },
  { deep: true },
);

onBeforeUnmount(() => {
  const currentMap = map.value;
  if (currentMap) {
    currentMap.remove();
  }
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
