/**
 * WEUP-SYNTH (G9 — Saved/Profile) coherence tests.
 *
 * Covers the G9 contract surface:
 * 1. Saved-state sync: save -> useSavedEventState (canonical authority) ->
 *    anonymous-local persistence -> useSavedEventsCollection reloads via the
 *    mutationRevision watcher so panel/badge/profile counts converge on one
 *    truth. Unsave converges back down.
 * 2. Social prototype isolation: useSocialPrototype never writes to storage,
 *    never calls the network, and resets on reload by construction.
 * 3. Profile folders: prototype-local folders with counts derived from the
 *    canonical saved list (never fabricated), and folder ops never mutate
 *    saved state.
 */
import { beforeEach, describe, expect, it, vi } from "vitest";
import { nextTick } from "vue";

const { fetchSavedState, saveEvent, unsaveEvent, fetchEventDetail } =
  vi.hoisted(() => ({
    fetchSavedState: vi.fn(),
    saveEvent: vi.fn(),
    unsaveEvent: vi.fn(),
    fetchEventDetail: vi.fn(),
  }));

const { fetchSavedEvents } = vi.hoisted(() => ({
  fetchSavedEvents: vi.fn(),
}));

vi.mock("../services/eventDetailService", async () => {
  const actual =
    await vi.importActual<typeof import("../services/eventDetailService")>(
      "../services/eventDetailService",
    );
  return {
    ...actual,
    fetchSavedState,
    saveEvent,
    unsaveEvent,
    fetchEventDetail,
  };
});

vi.mock("../services/savedEventsService", async () => {
  const actual =
    await vi.importActual<typeof import("../services/savedEventsService")>(
      "../services/savedEventsService",
    );
  return { ...actual, fetchSavedEvents };
});

import { ApiRequestError } from "../services/eventDetailService";
import { useAnonymousLocalPersistence } from "../composables/useAnonymousLocalPersistence";
import { useSavedEventState } from "../composables/useSavedEventState";
import { useSavedEventsCollection } from "../composables/useSavedEventsCollection";
import {
  resetSocialPrototypeForTests,
  useSocialPrototype,
} from "../composables/useSocialPrototype";
import {
  resetProfileFoldersForTests,
  useProfileFolders,
} from "../composables/useProfileFolders";

function unauthorized(): ApiRequestError {
  return new ApiRequestError("Unauthorized", 401);
}

function canonicalDetail(eventId: string) {
  return {
    id: eventId,
    title: `Event ${eventId}`,
    description: null,
    venueName: "Venue",
    address: "123 Main St",
    lat: 29.76,
    lng: -95.36,
    category: "music",
    categories: ["music"],
    startUtc: "2026-10-01T00:00:00Z",
    endUtc: null,
    timezone: "America/Chicago",
    flyerImageUrl: null,
    mediaRefs: [],
    tags: [],
    status: "PUBLISHED",
    confidence: 0.9,
    sourceKind: "manual",
    provenanceSummary: {
      primarySourceKind: "manual",
      sourceCount: 1,
      firstObservedAtUtc: "2026-09-29T00:00:00Z",
      lastObservedAtUtc: "2026-09-29T00:00:00Z",
      summaryLabel: "manual",
    },
    savedByCurrentUser: true,
    version: 1,
    lastChangeType: null,
    concurrencyToken: null,
  };
}

beforeEach(() => {
  window.localStorage.clear();
  vi.clearAllMocks();
  resetSocialPrototypeForTests();
  resetProfileFoldersForTests();
});

describe("G9 saved-state sync coherence", () => {
  it("save -> persistence, cache, and collection converge on one canonical truth", async () => {
    // Anonymous session: the backend save-state endpoint rejects with 401.
    fetchSavedState.mockRejectedValue(unauthorized());
    fetchSavedEvents.mockRejectedValue(unauthorized());
    fetchEventDetail.mockImplementation(async (eventId: string) => ({
      event: canonicalDetail(eventId),
    }));

    const authority = useSavedEventState();
    const collection = useSavedEventsCollection(authority);

    const applied: boolean[] = [];
    const resolved = await authority.mutateSavedState("evt-1", true, (saved) =>
      applied.push(saved),
    );

    expect(resolved.saved).toBe(true);
    expect(resolved.sessionKind).toBe("anonymous");
    expect(resolved.persistenceSource).toBe("anonymous-local");
    expect(applied).toEqual([true, true]);

    // Persistence layer agrees.
    expect(
      useAnonymousLocalPersistence().getSavedState().savedEventIds,
    ).toContain("evt-1");

    // Cache read path (the event modal's session-kind chip) agrees.
    expect(authority.getCachedSavedState("evt-1")?.saved).toBe(true);

    // Collection reloads via the mutationRevision watcher and converges.
    await nextTick();
    await Promise.resolve();
    await nextTick();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(collection.surfaceSnapshot.value.savedCountBadgeValue).toBe(1);
    expect(collection.surfaceSnapshot.value.sessionKind).toBe("anonymous");
    expect(collection.items.value[0]?.eventId).toBe("evt-1");
  });

  it("unsave -> panel, badge, and profile counts converge back down", async () => {
    fetchSavedState.mockRejectedValue(unauthorized());
    fetchSavedEvents.mockRejectedValue(unauthorized());
    fetchEventDetail.mockImplementation(async (eventId: string) => ({
      event: canonicalDetail(eventId),
    }));

    const authority = useSavedEventState();
    const collection = useSavedEventsCollection(authority);

    await authority.mutateSavedState("evt-1", true, () => undefined);
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(collection.surfaceSnapshot.value.savedCountBadgeValue).toBe(1);

    await authority.mutateSavedState("evt-1", false, () => undefined);
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(collection.surfaceSnapshot.value.savedCountBadgeValue).toBe(0);
    expect(
      useAnonymousLocalPersistence().getSavedState().savedEventIds,
    ).not.toContain("evt-1");
    expect(authority.getCachedSavedState("evt-1")?.saved).toBe(false);
  });

  it("does not create a second saved-state source: authority cache is the sole read path", async () => {
    fetchSavedState.mockResolvedValue({
      eventId: "evt-2",
      saved: true,
      sessionKind: "authenticated",
      persistenceSource: "backend",
    });

    const authority = useSavedEventState();
    const resolved = await authority.resolveSavedState("evt-2");

    expect(resolved.sessionKind).toBe("authenticated");
    expect(authority.getCachedSavedState("evt-2")).toEqual(resolved);
  });
});

describe("G9 social prototype isolation", () => {
  it("prototype tier toggles and interested flags never touch storage or network", () => {
    const fetchSpy = vi.spyOn(globalThis, "fetch");
    const storageKeysBefore = window.localStorage.length;

    const social = useSocialPrototype();

    social.toggleTierUnlock(2);
    social.setEventInterested("evt-1", true);

    expect(social.isTierUnlocked(2)).toBe(true);
    expect(social.isEventMarkedInterested("evt-1")).toBe(true);

    // Isolation proof: no storage mutation, no network calls.
    expect(window.localStorage.length).toBe(storageKeysBefore);
    expect(fetchSpy).not.toHaveBeenCalled();
    fetchSpy.mockRestore();
  });

  it("hidden tiers stay hidden until unlocked this session, and state resets on reload", () => {
    const social = useSocialPrototype();
    const hiddenTier = social.tiers.value.find(
      (tier) => tier.visibility === "hidden",
    );

    expect(hiddenTier).toBeDefined();
    expect(social.isTierVisible(hiddenTier!)).toBe(false);

    social.toggleTierUnlock(3);
    expect(social.isTierVisible(hiddenTier!)).toBe(true);

    // Reload simulation: fresh session state is empty by construction.
    resetSocialPrototypeForTests();
    const fresh = useSocialPrototype();
    expect(fresh.isTierUnlocked(3)).toBe(false);
    expect(fresh.isEventMarkedInterested("evt-1")).toBe(false);
    expect(fresh.isTierVisible(hiddenTier!)).toBe(false);
  });

  it("keeps prototype state out of the canonical saved state", async () => {
    fetchSavedState.mockRejectedValue(unauthorized());

    const social = useSocialPrototype();
    const authority = useSavedEventState();

    social.setEventInterested("evt-9", true);
    expect(social.isEventMarkedInterested("evt-9")).toBe(true);

    // Canonical authority never sees the prototype flag.
    await authority.resolveSavedState("evt-9");
    expect(authority.getCachedSavedState("evt-9")?.saved).toBe(false);
  });
});

describe("G9 prototype-local folders", () => {
  it("creates folders and derives counts from the canonical saved list only", async () => {
    fetchSavedState.mockRejectedValue(unauthorized());

    const folders = useProfileFolders();
    const authority = useSavedEventState();

    await authority.mutateSavedState("evt-1", true, () => undefined);
    await authority.mutateSavedState("evt-2", true, () => undefined);

    folders.createFolder("Weekend");
    folders.createFolder("Raves");
    folders.assignEventToFolder("evt-1", "Weekend");
    // evt-2 stays unfiled; a phantom id is not counted.
    folders.assignEventToFolder("evt-2", null);

    const views = folders.foldersWithSavedCounts(["evt-1", "evt-2"]);
    expect(views.find((view) => view.name === "Weekend")?.savedCount).toBe(1);
    expect(views.find((view) => view.name === "Raves")?.savedCount).toBe(0);
    expect(folders.folderNameFor("evt-1")).toBe("Weekend");
    expect(folders.folderNameFor("evt-2")).toBeNull();
  });

  it("deleting a folder clears its assignments without touching saved state", async () => {
    fetchSavedState.mockRejectedValue(unauthorized());

    const folders = useProfileFolders();
    const authority = useSavedEventState();

    await authority.mutateSavedState("evt-1", true, () => undefined);
    folders.createFolder("Weekend");
    folders.assignEventToFolder("evt-1", "Weekend");

    folders.deleteFolder("Weekend");

    expect(folders.folderNames.value).toHaveLength(0);
    expect(folders.folderNameFor("evt-1")).toBeNull();
    expect(
      useAnonymousLocalPersistence().getSavedState().savedEventIds,
    ).toContain("evt-1");
  });

  it("rejects invalid folder operations with explicit errors (no fake success)", () => {
    const folders = useProfileFolders();

    expect(() => folders.createFolder("   ")).toThrow();
    folders.createFolder("Weekend");
    expect(() => folders.createFolder("Weekend")).toThrow(
      /already exists/,
    );
    expect(() => folders.assignEventToFolder("evt-1", "Unknown")).toThrow(
      /Unknown folder/,
    );
  });

  it("persists folders browser-locally with an explicit prototype label", () => {
    const folders = useProfileFolders();
    folders.createFolder("Weekend");

    expect(window.localStorage.getItem("weup.profile.folders.v1")).toContain(
      "Weekend",
    );
    expect(folders.prototypeLabel).toMatch(/prototype/i);
  });
});
