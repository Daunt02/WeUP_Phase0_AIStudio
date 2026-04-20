# Temporal Navigation & Scrubbing Controls v1.0

**Status**: Production-grade Vue 3 + Quasar component implementing temporal navigation and scrubbing for map-plus-calendar event discovery.

**M6-P29 Implementation**

---

## Overview

This implementation provides a unified temporal state management system that drives both map feed and calendar overlay queries from a single source. Users can navigate time windows using presets, date stepping, timeline scrubbing, and return to the present moment.

### Key Capabilities

- ✓ **Preset navigation**: Now, Tonight, Tomorrow, This Weekend
- ✓ **Date stepping**: Forward/backward by days (with configurable units)
- ✓ **Timeline scrubbing**: Drag to focus within a window (0-1 position)
- ✓ **Reset to now**: Clear all state and return to current time
- ✓ **Debounced updates**: 300ms default (configurable) to batch interactions
- ✓ **Selected event stability**: Preserve or clear selection based on window shift size
- ✓ **Mobile responsive**: Works on 320px+ screens
- ✓ **Accessible**: ARIA roles, keyboard support framework, color contrast

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    App.vue (Parent)                         │
│  - Initializes useTemporalNavigation                        │
│  - Builds mapFeedQuery and calendarTemporalQuery            │
│  - Watches temporal state changes                           │
└──────────────┬────────────────────────────────┬─────────────┘
               │                                │
               ▼                                ▼
    ┌──────────────────────────────────────────────────────┐
    │                 useTemporalNavigation                │
    │               (Composable Layer)                     │
    │                                                      │
    │  - Wraps useMapFeedFilters                           │
    │  - Adds: stepDateTime, scrub, reset logic            │
    │  - Exports: temporalContract, buildMapFeedQuery      │
    │  - Returns: effectiveQueryWindow for backend         │
    └┬───────────────────────────────────────────────────┬─┘
     │                                                    │
     ├─ useMapFeedFilters ────────────────────────────┐ │
     │  (Lower-level contract management)             │ │
     │  - Manages preset, custom range, timezone       │ │
     │  - Validates ranges                            │ │
     │  - Converts local ↔ UTC                        │ │
     └────────────────────────────────────────────────┘ │
     │                                                    │
     ├── TemporalNavigationControls.vue ─────────────────┤
     │   (UI Layer)                                       │
     │   - Preset buttons                                 │
     │   - Date step controls                             │
     │   - Scrub track & handle                           │
     │   - Reset button                                   │
     │   - Status/validation display                      │
     │   - Debug panel (dev only)                         │
     └────────────────────────────────────────────────────┘
                        │
        ┌───────────────┼───────────────┐
        ▼               ▼               ▼
   MapSurface    CalendarOverlay   EventDetailModal
   (Map Feed)    (Calendar Grid)   (Detail View)
```

---

## Files & Responsibilities

### Core Composables

#### `useTemporalNavigation.ts`

Main composable providing temporal navigation and query generation.

**Key exports:**

- `selectPreset(preset)` — Switch to a temporal preset
- `stepDateTime(units, unit)` — Move backward/forward in days/weeks
- `updateScrubPosition(position)` — Set scrub position [0, 1]
- `endScrubbing()` — Finalize scrub interaction
- `resetToNow()` — Clear state and return to present
- `buildMapFeedQuery(bbox)` — Generate API query for map
- `toCalendarOverlayTemporalQuery()` — Generate API query for calendar
- `effectiveQueryWindow` (computed) — Full unmodified preset/custom window
- `shouldClearSelectedEventOnWindowShift()` — Guard for event selection stability

**State exposed:**

```ts
preset; // Current TimeWindowPreset
timezone; // Detected from browser locale
customStartLocal; // User-entered start (local datetime string)
customEndLocal; // User-entered end (local datetime string)
scrubPosition; // 0-1 position within window
stepOffset; // Number of days stepped
isValid; // Boolean: passes validation?
validationMessage; // Error message if invalid
hasPendingTemporalChange; // Debounce-pending indicator
```

---

### Vue Component

#### `TemporalNavigationControls.vue`

Quasar-based UI component for all temporal interactions.

**Props:**

```ts
interface Props {
  temporal: UseTemporalNavigation;
}
```

**Renders:**

- Preset button row (responsive to 2 per row on mobile)
- Temporal status display with validation errors
- Date stepping buttons with offset display
- Interactive timeline scrubber (mouse + touch support)
- Reset button
- Debug info panel (dev mode only)

**Styling:**

- Glassmorphism card with backdrop blur
- Responsive grid layout
- Accessible color contrast
- Touch-optimized sizes

---

### Supporting Files

#### `time-window.contracts.ts`

Canonical contracts (already existed; used by temporal system):

```ts
enum TimeWindowPreset {
  Now = "now",
  Tonight = "tonight",
  Tomorrow = "tomorrow",
  ThisWeekend = "thisWeekend",
  Custom = "custom",
}

interface TimeWindowFilterDto {
  preset: TimeWindowPreset;
  timezone: string;
  customStartUtc?: string; // ISO string
  customEndUtc?: string; // ISO string
}
```

#### `useMapFeedFilters.ts`

Lower-level composable (pre-existing; enhanced by temporal system):

- Manages preset/custom range state
- Validates ranges
- Converts local ↔ UTC
- Builds backend query contracts

---

### Documentation Files

#### `TEMPORAL_NAVIGATION_GUIDE.ts`

Comprehensive guide covering:

- Component integration patterns
- Backend query examples (scenarios 1-6 with actual DTOs)
- State management guardrails
- Scrubbing technical details
- Accessibility features
- Mobile responsive behavior
- Error handling & edge cases
- Testing checklist

#### `TEMPORAL_INTEGRATION_EXAMPLE.ts`

Working code examples:

- Minimal integration boilerplate
- Template snippet
- State debugging helper
- Common patterns (reset, URL share, restore, keyboard)
- User-friendly error messages

#### `README.md` (this file)

System overview and quick-start.

---

## Quick Start

### 1. Initialize Composable (in App.vue or parent)

```ts
import { useTemporalNavigation } from "./composables/useTemporalNavigation";

const temporal = useTemporalNavigation({
  debounceMs: 300, // Optional
  onSelectedEventChange: (eventId) => {
    if (!eventId) selectedEventId.value = null;
  },
});
```

### 2. Build Queries

```ts
import type { EventMapFeedQueryDto } from "./contracts/map-feed.contracts";

const mapFeedQuery = computed<EventMapFeedQueryDto | null>(() => {
  if (!temporal.isValid.value) return null;
  try {
    return temporal.buildMapFeedQuery("[-96.5,29.5,-95.0,30.0]");
  } catch (e) {
    console.error(e);
    return null;
  }
});

const calendarTemporalQuery = computed(() => {
  if (!temporal.isValid.value) return null;
  try {
    return temporal.toCalendarOverlayTemporalQuery();
  } catch (e) {
    console.error(e);
    return null;
  }
});
```

### 3. Watch for Changes & Fetch Data

```ts
watch(
  () => temporal.requestSignature.value,
  async (signature) => {
    if (!signature || !mapFeedQuery.value) return;

    const response = await fetch("/api/events/map-feed", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(mapFeedQuery.value),
    });

    const data = await response.json();
    updateMapItems(data.events);
  },
);
```

### 4. Render Controls

```vue
<template>
  <TemporalNavigationControls :temporal="temporal" />
  <MapSurface v-if="mapFeedQuery" :query="mapFeedQuery" />
  <CalendarOverlayShell
    v-if="calendarTemporalQuery"
    :temporal-query="calendarTemporalQuery"
    :scrub-position="temporal.scrubPosition.value"
  />
</template>

<script setup lang="ts">
import TemporalNavigationControls from "./components/TemporalNavigationControls.vue";
// ... (rest of setup from step 1-3 above)
</script>
```

### 5. Handle Event Selection Stability

```ts
watch(
  () => temporal.navigationState.value,
  () => {
    if (temporal.shouldClearSelectedEventOnWindowShift()) {
      selectedEventId.value = null;
    }
  },
);
```

---

## Key Concepts

### Presets vs. Custom Ranges

**Presets** (e.g., "Now", "Tonight") expand to absolute windows on the **backend**:

- Frontend sends: `{ preset: "now", timezone: "America/Chicago" }`
- Backend responds with events in that window (exact expansion is backend logic)

**Custom ranges** are explicit absolute windows:

- Frontend sends: `{ preset: "custom", customStartUtc: "...", customEndUtc: "..." }`
- Backend returns all events within those bounds

### Date Stepping

Clicking forward/backward arrows moves the window by 1 day (configurable):

- ✓ User is on "Now" → step forward → Transition to custom range (tomorrow)
- ✓ User is on custom range → step forward → Shift both bounds forward by 1 day
- ✓ Step offset is tracked and used to determine if selection should clear

### Scrubbing

Dragging the scrubber **does not** modify the backend query window:

- Scrub position is **purely UI/UX** (0-1 indicates focus within window)
- Calendar overlay uses the position to decide which events to show first
- Backend always returns full window, same as if not scrubbing

**Example:**

```
User selects "This Weekend" → Backend returns all events Fri-Sun
User scrubs to 0.75 (late in window) → Calendar shows Sunday events first
Backend query unchanged; still returns full Fri-Sun window
```

### Debouncing

Temporal state changes are debounced to avoid API floods:

- Default: 300ms (configurable per instance)
- Rapid interactions (preset switches, scrubbing) are batched
- Query only fires after 300ms of inactivity

---

## Backend Contract Examples

### Scenario: Preset Selection (Now)

```json
{
  "preset": "now",
  "timezone": "America/Chicago",
  "bbox": "[-96.5,29.5,-95.0,30.0]"
}
```

Backend expands to something like: `[current_time - 1hr, current_time + 3hr]`

### Scenario: Date Stepping Forward

```json
{
  "preset": "custom",
  "timezone": "America/Chicago",
  "customStartUtc": "2026-04-21T00:00:00Z",
  "customEndUtc": "2026-04-21T23:59:59Z",
  "bbox": "[-96.5,29.5,-95.0,30.0]"
}
```

### Scenario: Scrubbing (Same Query)

User scrubs to 75% position:

```json
{
  "preset": "thisWeekend",
  "timezone": "America/Chicago",
  "bbox": "[-96.5,29.5,-95.0,30.0]"
  // Query unchanged from before scrub!
  // scrubPosition (0.75) is sent to CalendarOverlay separately
}
```

See `TEMPORAL_NAVIGATION_GUIDE.ts` for 6 complete scenario examples.

---

## Validation & Error Handling

### Validation Errors (Prevent API Call)

The system automatically validates and blocks invalid queries:

| Error                 | Trigger                            | Message                                 |
| --------------------- | ---------------------------------- | --------------------------------------- |
| Missing timezone      | Timezone string is empty           | "Timezone is required..."               |
| Missing custom bounds | Preset=Custom but start/end empty  | "Custom range requires both..."         |
| Invalid datetime      | Non-parseable datetime format      | "Custom range values must be valid..."  |
| Start >= end          | Custom start not before custom end | "Custom range start must be earlier..." |

**Usage in component:**

```ts
if (!temporal.isValid.value) {
  console.warn(temporal.validationMessage.value);
  return; // Don't send query
}
```

### Edge Cases Handled

- ✓ Rapid preset switching → Debounced to single query
- ✓ Scrubbing while stepping → Event selection preserved
- ✓ Window rotation → Scrubber recalculated via getBoundingClientRect()
- ✓ Invalid custom dates → Blocked by validation layer
- ✓ Timezone changes → Stored once, frozen for session

---

## Mobile & Accessibility

### Responsive Breakpoints

| Screen    | Preset Buttons          | Scrubber  | Details       |
| --------- | ----------------------- | --------- | ------------- |
| < 320px   | 1 per row, smaller font | 32px tall | Text: 0.7rem  |
| 320-512px | 2 per row               | 32px tall | Font: 0.85rem |
| > 512px   | 5 per row or wrap       | 40px tall | Font: 1rem    |

### Accessibility

- ✓ Preset buttons: `aria-pressed`, `aria-label`
- ✓ Step buttons: `aria-label="Step forward 1 day"`, etc.
- ✓ Scrubber: `role="slider"`, `aria-valuenow`, `aria-valuemin/max`
- ✓ Color contrast: WCAG AA compliant
- ✓ Touch targets: 32-40px minimum
- ✓ Focus management: Tab through controls naturally

---

## Testing

### Unit Tests (useTemporalNavigation)

```ts
describe("useTemporalNavigation", () => {
  it("selectPreset resets stepOffset", () => {
    const t = useTemporalNavigation();
    t.stepDateTime(2);
    expect(t.stepOffset.value).toBe(2);
    t.selectPreset("tonight");
    expect(t.stepOffset.value).toBe(0);
  });

  it("updateScrubPosition clamps to [0, 1]", () => {
    const t = useTemporalNavigation();
    t.updateScrubPosition(1.5);
    expect(t.scrubPosition.value).toBe(1);
    t.updateScrubPosition(-0.5);
    expect(t.scrubPosition.value).toBe(0);
  });

  it("shouldClearSelectedEventOnWindowShift returns true for large steps", () => {
    const t = useTemporalNavigation();
    t.stepDateTime(5); // Large step
    expect(t.shouldClearSelectedEventOnWindowShift()).toBe(true);
  });

  it("shouldClearSelectedEventOnWindowShift returns false during scrubbing", () => {
    const t = useTemporalNavigation();
    t.updateScrubPosition(0.8);
    expect(t.shouldClearSelectedEventOnWindowShift()).toBe(false);
  });

  it("validationError catches missing timezone", () => {
    const t = useTemporalNavigation();
    t.timezone.value = "";
    expect(t.validationMessage.value).toContain("Timezone");
  });

  it("buildMapFeedQuery throws if validation fails", () => {
    const t = useTemporalNavigation();
    t.preset.value = "custom";
    t.customStartLocal.value = "invalid";
    expect(() => t.buildMapFeedQuery("...")).toThrow();
  });
});
```

### Component Tests (TemporalNavigationControls.vue)

```ts
describe("TemporalNavigationControls", () => {
  it("renders preset buttons", () => {
    const temporal = useTemporalNavigation();
    const wrapper = mount(TemporalNavigationControls, {
      props: { temporal },
    });
    expect(wrapper.findAll(".preset-btn")).toHaveLength(5);
  });

  it("clicking preset button calls selectPreset", async () => {
    const temporal = useTemporalNavigation();
    const wrapper = mount(TemporalNavigationControls, {
      props: { temporal },
    });
    const tonightBtn = wrapper.find('[aria-label*="Tonight"]');
    await tonightBtn.trigger("click");
    expect(temporal.preset.value).toBe("tonight");
  });

  it("scrub handle responds to drag", async () => {
    const temporal = useTemporalNavigation();
    const wrapper = mount(TemporalNavigationControls, {
      props: { temporal },
    });
    const handle = wrapper.find(".scrub-handle");
    await handle.trigger("mousedown");
    // Simulate drag to 75%
    await document.dispatchEvent(new MouseEvent("mousemove", { clientX: 300 }));
    expect(temporal.scrubPosition.value).toBeGreaterThan(0.5);
  });
});
```

### Integration Tests

See `TEMPORAL_NAVIGATION_GUIDE.ts` section "PART 7: TESTING CHECKLIST" for full list.

---

## Debugging

### Enable Debug Panel

The component includes a dev-mode debug panel showing:

- Current request signature
- Step offset
- Scrub position percentage

Set `import.meta.env.DEV` to `true` in development.

### Log Temporal State

```ts
console.group("Temporal State");
console.log("Preset:", temporal.preset.value);
console.log("Step Offset:", temporal.stepOffset.value);
console.log("Scrub Position:", temporal.scrubPosition.value);
console.log("Validation Error:", temporal.validationMessage.value);
console.log("Request Signature:", temporal.requestSignature.value);
console.groupEnd();
```

---

## Future Enhancements

Post-MVP features not yet implemented:

- [ ] Keyboard navigation (arrow keys to step, etc.)
- [ ] Snap-to-grid for scrubbing (snap to hour boundaries)
- [ ] Animated window transitions (zoom/pan effects)
- [ ] User-defined presets
- [ ] Timezone switching (with all presets updating)
- [ ] Advanced filtering (time + category in single control)
- [ ] Temporal search history ("Recent Windows")
- [ ] Undo/Redo stack for temporal navigation

---

## Files Checklist

✓ `useTemporalNavigation.ts` — Core composable (250 lines)
✓ `TemporalNavigationControls.vue` — Vue component (500 lines)
✓ `TEMPORAL_NAVIGATION_GUIDE.ts` — Comprehensive guide (700+ lines)
✓ `TEMPORAL_INTEGRATION_EXAMPLE.ts` — Working examples (400+ lines)
✓ `README.md` — This file

**Pre-existing files used:**

- `useMapFeedFilters.ts` — Lower-level filter management
- `time-window.contracts.ts` — Canonical contracts
- `map-feed.contracts.ts` — Map feed DTOs

---

## Constraints Met

✅ No overly decorative controls without operational clarity
✅ No duplicated date logic (centralized in composables)
✅ Usable on mobile viewport sizes (responsive design)
✅ Controls not inventing independent time windows
✅ Scrubbing has bounded, understandable semantics (0-1 position)
✅ State changes debounced where appropriate (300ms default)
✅ Selected event behavior defined explicitly (guard function)
✅ Aligned to backend TimeWindowPreset/TimeWindowFilterDto contracts

---

## Support & Troubleshooting

### Issue: "Timezone is required" error

**Cause:** Browser didn't provide timezone via `Intl.DateTimeFormat()`
**Solution:** Fallback "UTC" is used; this is very rare in modern browsers

### Issue: Map doesn't update when I change presets

**Cause:** `mapFeedQuery.value` is still null due to validation error
**Solution:** Check console for `temporal.validationMessage.value`

### Issue: Selected event disappears when I step far forward

**Expected behavior:** `shouldClearSelectedEventOnWindowShift()` returns true for large steps (>2 days)
**Solution:** This is intentional; event likely out of new window range

### Issue: Scrubber feels laggy on mobile

**Cause:** Expensive calendar re-renders on each scrub update
**Solution:** Increase debounceMs; wrap CalendarOverlay in Suspense

---

## References

- **Contracts:** See `time-window.contracts.ts` and `map-feed.contracts.ts`
- **Existing filter logic:** See `useMapFeedFilters.ts`
- **Integration guide:** See `TEMPORAL_INTEGRATION_EXAMPLE.ts`
- **Full guide:** See `TEMPORAL_NAVIGATION_GUIDE.ts`

---

**M6-P29 Implementation** | Vue 3 + TypeScript + Quasar | 2026-04-20
