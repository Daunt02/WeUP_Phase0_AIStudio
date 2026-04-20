/**
 * M6-P29: Temporal Navigation Controls Migration Guide
 *
 * This document shows how to integrate the new TemporalNavigationControls
 * with the existing MapTimeFilterPanel, or transition away from it entirely.
 */

// ============================================================================
// OPTION A: REPLACE MapTimeFilterPanel WITH TemporalNavigationControls
// ============================================================================

/*
BEFORE (Old Pattern):

<template>
  <q-page class="map-page">
    <MapTimeFilterPanel
      :preset="preset"
      :timezone="timezone"
      :custom-start-local="customStartLocal"
      :custom-end-local="customEndLocal"
      :include-saved-only="includeSavedOnly"
      :is-loading="isLoading"
      :validation-error="validationError"
      @update:preset="updatePreset"
      @update:timezone="updateTimezone"
      @update:customStartLocal="updateCustomStart"
      @update:customEndLocal="updateCustomEnd"
      @update:includeSavedOnly="updateIncludeSaved"
      @refresh="onRefresh"
    />

    <MapSurface :query="mapQuery" />
  </q-page>
</template>

<script setup lang="ts">
import { ref, computed } from "vue";
import MapTimeFilterPanel from "./components/MapTimeFilterPanel.vue";
import { useMapFeedFilters } from "./composables/useMapFeedFilters";

const filterState = useMapFeedFilters();

const {
  preset,
  timezone,
  customStartLocal,
  customEndLocal,
  includeSavedOnly,
  validationError,
} = filterState;

const isLoading = ref(false);

async function onRefresh() {
  // Manual refresh logic
}
</script>

AFTER (New Pattern):

<template>
  <q-page class="map-page">
    <!-- Single integrated control replaces MapTimeFilterPanel -->
    <TemporalNavigationControls :temporal="temporal" />

    <MapSurface :query="mapQuery" />
  </q-page>
</template>

<script setup lang="ts">
import { computed, watch } from "vue";
import TemporalNavigationControls from "./components/TemporalNavigationControls.vue";
import { useTemporalNavigation } from "./composables/useTemporalNavigation";

const temporal = useTemporalNavigation({ debounceMs: 300 });

const mapQuery = computed(() => {
  if (!temporal.isValid.value) return null;
  try {
    return temporal.buildMapFeedQuery("[-96.5,29.5,-95.0,30.0]");
  } catch (e) {
    console.error(e);
    return null;
  }
});

// Automatic refresh on temporal change (no manual refresh button needed)
watch(() => temporal.requestSignature.value, async () => {
  if (!mapQuery.value) return;
  const response = await fetch("/api/events/map-feed", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(mapQuery.value),
  });
  const data = await response.json();
  // Update map...
});
</script>

BENEFITS OF MIGRATION:
✓ Removes verbose prop forwarding (5 props → 1 prop)
✓ Removes emit handlers (6 emits → 0 emits)
✓ Removes manual refresh button (automatic on temporal change)
✓ Adds preset buttons (Now, Tonight, Tomorrow, etc.)
✓ Adds date stepping (forward/back arrows)
✓ Adds timeline scrubbing (interactive scrub track)
✓ Adds debouncing (batches rapid interactions)
✓ Improves mobile UX (responsive, touch-optimized)
✓ Better accessibility (ARIA roles, color contrast)
*/

// ============================================================================
// OPTION B: COEXIST - Use Both For Transitional Period
// ============================================================================

/*
If you want to keep MapTimeFilterPanel for advanced users while adding
TemporalNavigationControls for primary interaction:

<template>
  <q-page class="map-page">
    <!-- Primary UI: Interactive temporal controls -->
    <div class="temporal-section">
      <TemporalNavigationControls :temporal="temporal" />
    </div>

    <!-- Secondary UI: Advanced filter panel (optional expand/collapse) -->
    <q-expansion-item
      header-class="advanced-filters-header"
      toggle-side="right"
      label="Advanced Temporal Filters"
      icon="tune"
    >
      <MapTimeFilterPanel
        :preset="temporal.preset.value"
        :timezone="temporal.timezone.value"
        :custom-start-local="temporal.customStartLocal.value"
        :custom-end-local="temporal.customEndLocal.value"
        :include-saved-only="temporal.includeSavedOnly.value"
        :is-loading="isLoading"
        :validation-error="temporal.validationMessage.value"
        @update:preset="temporal.selectPreset"
        @update:timezone="temporal.timezone.value = $event"
        @update:customStartLocal="temporal.customStartLocal.value = $event"
        @update:customEndLocal="temporal.customEndLocal.value = $event"
        @update:includeSavedOnly="temporal.includeSavedOnly.value = $event"
        @refresh="manualRefresh"
      />
    </q-expansion-item>

    <MapSurface :query="mapQuery" />
  </q-page>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import TemporalNavigationControls from "./components/TemporalNavigationControls.vue";
import MapTimeFilterPanel from "./components/MapTimeFilterPanel.vue";
import { useTemporalNavigation } from "./composables/useTemporalNavigation";

const temporal = useTemporalNavigation({ debounceMs: 300 });
const isLoading = ref(false);

const mapQuery = computed(() => {
  if (!temporal.isValid.value) return null;
  try {
    return temporal.buildMapFeedQuery("[-96.5,29.5,-95.0,30.0]");
  } catch (e) {
    return null;
  }
});

watch(() => temporal.requestSignature.value, autoFetch);

async function autoFetch() {
  if (!mapQuery.value) return;
  isLoading.value = true;
  try {
    const response = await fetch("/api/events/map-feed", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(mapQuery.value),
    });
    const data = await response.json();
    // Update map...
  } finally {
    isLoading.value = false;
  }
}

async function manualRefresh() {
  await autoFetch();
}
</script>

BENEFITS OF COEXISTENCE:
✓ Gradual migration path
✓ Advanced users can still edit raw timezone/ranges
✓ Primary interaction remains intuitive for casual users
✓ Easy to deprecate MapTimeFilterPanel later
*/

// ============================================================================
// COMPARISON TABLE: MapTimeFilterPanel vs TemporalNavigationControls
// ============================================================================

/*
ASPECT                          | MapTimeFilterPanel           | TemporalNavigationControls
──────────────────────────────── ────────────────────────────────────────────────────
Interaction Model              | Form inputs                  | Interactive buttons & drag
Preset Selection               | Dropdown (5 options)         | Buttons (1-click, visible)
Date Navigation                | Manual text input            | Forward/back arrows
Timeline Scrubbing             | Not supported                | Drag scrub handle
Reset to Now                   | Manual clear + set           | Single "Reset" button
User Feedback                  | Validation errors only       | Status, offset display
Mobile Responsiveness          | Single column (verbose)      | 2-col buttons, flexible
Accessibility                  | Basic ARIA                   | Full WCAG AA compliance
Debouncing                     | Manual "Refresh" button      | Automatic 300ms debounce
Visual Feedback                | Minimal                      | Progress bar, tooltips
Development Mode               | Debug limited                | Full state debug panel
Typescript Contracts           | Prop/emit forwarding         | Single composable result
Lines of Code (component)       | 70 lines                     | 500 lines (more features)
Learning Curve                 | Familiar form pattern        | New drag interaction
────────────────────────────────────────────────────────────────────────────
RECOMMENDATION:                 | Use for advanced users/API   | Primary user interaction
                                | clients; kept for compat     | (M6-P29 replacement)
*/

// ============================================================================
// IMPLEMENTATION DETAILS: How They Compose
// ============================================================================

/*
BOTH components build on useMapFeedFilters, but at different abstraction levels:

useMapFeedFilters.ts
  ↓ (Lower level: state management + validation)
  - Manages preset, timezone, customStart/End
  - Converts local datetime ↔ UTC
  - Provides buildMapFeedQuery() method
  - Used by both components

┌─────────────────────────────────────────────────────────────┐
│                                                             │
├──────────────────>  MapTimeFilterPanel.vue                 │
│                     (Direct access to useMapFeedFilters)    │
│                     - Individual prop/emit for each field   │
│                     - Manual refresh button                 │
│                     - Raw value editing                     │
│                                                             │
├──────────────────>  useTemporalNavigation.ts               │
│                     (Wraps useMapFeedFilters)               │
│                     - Adds date stepping logic              │
│                     - Adds scrubbing logic                  │
│                     - Adds reset convenience method         │
│                     - Debounces all changes                 │
│                                                             │
│                     ↓                                       │
│                     TemporalNavigationControls.vue         │
│                     (Built on useTemporalNavigation)        │
│                     - Interactive buttons & controls        │
│                     - Touch-optimized scrubber              │
│                     - Status/feedback display               │
│                     - Mobile responsive                     │
│                                                             │
└─────────────────────────────────────────────────────────────┘

ARCHITECTURE LAYERS:
┌─ useMapFeedFilters.ts ──────────────────> Contracts & Validation
├─ useTemporalNavigation.ts ──────────────> Navigation Logic
└─ TemporalNavigationControls.vue ────────> UI & Interactions
*/

// ============================================================================
// MIGRATION CHECKLIST
// ============================================================================

/*
[ ] Decision: Replace vs. Coexist?
    ☐ Replace: Remove MapTimeFilterPanel, use TemporalNavigationControls only
    ☐ Coexist: Keep both, hide MapTimeFilterPanel in expansion panel

[ ] If Replacing:
    ☐ Remove MapTimeFilterPanel from template
    ☐ Remove all prop/emit forwarding code
    ☐ Initialize useTemporalNavigation in parent
    ☐ Move temporal query building to parent (mapQuery computed property)
    ☐ Set up watch on temporal.requestSignature for auto-fetch
    ☐ Remove manual "refresh" button handlers
    ☐ Test all preset buttons, stepping, scrubbing
    ☐ Test on mobile (portrait & landscape)

[ ] If Coexisting:
    ☐ Add TemporalNavigationControls above MapTimeFilterPanel
    ☐ Wrap MapTimeFilterPanel in q-expansion-item
    ☐ Forward temporal state from composable to panel props
    ☐ Bind panel emissions back to temporal composable methods
    ☐ Set up isLoading state for manual refresh
    ☐ Test both UIs independently
    ☐ Verify no state conflicts

[ ] Testing:
    ☐ User can select presets via buttons
    ☐ Preset buttons show hover/active states
    ☐ Date stepping works forward and backward
    ☐ Scrubber drag is smooth
    ☐ Reset button clears all state
    ☐ Map query updates automatically (no manual button needed)
    ☐ Calendar query also updates from same temporal state
    ☐ Validation errors display correctly
    ☐ Works on mobile (< 512px)
    ☐ Keyboard navigation works (Tab through controls)
    ☐ Selected event is cleared/preserved as expected

[ ] Deployment:
    ☐ Merge to develop branch
    ☐ Notify team of new component & removal of old one
    ☐ Update documentation links
    ☐ Monitor production for any temporal filtering issues
    ☐ Plan removal of MapTimeFilterPanel if on replacement path
*/

// ============================================================================
// BACKWARD COMPATIBILITY & DEPRECATION
// ============================================================================

/*
DEPRECATION PATH (if choosing full replacement):

Phase 1 (Now):
  - MapTimeFilterPanel remains available
  - TemporalNavigationControls introduced as recommended new component
  - Both can run side-by-side

Phase 2 (After 3 months):
  - MapTimeFilterPanel deprecated in documentation
  - Warnings logged if MapTimeFilterPanel is used
  - TemporalNavigationControls becomes default in examples

Phase 3 (After 6 months):
  - MapTimeFilterPanel removed from codebase
  - Migration complete

NOTE: useMapFeedFilters.ts will remain as it's still used by MapTimeFilterPanel
and may be used by other components. Only MapTimeFilterPanel itself is deprecated.
*/

// ============================================================================
// COMMON GOTCHAS DURING MIGRATION
// ============================================================================

/*
GOTCHA 1: Forgetting to debounce watch

❌ WRONG:
watch(() => temporal.preset.value, fetchMapData);
// Fires on every preset change, even during debounce

✓ RIGHT:
watch(() => temporal.requestSignature.value, fetchMapData);
// Only fires after debounce period (300ms)

GOTCHA 2: Accessing internal state directly

❌ WRONG:
temporal.navigationState.value.currentPreset; // Internal field

✓ RIGHT:
temporal.preset.value; // Official public API

GOTCHA 3: Not handling validation errors

❌ WRONG:
const query = temporal.buildMapFeedQuery(bbox); // May throw!
updateMap(query);

✓ RIGHT:
if (!temporal.isValid.value) {
  console.warn(temporal.validationMessage.value);
  return;
}
const query = temporal.buildMapFeedQuery(bbox);
updateMap(query);

GOTCHA 4: Scrubbing should not affect backend query

❌ WRONG:
// During scrubbing, send a modified query window
if (temporal.isScrubbingActive.value) {
  const adjusted = shrinkWindow(temporal.effectiveQueryWindow.value, temporal.scrubPosition.value);
  fetchMapData(adjusted); // Wrong! Backend sees reduced window
}

✓ RIGHT:
// Always send full window; only calendar uses scrubPosition for focus
watch(() => temporal.requestSignature.value, () => {
  // Query always uses effectiveQueryWindow (full, unmodified)
  fetchMapData(temporal.effectiveQueryWindow.value);
  // Calendar overlay separately uses temporal.scrubPosition.value
});

GOTCHA 5: Clearing selected event incorrectly

❌ WRONG:
// Always clear when temporal state changes
watch(() => temporal.navigationState.value, () => {
  selectedEventId.value = null; // Too aggressive!
});

✓ RIGHT:
// Only clear when temporal shift is large
watch(() => temporal.navigationState.value, () => {
  if (temporal.shouldClearSelectedEventOnWindowShift()) {
    selectedEventId.value = null;
  }
});
*/

export {};
