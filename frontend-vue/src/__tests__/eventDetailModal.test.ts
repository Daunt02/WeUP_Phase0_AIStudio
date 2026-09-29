/**
 * WEUP-SYNTH G8 tests: canonical event surface behavior.
 * Mission: WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
 */
import { defineComponent } from "vue";
import { mount, flushPromises } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { EventDetailDto } from "../contracts/event-detail.contracts";
import EventDetailModal from "../components/EventDetailModal.vue";
import { resetTemporalNavigationForTests } from "../composables/useTemporalNavigation";
import { resetDiscoveryStateForTests } from "../composables/useDiscoveryState";

// EventDetailModal reads $q.screen for its compact breakpoint (same pattern
// as the App event-detail flow test, which mocks useQuasar).
vi.mock("quasar", async () => {
  const actual = await vi.importActual<typeof import("quasar")>("quasar");

  return {
    ...actual,
    useQuasar: () => ({
      screen: { lt: { md: false } },
    }),
  };
});

function buildEvent(overrides: Partial<EventDetailDto> = {}): EventDetailDto {
  return {
    id: "evt-001",
    title: "Neon Bloom",
    description: "A late-night set.",
    venueName: "Warehouse Live",
    address: "813 St Emanuel St, Houston, TX",
    lat: 29.7566,
    lng: -95.3597,
    category: "nightlife",
    categories: ["nightlife"],
    startUtc: "2026-09-30T02:00:00Z",
    endUtc: "2026-09-30T05:00:00Z",
    timezone: "America/Chicago",
    flyerImageUrl: "https://example.com/flyer.jpg",
    mediaRefs: [
      { url: "https://example.com/flyer.jpg", kind: "poster" },
      { url: "https://example.com/photo.jpg", kind: "image" },
    ],
    tags: ["house", "late-night"],
    status: "PUBLISHED",
    confidence: 0.85,
    sourceKind: "venue-feed",
    provenanceSummary: {
      primarySourceKind: "venue-feed",
      sourceCount: 3,
      firstObservedAtUtc: "2026-09-28T10:00:00Z",
      lastObservedAtUtc: "2026-09-29T12:00:00Z",
      summaryLabel: "Observed across 3 venue feeds",
    },
    savedByCurrentUser: false,
    version: 3,
    lastChangeType: "title-updated",
    concurrencyToken: null,
    ...overrides,
  };
}

const GenericStub = (name: string) =>
  defineComponent({
    name,
    template: `<div class="${name}"><slot /></div>`,
  });

const QBtnStub = defineComponent({
  name: "QBtnStub",
  props: { label: String },
  emits: ["click"],
  template: `<button class="q-btn-stub" @click="$emit('click', $event)">{{ label }}<slot /></button>`,
});

const QImgStub = defineComponent({
  name: "QImgStub",
  props: { src: String, alt: String },
  template: `<img class="q-img-stub" :src="src" :alt="alt" />`,
});

const QIconStub = defineComponent({
  name: "QIconStub",
  props: { name: String },
  template: `<i class="q-icon-stub">{{ name }}</i>`,
});

const QChipStub = defineComponent({
  name: "QChipStub",
  template: `<span class="q-chip-stub"><slot /></span>`,
});

const QBadgeStub = defineComponent({
  name: "QBadgeStub",
  template: `<span class="q-badge-stub"><slot /></span>`,
});

function mountModal(props: Record<string, unknown> = {}) {
  return mount(EventDetailModal, {
    props: {
      modelValue: true,
      event: null,
      isLoading: false,
      isSavePending: false,
      error: null,
      ...props,
    },
    global: {
      stubs: {
        "q-dialog": GenericStub("q-dialog"),
        "q-card": GenericStub("q-card"),
        "q-card-section": GenericStub("q-card-section"),
        "q-separator": GenericStub("q-separator"),
        "q-card-actions": GenericStub("q-card-actions"),
        "q-skeleton": GenericStub("q-skeleton"),
        "q-banner": GenericStub("q-banner"),
        "q-img": QImgStub,
        "q-badge": QBadgeStub,
        "q-icon": QIconStub,
        "q-chip": QChipStub,
        "q-btn": QBtnStub,
      },
    },
  });
}

function findButton(wrapper: ReturnType<typeof mount>, text: string) {
  const buttons = wrapper.findAll("button.q-btn-stub");
  const match = buttons.find((button) =>
    button.text().toLowerCase().includes(text.toLowerCase()),
  );
  if (!match) {
    throw new Error(`button containing "${text}" not found`);
  }
  return match;
}

describe("EventDetailModal (G8 event surface)", () => {
  beforeEach(() => {
    resetTemporalNavigationForTests();
    resetDiscoveryStateForTests();
    vi.restoreAllMocks();
  });

  it("renders canonical data with cited signal chips", () => {
    const wrapper = mountModal({ event: buildEvent() });
    const text = wrapper.text();

    expect(text).toContain("Neon Bloom");
    expect(text).toContain("Warehouse Live");
    // Signal chips derived from canonical fields with source citations.
    expect(text).toContain("Signal strength");
    expect(text).toContain("85%");
    expect(text).toContain("src: EventDetailDto.confidence");
    expect(text).toContain("src: EventDetailDto.status");
    expect(text).toContain("src: EventDetailDto.sourceKind");
    // Flyer grammar: 4/5 media block with caption.
    expect(text).toContain("Event flyer");
    const flyer = wrapper.find(".flyer-media img.q-img-stub");
    expect(flyer.attributes("src")).toBe("https://example.com/flyer.jpg");
  });

  it("shows the AWAITING SIGNAL overlay for NEEDS_REVIEW media policy", () => {
    const wrapper = mountModal({
      event: buildEvent({ status: "NEEDS_REVIEW" }),
    });

    expect(wrapper.find(".awaiting-signal-overlay").exists()).toBe(true);
    expect(wrapper.text()).toContain("Awaiting signal");
    expect(wrapper.text()).toContain("awaiting signal");
  });

  it("does not show the pending overlay for published events", () => {
    const wrapper = mountModal({ event: buildEvent() });
    expect(wrapper.find(".awaiting-signal-overlay").exists()).toBe(false);
  });

  it("renders the session-kind chip when the session kind is known", () => {
    const wrapper = mountModal({
      event: buildEvent(),
      sessionKind: "anonymous",
    });
    expect(wrapper.text()).toContain("anonymous");
  });

  it("wires save, share, and directions to real actions", () => {
    const openSpy = vi
      .spyOn(window, "open")
      .mockImplementation(() => null);
    const wrapper = mountModal({ event: buildEvent() });

    findButton(wrapper, "Save").trigger("click");
    expect(wrapper.emitted("toggle-save")).toBeTruthy();

    findButton(wrapper, "Share").trigger("click");
    expect(wrapper.emitted("share")).toBeTruthy();

    findButton(wrapper, "Directions").trigger("click");
    expect(openSpy).toHaveBeenCalledWith(
      expect.stringContaining("google.com/maps/dir"),
      "_blank",
      "noopener,noreferrer",
    );
    expect(openSpy.mock.calls[0][0]).toContain("29.7566");
    expect(openSpy.mock.calls[0][0]).toContain("-95.3597");
  });

  it("opens the provenance drawer with the 8-stage chain", async () => {
    const wrapper = mountModal({ event: buildEvent() });

    findButton(wrapper, "View provenance").trigger("click");
    await flushPromises();

    const drawer = wrapper.find(".provenance-drawer-root");
    expect(drawer.exists()).toBe(true);
    const stageNames = wrapper.findAll(".stage-name").map((node) => node.text());
    expect(stageNames).toEqual([
      "Source",
      "Ingestion job",
      "Extraction",
      "Normalization",
      "Confidence",
      "Issues",
      "Canonical event",
      "UI projection",
    ]);

    // Stubbed backend stages are labeled pending, never success.
    const badges = wrapper.findAll(".stage-badge").map((node) => node.text());
    expect(badges).toContain("Pending");
    expect(badges).toContain("Unavailable");
    // No fabricated purchase artifacts.
    expect(wrapper.text()).not.toContain("DEVICE KEY ACTIVATED");
    expect(wrapper.text()).not.toContain("WEUP-#");
  });

  it("renders the DynamicEvent signal-lost grammar when no event resolves", () => {
    const wrapper = mountModal({ event: null });

    expect(wrapper.text()).toContain("Signal Lost");
    expect(wrapper.text()).toContain("current sector");

    findButton(wrapper, "Return to radar").trigger("click");
    expect(wrapper.emitted("update:modelValue")?.[0]).toEqual([false]);
  });

  it("renders the temporal context from the temporal singleton", () => {
    const wrapper = mountModal({ event: buildEvent() });
    const text = wrapper.text();

    expect(text).toContain("Temporal context");
    // The mode comes from the singleton, not local modal state.
    expect(text).toContain("Viewing in");
  });

  it("renders loading skeletons while loading and the error banner on error", () => {
    const loading = mountModal({ isLoading: true });
    expect(loading.find(".hero-skeleton").exists()).toBe(true);

    const failed = mountModal({ error: "Detail request failed" });
    expect(failed.text()).toContain("Detail request failed");
  });

  it("G9: exposes the prototype social layer only for saved events", async () => {
    // Stub the prototype panel so this test stays at the affordance level;
    // the panel's own isolation contract is covered in g9SavedProfilePanels.
    const SocialStub = defineComponent({
      name: "SocialSignalPanelStub",
      props: { isOpen: { type: Boolean, default: false } },
      template: `<div class="social-signal-panel-stub" :data-open="isOpen"></div>`,
    });

    const mountWithSocialStub = (props: Record<string, unknown>) =>
      mount(EventDetailModal, {
        props: {
          modelValue: true,
          event: null,
          isLoading: false,
          isSavePending: false,
          error: null,
          ...props,
        },
        global: {
          stubs: {
            "q-dialog": GenericStub("q-dialog"),
            "q-card": GenericStub("q-card"),
            "q-card-section": GenericStub("q-card-section"),
            "q-separator": GenericStub("q-separator"),
            "q-card-actions": GenericStub("q-card-actions"),
            "q-skeleton": GenericStub("q-skeleton"),
            "q-banner": GenericStub("q-banner"),
            "q-img": QImgStub,
            "q-badge": QBadgeStub,
            "q-icon": QIconStub,
            "q-chip": QChipStub,
            "q-btn": QBtnStub,
            SocialSignalPanel: SocialStub,
          },
        },
      });

    const saved = mountWithSocialStub({
      event: buildEvent({ savedByCurrentUser: true }),
    });
    await findButton(saved, "Social").trigger("click");
    expect(
      saved
        .findComponent({ name: "SocialSignalPanelStub" })
        .attributes("data-open"),
    ).toBe("true");

    // Unsaved events have no "Interest Saved" entry affordance.
    const unsaved = mountWithSocialStub({
      event: buildEvent({ savedByCurrentUser: false }),
    });
    expect(() => findButton(unsaved, "Social")).toThrow(/not found/);
  });
});
