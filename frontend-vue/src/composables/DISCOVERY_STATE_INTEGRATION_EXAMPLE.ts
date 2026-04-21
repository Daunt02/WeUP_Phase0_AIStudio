/**
 * M6-P28 — Sync Calendar Overlay with Map State v1.0
 *
 * DISCOVERY STATE INTEGRATION EXAMPLE
 *
 * Shows how MapSurface, CalendarOverlayShell, EventDetailModal, and the
 * temporal layer all wire into useDiscoveryState as the single source of truth.
 *
 * Replace this comment block and unused imports before shipping.
 */

// ─── Part 1: App.vue — the integration hub ────────────────────────────────────
//
// App.vue is the only component allowed to call useDiscoveryState().
// Child components receive state via props and emit actions via events.
// This prevents scattered singleton access and keeps the dependency tree flat.
//
// BEFORE (fragmented):
//   const { selectedEventId } = useEventDetailModal();
//   const { layer, ... } = useCalendarOverlayState(selectedEventId);
//   const latestMapFeedQuery = ref(null);       // local, not shared
//   const latestMapItems = ref([]);             // local, not shared
//
// AFTER (unified):
//   const discovery = useDiscoveryState();
//   All state and actions come from one call.
//

/**
 * App.vue <script setup> — reference integration
 *
 * This is the minimal wiring needed. Copy into App.vue and delete the old
 * useEventDetailModal / useCalendarOverlayState pattern.
 */

/*
──────────────────────────────────────────────────────────────────────────────
TEMPLATE DIFF (App.vue)
──────────────────────────────────────────────────────────────────────────────

<MapSurface
  :selected-event-id="discovery.selectedEventId.value"
  :active-filters="discovery.activeFilters.value"
  @marker-click="discovery.selectEvent"
  @map-feed-query-updated="discovery.reportMapFeedQuery"
  @map-items-updated="discovery.reportVisibleEvents"
/>

<CalendarOverlayShell
  :layer="discovery.overlayMode.value"
  :overlay-height="discovery.overlayHeight.value"
  :selected-event-id="discovery.selectedEventId.value"
  :calendar-feed-query="discovery.calendarFeedQuery.value"
  @set-layer="discovery.setOverlayMode"
  @select-event="discovery.selectEvent"
/>

<EventDetailModal
  :model-value="discovery.isEventSelected.value"
  :event-id="discovery.selectedEventId.value"
  @update:model-value="(visible) => !visible && discovery.clearSelection()"
/>

──────────────────────────────────────────────────────────────────────────────
*/

import { computed, watch } from "vue";
import { useDiscoveryState } from "./useDiscoveryState";
import { useTemporalNavigation } from "./useTemporalNavigation";

// ─── App.vue <script setup> ───────────────────────────────────────────────────

function exampleAppSetup() {
  const discovery = useDiscoveryState();
  const temporal = useTemporalNavigation({ debounceMs: 300 });

  // ── Temporal ↔ discovery bridge ───────────────────────────────────────────
  //
  // Watch temporal.requestSignature (not individual time refs) to avoid
  // triggering on each character typed in a custom range input.
  // This is the ONLY place temporal and discovery state are connected.
  //
  // ANTI-LOOP: the watch reads temporal.requestSignature and calls
  // discovery.onTemporalWindowShift().  Neither of these writes back to
  // temporal.requestSignature.  No circular dependency.
  watch(temporal.requestSignature, (_next, prev) => {
    if (prev === undefined) {
      // Skip the immediate initial call — this is the first paint, not a shift.
      return;
    }
    if (temporal.shouldClearSelectedEventOnWindowShift()) {
      discovery.onTemporalWindowShift();
    }
  });

  // ── Map feed query builder (for MapSurface) ──────────────────────────────
  //
  // MapSurface calls this to get its current query every time it needs to reload.
  // MapSurface does NOT own filter state — it receives the query.
  function buildMapQuery(bbox: string) {
    const base = temporal.buildMapFeedQuery(bbox);
    // Merge categorical filters from discovery state.
    return {
      ...base,
      district: discovery.activeFilters.value.district,
      categories: discovery.activeFilters.value.categories,
      includeSavedOnly: discovery.activeFilters.value.includeSavedOnly,
    };
  }

  return { discovery, temporal, buildMapQuery };
}

// ─── Part 2: MapSurface.vue — emitter side ────────────────────────────────────
//
// MapSurface.vue emits three discovery-relevant events:
//   @marker-click     ─ user tapped a map marker
//   @map-feed-query-updated ─ just built & dispatched a query
//   @map-items-updated      ─ feed response arrived
//
// It does NOT manage selection state internally.  It drives the shared state
// by emitting, not by writing to refs.
//
// props accepted from App.vue:
//   selectedEventId: string | null   ← prop, read-only inside MapSurface
//   activeFilters: DiscoveryFilterState ← prop, used to build the query
//
// Anti-loop invariant:
//   When MapSurface re-renders because selectedEventId changed, it must NOT
//   emit @marker-click again.  The marker render is driven by the prop, not
//   the click handler.

/*
──────────────────────────────────────────────────────────────────────────────
MapSurface.vue <script setup> — relevant excerpts
──────────────────────────────────────────────────────────────────────────────

const props = defineProps<{
  selectedEventId: string | null;
  activeFilters: DiscoveryFilterState;
}>();

const emit = defineEmits<{
  'marker-click': [eventId: string];
  'map-feed-query-updated': [query: EventMapFeedQueryDto];
  'map-items-updated': [items: EventMapItemDto[]];
}>();

// Marker click → emit up; do NOT write any local selection ref.
function onMarkerClick(eventId: string): void {
  emit('marker-click', eventId);
}

// After successful feed load → emit both the query and the items.
async function reloadFeed(bbox: string): Promise<void> {
  const query = buildMapQuery(bbox);           // uses props.activeFilters
  emit('map-feed-query-updated', query);
  const response = await fetchEventMapFeed(query);
  emit('map-items-updated', response.events);
}

// Effective marker state is derived from the selectedEventId prop.
// ANTI-LOOP: this is a pure computation, not a watcher that writes back.
const markers = computed(() =>
  events.value.map(event => ({
    ...event,
    effectiveMarkerState:
      props.selectedEventId === event.eventId ? 'selected'
      : event.savedByCurrentUser ? 'saved'
      : 'default',
  }))
);
──────────────────────────────────────────────────────────────────────────────
*/

// ─── Part 3: CalendarOverlayShell.vue — emitter side ────────────────────────
//
// CalendarOverlayShell reads state from props only.  When the user taps a
// calendar card, it emits @select-event.  App.vue wires that to
// discovery.selectEvent — the same action as a map marker click.
//
// This is the "no ownership competition" rule in practice:
//   Map click   → App:discovery.selectEvent(id) → overlay transitions
//   Calendar tap → App:discovery.selectEvent(id) → map marker transitions
//
// Both surfaces converge symmetrically.

/*
──────────────────────────────────────────────────────────────────────────────
CalendarOverlayShell.vue <script setup> — relevant excerpts
──────────────────────────────────────────────────────────────────────────────

const props = defineProps<{
  layer: CalendarOverlayLayerState;
  overlayHeight: string;
  selectedEventId: string | null;
  calendarFeedQuery: EventCalendarFeedQueryDto | null;
}>();

const emit = defineEmits<{
  'set-layer': [layer: CalendarOverlayLayerState];
  'select-event': [eventId: string];
}>();

// Derived highlight state — driven purely by the prop, not internal refs.
// ANTI-LOOP: this computed reads selectedEventId but never writes it.
function isHighlighted(eventId: string): boolean {
  return props.selectedEventId === eventId;
}

function onCardClick(eventId: string): void {
  // Route through App.vue → discovery.selectEvent(eventId).
  // CalendarOverlayShell does not know about the map; it just emits.
  emit('select-event', eventId);
}
──────────────────────────────────────────────────────────────────────────────
*/

// ─── Part 4: Filter control component ────────────────────────────────────────
//
// A standalone filter panel (DistrictFilterPanel.vue or similar) calls
// applyFilters() directly through the parent's discovery reference.
// It must NOT call useDiscoveryState() itself — only App.vue owns the call.

/*
──────────────────────────────────────────────────────────────────────────────
DistrictFilterPanel.vue — relevant excerpts
──────────────────────────────────────────────────────────────────────────────

const emit = defineEmits<{
  'filter-change': [partial: Partial<DiscoveryFilterState>];
}>();

function onDistrictSelect(district: string | undefined): void {
  emit('filter-change', { district });
}

// App.vue wires this:
// @filter-change="discovery.applyFilters"
// then triggers a map reload via buildMapQuery(currentBbox)
──────────────────────────────────────────────────────────────────────────────
*/

// ─── Part 5: State transition rules — machine contract ────────────────────────
//
// This table describes every valid surface action and its outcome.
// Use this as a cross-reference when writing tests.

const STATE_TRANSITIONS = [
  // ── Selection transitions ──────────────────────────────────────────────
  {
    trigger: "MapSurface emits @marker-click(eventId)",
    action: "discovery.selectEvent(eventId)",
    selectedEventId: "→ eventId",
    overlayMode: "→ 'event-selected'",
    notes: "Idempotent if same event already selected.",
  },
  {
    trigger: "CalendarOverlayShell emits @select-event(eventId)",
    action: "discovery.selectEvent(eventId)",
    selectedEventId: "→ eventId",
    overlayMode: "→ 'event-selected'",
    notes: "Identical to marker click. No ownership conflict.",
  },
  {
    trigger: "EventDetailModal emits @update:model-value(false)",
    action: "discovery.clearSelection()",
    selectedEventId: "→ null",
    overlayMode: "→ lastNonSelectedOverlay ('partial' | 'expanded')",
    notes: "Restores overlay to pre-selection state.",
  },
  {
    trigger: "Temporal window shifts (preset change / step)",
    action: "discovery.onTemporalWindowShift()",
    selectedEventId: "→ null (eager eviction)",
    overlayMode: "→ lastNonSelectedOverlay",
    notes: "Prevents stale marker in new time window before feed reloads.",
  },
  {
    trigger: "MapSurface calls reportVisibleEvents(events) after feed load",
    action: "discovery.reportVisibleEvents(events)",
    selectedEventId:
      "→ null if not in new event set; unchanged if still in scope",
    overlayMode: "→ lastNonSelectedOverlay if evicted; unchanged if in scope",
    notes: "Scope eviction. The ONLY automatic selection clear path.",
  },

  // ── Filter transitions ─────────────────────────────────────────────────
  {
    trigger: "User changes district/category filter",
    action: "discovery.applyFilters({ district?, categories? })",
    selectedEventId: "unchanged until reportVisibleEvents() runs after reload",
    overlayMode: "unchanged",
    notes:
      "Caller triggers map reload. Selection eviction is deferred to scope check.",
  },
  {
    trigger: "User clears all filters",
    action: "discovery.clearFilters()",
    selectedEventId: "unchanged until next reportVisibleEvents()",
    overlayMode: "unchanged",
    notes: "Same deferred eviction contract as applyFilters.",
  },

  // ── Overlay mode transitions ───────────────────────────────────────────
  {
    trigger: "User drags overlay handle to expand",
    action: "discovery.expandOverlay()",
    selectedEventId: "unchanged",
    overlayMode: "→ 'expanded'",
    notes: "Invalid from 'closed' — will warn and no-op.",
  },
  {
    trigger: "User swipes overlay down to close",
    action: "discovery.closeOverlay()",
    selectedEventId: "unchanged (close does not clear selection)",
    overlayMode: "→ 'closed'",
    notes:
      "The overlay can be closed while an event is selected. The selection persists in state but the detail modal will close independently.",
  },
] as const;

// ─── Part 6: Testing quick-start ─────────────────────────────────────────────
//
// Because useDiscoveryState uses module-level singleton refs, tests MUST reset
// state between test cases.  The cleanest approach is to mock the module.
//
// Alternatively, expose a _resetForTesting() function in test builds:
//
//   if (import.meta.env.MODE === 'test') {
//     export function _resetDiscoveryState(): void {
//       _selectedEventId.value = null;
//       _overlayMode.value = 'closed';
//       _lastNonSelectedOverlay.value = 'partial';
//       _district.value = undefined;
//       _categories.value = undefined;
//       _includeSavedOnly.value = false;
//       _visibleEventIds.value = new Set();
//       _latestMapFeedQuery.value = null;
//     }
//   }
//
// Unit test sketch:
//
//   import { useDiscoveryState, _resetDiscoveryState } from './useDiscoveryState';
//
//   describe('selectEvent', () => {
//     beforeEach(() => _resetDiscoveryState());
//
//     it('transitions overlay to event-selected', () => {
//       const d = useDiscoveryState();
//       d.openPartialOverlay();
//       d.selectEvent('evt-001');
//       expect(d.selectedEventId.value).toBe('evt-001');
//       expect(d.overlayMode.value).toBe('event-selected');
//     });
//
//     it('is idempotent when same event reselected', () => {
//       const d = useDiscoveryState();
//       d.openPartialOverlay();
//       d.selectEvent('evt-001');
//       const mode = d.overlayMode.value;
//       d.selectEvent('evt-001'); // same ID again
//       expect(d.overlayMode.value).toBe(mode); // no second transition
//     });
//   });
//
//   describe('reportVisibleEvents', () => {
//     beforeEach(() => _resetDiscoveryState());
//
//     it('evicts selected event when removed from scope', () => {
//       const d = useDiscoveryState();
//       d.openPartialOverlay();
//       d.selectEvent('evt-001');
//       d.reportVisibleEvents([]); // event gone
//       expect(d.selectedEventId.value).toBeNull();
//       expect(d.overlayMode.value).toBe('partial'); // restored
//     });
//
//     it('preserves selection when event still in scope', () => {
//       const d = useDiscoveryState();
//       d.openPartialOverlay();
//       d.selectEvent('evt-001');
//       d.reportVisibleEvents([{ eventId: 'evt-001' } as any]);
//       expect(d.selectedEventId.value).toBe('evt-001');
//     });
//   });
//
//   describe('calendarFeedQuery', () => {
//     beforeEach(() => _resetDiscoveryState());
//
//     it('is null until reportMapFeedQuery is called', () => {
//       const d = useDiscoveryState();
//       expect(d.calendarFeedQuery.value).toBeNull();
//     });
//
//     it('inherits district override from applyFilters', () => {
//       const d = useDiscoveryState();
//       d.reportMapFeedQuery({ bbox: '...', preset: 'now', timezone: 'UTC' } as any);
//       d.applyFilters({ district: 'Midtown' });
//       expect(d.calendarFeedQuery.value?.district).toBe('Midtown');
//     });
//   });
// ─────────────────────────────────────────────────────────────────────────────

// Suppress "unused variable" lint warnings — this file is a reference doc.
void exampleAppSetup;
void STATE_TRANSITIONS;
