import { analytics } from "../lib/analytics";
import type { AnalyticsEvent } from "../lib/types/analytics";

export function useAnalytics() {
  return {
    track: (ev: AnalyticsEvent) => analytics.track(ev),
  };
}
