"use client";
/**
 * useEventFeed
 *
 * Encapsulates bounds-driven event fetching. Re-fetches whenever mapBounds
 * changes (driven by the world-surface reducer). Returns a stable events
 * array and a loading flag.
 */

import { useEffect, useState } from "react";
import { eventService } from "@/services/eventService";
import type { GeoBoundingBox, MapFeedQuery } from "@/domains/query/contracts";
import {
  RuntimeEventProjection,
  fromMapCardProjection,
} from "@/features/world/runtimeTypes";

export interface EventFeedResult {
  events: RuntimeEventProjection[];
  loading: boolean;
  /** Imperative append used after a local publish succeeds. */
  prependEvent: (event: RuntimeEventProjection) => void;
}

export function useEventFeed(
  mapBounds: GeoBoundingBox | null,
): EventFeedResult {
  const [events, setEvents] = useState<RuntimeEventProjection[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!mapBounds) return;

    let cancelled = false;
    // Defer loading state update to avoid setting state synchronously in effect
    requestAnimationFrame(() => {
      if (!cancelled) setLoading(true);
    });

    const mapQuery: MapFeedQuery = {
      bounds: mapBounds,
      window: {
        startUtc: "1970-01-01T00:00:00.000Z",
        endUtc: "2100-01-01T00:00:00.000Z",
        timezone: "UTC",
      },
      filters: {},
    };

    eventService
      .fetchMapFeed(mapQuery)
      .then((projectionFeed) => {
        if (cancelled) return;
        setEvents(projectionFeed.events.map(fromMapCardProjection));
      })
      .catch((err) => {
        console.error("[useEventFeed] fetch error:", err);
      })
      .finally(() => {
        // Defer clearing loading state to avoid sync state updates in effect
        requestAnimationFrame(() => {
          if (!cancelled) setLoading(false);
        });
      });

    return () => {
      cancelled = true;
    };
  }, [mapBounds]);

  function prependEvent(event: RuntimeEventProjection) {
    setEvents((prev) => [event, ...prev]);
  }

  return { events, loading, prependEvent };
}
