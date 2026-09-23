"use client";

/**
 * MAP_FEED_INTEGRATION_EXAMPLE.tsx — Complete Integration Pattern
 *
 * This file demonstrates how to wire useMapFeedOptimized into your map component
 * with full instrumentation, error handling, and state transitions.
 *
 * Copy this pattern into your RadarMap or WorldCoordinator to enable
 * production-grade performance optimization.
 */

import { useCallback, useState } from "react";
import { useMapFeedOptimized } from "@/hooks/useMapFeedOptimized";
import { createMapPerformanceInstrumentation } from "@/services/mapPerformanceService";
import type { GeoBoundingBox, MapFeedQuery } from "@/domains/query/contracts";
import type { RuntimeEventProjection } from "@/features/world/runtimeTypes";

// ============================================================================
// Example: Complete Map Component Integration
// ============================================================================

interface MapContainerProps {
  selectedDate: string;
  timePreset: "now" | "tonight" | "tomorrow" | "thisWeekend";
  timezone: string;
}

interface MapViewport {
  bounds: GeoBoundingBox;
  zoom: number;
}

export function MapIntegrationExample({
  selectedDate,
  timePreset,
  timezone,
}: MapContainerProps) {
  // -----------------------------------------------------------------------
  // Viewport State (from map pan/zoom events)
  // -----------------------------------------------------------------------

  const [viewport, setViewport] = useState<MapViewport>({
    bounds: {
      minLng: -122.52,
      minLat: 37.7,
      maxLng: -122.37,
      maxLat: 37.85,
    },
    zoom: 12,
  });

  const [detailPanelOpenTime, setDetailPanelOpenTime] = useState<number>();
  const [selectedEventId, setSelectedEventId] = useState<string>();

  // -----------------------------------------------------------------------
  // Map Window (temporal filter)
  // -----------------------------------------------------------------------

  const mapWindow: MapFeedQuery["window"] = {
    /* eslint-disable react-hooks/purity -- example intentionally samples the current time on each render */
    startUtc: new Date().toISOString(), // TODO: Use preset resolution
    endUtc: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(), // Next 7 days
    /* eslint-enable react-hooks/purity */
    timezone,
  };

  // -----------------------------------------------------------------------
  // Optimized Feed Hook with Instrumentation
  // -----------------------------------------------------------------------

  const mapFeed = useMapFeedOptimized(
    viewport.bounds,
    mapWindow,
    detailPanelOpenTime,
    {
      debounceMs: 400, // Wait 400ms after viewport settle before fetching
      cacheEnabled: true,
      timeoutMs: 8000,
      instrumentation: createMapPerformanceInstrumentation(),
    },
  );

  // -----------------------------------------------------------------------
  // Event Handlers
  // -----------------------------------------------------------------------

  const handleViewportChange = useCallback((newViewport: MapViewport) => {
    setViewport(newViewport);
    // Feed will automatically debounce and fetch
  }, []);

  const handleEventSelect = useCallback((event: RuntimeEventProjection) => {
    setSelectedEventId(event.id);
    // Record detail open time for latency measurement
    setDetailPanelOpenTime(Date.now());
  }, []);

  const handleRefresh = useCallback(() => {
    mapFeed.refresh();
  }, [mapFeed]);

  const handleClearCache = useCallback(() => {
    mapFeed.clearCache();
  }, [mapFeed]);

  // -----------------------------------------------------------------------
  // Render
  // -----------------------------------------------------------------------

  return (
    <div className="flex flex-col gap-4">
      {/* Status Bar */}
      <div className="flex items-center justify-between px-4 py-2 bg-slate-800 rounded">
        <div className="text-sm text-gray-300">
          {mapFeed.loadState === "loading" && "Loading..."}
          {mapFeed.loadState === "success" && (
            <>
              {mapFeed.markerCount} markers
              {mapFeed.isCached && " (cached)"}
            </>
          )}
          {mapFeed.loadState === "empty" && "No events in this area"}
          {mapFeed.loadState === "error" && (
            <>Error: {mapFeed.error?.message}</>
          )}
          {mapFeed.loadState === "timeout" && "Request timed out"}
        </div>
        <div className="flex gap-2">
          <button
            onClick={handleRefresh}
            className="px-3 py-1 text-sm bg-blue-600 rounded hover:bg-blue-700"
          >
            Refresh
          </button>
          <button
            onClick={handleClearCache}
            className="px-3 py-1 text-sm bg-gray-600 rounded hover:bg-gray-700"
          >
            Clear Cache
          </button>
        </div>
      </div>

      {/* Map Container Placeholder */}
      <div className="relative w-full h-96 bg-slate-900 rounded border border-slate-700">
        {/* Replace with your Map component (react-map-gl, Mapbox, etc.) */}
        <div className="flex items-center justify-center h-full text-gray-500">
          Map surface would render {mapFeed.markerCount} markers here
        </div>

        {/* Event Detail Panel */}
        {selectedEventId &&
          mapFeed.events.find((e) => e.id === selectedEventId) && (
            <div className="absolute bottom-4 right-4 w-64 p-4 bg-white rounded shadow-lg">
              {(() => {
                const event = mapFeed.events.find(
                  (e) => e.id === selectedEventId,
                )!;
                return (
                  <>
                    <h3 className="font-bold text-lg">{event.title}</h3>
                    <p className="text-sm text-gray-600">{event.venueName}</p>
                    <button
                      onClick={() => setSelectedEventId(undefined)}
                      className="mt-3 px-3 py-1 text-sm bg-red-600 text-white rounded hover:bg-red-700"
                    >
                      Close
                    </button>
                  </>
                );
              })()}
            </div>
          )}
      </div>

      {/* Error State */}
      {mapFeed.loadState === "error" && (
        <div className="p-4 bg-red-100 border border-red-400 rounded text-red-700">
          <p className="font-bold">Map Feed Error</p>
          <p className="text-sm">{mapFeed.error?.message}</p>
          <button
            onClick={handleRefresh}
            className="mt-2 px-3 py-1 text-sm bg-red-600 text-white rounded hover:bg-red-700"
          >
            Retry
          </button>
        </div>
      )}

      {/* Empty State */}
      {mapFeed.loadState === "empty" && (
        <div className="p-4 bg-blue-100 border border-blue-400 rounded text-blue-700">
          <p className="font-bold">No Events Found</p>
          <p className="text-sm">Try adjusting your filters or viewport.</p>
        </div>
      )}
    </div>
  );
}

// ============================================================================
// Example: Usage in WorldCoordinator
// ============================================================================

/**
 * In your WorldCoordinator component:
 *
 * ```typescript
 * import { MapIntegrationExample } from './examples/MAP_FEED_INTEGRATION_EXAMPLE';
 *
 * export function WorldCoordinator() {
 *   return (
 *     <MapIntegrationExample
 *       selectedDate="MAR 25"
 *       timePreset="tomorrow"
 *       timezone="America/Chicago"
 *     />
 *   );
 * }
 * ```
 *
 * Replace the placeholder map container with your actual react-map-gl
 * or Mapbox GL integration.
 */
