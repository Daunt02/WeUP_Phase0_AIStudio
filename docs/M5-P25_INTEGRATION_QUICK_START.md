# M5-P25 Integration Quick Start

**Version:** 1.0  
**Purpose:** Step-by-step guide to integrate map performance optimizations into your codebase

---

## Files Created / Modified

| File                                          | Purpose                 | Status     |
| --------------------------------------------- | ----------------------- | ---------- |
| `hooks/useMapFeedOptimized.ts`                | Core optimization hook  | ✅ Created |
| `services/mapPerformanceService.ts`           | Metrics instrumentation | ✅ Created |
| `components/MAP_FEED_INTEGRATION_EXAMPLE.tsx` | Integration example     | ✅ Created |
| `__tests__/unit/useMapFeedOptimized.test.ts`  | Unit tests              | ✅ Created |
| `docs/M5-P25_MAP_PERFORMANCE_STRATEGY.md`     | Strategy document       | ✅ Created |
| `docs/BACKEND_MAP_OPTIMIZATION.md`            | Backend guidance        | ✅ Created |

---

## Quick Start: 3 Steps

### Step 1: Replace useEventFeed with useMapFeedOptimized

**Before:**

```typescript
import { useEventFeed } from "@/hooks/useEventFeed";

export function WorldCoordinator() {
  const mapFeed = useEventFeed(mapBounds); // No viewport debouncing
}
```

**After:**

```typescript
import { useMapFeedOptimized } from "@/hooks/useMapFeedOptimized";
import { createMapPerformanceInstrumentation } from "@/services/mapPerformanceService";

export function WorldCoordinator() {
  const mapFeed = useMapFeedOptimized(mapBounds, mapWindow, detailOpenTime, {
    debounceMs: 400,
    cacheEnabled: true,
    timeoutMs: 8000,
    instrumentation: createMapPerformanceInstrumentation(),
  });
}
```

### Step 2: Update State Handling

**Before:**

```typescript
if (mapFeed.loading) return <LoadingSpinner />;
if (mapFeed.events.length === 0) return <></>;
if (mapFeed.error) return <ErrorMessage />;
```

**After:**

```typescript
const { loadState, events, error, markerCount } = mapFeed;

switch (loadState) {
  case 'idle':
    return null; // No UI needed

  case 'loading':
    return <LoadingSpinner />;

  case 'success':
    return <MapWithMarkers events={events} />;

  case 'empty':
    return <EmptyStateUI />;

  case 'error':
  case 'timeout':
    return <ErrorStateUI error={error} onRetry={() => mapFeed.refresh()} />;

  case 'degraded':
    return <DegradedUI events={events} />;
}
```

### Step 3: Wiring Map Viewport Changes

**In RadarMap.tsx (or your map component):**

```typescript
import { ViewStateChangeEvent } from 'react-map-gl';

export function RadarMap({ mapFeed, onViewportChange }) {
  const handleViewChange = useCallback((event: ViewStateChangeEvent) => {
    const { latitude, longitude, zoom } = event.viewState;

    // Calculate new bounds from viewport
    const bounds = calculateBoundsFromViewport(latitude, longitude, zoom);

    // Trigger parent to update viewport
    // This will feed into useMapFeedOptimized via mapBounds prop
    onViewportChange(bounds);
  }, []);

  return (
    <Map
      onMove={handleViewChange}
      {...otherProps}
    >
      {mapFeed.events.map(event => (
        <Marker key={event.id} {...event} />
      ))}
    </Map>
  );
}
```

---

## Detailed Integration Examples

### Option A: New Component Integration

```typescript
// components/OptimizedMapSurface.tsx

"use client";

import React, { useCallback, useState } from 'react';
import Map, { ViewStateChangeEvent } from 'react-map-gl';
import { useMapFeedOptimized } from '@/hooks/useMapFeedOptimized';
import { createMapPerformanceInstrumentation } from '@/services/mapPerformanceService';
import type { GeoBoundingBox } from '@/domains/query/contracts';

export function OptimizedMapSurface() {
  const [bounds, setBounds] = useState<GeoBoundingBox | null>(null);
  const [viewport, setViewport] = useState({ lat: 29.76, lng: -95.37, zoom: 12 });
  const [detailOpenTime, setDetailOpenTime] = useState<number>();

  // Define temporal window (example: next 7 days)
  const mapWindow = {
    startUtc: new Date().toISOString(),
    endUtc: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
    timezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
  };

  // Core optimization hook
  const mapFeed = useMapFeedOptimized(bounds, mapWindow, detailOpenTime, {
    debounceMs: 400,
    cacheEnabled: true,
    timeoutMs: 8000,
    instrumentation: createMapPerformanceInstrumentation(),
  });

  const handleMapMove = useCallback((event: ViewStateChangeEvent) => {
    const { latitude, longitude, zoom, padding } = event.viewState;
    setViewport({ lat: latitude, lng: longitude, zoom });

    // Calculate new bounds (simplified; use proper calculation for production)
    const latDelta = (40075000 / Math.pow(2, zoom)) / 256;
    const lngDelta = latDelta * Math.cos((latitude * Math.PI) / 180);

    setBounds({
      minLat: latitude - latDelta,
      maxLat: latitude + latDelta,
      minLng: longitude - lngDelta,
      maxLng: longitude + lngDelta,
    });
  }, []);

  const handleEventDetail = useCallback(() => {
    setDetailOpenTime(Date.now());
  }, []);

  return (
    <div className="relative w-full h-screen">
      {/* Map Container */}
      <Map
        initialViewState={viewport}
        onMove={handleMapMove}
        style={{ width: '100%', height: '100%' }}
        mapboxAccessToken={process.env.NEXT_PUBLIC_MAPBOX_TOKEN}
      >
        {/* Markers */}
        {mapFeed.loadState === 'success' && mapFeed.events.map(event => (
          <Marker
            key={event.id}
            latitude={event.latitude}
            longitude={event.longitude}
            onClick={handleEventDetail}
          >
            {/* Your marker component */}
          </Marker>
        ))}
      </Map>

      {/* Status Bar */}
      <div className="absolute top-4 left-4 p-3 bg-white rounded shadow">
        {mapFeed.loadState === 'loading' && 'Loading...'}
        {mapFeed.loadState === 'success' && `${mapFeed.markerCount} events`}
        {mapFeed.loadState === 'empty' && 'No events in this area'}
        {mapFeed.loadState === 'error' && 'Error loading events'}
      </div>

      {/* Refresh Button */}
      <div className="absolute bottom-4 right-4">
        <button
          onClick={() => mapFeed.refresh()}
          className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700"
        >
          Refresh
        </button>
      </div>
    </div>
  );
}
```

### Option B: Migrate Existing useEventFeed

**Before (hooks/useEventFeed.ts):**

```typescript
export function useEventFeed(
  mapBounds: GeoBoundingBox | null,
): EventFeedResult {
  const [events, setEvents] = useState<RuntimeEventProjection[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!mapBounds) return;

    const controller = new AbortController();
    setLoading(true);

    // ❌ No debounce, no cache, full re-render on marker change
    eventService
      .fetchMapFeed({ bounds: mapBounds, window: {}, filters: {} })
      .then((feed) => {
        setEvents(feed.events.map(fromMapCardProjection));
      })
      .finally(() => setLoading(false));

    return () => controller.abort();
  }, [mapBounds]);

  return { events, loading, prependEvent: () => {} };
}
```

**After (hooks/useEventFeed.ts):**

```typescript
import { useMapFeedOptimized } from "./useMapFeedOptimized";
import { createMapPerformanceInstrumentation } from "@/services/mapPerformanceService";

/**
 * @deprecated Use useMapFeedOptimized instead.
 * This wrapper maintains backwards compatibility during migration.
 */
export function useEventFeed(
  mapBounds: GeoBoundingBox | null,
): EventFeedResult {
  const result = useMapFeedOptimized(
    mapBounds,
    {
      startUtc: new Date().toISOString(),
      endUtc: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
      timezone: "UTC",
    },
    undefined,
    {
      instrumentation: createMapPerformanceInstrumentation(),
    },
  );

  return {
    events: result.events,
    loading: result.loadState === "loading",
    prependEvent: () => {}, // TODO: Implement if needed
  };
}
```

---

## Testing the Integration

### 1. Unit Tests (provided)

```bash
npm test -- __tests__/unit/useMapFeedOptimized.test.ts
```

Expected output:

```
✓ Debounce Tests (8 tests)
✓ Caching Tests (3 tests)
✓ Marker Diffing Tests (2 tests)
✓ State Transition Tests (4 tests)
✓ Refresh Tests (2 tests)

Total: 19 tests, 19 passed
```

### 2. Manual Testing Checklist

- [ ] **Debounce:** Pan map rapidly → should see only 1 request, not 50
- [ ] **Cache Hit:** Pan to same area twice → second time should use cache (no network tab activity)
- [ ] **Empty State:** Pan to area with no events → shows "no events" message (not treated as error)
- [ ] **Error Handling:** Unplug network while panning → shows error message with retry button
- [ ] **Detail Open:** Click marker → detail panel opens in < 300ms (measure via DevTools)
- [ ] **Refresh Button:** Click refresh → ignores cache, fetches fresh data
- [ ] **Marker Count:** Zoom in/out → marker count changes appropriately (no lag)
- [ ] **State Transitions:** Monitor Network tab → see state changes (idle → loading → success)

### 3. Performance Testing

```javascript
// In browser console while using map

// Check cache stats
performance.mark("map-feed-test-start");

// Pan map
// ...

performance.mark("map-feed-test-end");
performance.measure(
  "map-feed-test",
  "map-feed-test-start",
  "map-feed-test-end",
);
performance.getEntriesByName("map-feed-test")[0].duration; // Should be < 1200ms
```

---

## Instrumentation & Monitoring

### Setup Analytics Integration

```typescript
// services/analyticsService.ts — extend with map metrics

import { analyticsService } from "@/services/analyticsService";

export function trackMapFeedMetrics(metrics: MapFeedMetrics) {
  analyticsService.trackEvent("map_feed_request_end", {
    duration_ms: metrics.durationMs,
    marker_count: metrics.markerCountRendered,
    is_cache_hit: metrics.isCacheHit,
    is_slow_request: metrics.durationMs > 1200,
  });
}
```

### Key Metrics to Monitor

**In Application Insights / Google Analytics / Segment:**

1. **map_feed_request_start** → map_feed_request_end duration
   - Alert if p95 > 1500ms
   - Alert if p99 > 3000ms

2. **marker_diff_calculated** → diff_added + diff_removed
   - Track trend of marker churn rate

3. **detail_panel_open** → latency_ms
   - Alert if p95 > 500ms

4. **map_feed_degraded** → reason distribution (error / timeout / empty)
   - Alert if error rate > 5%

---

## Common Pitfalls

### ❌ Pitfall 1: Not Setting mapWindow

```typescript
// Wrong: mapWindow is null
const mapFeed = useMapFeedOptimized(mapBounds, null);
// → Hook will not fetch (expects both bounds AND window)
```

```typescript
// Correct: provide temporal window
const mapWindow = {
  startUtc: new Date().toISOString(),
  endUtc: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
  timezone: "America/Chicago",
};
const mapFeed = useMapFeedOptimized(mapBounds, mapWindow);
```

### ❌ Pitfall 2: Ignoring Load States

```typescript
// Wrong: treats empty and error the same
if (!mapFeed.events.length) return <NoData />;

// Correct: distinguish between empty and error
if (mapFeed.loadState === 'empty') return <NoEventsInArea />;
if (mapFeed.loadState === 'error') return <ErrorRetry />;
```

### ❌ Pitfall 3: Mounting Hook with Stale Dependencies

```typescript
// Wrong: mapBounds not in dependency array
useEffect(() => {
  mapFeed.refresh();
}, []); // ← Will never respond to bounds changes!

// Correct: include bounds
useEffect(() => {
  if (mapBounds) {
    // trigger action
  }
}, [mapBounds]);
```

### ❌ Pitfall 4: Disabling Cache on Production

```typescript
// Testing only: disable cache to simulate fresh fetches
const mapFeed = useMapFeedOptimized(bounds, window, undefined, {
  cacheEnabled: false, // ← Good for testing
});

// Production: keep cache enabled
const mapFeed = useMapFeedOptimized(bounds, window, undefined, {
  cacheEnabled: true, // ← Production setting
});
```

---

## Fallback: Gradual Rollout

If you want to test incrementally without immediately replacing the old hook:

### Feature Flag Approach

```typescript
import { useEventFeed as useOldEventFeed } from "@/hooks/useEventFeed";
import { useMapFeedOptimized } from "@/hooks/useMapFeedOptimized";

export function useEventFeed(mapBounds: GeoBoundingBox | null) {
  const useNewHook =
    process.env.NEXT_PUBLIC_MAP_OPTIMIZATION_ENABLED === "true";

  if (useNewHook) {
    return useMapFeedOptimized(mapBounds, defaultWindow);
  } else {
    return useOldEventFeed(mapBounds);
  }
}
```

Environment file (`.env.local`):

```
NEXT_PUBLIC_MAP_OPTIMIZATION_ENABLED=false  # Start with old hook
# Gradually enable: false → 'canary' → 'beta' → true
```

---

## Troubleshooting Integration Issues

| Issue                              | Solution                                                                                                                                    |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| **"markerCount is always 0"**      | Check: Are bounds and window both provided? Fetch may be in `idle` state.                                                                   |
| **"Cache never hits"**             | Check: Are you changing layout/component on every render? This changes object refs, causing different cache keys. Use `useMemo` for bounds. |
| **"Markers flicker/disappear"**    | Check: Are you clearing state on unmount? Verify cleanup function is not clearing events too aggressively.                                  |
| **"Detail panel latency is slow"** | Check: Are you measuring from when map feed completes or from when user clicks marker? Consider prefetching on hover.                       |
| **"Tests timeout"**                | Check: Did you remember `jest.useFakeTimers()` and `jest.advanceTimersByTime()`?                                                            |

---

## Next Steps

1. **Integrate:** Copy `useMapFeedOptimized` hook into your component
2. **Test:** Run unit tests and manual testing checklist
3. **Monitor:** Set up instrumentation hooks + analytics
4. **Deploy:** Use feature flag for gradual rollout
5. **Optimize Backend:** Follow [BACKEND_MAP_OPTIMIZATION.md](./BACKEND_MAP_OPTIMIZATION.md)
6. **Measure:** Track metrics; adjust debounce/TTL if needed

---

## Support & Documentation

- **Main Strategy:** [M5-P25_MAP_PERFORMANCE_STRATEGY.md](./M5-P25_MAP_PERFORMANCE_STRATEGY.md)
- **Backend Guide:** [BACKEND_MAP_OPTIMIZATION.md](./BACKEND_MAP_OPTIMIZATION.md)
- **Example Integration:** [MAP_FEED_INTEGRATION_EXAMPLE.tsx](../components/MAP_FEED_INTEGRATION_EXAMPLE.tsx)
- **Tests:** [**tests**/unit/useMapFeedOptimized.test.ts](__tests__/unit/useMapFeedOptimized.test.ts)

---

**End of Quick Start Guide**
