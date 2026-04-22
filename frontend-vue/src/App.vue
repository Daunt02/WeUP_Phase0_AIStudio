<template>
  <q-layout view="hHh lpR fFf" class="app-layout">
    <q-header bordered class="header">
      <q-toolbar>
        <q-toolbar-title>WeUP Discovery Map</q-toolbar-title>
      </q-toolbar>
    </q-header>

    <q-page-container>
      <q-page class="map-page">
        <div class="map-stack">
          <MapSurface
            :selected-event-id="discovery.selectedEventId.value"
            :selected-event-saved-state="selectedEventSavedState"
            :active-filters="discovery.activeFilters.value"
            @update:selected-event-id="onMapSelectedEventChanged"
            @filters-updated="onMapFiltersUpdated"
            @map-feed-query-updated="onMapFeedQueryUpdated"
            @map-items-updated="onMapItemsUpdated"
          />

          <CalendarOverlayShell
            :layer="discovery.overlayMode.value"
            :overlay-height="discovery.overlayHeight.value"
            :selected-event-id="discovery.selectedEventId.value"
            :items="calendarItems"
            :is-filter-refresh-pending="isCalendarFilterRefreshPending"
            :degraded-reason="calendarDegradedReason"
            @set-layer="discovery.setOverlayMode"
            @select-event="discovery.selectEvent"
          />
        </div>

        <EventDetailModal
          :model-value="isOpen"
          :event="eventDetail"
          :is-loading="isLoading"
          :is-save-pending="isSavePending"
          :error="error"
          @update:model-value="onModalVisibilityChange"
          @toggle-save="toggleSavedState"
          @share="showSharePlaceholder"
        />
      </q-page>
    </q-page-container>
  </q-layout>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from "vue";
import { useQuasar } from "quasar";
import CalendarOverlayShell from "./components/CalendarOverlayShell.vue";
import EventDetailModal from "./components/EventDetailModal.vue";
import MapSurface from "./components/MapSurface.vue";
import {
  useDiscoveryState,
  type DiscoveryFilterState,
} from "./composables/useDiscoveryState";
import { useEventDetailModal } from "./composables/useEventDetailModal";
import { useSavedEventState } from "./composables/useSavedEventState";
import type {
  EventMapFeedQueryDto,
  EventMapItemDto,
} from "./contracts/map-feed.contracts";
import { shareEventDetail } from "./services/eventDetailService";

const $q = useQuasar();

const discovery = useDiscoveryState();

const selectedEventIdModel = computed<string | null>({
  get() {
    return discovery.selectedEventId.value;
  },
  set(eventId) {
    if (eventId) {
      discovery.selectEvent(eventId);
      return;
    }

    discovery.clearSelection();
  },
});

const savedEventState = useSavedEventState();

const {
  eventDetail,
  isOpen,
  isLoading,
  isSavePending,
  error,
  closeModal,
  toggleSavedState,
} = useEventDetailModal(selectedEventIdModel, {
  resolveSavedState: savedEventState.resolveSavedState,
  mutateSavedState: savedEventState.mutateSavedState,
});

const selectedEventSavedState = computed(() => {
  return eventDetail.value?.savedByCurrentUser ?? null;
});

const mapItemsState = ref<EventMapItemDto[]>([]);
const isCalendarFilterRefreshPending = ref(false);
const calendarDegradedReason = ref<string | null>(null);
const latestMapFeedQueryState = ref<EventMapFeedQueryDto | null>(null);
let refreshDegradedHandle: ReturnType<typeof setTimeout> | null = null;

function clearRefreshDegradedHandle(): void {
  if (refreshDegradedHandle) {
    clearTimeout(refreshDegradedHandle);
    refreshDegradedHandle = null;
  }
}

function beginCalendarFilterRefresh(): void {
  isCalendarFilterRefreshPending.value = true;
  clearRefreshDegradedHandle();

  // Stop motion escalation if data response is delayed: degrade to explicit text.
  refreshDegradedHandle = setTimeout(() => {
    if (isCalendarFilterRefreshPending.value) {
      calendarDegradedReason.value =
        "Calendar refresh is delayed. Showing last stable projection while map data catches up.";
    }
  }, 1800);
}

function settleCalendarFilterRefresh(): void {
  isCalendarFilterRefreshPending.value = false;
  calendarDegradedReason.value = null;
  clearRefreshDegradedHandle();
}

function hasFilterScopeChange(
  previous: EventMapFeedQueryDto | null,
  next: EventMapFeedQueryDto,
): boolean {
  if (!previous) {
    return false;
  }

  const previousCategories = previous.categories ?? [];
  const nextCategories = next.categories ?? [];

  return (
    previous.preset !== next.preset ||
    previous.timezone !== next.timezone ||
    previous.customStartUtc !== next.customStartUtc ||
    previous.customEndUtc !== next.customEndUtc ||
    previous.district !== next.district ||
    previous.includeSavedOnly !== next.includeSavedOnly ||
    previousCategories.length !== nextCategories.length ||
    previousCategories.some(
      (category, index) => category !== nextCategories[index],
    )
  );
}

const calendarItems = computed(() => {
  // Calendar is an alternate temporal projection of the same canonical map set.
  // No route transition and no contract fork are allowed in this mapping path.
  if (!discovery.calendarFeedQuery.value) {
    return [];
  }
  return [...discovery.visibleEventIds.value]
    .map(
      (eventId) =>
        latestVisibleMapItems.value.find((item) => item.eventId === eventId) ??
        null,
    )
    .filter((item): item is EventMapItemDto => item !== null)
    .map((item) => ({
      eventId: item.eventId,
      title: item.title,
      startUtc: item.startUtc,
      endUtc: item.endUtc,
      timezone: discovery.activeTemporalFilter.value?.timezone ?? "UTC",
      venueName: item.venueName,
      district: item.district,
      primaryCategory: item.primaryCategory,
      savedByCurrentUser: item.savedByCurrentUser,
      markerState: item.markerState,
      thumbnailUrl: null,
    }))
    .sort((left, right) => left.startUtc.localeCompare(right.startUtc));
});

const latestVisibleMapItems = computed<EventMapItemDto[]>(() => {
  const visibleIds = discovery.visibleEventIds.value;
  return mapItemsState.value.filter((item) => visibleIds.has(item.eventId));
});

function onMapSelectedEventChanged(eventId: string | null): void {
  if (eventId) {
    discovery.selectEvent(eventId);
    return;
  }

  discovery.clearSelection();
}

function onMapFiltersUpdated(partial: Partial<DiscoveryFilterState>): void {
  discovery.applyFilters(partial);
  beginCalendarFilterRefresh();
}

function onMapItemsUpdated(items: EventMapItemDto[]): void {
  mapItemsState.value = items;
  discovery.reportVisibleEvents(items);
  settleCalendarFilterRefresh();
}

function onMapFeedQueryUpdated(query: EventMapFeedQueryDto): void {
  const hadScopeChange = hasFilterScopeChange(
    latestMapFeedQueryState.value,
    query,
  );

  latestMapFeedQueryState.value = query;
  discovery.reportMapFeedQuery(query);

  if (hadScopeChange) {
    beginCalendarFilterRefresh();
  }
}

function onModalVisibilityChange(isVisible: boolean): void {
  // Closing the dialog clears the shared selection so the map returns to its
  // default interaction state without a stranded highlighted marker.
  if (!isVisible) {
    closeModal();
  }
}

async function showSharePlaceholder(): Promise<void> {
  if (!eventDetail.value) {
    return;
  }

  try {
    const result = await shareEventDetail(eventDetail.value);

    if (result === "dismissed") {
      return;
    }

    $q.notify({
      type: result === "shared" ? "positive" : "info",
      message:
        result === "shared"
          ? "Event shared."
          : result === "copied"
            ? "Share link copied to clipboard."
            : "Opened an email share draft.",
    });
  } catch (cause) {
    $q.notify({
      type: "negative",
      message:
        cause instanceof Error ? cause.message : "Failed to share event.",
    });
  }
}

onBeforeUnmount(() => {
  clearRefreshDegradedHandle();
});
</script>

<style scoped>
.app-layout {
  min-height: 100vh;
  background: linear-gradient(180deg, #f7fbff 0%, #eef5ff 100%);
}

.header {
  background: #ffffff;
  color: #1f2937;
}

.map-page {
  height: calc(100vh - 50px);
  padding: 12px;
}

.map-stack {
  position: relative;
  height: 100%;
}

@media (max-width: 768px) {
  .map-page {
    padding: 8px;
    height: calc(100vh - 50px);
  }
}
</style>
