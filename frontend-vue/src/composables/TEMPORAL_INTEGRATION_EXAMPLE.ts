/**
 * Temporal Navigation Integration Example
 *
 * Quick-start template for integrating TemporalNavigationControls
 * and useTemporalNavigation into your Vue app.
 */

// =============================================================================
// MINIMAL INTEGRATION EXAMPLE
// =============================================================================

import { computed, ref, watch } from "vue";
import TemporalNavigationControls from "./components/TemporalNavigationControls.vue";
import MapSurface from "./components/MapSurface.vue";
import CalendarOverlayShell from "./components/CalendarOverlayShell.vue";
import { useTemporalNavigation } from "./composables/useTemporalNavigation";
import type { EventMapFeedQueryDto } from "./contracts/map-feed.contracts";

/**
 * SETUP: Initialize temporal navigation
 */
const temporal = useTemporalNavigation({
  // Optional: set initial preset
  preset: "now",
  // debounceMs defaults to 300
  debounceMs: 300,
  onSelectedEventChange: (eventId) => {
    // Optional: callback when selection should change
    if (!eventId) {
      selectedEventId.value = null;
    }
  },
});

const selectedEventId = ref<string | null>(null);

/**
 * QUERIES: Build map and calendar queries from temporal state
 */
const mapFeedQuery = computed<EventMapFeedQueryDto | null>(() => {
  // Guard 1: Check validation
  if (!temporal.isValid.value) {
    console.warn(
      "Temporal validation error:",
      temporal.validationMessage.value,
    );
    return null;
  }

  // Guard 2: Build query
  try {
    // Use your bbox (Houston in this example)
    return temporal.buildMapFeedQuery("[-96.5,29.5,-95.0,30.0]");
  } catch (e) {
    console.error("Failed to build map feed query:", e);
    return null;
  }
});

const calendarTemporalQuery = computed(() => {
  if (!temporal.isValid.value) return null;

  try {
    return temporal.toCalendarOverlayTemporalQuery();
  } catch (e) {
    console.error("Failed to build calendar temporal query:", e);
    return null;
  }
});

/**
 * QUERIES: Send API requests when temporal state changes
 */
watch(
  () => temporal.requestSignature.value,
  async (signature) => {
    // Only fire if we have a valid query
    if (!signature || !mapFeedQuery.value) {
      return;
    }

    console.log("Temporal state changed, fetching map data...", {
      preset: temporal.preset.value,
      signature,
    });

    try {
      // Example: POST to your backend map feed endpoint
      const response = await fetch("/api/events/map-feed", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(mapFeedQuery.value),
      });

      if (!response.ok) {
        throw new Error(`API error: ${response.status}`);
      }

      const data = await response.json();
      console.log("Map feed response:", data);
      // Update your map items...
    } catch (e) {
      console.error("Map feed error:", e);
      // Show error to user
    }
  },
);

/**
 * SELECTION: Clear selected event on large window shifts
 */
watch(
  () => temporal.navigationState.value,
  () => {
    // Check if temporal shift should clear the selected event
    if (temporal.shouldClearSelectedEventOnWindowShift()) {
      console.log("Event selection cleared due to temporal shift");
      selectedEventId.value = null;
    }
  },
);

// =============================================================================
// TEMPLATE INTEGRATION
// =============================================================================

const templateExample = `
<template>
  <q-layout view="hHh lpR fFf" class="app-layout">
    <q-header bordered class="header">
      <q-toolbar>
        <q-toolbar-title>WeUP Discovery Map</q-toolbar-title>
      </q-toolbar>
    </q-header>

    <q-page-container>
      <q-page class="map-page">
        <!-- Temporal navigation controls -->
        <TemporalNavigationControls :temporal="temporal" />

        <!-- Map and calendar -->
        <div class="map-stack">
          <MapSurface
            v-if="mapFeedQuery"
            :query="mapFeedQuery"
            :selected-event-id="selectedEventId"
            @map-items-updated="onMapItemsUpdated"
            @select-event="selectedEventId = $event"
          />

          <CalendarOverlayShell
            v-if="calendarTemporalQuery"
            :temporal-query="calendarTemporalQuery"
            :scrub-position="temporal.scrubPosition.value"
            :items="calendarItems"
            @select-event="selectedEventId = $event"
          />
        </div>

        <!-- Event detail modal -->
        <EventDetailModal
          v-if="selectedEventId"
          :event-id="selectedEventId"
          @close="selectedEventId = null"
        />
      </q-page>
    </q-page-container>
  </q-layout>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import TemporalNavigationControls from "./components/TemporalNavigationControls.vue";
import MapSurface from "./components/MapSurface.vue";
import CalendarOverlayShell from "./components/CalendarOverlayShell.vue";
import EventDetailModal from "./components/EventDetailModal.vue";
import { useTemporalNavigation } from "./composables/useTemporalNavigation";
import type { EventMapItemDto } from "./contracts/map-feed.contracts";

const temporal = useTemporalNavigation({ debounceMs: 300 });
const selectedEventId = ref<string | null>(null);
const calendarItems = ref<EventMapItemDto[]>([]);

const mapFeedQuery = computed(() => {
  if (!temporal.isValid.value) return null;
  try {
    return temporal.buildMapFeedQuery("[-96.5,29.5,-95.0,30.0]");
  } catch (e) {
    console.error(e);
    return null;
  }
});

const calendarTemporalQuery = computed(() => {
  if (!temporal.isValid.value) return null;
  try {
    return temporal.toCalendarOverlayTemporalQuery();
  } catch (e) {
    console.error(e);
    return null;
  }
});

watch(() => temporal.requestSignature.value, async (signature) => {
  if (!signature || !mapFeedQuery.value) return;

  try {
    const res = await fetch("/api/events/map-feed", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(mapFeedQuery.value),
    });
    const data = await res.json();
    calendarItems.value = data.events;
  } catch (e) {
    console.error("Map feed error:", e);
  }
});

watch(() => temporal.navigationState.value, () => {
  if (temporal.shouldClearSelectedEventOnWindowShift()) {
    selectedEventId.value = null;
  }
});

function onMapItemsUpdated(items: EventMapItemDto[]) {
  calendarItems.value = items;
}
</script>
`;

// =============================================================================
// STATE DEBUGGING HELPER
// =============================================================================

/**
 * Use this function in development to log temporal state for debugging
 */
function debugTemporalState(): void {
  console.group("Temporal Navigation State Debug");

  console.log("Preset & Timezone:", {
    preset: temporal.preset.value,
    timezone: temporal.timezone.value,
  });

  console.log("Custom Range:", {
    customStartLocal: temporal.customStartLocal.value,
    customEndLocal: temporal.customEndLocal.value,
  });

  console.log("Navigation:", {
    stepOffset: temporal.stepOffset.value,
    scrubPosition: temporal.scrubPosition.value,
    isScrubbingActive: temporal.isScrubbingActive.value,
  });

  console.log("Validation:", {
    isValid: temporal.isValid.value,
    message: temporal.validationMessage.value,
  });

  console.log("Query Contract:", {
    effectiveQueryWindow: temporal.effectiveQueryWindow.value,
    requestSignature: temporal.requestSignature.value,
  });

  console.log("Event Selection:", {
    shouldClear: temporal.shouldClearSelectedEventOnWindowShift(),
  });

  console.groupEnd();
}

// =============================================================================
// COMMON PATTERNS
// =============================================================================

/**
 * PATTERN 1: Reset controls and clear map
 */
function resetMap(): void {
  temporal.resetToNow();
  selectedEventId.value = null;
  // Map will automatically re-query via requestSignature watcher
}

/**
 * PATTERN 2: Allow user to "share" current temporal state as URL
 */
function getTemporalShareUrl(): string {
  const params = new URLSearchParams({
    preset: temporal.preset.value,
    timezone: temporal.timezone.value,
    startUtc: temporal.customStartLocal.value || "",
    endUtc: temporal.customEndLocal.value || "",
  });
  return `${window.location.origin}${window.location.pathname}?${params.toString()}`;
}

/**
 * PATTERN 3: Restore temporal state from URL params
 */
function restoreTemporalFromUrl(params: URLSearchParams): void {
  const preset = params.get("preset");
  const startUtc = params.get("startUtc");
  const endUtc = params.get("endUtc");

  if (
    preset &&
    ["now", "tonight", "tomorrow", "thisWeekend"].includes(preset)
  ) {
    temporal.selectPreset(preset as any);
  }

  if (startUtc && endUtc) {
    // Convert UTC back to local and restore
    temporal.customStartLocal.value = new Date(startUtc)
      .toISOString()
      .slice(0, 16);
    temporal.customEndLocal.value = new Date(endUtc).toISOString().slice(0, 16);
    temporal.selectPreset("custom");
  }
}

/**
 * PATTERN 4: Keyboard shortcuts (future enhancement)
 */
function setupKeyboardShortcuts(): void {
  document.addEventListener("keydown", (e) => {
    if (e.target instanceof HTMLInputElement) return; // Don't intercept form inputs

    switch (e.key) {
      case "ArrowRight":
        e.preventDefault();
        temporal.stepDateTime(1, "day");
        break;
      case "ArrowLeft":
        e.preventDefault();
        temporal.stepDateTime(-1, "day");
        break;
      case "Home":
        e.preventDefault();
        temporal.resetToNow();
        break;
      case "n":
        // Quick preset: press 'n' for "Now"
        if (e.ctrlKey || e.metaKey) {
          e.preventDefault();
          temporal.selectPreset("now");
        }
        break;
    }
  });
}

// =============================================================================
// ERROR HANDLING HELPERS
// =============================================================================

/**
 * Show validation errors to user in a friendly way
 */
function getValidationErrorMessage(): string | null {
  const msg = temporal.validationMessage.value;
  if (!msg) return null;

  // Map technical messages to user-friendly text
  if (msg.includes("Timezone")) {
    return "There's a timezone issue. Please refresh the page.";
  }
  if (msg.includes("requires both")) {
    return "Please fill in both start and end dates for a custom range.";
  }
  if (msg.includes("valid datetimes")) {
    return "One of your date or time values isn't valid. Please check the format.";
  }
  if (msg.includes("start must be earlier")) {
    return "Start date must be before end date.";
  }

  return msg;
}

// =============================================================================
// EXPORT FOR USE IN COMPONENTS
// =============================================================================

export {
  temporal,
  selectedEventId,
  mapFeedQuery,
  calendarTemporalQuery,
  resetMap,
  getTemporalShareUrl,
  restoreTemporalFromUrl,
  setupKeyboardShortcuts,
  getValidationErrorMessage,
  debugTemporalState,
};
