/**
 * TEMPORAL NAVIGATION & SCRUBBING CONTROLS v1.0
 *
 * M6-P29 Implementation Guide: Integration, Query Examples, and Guardrails
 *
 * This guide covers:
 * 1. Component usage and integration patterns
 * 2. Backend query examples showing temporal contract mapping
 * 3. State management and selected event stability guardrails
 * 4. Scrubbing semantics and time window bounds
 * 5. Accessibility and mobile UX considerations
 */

// ============================================================================
// PART 1: COMPONENT INTEGRATION
// ============================================================================

/**
 * Usage Example: App.vue Integration
 *
 * The TemporalNavigationControls component wraps the useTemporalNavigation
 * composable. The composable drives both map feed queries and calendar
 * overlay queries from a single temporal state source.
 */

/*
<template>
  <q-layout view="hHh lpR fFf" class="app-layout">
    <q-header bordered class="header">
      <q-toolbar>
        <q-toolbar-title>WeUP Discovery Map</q-toolbar-title>
      </q-toolbar>
    </q-header>

    <q-page-container>
      <q-page class="map-page">
        <!-- Temporal navigation controls at top -->
        <TemporalNavigationControls :temporal="temporalNav" />

        <div class="map-stack">
          <!-- MapSurface receives queries built from temporalNav -->
          <MapSurface
            :query="mapFeedQuery"
            :selected-event-id="selectedEventId"
            @map-items-updated="onMapItemsUpdated"
          />

          <!-- CalendarOverlay receives queries from same temporal source -->
          <CalendarOverlayShell
            :temporal-query="calendarTemporalQuery"
            :items="calendarItems"
            @select-event="selectEvent"
          />
        </div>

        <EventDetailModal :event="eventDetail" />
      </q-page>
    </q-page-container>
  </q-layout>
</template>

<script setup lang="ts">
import { computed, watch } from "vue";
import TemporalNavigationControls from "./components/TemporalNavigationControls.vue";
import { useTemporalNavigation } from "./composables/useTemporalNavigation";
import type { EventMapFeedQueryDto } from "./contracts/map-feed.contracts";

// Initialize temporal navigation with 300ms debounce
const temporalNav = useTemporalNavigation({
  debounceMs: 300,
  onSelectedEventChange: (eventId) => {
    // Handle selected event clear/update
    if (!eventId) {
      closeEventDetail();
    }
  },
});

// Derive map feed query from temporal state
const mapFeedQuery = computed<EventMapFeedQueryDto | null>(() => {
  if (!temporalNav.isValid.value) {
    return null; // Guard: don't query if validation fails
  }

  try {
    // Use the composed bbox + temporal contract from composable
    return temporalNav.buildMapFeedQuery("[-96.5,29.5,-95.0,30.0]"); // Houston
  } catch (e) {
    console.error("Map feed query error:", e);
    return null;
  }
});

// Derive calendar temporal query from same source
const calendarTemporalQuery = computed(() => {
  if (!temporalNav.isValid.value) {
    return null;
  }

  try {
    return temporalNav.toCalendarOverlayTemporalQuery();
  } catch (e) {
    console.error("Calendar temporal query error:", e);
    return null;
  }
});

// Watch temporal changes to trigger API calls
watch(
  () => temporalNav.requestSignature.value,
  async (signature) => {
    if (!signature || !mapFeedQuery.value) {
      return;
    }

    try {
      const response = await fetch("/api/events/map-feed", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(mapFeedQuery.value),
      });

      if (!response.ok) throw new Error("Map feed query failed");
      const data = await response.json();
      updateMapItems(data.events);
    } catch (e) {
      console.error("Map feed error:", e);
    }
  },
);

// Handle selected event stability across temporal shifts
watch(
  () => temporalNav.navigationState.value,
  () => {
    if (temporalNav.shouldClearSelectedEventOnWindowShift()) {
      selectedEventId.value = null;
    }
  },
);
</script>
*/

// ============================================================================
// PART 2: BACKEND QUERY EXAMPLES
// ============================================================================

/**
 * CANONICAL SCENARIO 1: Preset Selection (Now)
 *
 * User clicks "Now" button
 * → Frontend state: TimeWindowPreset.Now
 * → Query sent to backend:
 */

const example1PresetNow: EventMapFeedQueryDto = {
  preset: "now",
  timezone: "America/Chicago", // Client detects via Intl.DateTimeFormat
  bbox: "[-96.5,29.5,-95.0,30.0]",
  // Backend expands "now" to [current_time - 1hr, current_time + 3hr]
};

/**
 * CANONICAL SCENARIO 2: Preset Selection (Tonight)
 *
 * User clicks "Tonight" button
 * → Frontend state: TimeWindowPreset.Tonight, timezone "America/Chicago"
 * → Query sent to backend:
 */

const example2PresetTonight: EventMapFeedQueryDto = {
  preset: "tonight",
  timezone: "America/Chicago",
  bbox: "[-96.5,29.5,-95.0,30.0]",
  // Backend expands "tonight" to [today 6pm, today 11:59pm] in America/Chicago TZ
};

/**
 * CANONICAL SCENARIO 3: Date Stepping Forward
 *
 * User clicks "Now" → then clicks forward arrow (+1 day)
 * → Frontend transitions from preset to Custom with stepped bounds
 * → Query sent to backend:
 */

const example3DateStepForward: EventMapFeedQueryDto = {
  preset: "custom",
  timezone: "America/Chicago",
  customStartUtc: "2026-04-21T00:00:00Z", // Tomorrow 00:00 UTC
  customEndUtc: "2026-04-21T23:59:59Z", // Tomorrow 23:59 UTC
  bbox: "[-96.5,29.5,-95.0,30.0]",
  // Backend returns all events within this absolute window
};

/**
 * CANONICAL SCENARIO 4: Date Stepping Backward
 *
 * User is on "Tomorrow" → clicks back arrow (-1 day)
 * → Frontend transitions to Custom with stepped backward bounds
 * → Query sent to backend:
 */

const example4DateStepBackward: EventMapFeedQueryDto = {
  preset: "custom",
  timezone: "America/Chicago",
  customStartUtc: "2026-04-19T00:00:00Z", // Yesterday 00:00 UTC
  customEndUtc: "2026-04-19T23:59:59Z", // Yesterday 23:59 UTC
  bbox: "[-96.5,29.5,-95.0,30.0]",
};

/**
 * CANONICAL SCENARIO 5: Custom Range
 *
 * User manually enters:
 *   Start: 2026-04-20 10:00 (local time)
 *   End:   2026-04-22 18:00 (local time)
 * → Frontend converts to UTC (assuming America/Chicago):
 * → Query sent to backend:
 */

const example5CustomRange: EventMapFeedQueryDto = {
  preset: "custom",
  timezone: "America/Chicago",
  customStartUtc: "2026-04-20T15:00:00Z", // 10:00 CDT = 15:00 UTC (CDT = UTC-5)
  customEndUtc: "2026-04-22T23:00:00Z", // 18:00 CDT = 23:00 UTC
  bbox: "[-96.5,29.5,-95.0,30.0]",
};

/**
 * CANONICAL SCENARIO 6: Scrubbing Within Window
 *
 * User is on "This Weekend" preset → scrubs to 75% position (later in window)
 * → Frontend emits:
 *    - scrubPosition = 0.75
 *    - isScrubbingActive = true
 * → Query sent to backend is UNCHANGED (full preset window):
 */

const example6ScrubbingWithinPreset: EventMapFeedQueryDto = {
  preset: "thisWeekend",
  timezone: "America/Chicago",
  bbox: "[-96.5,29.5,-95.0,30.0]",
  // Scrubbing does NOT modify the backend window.
  // Backend returns all events in this weekend.
  // Calendar overlay uses scrubPosition (0.75) to focus on later events visually.
};

/**
 * SCRUBBING SEMANTIC CLARIFICATION:
 *
 * Scrubbing is a VISUAL/UX interaction, NOT a query modifier:
 *
 * ✓ CORRECT: Scrubbing moves the calendar view focus within the full window
 *   The user sees events earlier or later in the window, but the same events
 *   are returned from the backend.
 *
 * ✗ INCORRECT: Scrubbing should NOT reduce the backend query window size
 *   Do NOT send a modified customStartUtc/customEndUtc with a smaller range.
 *   Always send the full window to the backend.
 *
 * Used by CalendarOverlay to determine which slice of the window to render first:
 *   scrubPosition = 0.0 → Show earliest events in window first
 *   scrubPosition = 0.5 → Show middle of window first
 *   scrubPosition = 1.0 → Show latest events in window first
 */

// ============================================================================
// PART 3: STATE MANAGEMENT & GUARDRAILS
// ============================================================================

/**
 * GUARDRAIL 1: Invalid Range Detection
 *
 * useTemporalNavigation.validationError is set when:
 * - Timezone is empty
 * - Custom preset but start or end is missing
 * - Custom range with invalid datetime syntax
 * - Custom range start >= end
 *
 * Usage:
 *   if (temporal.validationMessage.value) {
 *     // Don't send query; show validation error
 *     console.warn(temporal.validationMessage.value);
 *     return;
 *   }
 *   const query = temporal.buildMapFeedQuery(bbox);
 */

/**
 * GUARDRAIL 2: Selected Event Stability
 *
 * When temporal navigation occurs, the selected event ID may become stale.
 * Rules for clearing selection:
 *
 * ✓ Preserve selection during:
 *   - Scrubbing (same window, just different focus)
 *   - Small step offsets (±1-2 days; event likely still in expanded window)
 *   - Same preset selection (re-selecting)
 *
 * ✗ Clear selection during:
 *   - Large step offsets (>2 days; event likely out of range)
 *   - Transitioning from one preset to another
 *   - Custom range shifts with non-zero step offset
 *
 * Implementation:
 *   watch(() => temporal.navigationState.value, () => {
 *     if (temporal.shouldClearSelectedEventOnWindowShift()) {
 *       selectedEventId.value = null;
 *     }
 *   });
 */

/**
 * GUARDRAIL 3: Debounced State Updates
 *
 * Temporal changes are debounced to avoid flooding the API.
 * Default: 300ms debounce (configurable via useTemporalNavigation options).
 *
 * hasPendingTemporalChange indicates the UI should not fire queries yet:
 *   <q-spinner v-if="temporal.hasPendingTemporalChange.value" />
 *
 * This gives time for rapid interactions (scrubbing, stepping) to batch
 * before final API call.
 */

/**
 * GUARDRAIL 4: Timezone Consistency
 *
 * The timezone must be determined once and remain stable for the session.
 * Reason: Preset windows expand relative to timezone (tonight = 6pm-11:59pm
 * in that timezone). Changing timezone mid-session would shift all presets.
 *
 * Implementation:
 *   - useTemporalNavigation detects via:
 *     Intl.DateTimeFormat().resolvedOptions().timeZone
 *   - Fallback to "UTC" if unavailable
 *   - Do NOT allow user to change timezone in UI
 */

/**
 * GUARDRAIL 5: Window Transition Warnings
 *
 * When stepping far in the future or past, show a warning:
 */

// Example guard function
function checkForLargeWindowShift(
  oldOffset: number,
  newOffset: number,
): boolean {
  const delta = Math.abs(newOffset - oldOffset);
  if (delta > 7) {
    console.warn(
      `Large step shift: ${delta} days. Selected event will be cleared.`,
    );
    return true; // Should clear selection
  }
  return false;
}

// ============================================================================
// PART 4: SCRUBBING TECHNICAL DETAILS
// ============================================================================

/**
 * SCRUBBING POSITION CALCULATION
 *
 * scrubPosition is normalized to [0, 1]:
 *
 * User drags scrub handle from start to end of 400px track:
 *   x = 100px → scrubPosition = 100 / 400 = 0.25
 *   x = 200px → scrubPosition = 200 / 400 = 0.50
 *   x = 400px → scrubPosition = 400 / 400 = 1.00
 *
 * Bounded by:
 *   scrubPosition = clamp(position, 0, 1)
 *
 * Tooltip display:
 *   0.0-0.25 → "Earlier"
 *   0.25-0.5 → "Early"
 *   0.5-0.75 → "Late"
 *   0.75-1.0 → "Latest"
 */

/**
 * SCRUBBING EVENT FLOW
 *
 * 1. User mousedown/touchstart on scrub handle
 *    → isScrubbingActive = true
 *    → Add document mousemove/touchmove listeners
 *
 * 2. User drags (mousemove/touchmove)
 *    → Calculate position from cursor X relative to track width
 *    → Call updateScrubPosition(position)
 *    → Debounced update; no API call yet
 *
 * 3. User mouseup/touchend
 *    → isScrubbingActive = false
 *    → Remove listeners
 *    → endScrubbing() finalizes state
 *
 * Touch support:
 *    event.touches[0].clientX for touch events
 *    event.clientX for mouse events
 *    Both use getBoundingClientRect() for position calculation
 */

/**
 * CALENDAR INTEGRATION WITH SCRUBBING
 *
 * The CalendarOverlay receives scrubPosition and uses it to:
 * 1. Determine which time slice to render first (earliest vs latest)
 * 2. Scroll focus to that slice in the calendar grid
 * 3. Provide visual feedback (e.g., highlight current scrub position)
 *
 * Example pseudocode in CalendarOverlay:
 *
 *   computed(() => {
 *     const events = props.items; // All events in window
 *     const midpoint = events.length * props.scrubPosition; // 0-indexed
 *     return events.slice(midpoint - 10, midpoint + 10); // Show ±10 around scrub pos
 *   })
 */

// ============================================================================
// PART 5: ACCESSIBILITY & MOBILE UX
// ============================================================================

/**
 * ACCESSIBILITY FEATURES IMPLEMENTED
 *
 * 1. Preset buttons:
 *    - aria-pressed="true/false" indicates active state
 *    - aria-label="Navigate to X" describes action
 *
 * 2. Date stepping buttons:
 *    - aria-label="Step backward 1 day"
 *    - aria-label="Step forward 1 day"
 *    - Keyboard may support arrow keys (future enhancement)
 *
 * 3. Scrubber track:
 *    - role="slider" indicates it's a range control
 *    - aria-valuenow="[0-100]" indicates current position %
 *    - aria-valuemin="0", aria-valuemax="100"
 *    - aria-label shows active preset
 *    - aria-disabled for validation errors
 *
 * 4. Color contrast:
 *    - Primary color (#2196f3) >= AA standard on backgrounds
 *    - Status text colors meet WCAG AA for contrast ratios
 *    - Icons paired with text labels (not icon-only buttons)
 */

/**
 * MOBILE RESPONSIVE DESIGN
 *
 * Breakpoint: 512px (typical mobile width)
 * - Preset buttons: 2 per row (50% width minus gap)
 * - Scrubber height: 32px (from 40px) for easier touch
 * - Scrub handle: 20px (from 24px) to prevent overlap
 * - Time markers hidden (clutter on small screens)
 * - Font sizes scaled down
 *
 * Breakpoint: 320px (small phones)
 * - Preset button text: 0.7rem
 * - Further size reduction to prevent overflow
 */

/**
 * TOUCH INTERACTION BEST PRACTICES
 *
 * 1. Hit targets: Scrub handle is 20-24px, >= 44px recommended
 *    Mitigation: Increase touch area via invisible expanded hitbox
 *    (Not explicitly visible, but scrubber track itself is 32-40px tall)
 *
 * 2. Scrubbing momentum: Handled by transition: width 0.1s ease-out
 *    Provides smooth visual feedback without scroll momentum issues
 *
 * 3. Prevented defaults: touchmove listener doesn't preventDefault()
 *    to allow page scroll if user starts drag outside scrubber
 *
 * 4. No hover states on touch devices:
 *    Handled by :hover pseudo-class (inherent to CSS; browsers ignore on touch)
 */

// ============================================================================
// PART 6: ERROR HANDLING & EDGE CASES
// ============================================================================

/**
 * ERROR: Invalid datetime in custom range
 *
 * User enters: "2026-04-20 25:00" (invalid hour)
 * Detected by: new Date(input).getTime() returns NaN
 * Message: "Custom range values must be valid datetimes."
 * Action: Block query; show validation error; highlight input field
 */

/**
 * ERROR: Start after end in custom range
 *
 * User enters:
 *   Start: 2026-04-22
 *   End: 2026-04-20
 * Detected by: customStartUtc >= customEndUtc comparison
 * Message: "Custom range start must be earlier than custom range end."
 * Action: Block query; highlight both fields; suggest swap
 */

/**
 * ERROR: Timezone not available
 *
 * Browser doesn't support Intl.DateTimeFormat?
 * Fallback to: "UTC"
 * Note: This is extremely unlikely in modern browsers (ES2017+)
 */

/**
 * EDGE CASE: Rapid preset switching
 *
 * User clicks: Now → Tonight → Tomorrow → Custom (in rapid succession)
 * Behavior:
 *   - Each click updates preset.value immediately
 *   - Debounce delays query until 300ms of inactivity
 *   - Only last preset is queried
 * No issues: Debouncing prevents API storm
 */

/**
 * EDGE CASE: Scrubbing while stepping
 *
 * User steps forward → begins scrubbing before step completes
 * Behavior:
 *   - Step transitions to custom range
 *   - Scrubbing moves handle within that custom range
 *   - isScrubbingActive blocks event deselection
 * Expected: Smooth UX; no query duplication
 */

/**
 * EDGE CASE: Window size changes on small screens
 *
 * Device rotates from portrait to landscape mid-scrub
 * Behavior:
 *   - Scrubber track width changes
 *   - Scrub handle position recalculated via getBoundingClientRect()
 *   - User's drag continues accurately
 * Expected: Accurate scrubbing after rotation
 */

// ============================================================================
// PART 7: TESTING CHECKLIST
// ============================================================================

/*
Unit Tests (useTemporalNavigation):
[ ] selectPreset() resets stepOffset to 0
[ ] selectPreset() resets scrubPosition to 0.5
[ ] stepDateTime(1, "day") increments stepOffset
[ ] stepDateTime(-1, "day") decrements stepOffset
[ ] updateScrubPosition() clamps to [0, 1]
[ ] shouldClearSelectedEventOnWindowShift() returns true for large steps
[ ] shouldClearSelectedEventOnWindowShift() returns false during scrubbing
[ ] validationError catches missing timezone
[ ] validationError catches start >= end in custom range
[ ] buildMapFeedQuery() throws if validationError is set
[ ] debounceMs delays hasPendingTemporalChange reset

Component Tests (TemporalNavigationControls):
[ ] Preset buttons render with correct labels
[ ] Active preset button has :outline="false"
[ ] Clicking preset calls temporal.selectPreset()
[ ] Step buttons increment/decrement date display
[ ] Scrubber track responds to click
[ ] Scrub handle responds to mousedown/touchstart drag
[ ] handleMouseUp() removes document listeners
[ ] Status message updates with step offset
[ ] Validation error displays when present
[ ] Reset button clears all state
[ ] Mobile layout: preset buttons 2 per row on small screens

Integration Tests (App.vue):
[ ] Map query updates when temporal state changes
[ ] Calendar query updates when temporal state changes
[ ] Selected event is cleared on large window shift
[ ] Selected event is preserved during scrubbing
[ ] Rapid preset switching results in single API call
[ ] Debounce delay is respected before query sends

E2E Tests (Playwright):
[ ] User selects "Tonight" →events load
[ ] User selects "Now" → steps forward →calendar updates
[ ] User drags scrubber → events stay same, calendar focus shifts
[ ] User rotates device → scrubber still responsive
[ ] User taps "Reset" → returns to "Now"
*/

// ============================================================================
// PART 8: FUTURE ENHANCEMENTS
// ============================================================================

/*
Potential Post-MVP Additions:

1. Keyboard navigation:
   - Arrow keys to step date
   - Space to toggle scrubbing
   - Tab through controls

2. Snap-to-grid for scrubbing:
   - Snap to hour boundaries within window
   - Haptic feedback on snap

3. Animated window transitions:
   - Smoothly zoom calendar from one window to next
   - Scrubber position interpolation

4. Preset customization:
   - User-defined presets (e.g., "Weekend events only")
   - Saved temporal ranges

5. Time zone switching:
   - Allow user to view events in different timezone
   - Update all presets on switch

6. Advanced filtering:
   - Time + category + distance in single control
   - Temporal filter templates ("This week in Arts")

7. Temporal search history:
   - "Recent Windows" dropdown
   - Suggest ranges based on usage patterns

8. Undo/Redo:
   - Temporal navigation stack
   - Revert to previous window
*/

export {}; // Mark file as module
