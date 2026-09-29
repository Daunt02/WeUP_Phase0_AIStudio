/**
 * WEUP-SYNTH (G6 — Navigation) verification:
 * mode-state transitions, the mode→surface mapping contract, and the
 * presentational navigation components (WeupBottomNav, WeupTopBar).
 */
import { describe, expect, it, beforeEach } from "vitest";
import { mount } from "@vue/test-utils";
import {
  NAV_SURFACE_MAP,
  WEUP_NAV_MODES,
  isWeupNavMode,
  type WeupNavMode,
} from "../navigation/weupNavModes";
import {
  resetWeupNavModeForTests,
  useWeupNavMode,
} from "../composables/useWeupNavMode";
import WeupBottomNav from "../components/WeupBottomNav.vue";
import WeupTopBar from "../components/WeupTopBar.vue";

describe("weupNavModes contract", () => {
  it("defines all five canonical navigation modes", () => {
    expect([...WEUP_NAV_MODES]).toEqual([
      "DISCOVER",
      "ACTIVITY",
      "SAVED",
      "PROFILE",
      "CREATE",
    ]);
  });

  it("maps every mode to a declared surface — no dead navigation entries", () => {
    for (const mode of WEUP_NAV_MODES) {
      const mapping = NAV_SURFACE_MAP[mode];
      expect(mapping.mode).toBe(mode);
      expect(mapping.vueSurface.length).toBeGreaterThan(0);
      expect(mapping.sourceBehavior.length).toBeGreaterThan(0);
    }
  });

  it("marks DISCOVER/ACTIVITY/SAVED as ADAPTED with real G6 surfaces", () => {
    expect(NAV_SURFACE_MAP.DISCOVER.disposition).toBe("ADAPTED");
    expect(NAV_SURFACE_MAP.ACTIVITY.disposition).toBe("ADAPTED");
    expect(NAV_SURFACE_MAP.SAVED.disposition).toBe("ADAPTED");
    expect(NAV_SURFACE_MAP.DISCOVER.owner).toBe("G6");
    expect(NAV_SURFACE_MAP.ACTIVITY.owner).toBe("G6");
    expect(NAV_SURFACE_MAP.SAVED.owner).toBe("G6");
  });

  it("declares PROFILE/CREATE projections with downstream owners (no invented surfaces)", () => {
    expect(NAV_SURFACE_MAP.PROFILE.disposition).toBe("PROJECTION_DEFINED");
    expect(NAV_SURFACE_MAP.PROFILE.owner).toBe("G9");
    expect(NAV_SURFACE_MAP.CREATE.disposition).toBe("PROJECTION_DEFINED");
    expect(NAV_SURFACE_MAP.CREATE.owner).toBe("G11");
  });

  it("rejects non-mode values in the type guard", () => {
    expect(isWeupNavMode("DISCOVER")).toBe(true);
    expect(isWeupNavMode("RADAR")).toBe(false);
    expect(isWeupNavMode("")).toBe(false);
    expect(isWeupNavMode(null)).toBe(false);
    expect(isWeupNavMode(undefined)).toBe(false);
  });
});

describe("useWeupNavMode transitions", () => {
  beforeEach(() => {
    resetWeupNavModeForTests();
  });

  it("starts in DISCOVER", () => {
    const nav = useWeupNavMode();
    expect(nav.activeMode.value).toBe("DISCOVER");
  });

  it("transitions through every mode in order", () => {
    const nav = useWeupNavMode();
    const sequence: WeupNavMode[] = [
      "ACTIVITY",
      "SAVED",
      "PROFILE",
      "CREATE",
      "DISCOVER",
    ];

    for (const mode of sequence) {
      expect(nav.requestMode(mode)).toBe(true);
      expect(nav.activeMode.value).toBe(mode);
    }
  });

  it("rejects invalid modes without changing state", () => {
    const nav = useWeupNavMode();
    expect(nav.requestMode("SAVED")).toBe(true);
    expect(nav.requestMode("BOGUS")).toBe(false);
    expect(nav.requestMode(null)).toBe(false);
    expect(nav.activeMode.value).toBe("SAVED");
  });

  it("treats re-requesting the active mode as an idempotent no-op", () => {
    const nav = useWeupNavMode();
    expect(nav.requestMode("ACTIVITY")).toBe(true);
    expect(nav.requestMode("ACTIVITY")).toBe(true);
    expect(nav.activeMode.value).toBe("ACTIVITY");
  });

  it("resets to DISCOVER", () => {
    const nav = useWeupNavMode();
    nav.requestMode("PROFILE");
    nav.resetMode();
    expect(nav.activeMode.value).toBe("DISCOVER");
  });

  it("shares one session across callers (single authoritative mode)", () => {
    const first = useWeupNavMode();
    const second = useWeupNavMode();
    first.requestMode("CREATE");
    expect(second.activeMode.value).toBe("CREATE");
  });
});

describe("WeupBottomNav", () => {
  it("renders all five mode tabs", () => {
    const wrapper = mount(WeupBottomNav, {
      props: { activeMode: "DISCOVER" },
    });

    const tabs = wrapper.findAll(".nav-tab");
    expect(tabs).toHaveLength(4);
    expect(wrapper.find(".nav-create").exists()).toBe(true);

    const labels = tabs.map((tab) => tab.find(".nav-label").text());
    expect(labels).toEqual(["EXPLORE", "ACTIVITY", "SAVED", "KEYS"]);
  });

  it("highlights the active mode from shared state", () => {
    const wrapper = mount(WeupBottomNav, {
      props: { activeMode: "SAVED" },
    });

    const activeTabs = wrapper.findAll(".nav-tab.is-active");
    expect(activeTabs).toHaveLength(1);
    expect(activeTabs[0].find(".nav-label").text()).toBe("SAVED");
    expect(activeTabs[0].attributes("aria-current")).toBe("page");
  });

  it("highlights the CREATE primary action when active", () => {
    const wrapper = mount(WeupBottomNav, {
      props: { activeMode: "CREATE" },
    });

    expect(wrapper.find(".nav-create.is-active").exists()).toBe(true);
    expect(wrapper.findAll(".nav-tab.is-active")).toHaveLength(0);
  });

  it("emits mode-change with the canonical mode on tab click", async () => {
    const wrapper = mount(WeupBottomNav, {
      props: { activeMode: "DISCOVER" },
    });

    const tabs = wrapper.findAll(".nav-tab");
    await tabs[1].trigger("click");
    await wrapper.find(".nav-create").trigger("click");

    expect(wrapper.emitted("mode-change")).toEqual([
      ["ACTIVITY"],
      ["CREATE"],
    ]);
  });

  it("renders the saved-count badge from the canonical snapshot value", () => {
    const withBadge = mount(WeupBottomNav, {
      props: { activeMode: "DISCOVER", savedCount: 7 },
    });
    expect(withBadge.find(".saved-badge").text()).toBe("7");

    const capped = mount(WeupBottomNav, {
      props: { activeMode: "DISCOVER", savedCount: 142 },
    });
    expect(capped.find(".saved-badge").text()).toBe("99+");

    const empty = mount(WeupBottomNav, {
      props: { activeMode: "DISCOVER", savedCount: 0 },
    });
    expect(empty.find(".saved-badge").exists()).toBe(false);
  });
});

describe("WeupTopBar", () => {
  const liveStatus = { isLoading: false, error: null, offline: false };

  function mountTopBar(overrides: Record<string, unknown> = {}) {
    return mount(WeupTopBar, {
      props: {
        feedStatus: liveStatus,
        knownDistricts: ["Midtown", "Downtown"],
        ...overrides,
      },
    });
  }

  it("renders the brand mark", () => {
    const wrapper = mountTopBar();
    expect(wrapper.find(".brand-mark").text()).toBe("WEUP");
    expect(wrapper.find(".brand-sub").text()).toBe("Signal_Network");
  });

  it("derives the LIVE pill from real feed state", () => {
    expect(mountTopBar().find(".status-text").text()).toBe("LIVE");

    const loading = mountTopBar({
      feedStatus: { isLoading: true, error: null, offline: false },
    });
    expect(loading.find(".status-text").text()).toBe("LOADING");

    const offline = mountTopBar({
      feedStatus: { isLoading: false, error: "boom", offline: false },
    });
    expect(offline.find(".status-text").text()).toBe("OFFLINE");

    const tokenMissing = mountTopBar({
      feedStatus: { isLoading: false, error: null, offline: true },
    });
    expect(tokenMissing.find(".status-text").text()).toBe("OFFLINE");
  });

  it("applies a district filter only on canonical taxonomy matches", async () => {
    const wrapper = mountTopBar();
    const input = wrapper.find(".search-input");

    await input.setValue("midtown");
    expect(wrapper.emitted("apply-district-filter")).toEqual([["Midtown"]]);

    await input.setValue("nowhere-land");
    // No additional filter emission for non-matches — no fake filtering.
    expect(wrapper.emitted("apply-district-filter")).toHaveLength(1);
    expect(wrapper.find(".search-hint").exists()).toBe(true);
  });

  it("clears the district filter when the query is cleared", async () => {
    const wrapper = mountTopBar();
    const input = wrapper.find(".search-input");

    await input.setValue("downtown");
    expect(wrapper.emitted("apply-district-filter")).toEqual([["Downtown"]]);

    await input.setValue("");
    expect(wrapper.emitted("clear-district-filter")).toHaveLength(1);
  });

  it("shows the active district context chip with a working clear affordance", async () => {
    const wrapper = mountTopBar({ district: "Midtown" });
    expect(wrapper.find(".district-name").text()).toBe("Midtown");

    await wrapper.find(".district-clear").trigger("click");
    expect(wrapper.emitted("clear-district-filter")).toHaveLength(1);
  });

  it("dispatches SAVED/PROFILE modes from the header affordances", async () => {
    const wrapper = mountTopBar();
    const affordances = wrapper.findAll(".icon-affordance");
    expect(affordances).toHaveLength(2);

    await affordances[0].trigger("click");
    await affordances[1].trigger("click");

    expect(wrapper.emitted("mode-change")).toEqual([["SAVED"], ["PROFILE"]]);
  });
});
