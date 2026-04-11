"use client";
/**
 * useEventFeed
 *
 * Encapsulates bounds-driven event fetching. Re-fetches whenever mapBounds
 * changes (driven by the world-surface reducer). Returns a stable events
 * array and a loading flag.
 *
 * Seam: replace eventService.fetchEventsInBounds with a real API call in P09.
 */

import { useEffect, useState } from "react";
import { NightlifeItem } from "@/types";
import { eventService } from "@/services/eventService";
import type { GeoBoundingBox } from "@/domains/query/contracts";

export interface EventFeedResult {
  events: NightlifeItem[];
  loading: boolean;
  /** Imperative append used after a local publish succeeds. */
  prependEvent: (event: NightlifeItem) => void;
}

export function useEventFeed(
  mapBounds: GeoBoundingBox | null,
): EventFeedResult {
  const [events, setEvents] = useState<NightlifeItem[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!mapBounds) return;

    let cancelled = false;
    // Defer loading state update to avoid setting state synchronously in effect
    requestAnimationFrame(() => {
      if (!cancelled) setLoading(true);
    });

    eventService
      .fetchEventsInBounds(mapBounds)
      .then((result) => {
        if (!cancelled) setEvents(result);
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

  function prependEvent(event: NightlifeItem) {
    setEvents((prev) => [event, ...prev]);
  }

  return { events, loading, prependEvent };
}
