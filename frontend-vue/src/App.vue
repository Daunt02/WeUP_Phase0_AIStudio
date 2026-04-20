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
            v-model:selected-event-id="selectedEventId"
            :selected-event-saved-state="selectedEventSavedState"
            @map-feed-query-updated="onMapFeedQueryUpdated"
            @map-items-updated="onMapItemsUpdated"
          />

          <CalendarOverlayShell
            :layer="calendarLayer"
            :overlay-height="calendarOverlayHeight"
            :selected-event-id="selectedEventId"
            :items="calendarItems"
            @set-layer="setCalendarLayer"
            @select-event="selectCalendarEvent"
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
import { computed, ref } from "vue";
import { useQuasar } from "quasar";
import CalendarOverlayShell from "./components/CalendarOverlayShell.vue";
import EventDetailModal from "./components/EventDetailModal.vue";
import MapSurface from "./components/MapSurface.vue";
import { useCalendarOverlayState } from "./composables/useCalendarOverlayState";
import { useEventDetailModal } from "./composables/useEventDetailModal";
import type {
  EventMapFeedQueryDto,
  EventMapItemDto,
} from "./contracts/map-feed.contracts";
import { shareEventDetail } from "./services/eventDetailService";

const $q = useQuasar();

const {
  selectedEventId,
  eventDetail,
  isOpen,
  isLoading,
  isSavePending,
  error,
  closeModal,
  toggleSavedState,
} = useEventDetailModal();

const selectedEventSavedState = computed(() => {
  return eventDetail.value?.savedByCurrentUser ?? null;
});

const latestMapFeedQuery = ref<EventMapFeedQueryDto | null>(null);
const latestMapItems = ref<EventMapItemDto[]>([]);

const {
  layer: calendarLayer,
  overlayHeight: calendarOverlayHeight,
  setLayer: setCalendarLayer,
  selectEvent: selectCalendarEvent,
  buildCalendarFeedQuery,
  projectMapItemsToCalendarItems,
} = useCalendarOverlayState(selectedEventId);

const calendarFeedQuery = computed(() =>
  buildCalendarFeedQuery(latestMapFeedQuery.value),
);

const calendarItems = computed(() => {
  // Calendar is an alternate temporal projection of the same canonical map set.
  // No route transition and no contract fork are allowed in this mapping path.
  if (!calendarFeedQuery.value) {
    return [];
  }
  return projectMapItemsToCalendarItems(latestMapItems.value);
});

function onMapFeedQueryUpdated(query: EventMapFeedQueryDto): void {
  latestMapFeedQuery.value = query;
}

function onMapItemsUpdated(items: EventMapItemDto[]): void {
  latestMapItems.value = items;
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
