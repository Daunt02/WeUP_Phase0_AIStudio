import { computed, ref } from "vue";
import type {
  EventMapClusterStrategy,
  EventMapDensityControlDto,
  EventMapFeedClusterDto,
  EventMapFeedQueryDto,
  EventMapItemDto,
  EventMapMarkerViewModel,
} from "../contracts/map-feed.contracts";
import { fetchEventMapFeed } from "../services/mapFeedService";

const DEFAULT_DENSITY_CONTROL: EventMapDensityControlDto = {
  clusteringEnabled: false,
  activationVisibleEventCountThreshold: 24,
  activationMaxZoomInclusive: 13.5,
  clusterRadiusPixels: 56,
  clusterMaxZoomInclusive: 15,
  selectedMarkerBypassEnabled: true,
  expansionBehavior: "zoom_or_expand",
};

export function useMapEvents() {
  const events = ref<EventMapItemDto[]>([]);
  const clusters = ref<EventMapFeedClusterDto[]>([]);
  const densityControl = ref<EventMapDensityControlDto>(
    DEFAULT_DENSITY_CONTROL,
  );
  const clusterStrategy = ref<EventMapClusterStrategy>("client_v1");
  const isLoading = ref(false);
  const error = ref<string | null>(null);
  const selectedEventId = ref<string | null>(null);

  const visibleEvents = computed(() => {
    return events.value.filter(
      (event: EventMapItemDto) => event.markerState !== "low-confidence-hidden",
    );
  });

  const markers = computed<EventMapMarkerViewModel[]>(() => {
    return visibleEvents.value.map((event: EventMapItemDto) => {
      const effectiveMarkerState =
        selectedEventId.value === event.eventId
          ? "selected"
          : event.savedByCurrentUser
            ? "saved"
            : "default";

      return {
        ...event,
        effectiveMarkerState,
      };
    });
  });

  async function loadEvents(query: EventMapFeedQueryDto): Promise<void> {
    isLoading.value = true;
    error.value = null;

    try {
      const payload = await fetchEventMapFeed(query);
      events.value = payload.events ?? [];
      clusters.value = payload.clusters ?? [];
      densityControl.value = payload.densityControl ?? DEFAULT_DENSITY_CONTROL;
      clusterStrategy.value = payload.clusterStrategy ?? "client_v1";

      if (selectedEventId.value) {
        const stillExists = events.value.some(
          (event: EventMapItemDto) => event.eventId === selectedEventId.value,
        );
        if (!stillExists) {
          selectedEventId.value = null;
        }
      }
    } catch (e) {
      events.value = [];
      clusters.value = [];
      densityControl.value = DEFAULT_DENSITY_CONTROL;
      clusterStrategy.value = "client_v1";
      error.value =
        e instanceof Error ? e.message : "Failed to load map events.";
    } finally {
      isLoading.value = false;
    }
  }

  function selectByEventId(eventId: string | null): void {
    if (!eventId) {
      selectedEventId.value = null;
      return;
    }

    const exists = events.value.some(
      (event: EventMapItemDto) => event.eventId === eventId,
    );
    selectedEventId.value = exists ? eventId : null;
  }

  function setSavedStateByEventId(eventId: string, saved: boolean): void {
    let didUpdate = false;

    events.value = events.value.map((event: EventMapItemDto) => {
      if (event.eventId !== eventId) {
        return event;
      }

      didUpdate = true;
      return {
        ...event,
        savedByCurrentUser: saved,
      };
    });

    if (!didUpdate) {
      return;
    }
  }

  return {
    events,
    clusters,
    densityControl,
    clusterStrategy,
    visibleEvents,
    markers,
    isLoading,
    error,
    selectedEventId,
    loadEvents,
    selectByEventId,
    setSavedStateByEventId,
  };
}
