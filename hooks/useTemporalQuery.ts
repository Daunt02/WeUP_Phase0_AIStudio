"use client";
/**
 * useTemporalQuery
 *
 * Encapsulates temporal-preset → time-window resolution. Re-queries whenever
 * the preset string changes (driven by TimelineControl → coordinator).
 *
 * Seam: the backend temporal endpoint (/api/temporal/events-at-time) is called
 * by temporalService; replace the service internals in P09 without touching
 * this hook.
 */

import { useEffect, useState } from "react";
import * as temporalService from "@/services/temporalService";
import * as analyticsService from "@/services/analyticsService";
import type { TemporalQueryResponse } from "@/services/temporalService";
import type { TemporalPresetSelection } from "@/features/world/runtimeTypes";

export interface TemporalQueryResult {
  temporalWindow: TemporalQueryResponse | null;
  loading: boolean;
}

export function useTemporalQuery(
  preset: TemporalPresetSelection,
): TemporalQueryResult {
  const [temporalWindow, setTemporalWindow] =
    useState<TemporalQueryResponse | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const controller = new AbortController();

    // Defer loading state update to avoid setting state synchronously in effect
    requestAnimationFrame(() => {
      if (!cancelled) setLoading(true);
    });

    temporalService
      .getEventsAtTime(
        { preset, marketTimezone: "America/Chicago" },
        controller.signal,
      )
      .then((response) => {
        if (!cancelled) {
          setTemporalWindow(response);
          // Fire-and-forget — analytics failures must not block UI
          analyticsService
            .recordSimpleEvent(
              analyticsService.AnalyticsEventType.TemporalPresetSelected,
            )
            .catch(() => {});
        }
      })
      .catch((err) => {
        if (!cancelled) console.error("[useTemporalQuery] query failed:", err);
      })
      .finally(() => {
        // Defer clearing loading state to avoid sync state updates in effect
        requestAnimationFrame(() => {
          if (!cancelled) setLoading(false);
        });
      });

    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [preset]);

  return { temporalWindow, loading };
}
