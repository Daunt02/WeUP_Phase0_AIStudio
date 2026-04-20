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
import "mapbox-gl/dist/mapbox-gl.css";

import type {
  EventMapFeedQueryDto,
  EventMapMarkerViewModel,
} from "../contracts/map-feed.contracts";
import { useMapEvents } from "../composables/useMapEvents";

const MAP_SOURCE_ID = "weup-events";
const MAP_LAYER_ID = "weup-event-markers";

const mapContainer = ref<HTMLElement | null>(null);
const map = ref<any>(null);

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
  isLoading,
  error,
  selectedEventId,
  loadEvents,
  selectByEventId,
} = useMapEvents();

const token =
  (import.meta.env.VITE_MAPBOX_ACCESS_TOKEN as string | undefined) ?? "";
const tokenMissing = computed(() => token.trim().length === 0);

const emits = defineEmits<{
  (event: "event-selected", eventId: string): void;
}>();

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
): GeoJSON.FeatureCollection<GeoJSON.Point> {
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

function ensureLayer(currentMap: any): void {
  if (!currentMap.getSource(MAP_SOURCE_ID)) {
    currentMap.addSource(MAP_SOURCE_ID, {
      type: "geojson",
      data: toFeatureCollection([]),
    });
  }

  if (!currentMap.getLayer(MAP_LAYER_ID)) {
    currentMap.addLayer({
      id: MAP_LAYER_ID,
      type: "circle",
      source: MAP_SOURCE_ID,
      paint: {
        "circle-radius": [
          "case",
          ["==", ["get", "effectiveMarkerState"], "selected"],
          9,
          ["==", ["get", "effectiveMarkerState"], "saved"],
          7,
          6,
        ],
        "circle-color": [
          "case",
          ["==", ["get", "effectiveMarkerState"], "selected"],
          "#0B6E4F",
          ["==", ["get", "effectiveMarkerState"], "saved"],
          "#C44536",
          "#2563EB",
        ],
        "circle-stroke-color": "#FFFFFF",
        "circle-stroke-width": 1.5,
      },
    });
  }
}

function updateMapSource(items: EventMapMarkerViewModel[]): void {
  const currentMap = map.value;
  if (!currentMap) {
    return;
  }

  const source = currentMap.getSource(MAP_SOURCE_ID) as
    | GeoJSONSource
    | undefined;
  if (!source) {
    return;
  }

  source.setData(toFeatureCollection(items));
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

function onMapMarkerClick(evt: any): void {
  const currentMap = map.value;
  if (!currentMap) {
    return;
  }

  const features = currentMap.queryRenderedFeatures(evt.point, {
    layers: [MAP_LAYER_ID],
  });

  const eventId = features[0]?.properties?.eventId as string | undefined;
  if (!eventId) {
    return;
  }

  // Selection invariant: resolve marker interaction using canonical eventId only.
  selectByEventId(eventId);
  emits("event-selected", eventId);
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
    ensureLayer(currentMap);
    await refreshFromCurrentViewport();
  });

  currentMap.on("click", MAP_LAYER_ID, onMapMarkerClick);

  currentMap.on("moveend", async () => {
    await refreshFromCurrentViewport();
  });

  map.value = currentMap;
});

watch(markers, (nextMarkers: EventMapMarkerViewModel[]) => {
  updateMapSource(nextMarkers);
});

watch(selectedEventId, () => {
  updateMapSource(markers.value);
});

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
