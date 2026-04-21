import { computed, ref, type Ref } from "vue";
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

export function useMapEvents(selectedEventId: Ref<string | null>) {
  const events = ref<EventMapItemDto[]>([]);
  const clusters = ref<EventMapFeedClusterDto[]>([]);
  const densityControl = ref<EventMapDensityControlDto>(
    DEFAULT_DENSITY_CONTROL,
  );
  const clusterStrategy = ref<EventMapClusterStrategy>("client_v1");
  const isLoading = ref(false);
  const error = ref<string | null>(null);
  let latestRequestId = 0;

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
    const requestId = ++latestRequestId;
    isLoading.value = true;
    error.value = null;

    try {
      const payload = await fetchEventMapFeed(query);
      if (requestId !== latestRequestId) {
        return;
      }

      events.value = payload.events ?? [];
      clusters.value = payload.clusters ?? [];
      densityControl.value = payload.densityControl ?? DEFAULT_DENSITY_CONTROL;
      clusterStrategy.value = payload.clusterStrategy ?? "client_v1";
    } catch (e) {
      if (requestId !== latestRequestId) {
        return;
      }

      error.value =
        e instanceof Error ? e.message : "Failed to load map events.";
    } finally {
      if (requestId === latestRequestId) {
        isLoading.value = false;
      }
    }
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
    loadEvents,
    setSavedStateByEventId,
  };
}
