import { computed, ref } from "vue";
import type {
  EventMapFeedQueryDto,
  EventMapItemDto,
  EventMapMarkerViewModel,
} from "../contracts/map-feed.contracts";
import { fetchEventMapFeed } from "../services/mapFeedService";

export function useMapEvents() {
  const events = ref<EventMapItemDto[]>([]);
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

  return {
    events,
    visibleEvents,
    markers,
    isLoading,
    error,
    selectedEventId,
    loadEvents,
    selectByEventId,
  };
}
