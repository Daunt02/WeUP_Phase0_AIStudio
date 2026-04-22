import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { nextTick, ref } from "vue";
import {
  deriveOverlayPhase,
  useCalendarTransitionState,
} from "../composables/useCalendarTransitionState";
import type { CalendarOverlayLayerState } from "../contracts/calendar-overlay.contracts";

describe("deriveOverlayPhase", () => {
  it("maps overlay open/close and partial-expanded transitions", () => {
    expect(deriveOverlayPhase("closed", "partial")).toBe("overlay-open");
    expect(deriveOverlayPhase("partial", "closed")).toBe("overlay-close");
    expect(deriveOverlayPhase("partial", "expanded")).toBe(
      "partial-to-expanded",
    );
  });

  it("returns null for non-overlay-only changes", () => {
    expect(deriveOverlayPhase("event-selected", "expanded")).toBeNull();
    expect(deriveOverlayPhase("expanded", "event-selected")).toBeNull();
  });
});

describe("useCalendarTransitionState", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  function setup() {
    const layer = ref<CalendarOverlayLayerState>("closed");
    const selectedEventId = ref<string | null>(null);
    const isFilterRefreshPending = ref(false);

    const model = useCalendarTransitionState({
      layer,
      selectedEventId,
      isFilterRefreshPending,
    });

    return {
      layer,
      selectedEventId,
      isFilterRefreshPending,
      model,
    };
  }

  it("enters overlay-open then returns to idle", async () => {
    const { layer, model } = setup();

    layer.value = "partial";
    await nextTick();

    expect(model.phase.value).toBe("overlay-open");

    vi.advanceTimersByTime(220);
    await nextTick();

    expect(model.phase.value).toBe("idle");
  });

  it("tracks event-select and event-deselect transitions", async () => {
    const { selectedEventId, model } = setup();

    selectedEventId.value = "evt-100";
    await nextTick();

    expect(model.phase.value).toBe("event-select");

    vi.advanceTimersByTime(140);
    await nextTick();

    selectedEventId.value = null;
    await nextTick();

    expect(model.phase.value).toBe("event-deselect");
  });

  it("locks to filter-refresh while pending and defers selection motion", async () => {
    const { isFilterRefreshPending, selectedEventId, model } = setup();

    isFilterRefreshPending.value = true;
    await nextTick();

    expect(model.phase.value).toBe("filter-refresh");
    expect(model.shouldDeferSelectionMotion.value).toBe(true);

    selectedEventId.value = "evt-200";
    await nextTick();

    expect(model.phase.value).toBe("filter-refresh");

    isFilterRefreshPending.value = false;
    await nextTick();

    expect(model.phase.value).toBe("idle");
  });
});
