<!--
  WEUP-SYNTH (G6 — Navigation):
  sources=[components/TopBar.tsx, components/BottomNav.tsx, components/InteractionLayer.tsx]
  destination=frontend-vue/src/App.vue
  mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
  notes=App.vue is now the coherent application shell (mission §2): WORLD
    (MapSurface) / CALENDAR (CalendarOverlayShell) / EVENT (EventDetailModal)
    surfaces composed with the canonical WeupTopBar + WeupBottomNav chrome and
    the SAVED overlay sheet. All pre-existing behavior and contracts are
    preserved (discovery handlers, calendar sync, modal, share, persistence);
    G6 only extends. Mode dispatch follows navigation/weupNavModes.ts; the
    authoritative UI state remains useDiscoveryState + the existing composables.
-->
<template>
  <q-layout view="hHh lpR fFf" class="weup-shell">
    <q-header class="weup-header">
      <WeupTopBar
        :feed-status="feedStatus"
        :district="discovery.activeFilters.value.district"
        :known-districts="knownDistricts"
        @mode-change="handleNavModeChange"
        @apply-district-filter="onTopBarDistrictFilter"
        @clear-district-filter="onTopBarDistrictClear"
      />
    </q-header>

    <q-page-container>
      <q-page class="weup-page">
        <div class="surface-stack">
          <MapSurface
            :selected-event-id="discovery.selectedEventId.value"
            :selected-event-saved-state="selectedEventSavedState"
            :active-filters="discovery.activeFilters.value"
            @update:selected-event-id="onMapSelectedEventChanged"
            @filters-updated="onMapFiltersUpdated"
            @map-feed-query-updated="onMapFeedQueryUpdated"
            @map-items-updated="onMapItemsUpdated"
            @feed-status-changed="onFeedStatusChanged"
          />

          <CalendarOverlayShell
            :layer="discovery.overlayMode.value"
            :overlay-height="discovery.overlayHeight.value"
            :selected-event-id="discovery.selectedEventId.value"
            :items="calendarItems"
            :is-filter-refresh-pending="isCalendarFilterRefreshPending"
            :degraded-reason="calendarDegradedReason"
            :layout-mode="calendarLayoutMode"
            :active-day-key="temporal.activeDayKey.value"
            :scrub-cursor-utc="temporal.scrubCursorUtc.value"
            @set-layer="discovery.setOverlayMode"
            @select-event="discovery.selectEvent"
            @date-select="onCalendarDateSelect"
            @layout-mode-change="calendarLayoutMode = $event"
          />

          <!-- SAVED mode surface: existing SavedEventsPanel mounted as a shell
               overlay sheet, wired to useSavedEventsCollection. -->
          <Transition name="saved-sheet">
            <div
              v-if="isSavedSheetOpen"
              class="saved-sheet-wrap"
              role="dialog"
              aria-label="Saved events"
            >
              <button
                type="button"
                class="saved-sheet-close"
                aria-label="Close saved events"
                @click="onSavedSheetClose"
              >
                <q-icon name="close" />
              </button>
              <SavedEventsPanel
                :items="savedCollection.items.value"
                :saved-count-badge-value="
                  savedCollection.surfaceSnapshot.value.savedCountBadgeValue
                "
                :resolved-count="savedCollection.resolvedCount.value"
                :missing-or-deleted-count="
                  savedCollection.missingOrDeletedCount.value
                "
                :session-kind="savedCollection.sessionKind.value"
                :is-loading="savedCollection.isLoading.value"
                :is-refreshing="savedCollection.isRefreshing.value"
                :degraded-reason="savedCollection.degradedReason.value"
                :error="savedCollection.error.value"
                :selected-event-id="discovery.selectedEventId.value"
                :folder-names="profileFolders.folderNames.value"
                :folder-name-by-event-id="folderNameByEventId"
                @refresh="onSavedPanelRefresh"
                @select-event="onSavedPanelSelectEvent"
                @unsave-event="onSavedPanelUnsave"
                @assign-folder="onSavedPanelAssignFolder"
              />
            </div>
          </Transition>

          <!-- PROFILE mode surface (G9): ProfilePanel mounted as a shell
               overlay sheet, wired to the canonical saved-state authority.
               Fabrication path excluded: every field cites its real source. -->
          <Transition name="saved-sheet">
            <div
              v-if="isProfileSheetOpen"
              class="saved-sheet-wrap profile-sheet-wrap"
              role="dialog"
              aria-label="Profile"
            >
              <button
                type="button"
                class="saved-sheet-close"
                aria-label="Close profile"
                @click="onProfileSheetClose"
              >
                <q-icon name="close" />
              </button>
              <ProfilePanel
                :session-kind="savedCollection.sessionKind.value"
                :saved-count="
                  savedCollection.surfaceSnapshot.value.savedCountBadgeValue
                "
                :resolved-count="savedCollection.resolvedCount.value"
                :saved-in-current-view="profileSavedInCurrentView"
                :city-label="profileCityLabel"
                :district-label="profileDistrictLabel"
                :preferred-district-labels="profilePreferredDistrictLabels"
                :folders="profileFoldersView"
                :prototype-folders-label="profileFolders.prototypeLabel"
                :social-unlocked-count="
                  socialPrototype.unlockedTierLevels.length
                "
                @create-folder="onProfileCreateFolder"
                @delete-folder="onProfileDeleteFolder"
                @apply-district="onProfileApplyDistrict"
              />
            </div>
          </Transition>

          <WeupBottomNav
            :active-mode="navMode.activeMode.value"
            :saved-count="
              savedCollection.surfaceSnapshot.value.savedCountBadgeValue
            "
            @mode-change="handleNavModeChange"
          />
        </div>

        <EventDetailModal
          :model-value="isOpen"
          :event="eventDetail"
          :is-loading="isLoading"
          :is-save-pending="isSavePending"
          :error="error"
          :session-kind="selectedEventSessionKind"
          @update:model-value="onModalVisibilityChange"
          @toggle-save="toggleSavedState"
          @share="showSharePlaceholder"
        />
      </q-page>
    </q-page-container>
  </q-layout>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from "vue";
import { useQuasar } from "quasar";
import CalendarOverlayShell from "./components/CalendarOverlayShell.vue";
import EventDetailModal from "./components/EventDetailModal.vue";
import MapSurface from "./components/MapSurface.vue";
import ProfilePanel from "./components/ProfilePanel.vue";
import SavedEventsPanel from "./components/SavedEventsPanel.vue";
import WeupBottomNav from "./components/WeupBottomNav.vue";
import WeupTopBar, {
  type TopBarFeedStatus,
} from "./components/WeupTopBar.vue";
import {
  useDiscoveryState,
  type DiscoveryFilterState,
} from "./composables/useDiscoveryState";
import { useTemporalNavigation } from "./composables/useTemporalNavigation";
import type { CalendarLayoutMode } from "./components/CalendarOverlayShell.vue";
import { useAnonymousLocalPersistence } from "./composables/useAnonymousLocalPersistence";
import { useEventDetailModal } from "./composables/useEventDetailModal";
import { useProfileFolders } from "./composables/useProfileFolders";
import { useSavedEventState } from "./composables/useSavedEventState";
import { useSavedEventsCollection } from "./composables/useSavedEventsCollection";
import { useSocialPrototype } from "./composables/useSocialPrototype";
import { useUserContextPreferences } from "./composables/useUserContextPreferences";
import { useWeupNavMode } from "./composables/useWeupNavMode";
import type { WeupNavMode } from "./navigation/weupNavModes";
import type {
  EventMapFeedQueryDto,
  EventMapItemDto,
} from "./contracts/map-feed.contracts";
import { shareEventDetail } from "./services/eventDetailService";
import type { SaveSessionKind } from "./contracts/event-detail.contracts";

const $q = useQuasar();

const discovery = useDiscoveryState();
// WEUP-SYNTH (G7 — Temporal): the single authoritative temporal state,
// shared with MapSurface (same singleton instance). Calendar date selection
// and layout mode route through it; no second temporal store exists.
const temporal = useTemporalNavigation();
const calendarLayoutMode = ref<CalendarLayoutMode>("masonry");
const anonymousLocalPersistence = useAnonymousLocalPersistence();
const userContextPreferences = useUserContextPreferences();

// ─── G6 navigation shell state ──────────────────────────────────────────────
// The nav mode is shell chrome state only: every mode request dispatches into
// the canonical composables below. The authoritative UI state remains
// useDiscoveryState (+ useSavedEventsCollection / the saved-sheet mount flag).
const navMode = useWeupNavMode();
// WEUP-SYNTH (G9 — Saved/Profile): the canonical saved-state authority is
// instantiated BEFORE the collection so save/unsave mutations round-trip
// into the saved panel, badge, profile counts, and (via the collection's
// snapshot) the map/calendar projections — one source, all surfaces.
const savedEventState = useSavedEventState();
const savedCollection = useSavedEventsCollection(savedEventState);
const profileFolders = useProfileFolders();
const socialPrototype = useSocialPrototype();
const isSavedSheetOpen = ref(false);
const isProfileSheetOpen = ref(false);
const feedStatus = ref<TopBarFeedStatus>({
  isLoading: true,
  error: null,
  offline: false,
});

/**
 * Canonical district taxonomy derived from the visible feed. The TopBar
 * search resolves queries against this list only (no live search backend).
 */
const knownDistricts = computed<readonly string[]>(() => {
  const districts = new Set<string>();
  for (const item of mapItemsState.value) {
    if (item.district && item.district.trim().length > 0) {
      districts.add(item.district);
    }
  }
  return [...districts].sort((a, b) => a.localeCompare(b));
});

function openSavedSheet(): void {
  isSavedSheetOpen.value = true;
  void savedCollection.loadSavedEvents();
}

function closeSavedSheet(): void {
  isSavedSheetOpen.value = false;
}

function openProfileSheet(): void {
  isProfileSheetOpen.value = true;
  // Profile counts are canonical: refresh the saved collection so the
  // panel renders current saved state.
  void savedCollection.loadSavedEvents();
}

function closeProfileSheet(): void {
  isProfileSheetOpen.value = false;
}

/**
 * Mode -> surface dispatch, per navigation/weupNavModes.ts NAV_SURFACE_MAP.
 * Every mode transition first clears selection (source handleModeChange
 * semantics: selectedItemId is reset on any mode change).
 */
function handleNavModeChange(mode: WeupNavMode): void {
  if (!navMode.requestMode(mode)) {
    return;
  }

  discovery.clearSelection();

  switch (mode) {
    case "DISCOVER":
      closeSavedSheet();
      closeProfileSheet();
      discovery.setOverlayMode("partial");
      break;
    case "ACTIVITY":
      // Source: ACTIVITY opens the CulturalCalendar temporal overlay.
      // Vue counterpart: the calendar overlay shell at expanded layer.
      closeSavedSheet();
      closeProfileSheet();
      discovery.setOverlayMode("expanded");
      break;
    case "SAVED":
      closeProfileSheet();
      openSavedSheet();
      break;
    case "PROFILE":
      // G9 (ProfilePanel): the profile surface now exists as a shell overlay
      // sheet wired to the canonical saved-state authority.
      closeSavedSheet();
      openProfileSheet();
      break;
    case "CREATE":
      // PROJECTION_DEFINED (owner G11): no Vue create wizard exists yet.
      // World surface retained; no fabricated create flow is shown.
      closeSavedSheet();
      closeProfileSheet();
      break;
  }
}

function onSavedSheetClose(): void {
  closeSavedSheet();
  navMode.resetMode();
}

function onProfileSheetClose(): void {
  closeProfileSheet();
  navMode.resetMode();
}

function onSavedPanelRefresh(): void {
  void savedCollection.loadSavedEvents();
}

function onSavedPanelSelectEvent(eventId: string): void {
  // Hand off to the EVENT surface: selection drives the shared detail modal
  // via the existing useEventDetailModal wiring.
  closeSavedSheet();
  navMode.resetMode();
  discovery.selectEvent(eventId);
}

/**
 * G9: canonical unsave from the saved panel. The mutation goes through the
 * sole saved-state authority; the collection's mutationRevision watcher
 * reloads the list so panel, badge, and profile counts converge.
 */
async function onSavedPanelUnsave(eventId: string): Promise<void> {
  try {
    await savedEventState.mutateSavedState(eventId, false, () => {
      // UI projection refreshes via the collection watcher; no direct
      // DOM/list mutation here.
    });
  } catch (cause) {
    $q.notify({
      type: "negative",
      message:
        cause instanceof Error
          ? cause.message
          : "Failed to remove the saved event.",
    });
  }
}

/**
 * G9: prototype-local folder assignment from the saved panel. This is a
 * view-model mapping only — it never mutates the canonical saved state.
 */
function onSavedPanelAssignFolder(payload: {
  eventId: string;
  folderName: string | null;
}): void {
  try {
    profileFolders.assignEventToFolder(payload.eventId, payload.folderName);
  } catch (cause) {
    $q.notify({
      type: "negative",
      message:
        cause instanceof Error ? cause.message : "Failed to assign folder.",
    });
  }
}

function onProfileCreateFolder(name: string): void {
  try {
    profileFolders.createFolder(name);
  } catch (cause) {
    $q.notify({
      type: "negative",
      message:
        cause instanceof Error ? cause.message : "Failed to create folder.",
    });
  }
}

function onProfileDeleteFolder(name: string): void {
  profileFolders.deleteFolder(name);
}

function onProfileApplyDistrict(label: string): void {
  onMapFiltersUpdated({ district: label });
}

// ─── G9 profile derivations ────────────────────────────────────────────────
// Every profile field cites its real source. The operator city is the domain
// city (Houston); district context comes from the anonymous discovery context
// and user-context preferences (display only, non-authoritative).
const profileCityLabel = "Houston, TX";

const profileDistrictLabel = computed<string | null>(() => {
  return (
    discovery.activeFilters.value.district ??
    anonymousLocalPersistence.getDiscoveryContext().lastViewedDistrict ??
    null
  );
});

const profilePreferredDistrictLabels = computed<readonly string[]>(() => {
  return userContextPreferences.preferredDistrictCodes.value.map((code) =>
    code.replace(/-/g, " "),
  );
});

const profileFoldersView = computed(() =>
  profileFolders.foldersWithSavedCounts(
    savedCollection.surfaceSnapshot.value.savedEventIds,
  ),
);

const folderNameByEventId = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {};
  for (const eventId of savedCollection.savedEventIds.value) {
    const folderName = profileFolders.folderNameFor(eventId);
    if (folderName) {
      map[eventId] = folderName;
    }
  }
  return map;
});

const profileSavedInCurrentView = computed<number>(() => {
  return (
    userContextPreferences.savedCountSummary.value
      ?.savedEventsInCurrentMapWindow ?? 0
  );
});

function onFeedStatusChanged(status: TopBarFeedStatus): void {
  feedStatus.value = status;
}

function onTopBarDistrictFilter(district: string): void {
  onMapFiltersUpdated({ district });
}

function onTopBarDistrictClear(): void {
  onMapFiltersUpdated({ district: undefined });
}

const selectedEventIdModel = computed<string | null>({
  get() {
    return discovery.selectedEventId.value;
  },
  set(eventId) {
    if (eventId) {
      discovery.selectEvent(eventId);
      return;
    }

    discovery.clearSelection();
  },
});

const {
  eventDetail,
  isOpen,
  isLoading,
  isSavePending,
  error,
  closeModal,
  toggleSavedState,
} = useEventDetailModal(selectedEventIdModel, {
  resolveSavedState: savedEventState.resolveSavedState,
  mutateSavedState: savedEventState.mutateSavedState,
});

const selectedEventSavedState = computed(() => {
  return eventDetail.value?.savedByCurrentUser ?? null;
});

// Save-session kind for the event surface: read from the canonical
// saved-state cache (the same authority useEventDetailModal uses). Null when
// the state has not been resolved yet — the modal omits the chip then.
const selectedEventSessionKind = computed<SaveSessionKind | null>(() => {
  const eventId = eventDetail.value?.id;
  if (!eventId) {
    return null;
  }

  return savedEventState.getCachedSavedState(eventId)?.sessionKind ?? null;
});

const mapItemsState = ref<EventMapItemDto[]>([]);
const isCalendarFilterRefreshPending = ref(false);
const calendarDegradedReason = ref<string | null>(null);
const latestMapFeedQueryState = ref<EventMapFeedQueryDto | null>(null);
let refreshDegradedHandle: ReturnType<typeof setTimeout> | null = null;

function clearRefreshDegradedHandle(): void {
  if (refreshDegradedHandle) {
    clearTimeout(refreshDegradedHandle);
    refreshDegradedHandle = null;
  }
}

function beginCalendarFilterRefresh(): void {
  isCalendarFilterRefreshPending.value = true;
  clearRefreshDegradedHandle();

  // Stop motion escalation if data response is delayed: degrade to explicit text.
  refreshDegradedHandle = setTimeout(() => {
    if (isCalendarFilterRefreshPending.value) {
      calendarDegradedReason.value =
        "Calendar refresh is delayed. Showing last stable projection while map data catches up.";
    }
  }, 1800);
}

function settleCalendarFilterRefresh(): void {
  isCalendarFilterRefreshPending.value = false;
  calendarDegradedReason.value = null;
  clearRefreshDegradedHandle();
}

function hasFilterScopeChange(
  previous: EventMapFeedQueryDto | null,
  next: EventMapFeedQueryDto,
): boolean {
  if (!previous) {
    return false;
  }

  const previousCategories = previous.categories ?? [];
  const nextCategories = next.categories ?? [];

  return (
    previous.preset !== next.preset ||
    previous.timezone !== next.timezone ||
    previous.customStartUtc !== next.customStartUtc ||
    previous.customEndUtc !== next.customEndUtc ||
    previous.district !== next.district ||
    previous.includeSavedOnly !== next.includeSavedOnly ||
    previousCategories.length !== nextCategories.length ||
    previousCategories.some(
      (category, index) => category !== nextCategories[index],
    )
  );
}

const calendarItems = computed(() => {
  // Calendar is an alternate temporal projection of the same canonical map set.
  // No route transition and no contract fork are allowed in this mapping path.
  if (!discovery.calendarFeedQuery.value) {
    return [];
  }
  return [...discovery.visibleEventIds.value]
    .map(
      (eventId) =>
        latestVisibleMapItems.value.find((item) => item.eventId === eventId) ??
        null,
    )
    .filter((item): item is EventMapItemDto => item !== null)
    .map((item) => ({
      eventId: item.eventId,
      title: item.title,
      startUtc: item.startUtc,
      endUtc: item.endUtc,
      timezone: discovery.activeTemporalFilter.value?.timezone ?? "UTC",
      venueName: item.venueName,
      district: item.district,
      primaryCategory: item.primaryCategory,
      savedByCurrentUser: item.savedByCurrentUser,
      markerState: item.markerState,
      thumbnailUrl: null,
    }))
    .sort((left, right) => left.startUtc.localeCompare(right.startUtc));
});

const latestVisibleMapItems = computed<EventMapItemDto[]>(() => {
  const visibleIds = discovery.visibleEventIds.value;
  return mapItemsState.value.filter((item) => visibleIds.has(item.eventId));
});

function onMapSelectedEventChanged(eventId: string | null): void {
  if (eventId) {
    discovery.selectEvent(eventId);
    return;
  }

  discovery.clearSelection();
}

/**
 * WEUP-SYNTH (G7 — CulturalCalendar): calendar date selection is a temporal
 * transition through the single authority. The mode/preset change flows into
 * MapSurface's requestSignature watcher -> feed refetch -> map projection,
 * calendar projection, selection, filters, and visible set all update.
 */
function onCalendarDateSelect(dayKey: string): void {
  temporal.selectHoustonDay(dayKey);
}

function onMapFiltersUpdated(partial: Partial<DiscoveryFilterState>): void {
  discovery.applyFilters(partial);
  userContextPreferences.updateFromMapDiscoveryFilters(
    discovery.activeFilters.value,
  );

  // Anonymous discovery context is convenience state only.
  // It is never merged into authenticated backend records.
  if ("district" in partial) {
    anonymousLocalPersistence.setDiscoveryContext({
      lastViewedDistrict: partial.district,
    });
  }

  beginCalendarFilterRefresh();
}

function onMapItemsUpdated(items: EventMapItemDto[]): void {
  mapItemsState.value = items;
  discovery.reportVisibleEvents(items);
  userContextPreferences.updateSavedCountSummaryFromItems(items);
  settleCalendarFilterRefresh();
}

function onMapFeedQueryUpdated(query: EventMapFeedQueryDto): void {
  const hadScopeChange = hasFilterScopeChange(
    latestMapFeedQueryState.value,
    query,
  );

  latestMapFeedQueryState.value = query;
  discovery.reportMapFeedQuery(query);
  userContextPreferences.updatePreferredTemporalPreset(query.preset);
  userContextPreferences.updateLastUsedMapStateFromQuery(query);

  // Persist lightweight anonymous context for UX continuity across reloads.
  // This layer is explicitly non-authoritative and browser-local only.
  anonymousLocalPersistence.setDiscoveryContext({
    lastTemporalFilter: {
      preset: query.preset,
      timezone: query.timezone,
      customStartUtc: query.customStartUtc,
      customEndUtc: query.customEndUtc,
    },
    recentMapViewport: {
      bbox: query.bbox,
      capturedAtUtc: new Date().toISOString(),
    },
    lastViewedDistrict:
      discovery.activeFilters.value.district ?? query.district ?? undefined,
  });

  if (hadScopeChange) {
    beginCalendarFilterRefresh();
  }
}

function onModalVisibilityChange(isVisible: boolean): void {
  // Closing the dialog clears the shared selection so the map returns to its
  // default interaction state without a stranded highlighted marker.
  if (!isVisible) {
    closeModal();
  }
}

async function showSharePlaceholder(): Promise<void> {
  if (!eventDetail.value) {
    return;
  }

  try {
    const result = await shareEventDetail(eventDetail.value);

    if (result === "dismissed") {
      return;
    }

    $q.notify({
      type: result === "shared" ? "positive" : "info",
      message:
        result === "shared"
          ? "Event shared."
          : result === "copied"
            ? "Share link copied to clipboard."
            : "Opened an email share draft.",
    });
  } catch (cause) {
    $q.notify({
      type: "negative",
      message:
        cause instanceof Error ? cause.message : "Failed to share event.",
    });
  }
}

onBeforeUnmount(() => {
  clearRefreshDegradedHandle();
});
</script>

<style scoped>
/* G4 tokens: canonical dark application shell. One responsive composition
   (mission §20) — mobile prioritizes MAP → DISCOVERY → EVENT → CREATE →
   SAVED → PROFILE → ASSISTANT; desktop may expose simultaneous surfaces.
   No duplicated business logic, no separate mobile/desktop implementations. */
.weup-shell {
  min-height: 100vh;
  background: #050505;
  color: #fff;
}

.weup-header {
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(16px);
  -webkit-backdrop-filter: blur(16px);
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
  color: #fff;
}

.weup-page {
  height: calc(100vh - 57px);
  padding: 0;
}

.surface-stack {
  position: relative;
  height: 100%;
  overflow: hidden;
}

/* SAVED overlay sheet: G4 drawer grammar — bottom sheet, rounded-t-[18px],
   slide-up 400ms, overlay elevation. z-index 150: above map/calendar (0-60),
   below the bottom nav (200) so navigation stays reachable. */
.saved-sheet-wrap {
  position: absolute;
  inset-inline: 12px;
  bottom: calc(104px + env(safe-area-inset-bottom));
  z-index: 150;
  max-width: 480px;
  margin-inline: auto;
  max-height: calc(100% - 200px);
  overflow-y: auto;
  background: rgba(10, 10, 10, 0.92);
  backdrop-filter: blur(24px);
  -webkit-backdrop-filter: blur(24px);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-top: 1px solid rgba(255, 255, 255, 0.16);
  border-radius: 18px 18px 18px 18px;
  box-shadow: 0 20px 50px rgba(0, 0, 0, 0.8);
}

.saved-sheet-close {
  position: absolute;
  top: 8px;
  right: 8px;
  z-index: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  border: 0;
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.08);
  color: rgba(255, 255, 255, 0.7);
  font-size: 18px;
  cursor: pointer;
  transition:
    color 0.3s ease,
    background-color 0.3s ease;
}

.saved-sheet-close:hover {
  color: #00ff9c;
  background: rgba(255, 255, 255, 0.14);
}

.saved-sheet-enter-active,
.saved-sheet-leave-active {
  transition:
    transform 0.4s ease,
    opacity 0.4s ease;
}

.saved-sheet-enter-from,
.saved-sheet-leave-to {
  transform: translateY(48px);
  opacity: 0;
}

@media (max-width: 560px) {
  .saved-sheet-wrap {
    inset-inline: 8px;
    bottom: calc(96px + env(safe-area-inset-bottom));
  }
}

@media (prefers-reduced-motion: reduce) {
  .saved-sheet-enter-active,
  .saved-sheet-leave-active {
    transition: none;
  }

  .saved-sheet-enter-from,
  .saved-sheet-leave-to {
    transform: none;
    opacity: 1;
  }
}
</style>
