/**
 * WEUP-SYNTH:
 * sources=[components/TimelineControl.tsx, components/CulturalCalendar.tsx]
 * destination=frontend-vue/src/composables/useTemporalNavigation.ts
 * mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
 * notes=G7 temporal synthesis. Module-level singleton: ONE authoritative
 * temporal state shared by every surface (map, calendar, shell), per mission
 * section 5. TimelineControl capabilities merged: temporal mode inventory
 * (NOW/TODAY/TONIGHT/TOMORROW/WEEKEND/CUSTOM), hold-to-scrub label arbitration
 * (NOW/6PM/9PM/MIDNIGHT/3AM/FRI/SAT/SUN) via scrubToLabel, scrub-cursor
 * semantics. CulturalCalendar capabilities merged: Houston-day selection
 * (selectHoustonDay) drives a canonical CustomRange transition so the date
 * strip and the map query can never diverge. All Houston arithmetic flows
 * through utils/houstonTime (mission section 17); the market timezone defaults
 * to America/Chicago. Frontend never expands preset windows; TODAY and day
 * selection are expressed as explicit CustomRange UTC bounds, which the
 * backend already resolves.
 */
import { computed, ref, type ComputedRef, type Ref } from "vue";
import type {
  MapFeedFilterState,
  TimeWindowFilterDto,
} from "../contracts/time-window.contracts";
import { TimeWindowPreset } from "../contracts/time-window.contracts";
import {
  HOUSTON_TIMEZONE,
  getTimezoneOffsetMs,
  getUpcomingWeek,
  houstonDayBoundsUtc,
  houstonDayKey,
  tonightCursorUtc,
  tonightFractionLabel,
} from "../utils/houstonTime";
import { useMapFeedFilters } from "./useMapFeedFilters";

/**
 * Date step unit for forward/backward navigation
 */
export type DateStepUnit = "day" | "week";

/**
 * Canonical UI temporal modes (mission section 5). NOW/TONIGHT/TOMORROW/WEEKEND
 * map 1:1 to backend presets; TODAY and day selection are expressed as
 * explicit CustomRange UTC bounds for a Houston calendar day; CUSTOM is the
 * user-defined range edited in the filter panel.
 */
export type TemporalMode =
  | "NOW"
  | "TODAY"
  | "TONIGHT"
  | "TOMORROW"
  | "WEEKEND"
  | "CUSTOM";

/**
 * TimelineControl scrub labels. NOW/6PM/9PM/MIDNIGHT/3AM are cursor positions
 * inside the Tonight window; FRI/SAT/SUN step to that weekday's Houston day.
 */
export type TimelineScrubLabel =
  | "NOW"
  | "6PM"
  | "9PM"
  | "MIDNIGHT"
  | "3AM"
  | "FRI"
  | "SAT"
  | "SUN";

export const TIMELINE_SCRUB_LABELS: readonly TimelineScrubLabel[] = [
  "NOW",
  "6PM",
  "9PM",
  "MIDNIGHT",
  "3AM",
  "FRI",
  "SAT",
  "SUN",
] as const;

/** Scrub fractions inside the Tonight window for the hour labels. */
const TONIGHT_SCRUB_FRACTIONS: Record<string, number> = {
  "6PM": 0,
  "9PM": 1 / 3,
  MIDNIGHT: 2 / 3,
  "3AM": 1,
};

/** JS weekday index for the FRI/SAT/SUN scrub labels. */
const WEEKDAY_LABEL_INDEX: Record<string, number> = {
  FRI: 5,
  SAT: 6,
  SUN: 0,
};

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
  /** Canonical UI temporal mode (mission section 5). */
  currentMode: TemporalMode;
  /** Houston day key ("YYYY-MM-DD") when a single day is selected, else null. */
  selectedDayKey: string | null;
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
  /**
   * Deterministic reference instant for day-boundary math. Defaults to now.
   * Tests pass an explicit instant; production leaves it unset.
   */
  refInstant?: string | Date;
}

// ─── Module-level singleton state ─────────────────────────────────────────────
// One authoritative temporal state (mission section 5). Every caller of
// useTemporalNavigation shares the same reactive references, mirroring the
// useDiscoveryState singleton pattern: map, calendar, and shell are views of
// one temporal session.

interface TemporalModuleState {
  filter: ReturnType<typeof useMapFeedFilters>;
  navigation: Ref<TemporalNavigationState>;
  debounceMs: number;
}

let moduleState: TemporalModuleState | null = null;

/**
 * Selection-change listeners registered by callers (e.g. MapSurface clears
 * selection through its canonical emit path). A set (not a single callback)
 * so multiple surfaces can observe without a second state machine.
 */
const selectionChangeListeners = new Set<(eventId: string | null) => void>();

function ensureModuleState(options: UseTemporalNavigationOptions): TemporalModuleState {
  if (moduleState) {
    return moduleState;
  }

  // Layer on top of useMapFeedFilters for consistency
  const filter = useMapFeedFilters({
    preset: options.preset,
    // Mission section 17: the domain contract requires Houston time. The
    // market timezone defaults to America/Chicago; a caller-supplied timezone
    // (e.g. the filter panel) still wins.
    timezone: options.timezone ?? HOUSTON_TIMEZONE,
    customStartLocal: options.customStartLocal,
    customEndLocal: options.customEndLocal,
    includeSavedOnly: options.includeSavedOnly,
  });

  const navigation = ref<TemporalNavigationState>({
    currentPreset: filter.preset.value,
    currentMode: presetToMode(filter.preset.value),
    selectedDayKey: null,
    scrubPosition: 0.5, // Start at middle of window
    isScrubbingActive: false,
    stepOffset: 0,
  });

  moduleState = {
    filter,
    navigation,
    debounceMs: options.debounceMs ?? 300,
  };
  return moduleState;
}

function presetToMode(preset: TimeWindowPreset): TemporalMode {
  switch (preset) {
    case TimeWindowPreset.Now:
      return "NOW";
    case TimeWindowPreset.Tonight:
      return "TONIGHT";
    case TimeWindowPreset.Tomorrow:
      return "TOMORROW";
    case TimeWindowPreset.ThisWeekend:
      return "WEEKEND";
    default:
      return "CUSTOM";
  }
}

/**
 * Test-only reset. Mirrors resetDiscoveryStateForTests.
 */
export function resetTemporalNavigationForTests(): void {
  moduleState = null;
  selectionChangeListeners.clear();
}

function currentRefInstant(optionsRef: { refInstant?: string | Date }): Date {
  return optionsRef.refInstant ? new Date(optionsRef.refInstant) : new Date();
}

/**
 * Composable for temporal navigation and scrubbing
 * Integrates with useMapFeedFilters to drive both map and calendar queries.
 * Singleton: every caller shares one authoritative temporal state.
 */
export function useTemporalNavigation(
  options: UseTemporalNavigationOptions = {},
) {
  const state = ensureModuleState(options);
  const { filter: filterState, navigation: navigationState } = state;
  const debounceMs = state.debounceMs;

  if (options.onSelectedEventChange) {
    selectionChangeListeners.add(options.onSelectedEventChange);
  }

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
   * Notify listeners when the window shift makes the current selection stale.
   */
  function notifySelectionIfStale(): void {
    if (shouldClearSelectedEventOnWindowShift()) {
      for (const listener of [...selectionChangeListeners]) {
        listener(null);
      }
    }
  }

  function applyMode(mode: TemporalMode): void {
    navigationState.value.currentMode = mode;
    if (mode !== "TODAY" && mode !== "CUSTOM") {
      navigationState.value.selectedDayKey = null;
    }
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
    applyMode(presetToMode(preset));

    debouncedTemporalUpdate();
    notifySelectionIfStale();
  }

  /**
   * Select one of the canonical UI temporal modes (mission section 5).
   * NOW/TONIGHT/TOMORROW/WEEKEND map to backend presets. TODAY selects the
   * Houston calendar day containing the reference instant as an explicit
   * CustomRange (UTC bounds computed once, in the houstonTime boundary).
   * CUSTOM selects the user-editable custom range.
   */
  function selectTemporalMode(
    mode: TemporalMode,
    refInstant?: string | Date,
  ): void {
    switch (mode) {
      case "NOW":
        selectPreset(TimeWindowPreset.Now);
        break;
      case "TONIGHT":
        selectPreset(TimeWindowPreset.Tonight);
        break;
      case "TOMORROW":
        selectPreset(TimeWindowPreset.Tomorrow);
        break;
      case "WEEKEND":
        selectPreset(TimeWindowPreset.ThisWeekend);
        break;
      case "TODAY": {
        const instant = refInstant ?? currentRefInstant(options);
        selectHoustonDay(houstonDayKey(instant), "TODAY");
        break;
      }
      case "CUSTOM":
        selectPreset(TimeWindowPreset.Custom);
        break;
    }
  }

  /**
   * Select a single Houston calendar day ("YYYY-MM-DD") as an explicit
   * CustomRange. This is the CulturalCalendar date-selector translation: one
   * canonical transition, so the calendar strip and the map query can never
   * diverge. `resolvedMode` lets TODAY keep its mode label.
   */
  function selectHoustonDay(
    dayKey: string,
    resolvedMode: TemporalMode = "CUSTOM",
  ): void {
    const { startUtc, endUtc } = houstonDayBoundsUtc(dayKey);

    filterState.preset.value = TimeWindowPreset.CustomRange;
    filterState.customStartLocal.value = startUtc;
    filterState.customEndLocal.value = endUtc;
    navigationState.value.currentPreset = TimeWindowPreset.CustomRange;
    navigationState.value.stepOffset = 0;
    navigationState.value.scrubPosition = 0.5;
    navigationState.value.selectedDayKey = dayKey;
    applyMode(resolvedMode);
    navigationState.value.selectedDayKey = dayKey;

    debouncedTemporalUpdate();
    notifySelectionIfStale();
  }

  /**
   * Step forward or backward by the given number of days or weeks.
   *
   * Rules:
   * - Custom range: shift both bounds by the step amount (pure ms arithmetic,
   *   timezone-safe).
   * - Preset: resolve the target Houston calendar day and select it as an
   *   explicit CustomRange via the houstonTime boundary (no device-local
   *   day-boundary math).
   */
  function stepDateTime(
    units: number,
    unit: DateStepUnit = "day",
    refInstant?: string | Date,
  ): void {
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
      navigationState.value.stepOffset += units;
    } else if (filterState.preset.value === TimeWindowPreset.CustomRange) {
      if (
        !filterState.customStartLocal.value ||
        !filterState.customEndLocal.value
      ) {
        return;
      }
      const startDate = new Date(filterState.customStartLocal.value);
      const endDate = new Date(filterState.customEndLocal.value);
      startDate.setTime(startDate.getTime() + stepMs);
      endDate.setTime(endDate.getTime() + stepMs);
      filterState.customStartLocal.value = startDate.toISOString();
      filterState.customEndLocal.value = endDate.toISOString();
      navigationState.value.stepOffset += units;
      // Stepping off an explicit day selection clears the day key unless the
      // result is still exactly one Houston day.
      const key = houstonDayKey(startDate);
      const bounds = houstonDayBoundsUtc(key);
      if (
        bounds.startUtc === filterState.customStartLocal.value &&
        bounds.endUtc === filterState.customEndLocal.value
      ) {
        navigationState.value.selectedDayKey = key;
      } else {
        navigationState.value.selectedDayKey = null;
      }
      applyMode("CUSTOM");
    } else {
      // Preset-based: select the target Houston calendar day explicitly.
      const instant = refInstant ?? currentRefInstant(options);
      const week = getUpcomingWeek(instant);
      const baseKey = houstonDayKey(instant);
      const baseIndex = week.findIndex((day) => day.key === baseKey);
      const targetIndex = baseIndex + (unit === "day" ? units : units * 7);
      let targetKey: string;
      if (targetIndex >= 0 && targetIndex < week.length) {
        targetKey = week[targetIndex].key;
      } else {
        // Beyond the strip: walk Houston days arithmetically.
        const bounds = houstonDayBoundsUtc(baseKey);
        const targetStart = new Date(
          Date.parse(bounds.startUtc) + stepMs,
        );
        targetKey = houstonDayKey(targetStart);
      }
      selectHoustonDay(targetKey, "CUSTOM");
      navigationState.value.stepOffset += units;
    }

    navigationState.value.scrubPosition = 0.5;
    debouncedTemporalUpdate();
    notifySelectionIfStale();
  }

  /**
   * TimelineControl label arbitration (G3 chain): NOW selects the now mode;
   * 6PM/9PM/MIDNIGHT/3AM move the scrub cursor inside the Tonight window
   * (switching to TONIGHT first when necessary); FRI/SAT/SUN step to that
   * weekday's Houston day as an explicit custom range.
   */
  function scrubToLabel(
    label: TimelineScrubLabel,
    refInstant?: string | Date,
  ): void {
    if (label === "NOW") {
      selectTemporalMode("NOW");
      return;
    }

    const fraction = TONIGHT_SCRUB_FRACTIONS[label];
    if (fraction !== undefined) {
      if (navigationState.value.currentMode !== "TONIGHT") {
        selectTemporalMode("TONIGHT");
      }
      updateScrubPosition(fraction);
      endScrubbing();
      return;
    }

    const weekdayIndex = WEEKDAY_LABEL_INDEX[label];
    if (weekdayIndex !== undefined) {
      const instant = refInstant ? new Date(refInstant) : currentRefInstant(options);
      // Houston weekday of the reference instant.
      const wallWeekday = new Date(
        instant.getTime() +
          houstonOffsetForWeekday(instant, filterState.timezone.value),
      ).getUTCDay();
      const delta = (weekdayIndex - wallWeekday + 7) % 7;
      stepDateTime(delta, "day", instant);
    }
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
   * events within the full preset window or custom range.
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
    navigationState.value.selectedDayKey = null;
    applyMode("NOW");
    debouncedTemporalUpdate();
    notifySelectionIfStale();
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

    if (
      filterState.preset.value === TimeWindowPreset.CustomRange &&
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

  /** Canonical UI temporal mode (mission section 5). */
  const activeTemporalMode: ComputedRef<TemporalMode> = computed(
    () => navigationState.value.currentMode,
  );

  /** Houston day key when a single day is selected, else null. */
  const activeDayKey: ComputedRef<string | null> = computed(
    () => navigationState.value.selectedDayKey,
  );

  /**
   * Cursor instant inside the Tonight window for the current scrub position.
   * Null unless the active mode is TONIGHT. Consumed by the calendar
   * day-column projection as the scrub-cursor marker (real derived state, not
   * decorative animation).
   */
  const scrubCursorUtc: ComputedRef<string | null> = computed(() => {
    if (navigationState.value.currentMode !== "TONIGHT") {
      return null;
    }
    const instant = options.refInstant ? new Date(options.refInstant) : new Date();
    return tonightCursorUtc(
      instant,
      navigationState.value.scrubPosition,
      filterState.timezone.value || HOUSTON_TIMEZONE,
    );
  });

  /** Nearest hour label for the current scrub cursor (6PM/9PM/MIDNIGHT/3AM). */
  const scrubCursorLabel: ComputedRef<string | null> = computed(() => {
    if (navigationState.value.currentMode !== "TONIGHT") {
      return null;
    }
    return tonightFractionLabel(navigationState.value.scrubPosition);
  });

  /**
   * URL/query-state projection of the temporal query (mission section 5.6).
   * Pure serialization of the authoritative state; the shell can adopt it when
   * a router is mounted. The app currently has no router: reload continuity is
   * provided by the anonymous discovery-context persistence in App.vue.
   */
  function toQueryParams(): URLSearchParams {
    const query = effectiveQueryWindow.value;
    const params = new URLSearchParams();
    params.set("preset", query.preset);
    params.set("timezone", query.marketTimezone ?? query.timezone);
    if (query.fromUtc) {
      params.set("fromUtc", query.fromUtc);
    }
    if (query.toUtc) {
      params.set("toUtc", query.toUtc);
    }
    return params;
  }

  /**
   * Restore temporal state from URL query params (inverse of toQueryParams).
   * Unknown presets are ignored (fail-closed, no invented state).
   */
  function fromQueryParams(params: URLSearchParams): boolean {
    const presetParam = params.get("preset");
    const validPresets = Object.values(TimeWindowPreset) as string[];
    if (!presetParam || !validPresets.includes(presetParam)) {
      return false;
    }
    const preset = presetParam as TimeWindowPreset;
    const timezone = params.get("timezone");
    if (timezone) {
      filterState.timezone.value = timezone;
    }
    if (
      (preset === TimeWindowPreset.CustomRange ||
        preset === TimeWindowPreset.Custom) &&
      params.get("fromUtc") &&
      params.get("toUtc")
    ) {
      filterState.preset.value = preset;
      filterState.customStartLocal.value = params.get("fromUtc") as string;
      filterState.customEndLocal.value = params.get("toUtc") as string;
      navigationState.value.currentPreset = preset;
      navigationState.value.stepOffset = 0;
      navigationState.value.scrubPosition = 0.5;
      navigationState.value.selectedDayKey = null;
      applyMode("CUSTOM");
      debouncedTemporalUpdate();
      notifySelectionIfStale();
      return true;
    }
    selectPreset(preset);
    return true;
  }

  return {
    // State
    preset: filterState.preset,
    timezone: filterState.timezone,
    customStartLocal: filterState.customStartLocal,
    customEndLocal: filterState.customEndLocal,
    includeSavedOnly: filterState.includeSavedOnly,

    // Navigation state
    navigationState: computed(() => navigationState.value),
    activeTemporalMode,
    activeDayKey,
    scrubPosition: computed(() => navigationState.value.scrubPosition),
    isScrubbingActive: computed(() => navigationState.value.isScrubbingActive),
    stepOffset: computed(() => navigationState.value.stepOffset),
    scrubCursorUtc,
    scrubCursorLabel,

    // Status
    hasPendingTemporalChange: computed(() => hasPendingTemporalChange.value),
    isValid,
    validationMessage,
    validationError: filterState.validationError,

    // Query contracts
    effectiveQueryWindow,
    buildMapFeedQuery: filterState.buildMapFeedQuery,
    toCalendarOverlayTemporalQuery: filterState.toCalendarOverlayTemporalQuery,
    requestSignature: filterState.requestSignature,

    // URL/query-state projection
    toQueryParams,
    fromQueryParams,

    // Actions
    selectPreset,
    selectTemporalMode,
    selectHoustonDay,
    scrubToLabel,
    stepDateTime,
    updateScrubPosition,
    endScrubbing,
    resetToNow,
    shouldClearSelectedEventOnWindowShift,
  };
}

/**
 * Houston wall-clock offset helper used for weekday arithmetic in
 * scrubToLabel. Delegates to the canonical houstonTime boundary.
 */
function houstonOffsetForWeekday(instant: Date, timeZone: string): number {
  return getTimezoneOffsetMs(timeZone || HOUSTON_TIMEZONE, instant);
}

export type UseTemporalNavigation = ReturnType<typeof useTemporalNavigation>;
