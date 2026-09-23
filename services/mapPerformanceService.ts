"use client";

/**
 * mapPerformanceService — Instrumentation for Map Feed Performance Metrics
 *
 * Tracks:
 * - Map feed request duration (target: <1200ms)
 * - Marker render count progression
 * - Detail panel open latency (target: <300ms from cached query)
 * - Cache hit/miss ratio
 * - Error/timeout degradation
 *
 * Integration:
 * Pass these hooks to useMapFeedOptimized to collect production metrics.
 * Metrics are sent to analytics service (e.g., Application Insights, Segment, etc.)
 */

import { recordEventWithProperties } from "@/services/analyticsService";

export interface MapPerformanceEvent {
  timestamp: number;
  eventType: string;
  properties: Record<string, unknown>;
}

class MapPerformanceService {
  private eventBuffer: MapPerformanceEvent[] = [];
  private readonly flushIntervalMs = 30000; // Flush every 30s
  private flushTimer?: NodeJS.Timeout;

  constructor() {
    this.startFlushTimer();
  }

  // =======================================================================
  // Lifecycle Events
  // =======================================================================

  recordFeedRequestStart(bounds: {
    minLng: number;
    minLat: number;
    maxLng: number;
    maxLat: number;
  }): void {
    this.addEvent("map_feed_request_start", {
      bounds_min_lng: bounds.minLng,
      bounds_min_lat: bounds.minLat,
      bounds_max_lng: bounds.maxLng,
      bounds_max_lat: bounds.maxLat,
      bounds_area_degrees2:
        (bounds.maxLng - bounds.minLng) * (bounds.maxLat - bounds.minLat),
    });
  }

  recordFeedRequestEnd(metrics: {
    durationMs: number;
    markerCountRendered: number;
    markerCountAdded: number;
    markerCountRemoved: number;
    markerCountUpdated: number;
    isCacheHit: boolean;
    isStaleCache: boolean;
  }): void {
    const isSlowRequest = metrics.durationMs > 1200;
    const hasExcessiveMarkers = metrics.markerCountRendered > 500;

    this.addEvent("map_feed_request_end", {
      duration_ms: metrics.durationMs,
      marker_count_rendered: metrics.markerCountRendered,
      marker_count_added: metrics.markerCountAdded,
      marker_count_removed: metrics.markerCountRemoved,
      marker_count_updated: metrics.markerCountUpdated,
      is_cache_hit: metrics.isCacheHit,
      is_stale_cache: metrics.isStaleCache,
      is_slow_request: isSlowRequest,
      has_excessive_markers: hasExcessiveMarkers,
      severity: isSlowRequest || hasExcessiveMarkers ? "warning" : "info",
    });
  }

  recordMarkersDiffed(diff: {
    added: number;
    removed: number;
    updated: number;
  }): void {
    this.addEvent("markers_diff_calculated", {
      diff_added: diff.added,
      diff_removed: diff.removed,
      diff_updated: diff.updated,
      total_diff_operations: diff.added + diff.removed + diff.updated,
    });
  }

  recordDetailOpen(details: { latencyMs: number; fromCache: boolean }): void {
    const isSlowDetail = details.latencyMs > 300;

    this.addEvent("detail_panel_open", {
      latency_ms: details.latencyMs,
      from_cache: details.fromCache,
      is_slow_detail: isSlowDetail,
      severity: isSlowDetail ? "warning" : "info",
    });
  }

  recordDegradedState(reason: "error" | "timeout" | "empty"): void {
    this.addEvent("map_feed_degraded", {
      reason,
      severity: "warning",
    });
  }

  // =======================================================================
  // Cache Diagnostics
  // =======================================================================

  recordCacheStats(stats: {
    size: number;
    bytes: number;
    entries: number;
  }): void {
    const cacheUtilization = (stats.bytes / (10 * 1024 * 1024)) * 100;

    this.addEvent("cache_stats", {
      cache_size: stats.size,
      cache_bytes: stats.bytes,
      cache_entries: stats.entries,
      cache_utilization_percent: parseInt(cacheUtilization.toString()),
    });
  }

  // =======================================================================
  // Error Tracking
  // =======================================================================

  recordError(error: Error, context: string): void {
    this.addEvent("map_feed_error", {
      error_message: error.message,
      error_stack: error.stack,
      context,
      severity: "error",
    });
  }

  // =======================================================================
  // Internal Event Buffer
  // =======================================================================

  private addEvent(
    eventType: string,
    properties: Record<string, unknown>,
  ): void {
    const event: MapPerformanceEvent = {
      timestamp: Date.now(),
      eventType,
      properties,
    };

    this.eventBuffer.push(event);

    // Flush if buffer gets large
    if (this.eventBuffer.length > 50) {
      this.flush();
    }
  }

  private startFlushTimer(): void {
    if (this.flushTimer) {
      clearInterval(this.flushTimer);
    }

    this.flushTimer = setInterval(() => {
      this.flush();
    }, this.flushIntervalMs);
  }

  private flush(): void {
    if (this.eventBuffer.length === 0) {
      return;
    }

    const batch = this.eventBuffer.splice(0, this.eventBuffer.length);

    // Send batch to analytics (in production, this would go to Application Insights, Segment, etc.)
    batch.forEach((event) => {
      recordEventWithProperties(event.eventType, event.properties);
    });
  }

  destroy(): void {
    if (this.flushTimer) {
      clearInterval(this.flushTimer);
    }
    this.flush();
  }
}

export const mapPerformanceService = new MapPerformanceService();

// =============================================================================
// Integration Helper: Convert useMapFeedOptimized Options to Instrumentation
// =============================================================================

export function createMapPerformanceInstrumentation() {
  return {
    onFeedRequestStart: (metrics: { bounds: unknown; timestamp: number }) => {
      mapPerformanceService.recordFeedRequestStart(metrics.bounds as any);
    },

    onFeedRequestEnd: (metrics: {
      durationMs?: number;
      markerCountRendered: number;
      markerCountAdded: number;
      markerCountRemoved: number;
      markerCountUpdated: number;
      isCacheHit: boolean;
      isStaleCache: boolean;
    }) => {
      mapPerformanceService.recordFeedRequestEnd({
        durationMs: metrics.durationMs ?? 0,
        markerCountRendered: metrics.markerCountRendered,
        markerCountAdded: metrics.markerCountAdded,
        markerCountRemoved: metrics.markerCountRemoved,
        markerCountUpdated: metrics.markerCountUpdated,
        isCacheHit: metrics.isCacheHit,
        isStaleCache: metrics.isStaleCache,
      });
    },

    onMarkersDiffed: (diff: {
      added: number;
      removed: number;
      updated: number;
    }) => {
      mapPerformanceService.recordMarkersDiffed(diff);
    },

    onDetailOpen: (details: { latencyMs: number; fromCache: boolean }) => {
      mapPerformanceService.recordDetailOpen(details);
    },

    onDegradedState: (reason: "error" | "timeout" | "empty") => {
      mapPerformanceService.recordDegradedState(reason);
    },
  };
}
