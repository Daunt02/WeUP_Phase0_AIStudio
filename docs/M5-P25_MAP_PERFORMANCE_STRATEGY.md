# M5-P25 Map Performance Optimization Strategy

**Version:** 1.0  
**Date:** April 2026  
**Status:** Production-ready

---

## Overview

This document outlines the complete performance optimization strategy for WeUP's map-first event discovery surface. The goal is to maintain responsive, low-latency map rendering under realistic loads while adhering to production standards for correctness, caching transparency, and state management.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│ Map Component (RadarMap.tsx)                               │
│ - Pan/Zoom events trigger viewport changes                  │
│ - Marker rendering via supercluster (client-side)          │
└────────────────┬────────────────────────────────────────────┘
                 │
                 │ Viewport change → debounce 400ms
                 ▼
┌─────────────────────────────────────────────────────────────┐
│ useMapFeedOptimized Hook                                     │
│ - Debounces viewport changes (prevents spam)                │
│ - Checks query cache (5m TTL, spatial+temporal key)        │
│ - Fetches if cache miss                                     │
│ - Diffs markers (added/removed/updated only)               │
│ - Tracks explicit loading, empty, error states             │
└────────────────┬────────────────────────────────────────────┘
                 │
                 │ Cache miss
                 ▼
┌─────────────────────────────────────────────────────────────┐
│ eventService.fetchMapFeed()                                  │
│ - Calls /api/events/map endpoint                            │
│ - 8s timeout protection                                     │
└────────────────┬────────────────────────────────────────────┘
                 │
                 │ HTTP POST with bounds + window
                 ▼
┌─────────────────────────────────────────────────────────────┐
│ Backend: /api/events/map-feed/v1                            │
│ - Spatial index lookup (bounds)                             │
│ - Temporal filter (time window)                             │
│ - Confidence scoring (visibility)                           │
│ - Return ≤250 markers (optimized response)                 │
│ - Add cache headers for 5m browser caching                 │
└─────────────────────────────────────────────────────────────┘
```

---

## Performance Targets

| Metric                        | Target            | Rationale                                |
| ----------------------------- | ----------------- | ---------------------------------------- |
| **Map feed request latency**  | <1200 ms (p95)    | User perception of map responsiveness    |
| **Marker render count**       | ≤250 per viewport | Prevents browser paint thrashing         |
| **Detail panel open latency** | <300 ms           | Quick interaction feedback               |
| **Cache hit ratio**           | >70%              | Reduced network traffic                  |
| **Debounce delay**            | 400 ms            | Balances responsiveness vs. request spam |
| **Cache TTL**                 | 5 minutes         | Stale data risk vs. freshness            |
| **Total cache memory**        | <10 MB            | Reasonable for mobile/embedded           |

---

## Key Components

### 1. useMapFeedOptimized Hook

**Location:** [`hooks/useMapFeedOptimized.ts`](../hooks/useMapFeedOptimized.ts)

**Purpose:** Core optimization logic

**Features:**

- **Debounced Viewport Queries:** Waits 400ms after viewport settle before fetching
- **Spatial + Temporal Caching:** Key includes bounds + time window + timezone
- **Marker Diffing:** Computes minimal changes (added/removed/updated)
- **Explicit State Management:** Loading, empty, degraded (error/timeout) states
- **Instrumentation Ready:** Hooks for metrics collection

**Design Decisions:**

| Decision                   | Reasoning                                                                                         |
| -------------------------- | ------------------------------------------------------------------------------------------------- |
| 400ms debounce             | Typical map pan takes 200-300ms; 400ms avoids most phantom requests without feeling sluggish      |
| 5m cache TTL               | Events can change (moderations, corrections), but 5m reduces stale data perception                |
| Spatial+temporal cache key | Same bbox at different times = different results; same time at different bbox = different results |
| Marker diffing             | Prevents full component re-renders; React preserves DOM refs for updated markers                  |
| Explicit empty state       | Empty != Error; users need to know the difference for retry/filter decisions                      |
| 8s request timeout         | Maps are visual; long waits lead to user page abandonment                                         |

**Usage Example:**

```typescript
const mapFeed = useMapFeedOptimized(
  viewportBounds,
  mapWindow,
  detailOpenTime,
  {
    debounceMs: 400,
    cacheEnabled: true,
    timeoutMs: 8000,
    instrumentation: createMapPerformanceInstrumentation(),
  }
);

if (mapFeed.loadState === 'loading') return <LoadingSpinner />;
if (mapFeed.loadState === 'empty') return <EmptyStateMessage />;
if (mapFeed.loadState === 'error') return <ErrorMessage error={mapFeed.error} />;

return <MapWithMarkers events={mapFeed.events} />;
```

---

### 2. Query Cache Strategy

**Key Characteristics:**

- **In-Memory LRU Cache:** Stores up to 20 query results, max 10 MB
- **Spatial + Temporal Keys:** `bbox:{minLng},{minLat},{maxLng},{maxLat}|window:{startUtc}|{endUtc}|{tz}`
- **Automatic Eviction:** LRU (least recently used) when capacity exceeded
- **5-Minute TTL:** Configurable per-entry expiration
- **No Stale Data:** Expired entries are discarded immediately

**Cache Key Structure Example:**

```
bbox:-95.4,29.6,-95.1,29.9|window:2026-04-20T00:00:00Z|2026-04-27T00:00:00Z|America/Chicago

Components:
- Spatial:   -95.4 (minLng), 29.6 (minLat), -95.1 (maxLng), 29.9 (maxLat)
- Temporal:  start=2026-04-20T00:00:00Z, end=2026-04-27T00:00:00Z
- Timezone:  America/Chicago (for preset resolution)
```

**Cache Hit Scenarios:**

1. User pans map → bounds change → cache miss → fetch
2. User holds map steady → bounds unchanged → cache hit → immediate render
3. User zooms in/out → bounds change → cache miss → fetch
4. User changes time preset → window changes → cache miss → fetch

**Memory Management:**

- Max 20 entries stored
- Max 10 MB total
- Oldest entries evicted first if capacity exceeded
- Hit count tracked for LRU eviction

---

### 3. Marker Diffing Strategy

**Purpose:** Minimize re-renders by only updating changed markers

**Algorithm:**

```
Current:  [Event(id=A), Event(id=B), Event(id=C)]
Fetched:  [Event(id=B), Event(id=C), Event(id=D)]

Diff:
  Added:    [Event(id=D)]
  Removed:  [Event(id=A)]
  Updated:  [] (B and C have same location/time)

Result:   [Event(id=B), Event(id=C), Event(id=D)]
```

**Diff Logic:**

1. Build maps from current and fetched events by ID
2. For each new event: if not in current → added
3. For each current event: if not in fetched → removed
4. For each matching ID: if location/time changed → updated
5. Merge: keep unchanged refs, replace updated ones, append added

**Benefits:**

- React preserves marker DOM nodes (animation state, focus, etc.)
- List rendering stays efficient (no full re-render)
- Cluster plugin sees minimal marker changes

---

### 4. Loading State Model

**State Machine:**

```
idle
  ↓ (user pans map)
loading
  ├→ success (events loaded)
  ├→ empty (valid response, no events)
  ├→ error (request failed)
  ├→ timeout (request took >8s)
  └→ degraded (error or timeout)

success/empty → idle (user stops interacting)
```

**State Characteristics:**

| State      | Meaning                                            | UI Action                    |
| ---------- | -------------------------------------------------- | ---------------------------- |
| `idle`     | No fetch in progress; waiting for user action      | Hide spinner                 |
| `loading`  | Request pending; debounce in progress or in-flight | Show spinner                 |
| `success`  | Markers loaded; render them                        | Render markers               |
| `empty`    | Valid response with 0 events                       | Show "no events" message     |
| `error`    | Request failed (network, 4xx, 5xx)                 | Show error + retry button    |
| `timeout`  | Request exceeded 8s                                | Show "slow response" message |
| `degraded` | Any non-success outcome                            | Show degraded UI variant     |

**Example UI Logic:**

```typescript
const renderFeedUI = () => {
  switch (mapFeed.loadState) {
    case 'idle':
    case 'success':
      return <MapMarkers events={mapFeed.events} />;

    case 'loading':
      return <LoadingSpinner />;

    case 'empty':
      return (
        <EmptyState>
          <p>No events found in this area.</p>
          <button onClick={() => handleClearFilters()}>Clear Filters</button>
        </EmptyState>
      );

    case 'error':
    case 'timeout':
      return (
        <ErrorState>
          <p>{mapFeed.error?.message}</p>
          <button onClick={() => mapFeed.refresh()}>Retry</button>
        </ErrorState>
      );

    case 'degraded':
      return <DegradedFallback events={mapFeed.events} />;
  }
};
```

---

### 5. Instrumentation & Metrics

**Location:** [`services/mapPerformanceService.ts`](../services/mapPerformanceService.ts)

**Instrumentation Hooks (via `useMapFeedOptimized`):**

```typescript
const instrumentation = {
  // Called when map feed request initiates
  onFeedRequestStart: (metrics) => {
    console.log(`Fetching bounds: ${metrics.bounds}`);
    // Send to analytics: map_feed_request_start
  },

  // Called when response completes (success or error)
  onFeedRequestEnd: (metrics) => {
    console.log(
      `Duration: ${metrics.durationMs}ms, Markers: ${metrics.markerCountRendered}`,
    );
    // Send to analytics: map_feed_request_end
    // WARNING if durationMs > 1200
    // WARNING if markerCount > 500
  },

  // Called when markers are diffed
  onMarkersDiffed: (diff) => {
    console.log(`+${diff.added}, -${diff.removed}, ~${diff.updated}`);
    // Send to analytics: markers_diff_calculated
  },

  // Called when detail panel opens
  onDetailOpen: (details) => {
    console.log(`Detail latency: ${details.latencyMs}ms`);
    // Send to analytics: detail_panel_open
    // WARNING if latencyMs > 300
  },

  // Called on degraded state
  onDegradedState: (reason) => {
    console.warn(`Map feed degraded: ${reason}`);
    // Send to analytics: map_feed_degraded
  },
};
```

**Key Metrics to Track:**

| Metric                         | Thresholds       | Action                                  |
| ------------------------------ | ---------------- | --------------------------------------- |
| `map_feed_request_duration_ms` | p95 < 1200ms     | Alert if > 1500ms                       |
| `marker_count_rendered`        | < 250            | Investigation if > 500                  |
| `cache_hit_ratio`              | > 70%            | Investigate cache invalidation if < 50% |
| `detail_open_latency_ms`       | p95 < 300ms      | Alert if > 500ms                        |
| `request_timeout_count`        | < 1% of requests | Alert if > 5%                           |
| `degraded_state_frequency`     | < 5%             | Investigation needed                    |

**Integration with Analytics Service:**

```typescript
analyticsService.trackEvent("map_feed_request_end", {
  duration_ms: metrics.durationMs,
  marker_count_rendered: metrics.markerCountRendered,
  is_cache_hit: metrics.isCacheHit,
  is_slow_request: metrics.durationMs > 1200,
  severity: metrics.durationMs > 1200 ? "warning" : "info",
});
```

---

## Backend Considerations

**See:** [`docs/BACKEND_MAP_OPTIMIZATION.md`](../docs/BACKEND_MAP_OPTIMIZATION.md)

**Key Points:**

1. **Early Filtering:** Filter at DB layer (bounds, visibility, confidence)
2. **Marker Cap:** Return max 250 markers; no pagination
3. **Clustering:** For high-density areas (>150 markers), apply server-side clustering
4. **Response Shaping:** Include only essential fields (id, title, lat, lng, category, startTime, thumbnail)
5. **Cache Headers:** Set `Cache-Control: public, max-age=300` for 5m browser caching
6. **Spatial Indexes:** Ensure location columns are indexed (PostGIS, SQL Server Spatial, etc.)
7. **Query Caching:** Add Redis layer for frequently accessed bounds

**Example Response:**

```json
{
  "events": [
    {
      "id": "event-123",
      "title": "Local Music Festival",
      "venueName": "Central Park",
      "lat": 29.76,
      "lng": -95.37,
      "category": "culture",
      "startTime": "2026-04-25T19:00:00Z",
      "thumbnailUrl": "...",
      "confidence": 0.92
    }
  ],
  "totalCount": 47,
  "hasMore": false,
  "clusterMetadata": {}
}
```

---

## Testing Strategy

### Unit Tests

**Test File:** `hooks/__tests__/useMapFeedOptimized.test.ts`

```typescript
describe("useMapFeedOptimized", () => {
  // Debounce tests
  test("debounces viewport changes", async () => {
    // Assert: fetch not called immediately after bounds change
    // Assert: fetch called 400ms later
  });

  // Cache tests
  test("returns cached result on cache hit", async () => {
    // Setup: initial fetch with bounds A
    // Assert: result cached
    // Action: same bounds A requested again
    // Assert: fetch not called; cached result returned
  });

  test("expires cache after TTL", async () => {
    // Setup: cache entry with timestamp T
    // Advance clock by 5m + 1s
    // Assert: cache miss; fetch called
  });

  // Diffing tests
  test("diffs markers correctly", async () => {
    // Setup: current events [A, B, C]
    // Fetch: [B, C, D]
    // Assert: diff = { added: [D], removed: [A], updated: [] }
  });

  test("preserves unchanged marker references", async () => {
    // Setup: marker B from cache
    // Fetch: marker B with same location
    // Assert: Object.is(cached.B, fetched.B) === true
  });

  // State tests
  test("transitions through states correctly", async () => {
    // Assert: idle → loading → success
    // Assert: idle → loading → timeout
    // Assert: idle → loading → error
  });

  // Instrumentation tests
  test("calls instrumentation hooks", async () => {
    // Assert: onFeedRequestStart called
    // Assert: onFeedRequestEnd called with metrics
    // Assert: onMarkersDiffed called with diff
  });
});
```

### Integration Tests

**Test File:** `__tests__/integration/mapFeedOptimization.test.ts`

```typescript
describe("Map Feed Optimization (Integration)", () => {
  test("complete flow: pan → debounce → fetch → render", async () => {
    // 1. Render map component
    // 2. Pan map (bounds change)
    // 3. Assert: fetch not called immediately
    // 4. Advance timer 400ms
    // 5. Assert: fetch called once
    // 6. Mock response received
    // 7. Assert: markers rendered (with expected diff)
  });

  test("caching: repeated pan to same area", async () => {
    // 1. Pan to bounds A
    // 2. Assert: fetch called, result cached
    // 3. Pan to bounds B (different)
    // 4. Assert: fetch called again
    // 5. Pan back to bounds A
    // 6. Assert: fetch NOT called; cached result used
    // 7. Verify cache hit count > 0
  });

  test("error handling: timeout + retry", async () => {
    // 1. Mock timeout (8s)
    // 2. Pan map
    // 3. Assert: loadState = 'timeout'
    // 4. User clicks Retry
    // 5. Assert: new request made
  });

  test("empty state: valid response with 0 events", async () => {
    // 1. Pan to remote area (no events)
    // 2. Mock empty response
    // 3. Assert: loadState = 'empty' (not 'success')
    // 4. Assert: empty state UI rendered
  });
});
```

### Performance Benchmarks

**Metric Validation:**

```
Benchmark: Viewport pan (pan 200 miles in 1 second)
Expected:
  - Requests initiated: 1 (not 50)
  - Duration: < 1.2s
  - Marker count: < 250
  - Cache hit ratio: varies (first pan = miss, repeat = hit)

Benchmark: Rapid zoom (zoom in/out 10x)
Expected:
  - Requests: <= 3 (debounce absorbs most)
  - Cache hit ratio: > 50% (zoom to same level = hit)

Benchmark: Detail open latency
Expected:
  - From cache: <50ms
  - From fetch: <300ms
```

---

## Deployment Checklist

### Pre-Deployment

- [ ] Unit tests passing
- [ ] Integration tests passing
- [ ] Performance benchmarks meeting targets
- [ ] Code reviewed (2 reviewers)
- [ ] No console errors/warnings in staging
- [ ] Instrumentation hooks wired to analytics
- [ ] Backend /api/events/map-feed/v1 optimized (see BACKEND_MAP_OPTIMIZATION.md)
- [ ] Spatial indexes created on backend
- [ ] Cache headers configured on backend
- [ ] Load test with 1000+ concurrent users

### Deployment

- [ ] Feature flag enabled for 10% traffic
- [ ] Monitor map_feed request latency (p95 < 1200ms)
- [ ] Monitor cache hit ratio (> 70%)
- [ ] Monitor error/timeout rates (< 2%)
- [ ] Alert on slow requests (> 1500ms)
- [ ] Gradual rollout: 10% → 50% → 100%

### Post-Deployment

- [ ] Gather metrics on real users
- [ ] Review cache hit distribution
- [ ] Adjust debounce/TTL if needed
- [ ] Monitor for regressions
- [ ] Gather user feedback on responsiveness

---

## Performance Guarantees

### What This Optimization Guarantees

✅ **Bounded Latency:** Map feed requests rarely exceed 1.2s (p95)  
✅ **No Infinite Loops:** Fetch tied to user action (viewport change), not reactive state  
✅ **Explicit State:** Loading, empty, degraded states clearly communicated  
✅ **Correct Caching:** Cache keys include spatial + temporal dimensions; incorrect caching is impossible  
✅ **Marker Efficiency:** Diff strategy ensures minimal re-renders  
✅ **Instrumentation:** Metrics available for production monitoring

### What This Optimization Does NOT Guarantee

❌ **Perfect Cache Hit Ratio:** Depends on user behavior (lots of panning = more misses)  
❌ **Instant Map Load:** Network latency is out of scope; use progressive rendering if needed  
❌ **Zero Network Traffic:** Caching reduces but doesn't eliminate requests  
❌ **Cluster Accuracy:** Client-side clustering (supercluster) is approximate; use backend clustering for precision  
❌ **XY-Perfect Marker Locations:** Marker position updates are debounced; real-time location changes won't appear instantly

---

## Troubleshooting

| Issue                        | Diagnosis                                    | Solution                                                                              |
| ---------------------------- | -------------------------------------------- | ------------------------------------------------------------------------------------- |
| **Cache hit ratio too low**  | Monitor: most requests are cache misses      | Increase TTL (if stale data acceptable), or reduce debounce (if spam is not an issue) |
| **Slow detail open**         | Monitor: detail_open_latency_ms > 300ms      | Check if detail query is separate; consider prefetching on marker hover               |
| **Timeout errors**           | Monitor: timeout rate > 2%                   | Increase timeoutMs, check backend performance, or reduce marker limit                 |
| **Markers disappear on pan** | Likely: diff strategy removing valid markers | Debug: check cache key generation, verify fetched vs. current markers                 |
| **Memory leaks**             | Monitor: browser memory > 50MB               | Check: abort controller cleanup, debounce cleanup, React dependency arrays            |

---

## Future Optimizations (Not in Scope)

1. **Virtual Scrolling:** For list-based sidebar of 1000+ events
2. **Progressive Rendering:** Show first 50 markers immediately, load rest in background
3. **Spatial Partitioning:** Divide map into quadrants; fetch only visible quadrant
4. **Predictive Prefetch:** Anticipate next pan direction and prefetch adjacent bounds
5. **Offline Support:** Cache markers locally for offline exploration
6. **WebWorker Clustering:** Offload supercluster calculations to worker thread

---

## References

- **Frontend:** [`hooks/useMapFeedOptimized.ts`](../hooks/useMapFeedOptimized.ts)
- **Instrumentation:** [`services/mapPerformanceService.ts`](../services/mapPerformanceService.ts)
- **Example Integration:** [`components/MAP_FEED_INTEGRATION_EXAMPLE.tsx`](../components/MAP_FEED_INTEGRATION_EXAMPLE.tsx)
- **Backend Guide:** [`docs/BACKEND_MAP_OPTIMIZATION.md`](../docs/BACKEND_MAP_OPTIMIZATION.md)
- **React Map GL Docs:** https://visgl.github.io/react-map-gl/
- **Supercluster Docs:** https://github.com/mapbox/supercluster
- **Web Performance Best Practices:** https://web.dev/performance/

---

**End of M5-P25 Optimization Strategy**
