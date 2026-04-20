<template>
  <q-layout view="hHh lpR fFf" class="app-layout">
    <q-header bordered class="header">
      <q-toolbar>
        <q-toolbar-title>WeUP Discovery Map</q-toolbar-title>
      </q-toolbar>
    </q-header>

    <q-page-container>
      <q-page class="map-page">
        <MapSurface
          v-model:selected-event-id="selectedEventId"
          :selected-event-saved-state="selectedEventSavedState"
        />

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
import { computed } from "vue";
import { useQuasar } from "quasar";
import EventDetailModal from "./components/EventDetailModal.vue";
import MapSurface from "./components/MapSurface.vue";
import { useEventDetailModal } from "./composables/useEventDetailModal";
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

@media (max-width: 768px) {
  .map-page {
    padding: 8px;
    height: calc(100vh - 50px);
  }
}
</style>
