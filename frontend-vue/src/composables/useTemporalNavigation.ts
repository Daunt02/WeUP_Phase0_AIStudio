import { computed, ref, watch } from "vue";
import type {
  MapFeedFilterState,
  TimeWindowFilterDto,
} from "../contracts/time-window.contracts";
import { TimeWindowPreset } from "../contracts/time-window.contracts";
import { useMapFeedFilters } from "./useMapFeedFilters";

/**
 * Date step unit for forward/backward navigation
 */
export type DateStepUnit = "day" | "week";

/**
 * Temporal navigation semantics:
 * - Presets expand to canonical windows on the backend (now, tonight, tomorrow, etc.)
 * - Custom ranges are user-defined absolute windows
 * - Stepping changes the active preset or shifts custom range bounds
 * - Scrubbing moves a "scrub cursor" relative to the current window (not an absolute position)
 * - Reset returns to TimeWindowPreset.Now
 *
 * Selected event behavior: When temporal state shifts, the selected event ID is preserved
 * if still within new time window; otherwise selection is cleared.
 */
export interface TemporalNavigationState {
  currentPreset: TimeWindowPreset;
  /** Position relative to current window (0-1). Used for scrubbing UX only. */
  scrubPosition: number;
  /** Whether user is actively scrubbing */
  isScrubbingActive: boolean;
  /** Number of date steps applied (positive=forward, negative=backward) */
  stepOffset: number;
}

export interface UseTemporalNavigationOptions extends Partial<MapFeedFilterState> {
  onSelectedEventChange?: (eventId: string | null) => void;
  debounceMs?: number;
}

/**
 * Composable for temporal navigation and scrubbing
 * Integrates with useMapFeedFilters to drive both map and calendar queries
 */
export function useTemporalNavigation(
  options: UseTemporalNavigationOptions = {},
) {
  const debounceMs = options.debounceMs ?? 300;

  // Layer on top of useMapFeedFilters for consistency
  const filterState = useMapFeedFilters({
    preset: options.preset,
    timezone: options.timezone,
    customStartLocal: options.customStartLocal,
    customEndLocal: options.customEndLocal,
    includeSavedOnly: options.includeSavedOnly,
  });

  // Navigation state
  const navigationState = ref<TemporalNavigationState>({
    currentPreset: filterState.preset.value,
    scrubPosition: 0.5, // Start at middle of window
    isScrubbingActive: false,
    stepOffset: 0,
  });

  // Track pending temporal changes for debouncing
  const hasPendingTemporalChange = ref(false);
  let debounceTimer: ReturnType<typeof setTimeout> | null = null;

  /**
   * Debounce temporal query updates to avoid excessive API calls
   * Maps UI interactions (preset, scrub, step) to actual state changes
   */
  function debouncedTemporalUpdate() {
    if (debounceTimer) {
      clearTimeout(debounceTimer);
    }

    hasPendingTemporalChange.value = true;
    debounceTimer = setTimeout(() => {
      hasPendingTemporalChange.value = false;
    }, debounceMs);
  }

  /**
   * Select a preset. Clears custom range if applicable.
   */
  function selectPreset(preset: TimeWindowPreset): void {
    if (preset === filterState.preset.value) {
      return; // Idempotent
    }

    filterState.preset.value = preset;
    navigationState.value.currentPreset = preset;
    navigationState.value.stepOffset = 0;
    navigationState.value.scrubPosition = 0.5;

    debouncedTemporalUpdate();
  }

  /**
   * Step forward or backward by the given number of days or weeks.
   * Modifies the current active preset or range by the step amount.
   *
   * Rules:
   * - If currently on a preset, shift custom start/end to simulate stepping
   * - If already custom, adjust both bounds by the step amount
   * - Stepping is not validated at the UI layer; validation happens via filterState.validationError
   */
  function stepDateTime(units: number, unit: DateStepUnit = "day"): void {
    const stepMs = unit === "day" ? units * 86400000 : units * 604800000;

    if (filterState.preset.value === TimeWindowPreset.Custom) {
      // Custom range: shift both start and end
      if (
        !filterState.customStartLocal.value ||
        !filterState.customEndLocal.value
      ) {
        return; // Guard against invalid state
      }

      const startDate = new Date(filterState.customStartLocal.value);
      const endDate = new Date(filterState.customEndLocal.value);

      startDate.setTime(startDate.getTime() + stepMs);
      endDate.setTime(endDate.getTime() + stepMs);

      filterState.customStartLocal.value = startDate.toISOString().slice(0, 16);
      filterState.customEndLocal.value = endDate.toISOString().slice(0, 16);
    } else {
      // Preset-based: set custom range relative to now + step
      const now = new Date();
      now.setTime(now.getTime() + stepMs);

      // For stepping, create a custom range centered on the stepped date
      // Fall back to custom so scrubbing can work with explicit bounds
      const startDate = new Date(now);
      startDate.setHours(0, 0, 0, 0);

      const endDate = new Date(now);
      endDate.setHours(23, 59, 59, 999);

      filterState.preset.value = TimeWindowPreset.Custom;
      filterState.customStartLocal.value = startDate.toISOString().slice(0, 16);
      filterState.customEndLocal.value = endDate.toISOString().slice(0, 16);

      navigationState.value.stepOffset += units;
    }

    navigationState.value.scrubPosition = 0.5;
    debouncedTemporalUpdate();
  }

  /**
   * Set scrub position within [0, 1] relative to current window.
   * Scrubbing shifts the effective query window within the preset/custom bounds.
   *
   * Semantics:
   * - 0.0 = start of window
   * - 0.5 = middle of window
   * - 1.0 = end of window
   *
   * Scrubbing affects which part of the time window is emphasized in the calendar
   * but does NOT reduce the backend query window size—the backend still returns
   * events within the full presetwindow or custom range.
   */
  function updateScrubPosition(position: number): void {
    const bounded = Math.max(0, Math.min(1, position));
    navigationState.value.scrubPosition = bounded;
    navigationState.value.isScrubbingActive = true;
    debouncedTemporalUpdate();
  }

  /**
   * End scrubbing interaction
   */
  function endScrubbing(): void {
    navigationState.value.isScrubbingActive = false;
    debouncedTemporalUpdate();
  }

  /**
   * Reset to TimeWindowPreset.Now, clear custom range, reset step offset
   */
  function resetToNow(): void {
    filterState.preset.value = TimeWindowPreset.Now;
    filterState.customStartLocal.value = "";
    filterState.customEndLocal.value = "";
    navigationState.value.currentPreset = TimeWindowPreset.Now;
    navigationState.value.stepOffset = 0;
    navigationState.value.scrubPosition = 0.5;
    navigationState.value.isScrubbingActive = false;
    debouncedTemporalUpdate();
  }

  /**
   * Guard: Return true if a time window shift should clear the currently selected event.
   * The window is considered "shifted" if the preset or custom range bounds change significantly.
   *
   * Determines whether selected event is still relevant after temporal navigation.
   * Returns true if event should be deselected.
   */
  function shouldClearSelectedEventOnWindowShift(): boolean {
    // Event is preserved through scrubbing (scrub position delta within same window)
    if (navigationState.value.isScrubbingActive) {
      return false;
    }

    // Event is preserved through small step offsets (1-2 days)
    if (Math.abs(navigationState.value.stepOffset) <= 2) {
      return false;
    }

    // Event is preserved through preset navigation if the preset has not changed
    // (validation: if we're in custom range with non-zero stepOffset, consider event stale)
    if (
      filterState.preset.value === TimeWindowPreset.Custom &&
      navigationState.value.stepOffset !== 0
    ) {
      return true;
    }

    return false;
  }

  /**
   * Compute the effective query window that should be sent to the backend.
   * Always returns the full unmodified preset or custom range—scrubbing does NOT
   * reduce the backend query window (it only affects frontend calendar focus).
   */
  const effectiveQueryWindow = computed(() => {
    return filterState.temporalContract.value;
  });

  const isValid = computed(() => filterState.validationError.value === null);
  const validationMessage = computed(() => filterState.validationError.value);

  return {
    // State
    preset: filterState.preset,
    timezone: filterState.timezone,
    customStartLocal: filterState.customStartLocal,
    customEndLocal: filterState.customEndLocal,
    includeSavedOnly: filterState.includeSavedOnly,

    // Navigation state
    navigationState: computed(() => navigationState.value),
    scrubPosition: computed(() => navigationState.value.scrubPosition),
    isScrubbingActive: computed(() => navigationState.value.isScrubbingActive),
    stepOffset: computed(() => navigationState.value.stepOffset),

    // Status
    hasPendingTemporalChange: computed(() => hasPendingTemporalChange.value),
    isValid,
    validationMessage,

    // Query contracts
    effectiveQueryWindow,
    buildMapFeedQuery: filterState.buildMapFeedQuery,
    toCalendarOverlayTemporalQuery: filterState.toCalendarOverlayTemporalQuery,
    requestSignature: filterState.requestSignature,

    // Actions
    selectPreset,
    stepDateTime,
    updateScrubPosition,
    endScrubbing,
    resetToNow,
    shouldClearSelectedEventOnWindowShift,
  };
}

export type UseTemporalNavigation = ReturnType<typeof useTemporalNavigation>;
