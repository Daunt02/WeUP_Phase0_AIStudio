import { computed, ref, watch, type Ref } from "vue";
import type { CalendarOverlayLayerState } from "../contracts/calendar-overlay.contracts";

export type CalendarTransitionPhase =
  | "idle"
  | "overlay-open"
  | "overlay-close"
  | "partial-to-expanded"
  | "event-select"
  | "event-deselect"
  | "filter-refresh";

const PHASE_DURATION_MS: Record<
  Exclude<CalendarTransitionPhase, "idle" | "filter-refresh">,
  number
> = {
  "overlay-open": 220,
  "overlay-close": 180,
  "partial-to-expanded": 160,
  "event-select": 140,
  "event-deselect": 120,
};

export function deriveOverlayPhase(
  previous: CalendarOverlayLayerState,
  next: CalendarOverlayLayerState,
): Exclude<
  CalendarTransitionPhase,
  "idle" | "event-select" | "event-deselect" | "filter-refresh"
> | null {
  if (previous === next) {
    return null;
  }

  if (previous === "closed" && next !== "closed") {
    return "overlay-open";
  }

  if (previous !== "closed" && next === "closed") {
    return "overlay-close";
  }

  if (
    (previous === "partial" && next === "expanded") ||
    (previous === "expanded" && next === "partial")
  ) {
    return "partial-to-expanded";
  }

  return null;
}

export interface UseCalendarTransitionStateOptions {
  layer: Ref<CalendarOverlayLayerState>;
  selectedEventId: Ref<string | null>;
  isFilterRefreshPending: Ref<boolean | undefined>;
}

export interface UseCalendarTransitionStateReturn {
  phase: Ref<CalendarTransitionPhase>;
  shouldDeferSelectionMotion: Readonly<Ref<boolean>>;
}

export function useCalendarTransitionState(
  options: UseCalendarTransitionStateOptions,
): UseCalendarTransitionStateReturn {
  const phase = ref<CalendarTransitionPhase>("idle");
  let phaseResetHandle: ReturnType<typeof setTimeout> | null = null;

  function clearPhaseReset(): void {
    if (phaseResetHandle) {
      clearTimeout(phaseResetHandle);
      phaseResetHandle = null;
    }
  }

  function enterFinitePhase(
    nextPhase: Exclude<CalendarTransitionPhase, "idle" | "filter-refresh">,
  ): void {
    clearPhaseReset();
    phase.value = nextPhase;
    phaseResetHandle = setTimeout(() => {
      phase.value = "idle";
      phaseResetHandle = null;
    }, PHASE_DURATION_MS[nextPhase]);
  }

  watch(
    options.isFilterRefreshPending,
    (isPending) => {
      if (isPending === true) {
        // Clarity over motion: while filters are refreshing, lock phase to
        // filter-refresh and suppress other motion semantics.
        clearPhaseReset();
        phase.value = "filter-refresh";
        return;
      }

      if (phase.value === "filter-refresh") {
        phase.value = "idle";
      }
    },
    { immediate: true },
  );

  watch(options.layer, (next, previous) => {
    if (options.isFilterRefreshPending.value === true) {
      return;
    }
    if (previous === undefined) {
      return;
    }

    const overlayPhase = deriveOverlayPhase(previous, next);
    if (overlayPhase) {
      enterFinitePhase(overlayPhase);
    }
  });

  watch(options.selectedEventId, (next, previous) => {
    if (options.isFilterRefreshPending.value === true) {
      return;
    }
    if (previous === undefined || next === previous) {
      return;
    }

    if (previous === null && next !== null) {
      enterFinitePhase("event-select");
      return;
    }

    if (previous !== null && next === null) {
      enterFinitePhase("event-deselect");
    }
  });

  const shouldDeferSelectionMotion = computed(
    () =>
      options.isFilterRefreshPending.value === true ||
      phase.value === "overlay-close",
  );

  return {
    phase,
    shouldDeferSelectionMotion,
  };
}
