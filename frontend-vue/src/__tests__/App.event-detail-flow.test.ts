import { defineComponent, nextTick } from "vue";
import { mount, flushPromises } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";

const { notify, fetchEventDetail, fetchSavedState, saveEvent, unsaveEvent } =
  vi.hoisted(() => ({
    notify: vi.fn(),
    fetchEventDetail: vi.fn(),
    fetchSavedState: vi.fn(),
    saveEvent: vi.fn(),
    unsaveEvent: vi.fn(),
  }));

vi.mock("quasar", async () => {
  const actual = await vi.importActual<typeof import("quasar")>("quasar");

  return {
    ...actual,
    useQuasar: () => ({
      notify,
    }),
  };
});

vi.mock("../components/MapSurface.vue", () => ({
  default: defineComponent({
    name: "MapSurfaceStub",
    props: {
      selectedEventId: {
        type: String,
        default: null,
      },
      selectedEventSavedState: {
        type: Boolean,
        default: null,
      },
      activeFilters: {
        type: Object,
        default: null,
      },
    },
    emits: [
      "update:selectedEventId",
      "map-feed-query-updated",
      "map-items-updated",
    ],
    template: `
      <div>
        <button
          data-testid="marker-click"
          @click="$emit('update:selectedEventId', 'evt-map-001')"
        >
          select marker
        </button>
        <div data-testid="map-selected-id">{{ selectedEventId ?? 'none' }}</div>
        <div data-testid="map-saved-state">{{ selectedEventSavedState === null ? 'none' : String(selectedEventSavedState) }}</div>
      </div>
    `,
  }),
}));

vi.mock("../components/CalendarOverlayShell.vue", () => ({
  default: defineComponent({
    name: "CalendarOverlayShellStub",
    props: {
      layer: {
        type: String,
        required: true,
      },
      overlayHeight: {
        type: String,
        required: true,
      },
      selectedEventId: {
        type: String,
        default: null,
      },
      items: {
        type: Array,
        default: () => [],
      },
    },
    emits: ["select-event", "set-layer"],
    template: `
      <div>
        <button
          data-testid="calendar-click"
          @click="$emit('select-event', 'evt-cal-002')"
        >
          select calendar event
        </button>
        <div data-testid="calendar-selected-id">{{ selectedEventId ?? 'none' }}</div>
        <div data-testid="calendar-layer">{{ layer }}</div>
      </div>
    `,
  }),
}));

vi.mock("../components/EventDetailModal.vue", () => ({
  default: defineComponent({
    name: "EventDetailModalStub",
    props: {
      modelValue: {
        type: Boolean,
        required: true,
      },
      event: {
        type: Object,
        default: null,
      },
      isLoading: {
        type: Boolean,
        required: true,
      },
      isSavePending: {
        type: Boolean,
        required: true,
      },
      error: {
        type: String,
        default: null,
      },
    },
    emits: ["update:modelValue", "toggle-save", "share"],
    template: `
      <div>
        <div data-testid="modal-open">{{ String(modelValue) }}</div>
        <div data-testid="modal-title">{{ event?.title ?? 'none' }}</div>
        <button data-testid="modal-close" @click="$emit('update:modelValue', false)">
          close
        </button>
        <button data-testid="toggle-save" @click="$emit('toggle-save')">
          toggle save
        </button>
      </div>
    `,
  }),
}));

vi.mock("../services/eventDetailService", () => ({
  fetchEventDetail,
  fetchSavedState,
  saveEvent,
  unsaveEvent,
  shareEventDetail: vi.fn(),
}));

import App from "../App.vue";
import { resetDiscoveryStateForTests } from "../composables/useDiscoveryState";

const SlotStub = defineComponent({
  template: "<div><slot /></div>",
});

function mountApp() {
  return mount(App, {
    global: {
      stubs: {
        "q-layout": SlotStub,
        "q-header": SlotStub,
        "q-toolbar": SlotStub,
        "q-toolbar-title": SlotStub,
        "q-page-container": SlotStub,
        "q-page": SlotStub,
      },
    },
  });
}

describe("App event detail interaction flow", () => {
  beforeEach(() => {
    resetDiscoveryStateForTests();
    notify.mockReset();
    fetchEventDetail.mockReset();
    fetchSavedState.mockReset();
    saveEvent.mockReset();
    unsaveEvent.mockReset();

    fetchSavedState.mockResolvedValue({
      eventId: "evt-map-001",
      saved: false,
      sessionKind: "authenticated",
      persistenceSource: "backend",
    });

    fetchEventDetail.mockImplementation(async (eventId: string) => ({
      event: {
        id: eventId,
        title:
          eventId === "evt-cal-002" ? "Sunrise Session" : "Midnight Groove",
        description: "Canonical event detail.",
        venueName: "Warehouse 9",
        address: "100 Main St, Houston, TX",
        lat: 29.76,
        lng: -95.36,
        category: "nightlife",
        categories: ["nightlife"],
        startUtc: "2026-04-20T20:00:00Z",
        endUtc: null,
        timezone: "America/Chicago",
        flyerImageUrl: null,
        mediaRefs: [],
        tags: ["dj"],
        status: "PUBLISHED",
        confidence: 0.91,
        sourceKind: "manual_submission",
        provenanceSummary: {
          primarySourceKind: "manual_submission",
          sourceCount: 1,
          firstObservedAtUtc: "2026-04-19T20:00:00Z",
          lastObservedAtUtc: "2026-04-20T20:00:00Z",
          summaryLabel: "Normalized from manual submission.",
        },
        savedByCurrentUser: false,
        version: 1,
        lastChangeType: null,
        concurrencyToken: `${eventId}:v1`,
      },
    }));

    saveEvent.mockResolvedValue({
      eventId: "evt-map-001",
      saved: true,
      message: "Saved",
    });
    unsaveEvent.mockResolvedValue({
      eventId: "evt-map-001",
      saved: false,
      message: "Unsaved",
    });
  });

  it("opens the modal from marker selection and clears selection when the modal closes", async () => {
    const wrapper = mountApp();

    expect(wrapper.get('[data-testid="modal-open"]').text()).toBe("false");
    expect(wrapper.get('[data-testid="map-selected-id"]').text()).toBe("none");

    await wrapper.get('[data-testid="marker-click"]').trigger("click");
    await flushPromises();

    expect(fetchEventDetail).toHaveBeenCalledWith("evt-map-001");
    expect(wrapper.get('[data-testid="modal-open"]').text()).toBe("true");
    expect(wrapper.get('[data-testid="modal-title"]').text()).toBe(
      "Midnight Groove",
    );
    expect(wrapper.get('[data-testid="map-selected-id"]').text()).toBe(
      "evt-map-001",
    );

    await wrapper.get('[data-testid="modal-close"]').trigger("click");
    await nextTick();

    expect(wrapper.get('[data-testid="modal-open"]').text()).toBe("false");
    expect(wrapper.get('[data-testid="map-selected-id"]').text()).toBe("none");
  });

  it("pushes save-state changes back into the map selection props immediately", async () => {
    const wrapper = mountApp();

    await wrapper.get('[data-testid="marker-click"]').trigger("click");
    await flushPromises();

    expect(wrapper.get('[data-testid="map-saved-state"]').text()).toBe("false");

    await wrapper.get('[data-testid="toggle-save"]').trigger("click");
    await flushPromises();

    expect(saveEvent).toHaveBeenCalledWith({ eventId: "evt-map-001" });
    expect(wrapper.get('[data-testid="map-saved-state"]').text()).toBe("true");
  });

  it("routes calendar selection through the same shared selected-event state", async () => {
    const wrapper = mountApp();

    expect(wrapper.get('[data-testid="calendar-selected-id"]').text()).toBe(
      "none",
    );
    expect(wrapper.get('[data-testid="calendar-layer"]').text()).toBe("closed");

    await wrapper.get('[data-testid="calendar-click"]').trigger("click");
    await flushPromises();

    expect(fetchEventDetail).toHaveBeenCalledWith("evt-cal-002");
    expect(wrapper.get('[data-testid="map-selected-id"]').text()).toBe(
      "evt-cal-002",
    );
    expect(wrapper.get('[data-testid="calendar-selected-id"]').text()).toBe(
      "evt-cal-002",
    );
    expect(wrapper.get('[data-testid="calendar-layer"]').text()).toBe(
      "event-selected",
    );
    expect(wrapper.get('[data-testid="modal-open"]').text()).toBe("true");
    expect(wrapper.get('[data-testid="modal-title"]').text()).toBe(
      "Sunrise Session",
    );
  });
});
