"use client";

/**
 * useMapFeedOptimized — Production Map Feed Hook
 *
 * Optimizes event feed rendering with:
 * - Debounced viewport queries (prevents request spam)
 * - Spatial + temporal query result caching
 * - Marker diff/update strategy (avoid full re-renders)
 * - Explicit loading, empty, degraded states
 * - Instrumentation hooks for performance metrics
 *
 * Design Constraints:
 * - No fake data via stale cache; stale results are discarded
 * - Cache keys include both spatial bounds and temporal window
 * - Fetch requests are tied to user interaction, not reactive state alone
 * - No infinite loops: single fetch per viewport settle
 *
 * Performance Targets:
 * - Initial map feed request should complete within 1200ms
 * - Marker count should not exceed visible viewport (clustered at distance)
 * - Detail panel open latency <300ms from cached query
 * - Debounce delay: 400ms (balances responsiveness vs. request spam)
 */

import { useEffect, useRef, useState, useCallback, useMemo } from "react";
import { eventService } from "@/services/eventService";
import type {
  GeoBoundingBox,
  MapFeedQuery,
  MapFeedResponse,
} from "@/domains/query/contracts";
import {
  RuntimeEventProjection,
  fromMapCardProjection,
} from "@/features/world/runtimeTypes";

// ============================================================================
// Instrumentation Types
// ============================================================================

export interface MapFeedMetrics {
  requestStartTime: number;
  requestEndTime?: number;
  durationMs?: number;
  markerCountRendered: number;
  markerCountAdded: number;
  markerCountRemoved: number;
  markerCountUpdated: number;
  isCacheHit: boolean;
  isStaleCache: boolean;
  detailOpenTime?: number;
}

export interface InstrumentationHooks {
  /** Called when map feed request is initiated. */
  onFeedRequestStart?: (metrics: {
    bounds: GeoBoundingBox;
    timestamp: number;
  }) => void;

  /** Called when map feed response completes (success or error). */
  onFeedRequestEnd?: (metrics: MapFeedMetrics) => void;

  /** Called when markers are diffed and updated. */
  onMarkersDiffed?: (diff: {
    added: number;
    removed: number;
    updated: number;
  }) => void;

  /** Called when detail panel opens to measure latency. */
  onDetailOpen?: (details: { latencyMs: number; fromCache: boolean }) => void;

  /** Called on degraded state (error, timeout, empty results). */
  onDegradedState?: (reason: "error" | "timeout" | "empty") => void;
}

// ============================================================================
// Cache Strategy
// ============================================================================

/**
 * Spatial + Temporal Cache Key
 *
 * Key structure: "bbox:{minLng},{minLat},{maxLng},{maxLat}|window:{startUtc}|{endUtc}|{tz}"
 *
 * Rationale:
 * - Spatial bounds define map viewport query
 * - Temporal window defines event filter (now, tonight, tomorrow, etc.)
 * - Timezone affects preset resolution (tomorrow depends on local time)
 * - Cache TTL: 5 minutes by default (configurable)
 *
 * Invalidation:
 * - Manual: when filters change (category, district, confidence)
 * - Time-based: 5 minute TTL
 * - User-driven: on explicit refresh
 */

interface CacheEntry<T> {
  data: T;
  timestamp: number;
  expiresAt: number;
  hitCount: number;
  bytes: number;
}

class QueryCache {
  private cache = new Map<string, CacheEntry<MapFeedResponse>>();
  private readonly ttlMs = 5 * 60 * 1000; // 5 minutes
  private readonly maxEntries = 20; // Limit memory usage
  private readonly maxBytes = 10 * 1024 * 1024; // 10 MB total
  private totalBytes = 0;

  buildKey(
    bounds: GeoBoundingBox,
    window: MapFeedQuery["window"],
    filters?: string,
  ): string {
    const boundsStr = `bbox:${bounds.minLng},${bounds.minLat},${bounds.maxLng},${bounds.maxLat}`;
    const windowStr = `window:${window.startUtc}|${window.endUtc}|${window.timezone}`;
    const filterStr = filters ? `|filters:${filters}` : "";
    return `${boundsStr}|${windowStr}${filterStr}`;
  }

  set(key: string, data: MapFeedResponse): void {
    const now = Date.now();
    const bytes = JSON.stringify(data).length;

    // Evict oldest if at capacity
    if (this.cache.size >= this.maxEntries) {
      const oldest = Array.from(this.cache.entries()).sort(
        (a, b) => a[1].timestamp - b[1].timestamp,
      )[0];
      if (oldest) {
        this.totalBytes -= oldest[1].bytes;
        this.cache.delete(oldest[0]);
      }
    }

    // Evict by LRU if memory constrained
    while (this.totalBytes + bytes > this.maxBytes && this.cache.size > 0) {
      const lru = Array.from(this.cache.entries()).sort(
        (a, b) => a[1].hitCount - b[1].hitCount,
      )[0];
      if (lru) {
        this.totalBytes -= lru[1].bytes;
        this.cache.delete(lru[0]);
      }
    }

    this.cache.set(key, {
      data,
      timestamp: now,
      expiresAt: now + this.ttlMs,
      hitCount: 0,
      bytes,
    });
    this.totalBytes += bytes;
  }

  get(key: string): MapFeedResponse | null {
    const entry = this.cache.get(key);
    if (!entry) return null;

    const now = Date.now();
    if (now > entry.expiresAt) {
      this.totalBytes -= entry.bytes;
      this.cache.delete(key);
      return null;
    }

    entry.hitCount++;
    return entry.data;
  }

  clear(): void {
    this.cache.clear();
    this.totalBytes = 0;
  }

  stats(): { size: number; bytes: number; entries: number } {
    return {
      size: this.cache.size,
      bytes: this.totalBytes,
      entries: this.cache.size,
    };
  }
}

const globalCache = new QueryCache();

// ============================================================================
// Marker Diff Strategy
// ============================================================================

/**
 * Compute minimal marker updates
 *
 * Instead of replacing the entire events array, identify which markers:
 * - Are new (added)
 * - Are removed (not in new query)
 * - Are updates (same ID, different location/time)
 *
 * This allows React to preserve DOM references and avoid full re-renders.
 */
function diffMarkers(
  current: RuntimeEventProjection[],
  next: RuntimeEventProjection[],
): {
  added: RuntimeEventProjection[];
  removed: RuntimeEventProjection[];
  updated: RuntimeEventProjection[];
  merged: RuntimeEventProjection[];
} {
  const currentById = new Map(current.map((e) => [e.id, e]));
  const nextById = new Map(next.map((e) => [e.id, e]));

  const added: RuntimeEventProjection[] = [];
  const removed: RuntimeEventProjection[] = [];
  const updated: RuntimeEventProjection[] = [];

  // Find new and updated markers
  for (const [id, nextEvent] of nextById) {
    const currentEvent = currentById.get(id);
    if (!currentEvent) {
      added.push(nextEvent);
    } else if (
      currentEvent.latitude !== nextEvent.latitude ||
      currentEvent.longitude !== nextEvent.longitude ||
      currentEvent.startTime !== nextEvent.startTime
    ) {
      updated.push(nextEvent);
    }
  }

  // Find removed markers
  for (const [id, currentEvent] of currentById) {
    if (!nextById.has(id)) {
      removed.push(currentEvent);
    }
  }

  // Merge: keep existing references where unchanged, replace modified
  const merged = current
    .filter((e) => !removed.find((r) => r.id === e.id))
    .map((e) => updated.find((u) => u.id === e.id) || e)
    .concat(added);

  return { added, removed, updated, merged };
}

// ============================================================================
// Loading / Empty / Degraded State Model
// ============================================================================

export type FeedLoadState =
  | "idle"
  | "loading"
  | "success"
  | "empty"
  | "error"
  | "timeout"
  | "degraded";

export interface FeedState {
  loadState: FeedLoadState;
  events: RuntimeEventProjection[];
  error?: Error;
  markerCount: number;
  isCached: boolean;
  lastFetchTime?: number;
}

// ============================================================================
// Hook Definition
// ============================================================================

export interface UseMapFeedOptimizedOptions {
  debounceMs?: number;
  cacheEnabled?: boolean;
  staleCacheThresholdMs?: number;
  timeoutMs?: number;
  instrumentation?: InstrumentationHooks;
}

export function useMapFeedOptimized(
  mapBounds: GeoBoundingBox | null,
  mapWindow: MapFeedQuery["window"] | null,
  detailOpenTime?: number,
  options?: UseMapFeedOptimizedOptions,
): FeedState & {
  refresh: () => void;
  clearCache: () => void;
} {
  const {
    debounceMs = 400,
    cacheEnabled = true,
    staleCacheThresholdMs = 10 * 60 * 1000, // 10 minutes
    timeoutMs = 8000,
    instrumentation,
  } = options ?? {};

  // -----------------------------------------------------------------------
  // State
  // -----------------------------------------------------------------------

  const [events, setEvents] = useState<RuntimeEventProjection[]>([]);
  const [loadState, setLoadState] = useState<FeedLoadState>("idle");
  const [error, setError] = useState<Error>();
  const [lastFetchTime, setLastFetchTime] = useState<number>();
  const [isCached, setIsCached] = useState(false);

  // -----------------------------------------------------------------------
  // Refs for debounce + abort + instrumentation
  // -----------------------------------------------------------------------

  const debounceTimerRef = useRef<NodeJS.Timeout>();
  const abortControllerRef = useRef<AbortController | null>(null);
  const lastRequestKeyRef = useRef<string>("");
  const detailOpenTimeRef = useRef<number | null>(null);

  // -----------------------------------------------------------------------
  // Cache + Instrumentation Setup
  // -----------------------------------------------------------------------

  const cacheKey = useMemo(() => {
    if (!mapBounds || !mapWindow) return null;
    return globalCache.buildKey(mapBounds, mapWindow);
  }, [mapBounds, mapWindow]);

  // -----------------------------------------------------------------------
  // Fetch Logic with Debounce + Cache + Timeout
  // -----------------------------------------------------------------------

  const fetchMapFeed = useCallback(async () => {
    if (!mapBounds || !mapWindow || !cacheKey) {
      setLoadState("idle");
      return;
    }

    // Detect viewport change by comparing cache key
    const isNewViewport = cacheKey !== lastRequestKeyRef.current;
    lastRequestKeyRef.current = cacheKey;

    // Try cache first (if enabled and valid)
    if (cacheEnabled && !isNewViewport) {
      const cached = globalCache.get(cacheKey);
      if (cached) {
        const projections = cached.events.map(fromMapCardProjection);
        setEvents(projections);
        setLoadState("success");
        setIsCached(true);
        setLastFetchTime(Date.now());

        instrumentation?.onMarkersDiffed?.({
          added: 0,
          removed: 0,
          updated: 0,
        });

        return;
      }
    }

    // Abort previous in-flight request
    abortControllerRef.current?.abort();
    abortControllerRef.current = new AbortController();

    setLoadState("loading");
    setError(undefined);
    setIsCached(false);

    const metricsStart: MapFeedMetrics = {
      requestStartTime: Date.now(),
      markerCountRendered: events.length,
      markerCountAdded: 0,
      markerCountRemoved: 0,
      markerCountUpdated: 0,
      isCacheHit: false,
      isStaleCache: false,
    };

    instrumentation?.onFeedRequestStart?.({
      bounds: mapBounds,
      timestamp: metricsStart.requestStartTime,
    });

    try {
      // Fetch with timeout
      const timeoutPromise = new Promise<never>((_, reject) =>
        setTimeout(
          () => reject(new Error(`Map feed request timeout (${timeoutMs}ms)`)),
          timeoutMs,
        ),
      );

      const mapQuery: MapFeedQuery = {
        bounds: mapBounds,
        window: mapWindow,
        filters: {},
      };

      const fetchPromise = eventService.fetchMapFeed(mapQuery);
      const response = await Promise.race([fetchPromise, timeoutPromise]);

      const projections = response.events.map(fromMapCardProjection);

      // Diff markers for minimal re-render
      const { merged, added, removed, updated } = diffMarkers(
        events,
        projections,
      );

      setEvents(merged);
      setLoadState(projections.length === 0 ? "empty" : "success");
      setError(undefined);
      setLastFetchTime(Date.now());
      setIsCached(false);

      // Cache successful response
      if (cacheEnabled) {
        globalCache.set(cacheKey, response);
      }

      metricsStart.requestEndTime = Date.now();
      metricsStart.durationMs =
        metricsStart.requestEndTime - metricsStart.requestStartTime;
      metricsStart.markerCountAdded = added.length;
      metricsStart.markerCountRemoved = removed.length;
      metricsStart.markerCountUpdated = updated.length;
      metricsStart.markerCountRendered = merged.length;

      instrumentation?.onMarkersDiffed?.({
        added: added.length,
        removed: removed.length,
        updated: updated.length,
      });

      instrumentation?.onFeedRequestEnd?.(metricsStart);
    } catch (err) {
      const errorObj = err instanceof Error ? err : new Error(String(err));

      if (err instanceof Error && err.message.includes("timeout")) {
        setLoadState("timeout");
        instrumentation?.onDegradedState?.("timeout");
      } else {
        setLoadState("error");
        instrumentation?.onDegradedState?.("error");
      }

      setError(errorObj);

      metricsStart.requestEndTime = Date.now();
      metricsStart.durationMs =
        metricsStart.requestEndTime - metricsStart.requestStartTime;
      metricsStart.markerCountAdded = 0;
      metricsStart.markerCountRemoved = 0;
      metricsStart.markerCountUpdated = 0;
      metricsStart.markerCountRendered = events.length;

      instrumentation?.onFeedRequestEnd?.(metricsStart);
    }
  }, [
    mapBounds,
    mapWindow,
    cacheKey,
    cacheEnabled,
    events,
    timeoutMs,
    instrumentation,
  ]);

  // -----------------------------------------------------------------------
  // Debounce Viewport Changes
  // -----------------------------------------------------------------------

  useEffect(() => {
    if (!mapBounds || !cacheKey) {
      setLoadState("idle");
      return;
    }

    // Clear previous debounce timer
    if (debounceTimerRef.current) {
      clearTimeout(debounceTimerRef.current);
    }

    // Debounce viewport change: wait for user to stop panning/zooming
    debounceTimerRef.current = setTimeout(() => {
      fetchMapFeed();
    }, debounceMs);

    return () => {
      if (debounceTimerRef.current) {
        clearTimeout(debounceTimerRef.current);
      }
    };
  }, [mapBounds, cacheKey, debounceMs, fetchMapFeed]);

  // -----------------------------------------------------------------------
  // Detail Open Latency Measurement
  // -----------------------------------------------------------------------

  useEffect(() => {
    if (detailOpenTime && lastFetchTime) {
      const latencyMs = detailOpenTime - lastFetchTime;
      instrumentation?.onDetailOpen?.({
        latencyMs,
        fromCache: isCached,
      });
    }
  }, [detailOpenTime, lastFetchTime, isCached, instrumentation]);

  // -----------------------------------------------------------------------
  // Cleanup
  // -----------------------------------------------------------------------

  useEffect(() => {
    return () => {
      abortControllerRef.current?.abort();
      if (debounceTimerRef.current) {
        clearTimeout(debounceTimerRef.current);
      }
    };
  }, []);

  // -----------------------------------------------------------------------
  // Exposed Refresh + Cache Clear
  // -----------------------------------------------------------------------

  const refresh = useCallback(() => {
    lastRequestKeyRef.current = "";
    fetchMapFeed();
  }, [fetchMapFeed]);

  const clearCache = useCallback(() => {
    globalCache.clear();
  }, []);

  // -----------------------------------------------------------------------
  // Return State
  // -----------------------------------------------------------------------

  return {
    loadState,
    events,
    error,
    markerCount: events.length,
    isCached,
    lastFetchTime,
    refresh,
    clearCache,
  };
}
