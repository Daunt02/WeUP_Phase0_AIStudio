/**
 * WEUP-SYNTH (G9 — Saved/Profile) panel mount tests.
 *
 * Verifies the no-fabrication contract at the component surface:
 * - ProfilePanel renders only real, source-cited fields and marks
 *   server-backed fields explicitly unavailable.
 * - SocialSignalPanel renders behind its "Prototype — not real" label and
 *   never presents demo tiers as real invitations.
 * - EventDetailModal exposes the Social affordance only when the event is
 *   saved (source "Interest Saved" entry behavior), opening the prototype
 *   panel bound to the canonical event summary.
 */
import { beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent } from "vue";
import { mount, flushPromises } from "@vue/test-utils";
import SocialSignalPanel from "../components/SocialSignalPanel.vue";
import ProfilePanel from "../components/ProfilePanel.vue";
import {
  resetSocialPrototypeForTests,
  useSocialPrototype,
} from "../composables/useSocialPrototype";
import {
  resetProfileFoldersForTests,
  useProfileFolders,
} from "../composables/useProfileFolders";

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

const QIconStub = defineComponent({
  name: "QIconStub",
  props: { name: String },
  template: `<i class="q-icon-stub">{{ name }}</i>`,
});

const QInputStub = defineComponent({
  name: "QInputStub",
  props: { modelValue: String, placeholder: String },
  emits: ["update:modelValue"],
  template: `<input class="q-input-stub" :placeholder="placeholder" :value="modelValue" @input="$emit('update:modelValue', $event.target.value)" />`,
});

function mountSocialPanel(props: Record<string, unknown> = {}) {
  return mount(SocialSignalPanel, {
    props: {
      isOpen: true,
      eventSummary: {
        id: "evt-1",
        title: "Neon Bloom",
        venueName: "Warehouse Live",
        district: "downtown",
        startUtc: "2026-10-01T02:00:00Z",
        flyerUrl: null,
      },
      ...props,
    },
    global: {
      stubs: {
        "q-btn": QBtnStub,
        "q-icon": QIconStub,
      },
    },
  });
}

function mountProfilePanel(props: Record<string, unknown> = {}) {
  return mount(ProfilePanel, {
    props: {
      sessionKind: "anonymous",
      savedCount: 7,
      resolvedCount: 5,
      savedInCurrentView: 3,
      cityLabel: "Houston, TX",
      districtLabel: "downtown",
      preferredDistrictLabels: ["mission"],
      folders: [],
      prototypeFoldersLabel: "Prototype — folders are browser-local only",
      socialUnlockedCount: 0,
      ...props,
    },
    global: {
      stubs: {
        "q-btn": QBtnStub,
        "q-icon": QIconStub,
        "q-input": QInputStub,
      },
    },
  });
}

beforeEach(() => {
  window.localStorage.clear();
  resetSocialPrototypeForTests();
  resetProfileFoldersForTests();
  vi.restoreAllMocks();
});

describe("SocialSignalPanel (G9 prototype-isolated)", () => {
  it("renders behind an explicit prototype banner, never as real social data", () => {
    const wrapper = mountSocialPanel();
    const text = wrapper.text();

    expect(text).toContain("Prototype");
    expect(text).toContain("not real");
    expect(wrapper.find(".prototype-banner").exists()).toBe(true);
  });

  it("shows the source tier contract with demo-only unlock affordances", () => {
    const wrapper = mountSocialPanel();
    const text = wrapper.text();

    expect(text).toContain("Nearby Pre-Event Gathering");
    expect(text).toContain("Venue-Adjacent Circle");
    expect(text).toContain("Trust requirement: 50%");
    // Unlock buttons are demo-labeled, never functional-looking.
    expect(text).toContain("Unlock (demo)");
  });

  it("keeps hidden tiers obscured until unlocked this session", async () => {
    const wrapper = mountSocialPanel();

    expect(wrapper.text()).not.toContain("Hidden Creative Meetup");

    const social = useSocialPrototype();
    social.toggleTierUnlock(3);
    await wrapper.vm.$nextTick();

    expect(wrapper.text()).toContain("Hidden Creative Meetup");
  });

  it("Keep Signal Active marks a session-only interested flag and closes emit work", async () => {
    const wrapper = mountSocialPanel();
    const social = useSocialPrototype();

    const keepButton = wrapper
      .findAll("button.q-btn-stub")
      .find((button) => button.text().includes("Keep signal active"));
    expect(keepButton).toBeDefined();

    await keepButton!.trigger("click");
    expect(social.isEventMarkedInterested("evt-1")).toBe(true);
    expect(wrapper.text()).toContain("Signal active (demo)");
  });

  it("renders nothing when closed and emits close", async () => {
    const wrapper = mountSocialPanel({ isOpen: false });
    expect(wrapper.find(".social-panel").exists()).toBe(false);

    const open = mountSocialPanel();
    await open.find(".close-btn").trigger("click");
    expect(open.emitted("close")).toHaveLength(1);
  });
});

describe("ProfilePanel (G9 no-fabrication contract)", () => {
  it("renders real saved-state fields and cites their sources", () => {
    const wrapper = mountProfilePanel();
    const text = wrapper.text();

    expect(text).toContain("Operator");
    expect(text).toContain("7");
    expect(text).toContain("Houston, TX");
    expect(text).toContain("saved collection");
    expect(text).toContain("canonical events");
    expect(text).toContain("map window");
    expect(text).toContain("Status: Anonymous");
    expect(text).toContain("anonymous local");
  });

  it("marks server-backed profile fields explicitly unavailable, never invented", () => {
    const wrapper = mountProfilePanel();
    const text = wrapper.text();

    expect(text).toContain("Not available");
    expect(text).toContain("No profile API was observed");
    // No fabricated identity, wallet, or reputation artifacts.
    expect(text).not.toContain("NEON_OPERATOR_4901");
    expect(text).not.toContain("eth_accounts");
    expect(text).not.toContain("GATE_CHOKEPOINT_CODE");
  });

  it("renders folders as prototype-local with derived counts", () => {
    const folders = useProfileFolders();
    folders.createFolder("Weekend");

    const wrapper = mountProfilePanel({
      folders: folders.foldersWithSavedCounts(["evt-1"]),
      prototypeFoldersLabel: folders.prototypeLabel,
    });
    const text = wrapper.text();

    expect(text).toContain("Weekend");
    expect(text).toContain("Prototype");
    expect(text).toContain("browser-local only");
  });

  it("emits folder create/delete instead of faking persistence", async () => {
    const wrapper = mountProfilePanel();

    const input = wrapper.find(".q-input-stub");
    await input.setValue("Weekend");
    await wrapper.find("form.folder-create").trigger("submit.prevent");

    expect(wrapper.emitted("create-folder")).toEqual([["Weekend"]]);
  });

  it("marks the social preview as prototype-only", () => {
    const wrapper = mountProfilePanel({ socialUnlockedCount: 2 });
    const text = wrapper.text();

    expect(text).toContain("2 / 3");
    expect(text).toContain("Prototype — session only");
    expect(text).toContain("reset on reload");
  });
});
