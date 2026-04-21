/**
 * M6-P28 — Sync Calendar Overlay with Map State v1.0
 *
 * useDiscoveryState — Single source of truth for shared map/calendar state.
 *
 * ─── SYNCHRONIZATION INVARIANTS ──────────────────────────────────────────────
 *
 * 1. selectedEventId is owned exclusively by this module.
 *    Components call selectEvent() / clearSelection() — they never write the
 *    ref directly.  Breaking this rule creates ownership conflicts between the
 *    map and calendar surfaces.
 *
 * 2. Marker click and calendar card click both route through selectEvent().
 *    The two surfaces cannot compete for ownership because neither owns the
 *    ref; they only invoke actions.
 *
 * 3. District and category filters live here.  The temporal filter (preset,
 *    timezone, custom range) is owned by useTemporalNavigation; this module
 *    receives a reference to the latest compiled map feed query so the calendar
 *    can derive its own query without duplicating temporal logic.
 *
 * 4. The visible event set is reported by MapSurface after every successful
 *    feed load via reportVisibleEvents().  This module checks selection
 *    validity on each update and clears stale selections automatically.
 *
 * 5. Anti-loop contract: watchers here never write back to the reactive values
 *    they are watching.  All mutations route through named action functions.
 *    The overlay-bookkeeping watcher (_overlayMode → _lastNonSelectedOverlay)
 *    writes a *different* ref, not the one it watches.
 *
 * ─── SURFACE RESPONSIBILITY SPLIT ────────────────────────────────────────────
 *
 *   useDiscoveryState  ← data identity, filter state, selection, overlay mode
 *   useTemporalNavigation ← preset, timezone, stepping, scrubbing, query build
 *   MapSurface         ← renders markers, emits @marker-click / feed results
 *   CalendarOverlayShell ← renders cards, emits @select-event
 *   EventDetailModal   ← reads selectedEventId, emits @close
 */

import {
  computed,
  readonly,
  ref,
  type ComputedRef,
  type DeepReadonly,
  type Ref,
} from "vue";
import type { CalendarOverlayLayerState } from "../contracts/calendar-overlay.contracts";
import type { TimeWindowFilterDto } from "../contracts/time-window.contracts";
import type {
  EventMapFeedQueryDto,
  EventMapItemDto,
} from "../contracts/map-feed.contracts";

// ─── Module-level singleton state ─────────────────────────────────────────────
//
// All callers of useDiscoveryState share the SAME reactive references.
// This is intentional: map, calendar, and detail modal are views of one
// discovery session.  A new session (page reload / route navigation) resets
// state by reloading the module.

const _selectedEventId = ref<string | null>(null);
const _overlayMode = ref<CalendarOverlayLayerState>("closed");

/**
 * Remembers the last overlay layout before event-selected so it can be
 * restored when selection clears.  Defaults to "partial".
 */
const _lastNonSelectedOverlay = ref<"partial" | "expanded">("partial");

const _district = ref<string | undefined>(undefined);
const _categories = ref<string[] | undefined>(undefined);
const _includeSavedOnly = ref<boolean>(false);

/**
 * Populated by MapSurface via reportVisibleEvents() after each feed load.
 * Used purely for scope validation — never iterated for rendering.
 */
const _visibleEventIds = ref<ReadonlySet<string>>(new Set<string>());

/**
 * Snapshot of the last compiled map feed query.  Calendar derives its own
 * query from this instead of independently tracking temporal state.
 */
const _latestMapFeedQuery = ref<EventMapFeedQueryDto | null>(null);

// ─── Overlay transition table ─────────────────────────────────────────────────

const VALID_TRANSITIONS: Record<
  CalendarOverlayLayerState,
  readonly CalendarOverlayLayerState[]
> = {
  closed: ["partial", "expanded", "event-selected"],
  partial: ["closed", "expanded", "event-selected"],
  expanded: ["closed", "partial", "event-selected"],
  "event-selected": ["closed", "partial", "expanded"],
};

function canTransition(
  from: CalendarOverlayLayerState,
  to: CalendarOverlayLayerState,
): boolean {
  return VALID_TRANSITIONS[from].includes(to);
}

/**
 * Attempt an overlay transition.  Returns the current mode unchanged (with a
 * warning) if the transition is not valid.  Throws are avoided in production
 * path — an invalid transition is a UI routing mistake, not a data error.
 */
function safeTransition(
  from: CalendarOverlayLayerState,
  to: CalendarOverlayLayerState,
): CalendarOverlayLayerState {
  if (!canTransition(from, to)) {
    if (process.env.NODE_ENV !== "production") {
      console.warn(
        `[useDiscoveryState] Invalid overlay transition: ${from} → ${to}. Transition ignored.`,
      );
    }
    return from;
  }
  return to;
}

function commitOverlayMode(next: CalendarOverlayLayerState): void {
  if (next === "partial" || next === "expanded") {
    _lastNonSelectedOverlay.value = next;
  }

  _overlayMode.value = next;
}

// ─── Public types ─────────────────────────────────────────────────────────────

export interface DiscoveryFilterState {
  readonly district: string | undefined;
  readonly categories: string[] | undefined;
  readonly includeSavedOnly: boolean;
}

/**
 * Flatten of the full shared state for logging / DevTools / tests.
 * Never used for rendering — derive reactive computeds from the composable
 * return value instead.
 */
export interface DiscoveryStateSummary {
  readonly selectedEventId: string | null;
  readonly overlayMode: CalendarOverlayLayerState;
  readonly temporalPreset: TimeWindowFilterDto["preset"] | null;
  readonly temporalTimezone: string | null;
  readonly district: string | undefined;
  readonly categories: string[] | undefined;
  readonly includeSavedOnly: boolean;
  readonly visibleEventCount: number;
  readonly hasMapQuery: boolean;
}

// ─── Public composable ────────────────────────────────────────────────────────

export interface UseDiscoveryStateReturn {
  // ── Readonly reactive state ──────────────────────────────────────────────
  selectedEventId: DeepReadonly<Ref<string | null>>;
  overlayMode: DeepReadonly<Ref<CalendarOverlayLayerState>>;
  latestMapFeedQuery: DeepReadonly<Ref<EventMapFeedQueryDto | null>>;
  visibleEventIds: DeepReadonly<Ref<ReadonlySet<string>>>;

  // ── Computed state ───────────────────────────────────────────────────────
  /**
   * Latest canonical temporal filter snapshot compiled into the map query.
   * This is the temporal state shared with the calendar overlay.
   */
  activeTemporalFilter: ComputedRef<TimeWindowFilterDto | null>;
  isEventSelected: ComputedRef<boolean>;
  isOverlayOpen: ComputedRef<boolean>;
  overlayHeight: ComputedRef<string>;
  activeFilters: ComputedRef<DiscoveryFilterState>;
  /**
   * Calendar feed query derived from the last map query + active filters.
   * Null until the map surface emits its first query via reportMapFeedQuery().
   * The calendar must NOT build its own independent query — it reads this.
   */
  calendarFeedQuery: ComputedRef<EventMapFeedQueryDto | null>;
  stateSummary: ComputedRef<DiscoveryStateSummary>;

  // ── Selection actions ────────────────────────────────────────────────────
  /**
   * Select an event.  Safe to call from both map marker click and calendar
   * card click — idempotent when the same event is already selected.
   *
   * Transitions overlay to "event-selected" automatically.
   */
  selectEvent: (eventId: string) => void;
  /**
   * Clear active selection and restore overlay to its previous non-selected
   * layout (partial or expanded).
   *
   * Called by: EventDetailModal @close, temporal window shift, scope eviction.
   */
  clearSelection: () => void;

  // ── Filter actions ───────────────────────────────────────────────────────
  /**
   * Apply district / category / savedOnly filter changes.
   *
   * The temporal filter is owned by useTemporalNavigation.  After calling
   * this, the caller must trigger a map feed reload.  Selection is preserved
   * until reportVisibleEvents() confirms the selected event is still in scope.
   */
  applyFilters: (partial: Partial<DiscoveryFilterState>) => void;
  /** Reset all spatial/categorical filters. */
  clearFilters: () => void;

  // ── Overlay mode control ─────────────────────────────────────────────────
  setOverlayMode: (next: CalendarOverlayLayerState) => void;
  closeOverlay: () => void;
  openPartialOverlay: () => void;
  expandOverlay: () => void;

  // ── Map surface reporting ────────────────────────────────────────────────
  /**
   * Called by MapSurface after every successful feed load.
   *
   * This is the ONLY place where scope eviction happens.  Do not call this
   * from the calendar — both surfaces share the same event roster.
   */
  reportVisibleEvents: (events: EventMapItemDto[]) => void;
  /**
   * Called by MapSurface alongside reportVisibleEvents to keep the calendar
   * query in sync with the map's temporal/spatial parameters.
   */
  reportMapFeedQuery: (query: EventMapFeedQueryDto) => void;

  // ── Temporal integration ─────────────────────────────────────────────────
  /**
   * Call when useTemporalNavigation.shouldClearSelectedEventOnWindowShift()
   * returns true.  Eagerly clears selection before the new feed loads.
   *
   * This is intentionally eager: a stale selected marker is worse UX than a
   * brief deselected state while the feed reloads.
   */
  onTemporalWindowShift: () => void;
}

export function useDiscoveryState(): UseDiscoveryStateReturn {
  // ── Derived / computed ───────────────────────────────────────────────────

  const activeTemporalFilter = computed<TimeWindowFilterDto | null>(() => {
    const base = _latestMapFeedQuery.value;
    if (!base) {
      return null;
    }

    return {
      preset: base.preset,
      timezone: base.timezone,
      ...(base.customStartUtc !== undefined
        ? { customStartUtc: base.customStartUtc }
        : {}),
      ...(base.customEndUtc !== undefined
        ? { customEndUtc: base.customEndUtc }
        : {}),
    };
  });

  const isEventSelected = computed(() => _selectedEventId.value !== null);

  const isOverlayOpen = computed(() => _overlayMode.value !== "closed");

  const overlayHeight = computed<string>(() => {
    switch (_overlayMode.value) {
      case "closed":
        return "0px";
      case "partial":
        return "33vh";
      case "expanded":
        return "72vh";
      case "event-selected":
        return "58vh";
    }
  });

  const activeFilters = computed<DiscoveryFilterState>(() => ({
    district: _district.value,
    categories: _categories.value,
    includeSavedOnly: _includeSavedOnly.value,
  }));

  /**
   * Calendar feed query is always a projection of the map query with the
   * current shared filter overrides applied.  The calendar never builds an
   * independent query — that would allow filter drift.
   */
  const calendarFeedQuery = computed<EventMapFeedQueryDto | null>(() => {
    const base = _latestMapFeedQuery.value;
    if (!base) {
      return null;
    }

    return {
      bbox: base.bbox,
      preset: base.preset,
      timezone: base.timezone,
      ...(base.customStartUtc !== undefined
        ? { customStartUtc: base.customStartUtc }
        : {}),
      ...(base.customEndUtc !== undefined
        ? { customEndUtc: base.customEndUtc }
        : {}),
      // Shared categorical filters override whatever was in the map query.
      // Undefined means "no override" — falls back to the map's value.
      district: _district.value ?? base.district,
      categories:
        _categories.value !== undefined ? _categories.value : base.categories,
      includeSavedOnly: _includeSavedOnly.value,
    };
  });

  const stateSummary = computed<DiscoveryStateSummary>(() => ({
    selectedEventId: _selectedEventId.value,
    overlayMode: _overlayMode.value,
    temporalPreset: activeTemporalFilter.value?.preset ?? null,
    temporalTimezone: activeTemporalFilter.value?.timezone ?? null,
    district: _district.value,
    categories: _categories.value,
    includeSavedOnly: _includeSavedOnly.value,
    visibleEventCount: _visibleEventIds.value.size,
    hasMapQuery: _latestMapFeedQuery.value !== null,
  }));

  // ── Selection actions ────────────────────────────────────────────────────

  function selectEvent(eventId: string): void {
    // ANTI-LOOP GUARD: idempotent check prevents watcher re-entry if called
    // redundantly (e.g., map re-renders and re-emits the same event ID).
    if (_selectedEventId.value === eventId) {
      return;
    }

    _selectedEventId.value = eventId;
    // Overlay transitions after selection state is committed.
    // The overlay transition is explicit and synchronous to avoid stale
    // fallback restoration when users expand and select within the same tick.
    commitOverlayMode(safeTransition(_overlayMode.value, "event-selected"));
  }

  function clearSelection(): void {
    // Idempotent — prevents double-transition if modal @close fires twice.
    if (_selectedEventId.value === null) {
      return;
    }

    _selectedEventId.value = null;

    // Restore the previous non-selected layout.  If the overlay was already
    // in a non-event-selected mode (e.g., user closed it mid-selection), skip.
    if (_overlayMode.value === "event-selected") {
      commitOverlayMode(
        safeTransition(_overlayMode.value, _lastNonSelectedOverlay.value),
      );
    }
  }

  // ── Filter actions ───────────────────────────────────────────────────────

  function applyFilters(partial: Partial<DiscoveryFilterState>): void {
    if ("district" in partial) {
      _district.value = partial.district;
    }
    if ("categories" in partial) {
      _categories.value = partial.categories;
    }
    if ("includeSavedOnly" in partial) {
      _includeSavedOnly.value = partial.includeSavedOnly ?? false;
    }
    // Caller triggers map feed reload; reportVisibleEvents() handles scope eviction.
  }

  function clearFilters(): void {
    _district.value = undefined;
    _categories.value = undefined;
    _includeSavedOnly.value = false;
  }

  // ── Overlay mode control ─────────────────────────────────────────────────

  function setOverlayMode(next: CalendarOverlayLayerState): void {
    commitOverlayMode(safeTransition(_overlayMode.value, next));
  }

  function closeOverlay(): void {
    setOverlayMode("closed");
  }

  function openPartialOverlay(): void {
    setOverlayMode("partial");
  }

  function expandOverlay(): void {
    setOverlayMode("expanded");
  }

  // ── Map surface reporting ────────────────────────────────────────────────

  function reportVisibleEvents(events: EventMapItemDto[]): void {
    const ids = new Set(events.map((e) => e.eventId));

    // ANTI-LOOP: _visibleEventIds is written here.  clearSelection() writes
    // _selectedEventId, which is a different ref.  No circular dependency.
    _visibleEventIds.value = ids;

    if (_selectedEventId.value !== null && !ids.has(_selectedEventId.value)) {
      // Scope eviction: the selected event left the visible window.
      // clearSelection() restores the overlay to its pre-selection layout.
      clearSelection();
    }
  }

  function reportMapFeedQuery(query: EventMapFeedQueryDto): void {
    _latestMapFeedQuery.value = query;
  }

  // ── Temporal integration ─────────────────────────────────────────────────

  function onTemporalWindowShift(): void {
    // Eager eviction before new feed data arrives.  Prevents a stale selected
    // marker appearing in a time window where the event may not exist.
    clearSelection();
  }

  // ── Return ───────────────────────────────────────────────────────────────

  return {
    // Readonly refs — components observe, never mutate directly.
    selectedEventId: readonly(_selectedEventId),
    overlayMode: readonly(_overlayMode),
    latestMapFeedQuery: readonly(_latestMapFeedQuery),
    visibleEventIds: readonly(_visibleEventIds),

    // Computeds
    activeTemporalFilter,
    isEventSelected,
    isOverlayOpen,
    overlayHeight,
    activeFilters,
    calendarFeedQuery,
    stateSummary,

    // Actions
    selectEvent,
    clearSelection,
    applyFilters,
    clearFilters,
    setOverlayMode,
    closeOverlay,
    openPartialOverlay,
    expandOverlay,
    reportVisibleEvents,
    reportMapFeedQuery,
    onTemporalWindowShift,
  };
}

/**
 * Test-only singleton reset.
 *
 * useDiscoveryState intentionally keeps module-level state so map, calendar,
 * and detail modal share one session. Tests need a deterministic reset point.
 */
export function resetDiscoveryStateForTests(): void {
  _selectedEventId.value = null;
  _overlayMode.value = "closed";
  _lastNonSelectedOverlay.value = "partial";
  _district.value = undefined;
  _categories.value = undefined;
  _includeSavedOnly.value = false;
  _visibleEventIds.value = new Set<string>();
  _latestMapFeedQuery.value = null;
}
