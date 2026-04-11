import { AnalyticsEvent } from "./types/analytics";

const DEFAULT_API = "/api/analytics";

export class AnalyticsService {
  private endpoint: string;

  constructor(endpoint?: string) {
    this.endpoint = endpoint ?? DEFAULT_API;
  }

  async track(event: AnalyticsEvent): Promise<void> {
    try {
      // Enforce privacy: strip any obviously sensitive props
      const safe = this.minimize(event);
      await fetch(this.endpoint, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(safe),
      });
    } catch (err) {
      // Best-effort; do not throw from UI on tracking failure
      console.warn("Analytics track failed", err);
    }
  }

  private minimize(event: AnalyticsEvent): AnalyticsEvent {
    const allowedProps: Record<string, string | number | boolean> = {};
    if (event.props) {
      for (const [k, v] of Object.entries(event.props)) {
        // drop keys that look like PII
        if (/email|phone|name|address|ssn|dob/i.test(k)) continue;
        allowedProps[k] = v;
      }
    }

    return {
      name: event.name,
      props: allowedProps,
      userIdHash: event.userIdHash,
      timestamp: event.timestamp ?? new Date().toISOString(),
    };
  }
}

export const analytics = new AnalyticsService();
