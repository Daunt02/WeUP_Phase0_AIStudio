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

export interface TemporalQueryResult {
  temporalWindow: TemporalQueryResponse | null;
  loading: boolean;
}

export function useTemporalQuery(preset: string): TemporalQueryResult {
  const [temporalWindow, setTemporalWindow] =
    useState<TemporalQueryResponse | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const controller = new AbortController();

    setLoading(true);

    temporalService
      .getEventsAtTime(
        { preset, marketTimezone: "America/Los_Angeles" },
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
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [preset]);

  return { temporalWindow, loading };
}
