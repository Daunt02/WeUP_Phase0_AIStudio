import { computed, ref, watch, type Ref } from "vue";
import type {
  CalendarEventItemDto,
  CalendarOverlayLayerState,
  EventCalendarFeedQueryDto,
} from "../contracts/calendar-overlay.contracts";
import type {
  EventMapFeedQueryDto,
  EventMapItemDto,
} from "../contracts/map-feed.contracts";

const TRANSITIONS: Record<
  CalendarOverlayLayerState,
  readonly CalendarOverlayLayerState[]
> = {
  closed: ["partial", "expanded", "event-selected"],
  partial: ["closed", "expanded", "event-selected"],
  expanded: ["closed", "partial", "event-selected"],
  "event-selected": ["closed", "partial", "expanded"],
};

function canTransition(
  from: CalendarOverlayLayerState,
  to: CalendarOverlayLayerState,
): boolean {
  return TRANSITIONS[from].includes(to);
}

function transitionOrThrow(
  from: CalendarOverlayLayerState,
  to: CalendarOverlayLayerState,
): CalendarOverlayLayerState {
  if (!canTransition(from, to)) {
    throw new Error(`Invalid calendar overlay transition: ${from} -> ${to}`);
  }
  return to;
}

function toCalendarItem(mapItem: EventMapItemDto): CalendarEventItemDto {
  return {
    eventId: mapItem.eventId,
    title: mapItem.title,
    startUtc: mapItem.startUtc,
    endUtc: mapItem.endUtc,
    timezone: "UTC",
    venueName: mapItem.venueName,
    district: mapItem.district,
    primaryCategory: mapItem.primaryCategory,
    savedByCurrentUser: mapItem.savedByCurrentUser,
    markerState: mapItem.markerState,
    thumbnailUrl: null,
  };
}

export function useCalendarOverlayState(selectedEventId: Ref<string | null>) {
  const layer = ref<CalendarOverlayLayerState>("closed");
  const lastNonSelectedLayer =
    ref<Extract<CalendarOverlayLayerState, "partial" | "expanded">>("partial");

  // Rule: map selection is canonical. When map selects an event, overlay enters
  // event-selected. When map clears selection, overlay restores its previous
  // non-selected layout (partial/expanded), unless it was explicitly closed.
  watch(
    selectedEventId,
    (eventId) => {
      if (eventId) {
        layer.value = transitionOrThrow(layer.value, "event-selected");
        return;
      }

      if (layer.value === "event-selected") {
        layer.value = transitionOrThrow(
          layer.value,
          lastNonSelectedLayer.value,
        );
      }
    },
    { immediate: true },
  );

  watch(layer, (next) => {
    if (next === "partial" || next === "expanded") {
      lastNonSelectedLayer.value = next;
    }
  });

  const isOpen = computed(() => layer.value !== "closed");
  const overlayHeight = computed(() => {
    if (layer.value === "closed") {
      return "0px";
    }
    if (layer.value === "partial") {
      return "33vh";
    }
    if (layer.value === "expanded") {
      return "72vh";
    }
    return "58vh";
  });

  function setLayer(next: CalendarOverlayLayerState): void {
    layer.value = transitionOrThrow(layer.value, next);
  }

  function close(): void {
    setLayer("closed");
  }

  function openPartial(): void {
    setLayer("partial");
  }

  function expand(): void {
    setLayer("expanded");
  }

  function selectEvent(eventId: string): void {
    selectedEventId.value = eventId;
    setLayer("event-selected");
  }

  function clearSelection(): void {
    selectedEventId.value = null;
  }

  function buildCalendarFeedQuery(
    latestMapFeedQuery: EventMapFeedQueryDto | null,
  ): EventCalendarFeedQueryDto | null {
    if (!latestMapFeedQuery) {
      return null;
    }

    return {
      bbox: latestMapFeedQuery.bbox,
      preset: latestMapFeedQuery.preset,
      timezone: latestMapFeedQuery.timezone,
      customStartUtc: latestMapFeedQuery.customStartUtc,
      customEndUtc: latestMapFeedQuery.customEndUtc,
      district: latestMapFeedQuery.district,
      categories: latestMapFeedQuery.categories,
      includeSavedOnly: latestMapFeedQuery.includeSavedOnly,
    };
  }

  function projectMapItemsToCalendarItems(
    mapItems: EventMapItemDto[],
  ): CalendarEventItemDto[] {
    return [...mapItems]
      .map(toCalendarItem)
      .sort((left, right) => left.startUtc.localeCompare(right.startUtc));
  }

  return {
    layer,
    isOpen,
    overlayHeight,
    setLayer,
    close,
    openPartial,
    expand,
    selectEvent,
    clearSelection,
    buildCalendarFeedQuery,
    projectMapItemsToCalendarItems,
  };
}
