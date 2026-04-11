/**
 * WeUP Phase 0 — Event Service (frontend API seam)
 *
 * This service sits between UI components and data. Currently it returns
 * mock data; the backend integration step (P09) replaces the internals with
 * real HTTP calls while the public API surface here stays the same.
 *
 * IMPORTANT: generateDynamicEvents() (random synthetic data) is removed.
 * The service must return deterministic results from the mock dataset only.
 */

import { NightlifeItem } from "@/types";
import { SEEDED_EVENTS } from "@/lib/testing/phase0Seed";
import {
  MapFeedQuery,
  CalendarFeedQuery,
  EventDetailQuery,
  MapFeedResponse,
  CalendarFeedResponse,
  EventDetailResponse,
  GeoBoundingBox,
} from "@/domains/query/contracts";
import {
  toMapCard,
  toCalendarProjection,
  toDetailProjection,
} from "@/domains/event/projections";
import { EventAggregate, EventStatus, SourceKind } from "@/domains/event/types";

// ---------------------------------------------------------------------------
// Legacy adapter — converts NightlifeItem to EventAggregate for projection use
// ---------------------------------------------------------------------------

/** Maps old NightlifeItem.source values to canonical SourceKind. */
function legacySourceToKind(src: string): SourceKind {
  switch (src) {
    case "manual":
      return "manual_submission";
    case "scraped":
      return "scraped_venue_page";
    case "api":
      return "external_feed";
    default:
      return "manual_submission";
  }
}

function nightlifeItemToAggregate(item: NightlifeItem): EventAggregate {
  return {
    id: item.id,
    status: (item.status as EventStatus) ?? "PUBLISHED",
    canonicalTitle: item.title,
    canonicalDescription: item.description ?? null,
    category: item.category as EventAggregate["category"],
    venue: { venueId: null, name: item.venue_name },
    address: {
      line1: item.address,
      city: "",
      country: "US",
      raw: item.address,
    },
    geo: { lat: item.latitude, lng: item.longitude },
    timeRange: { startUtc: item.start_time, endUtc: item.end_time ?? null },
    timezone: "America/Chicago",
    sourceRefs: [
      {
        kind: legacySourceToKind(item.source),
        ref: item.id,
        ingestedAt: item.start_time,
      },
    ],
    mediaRefs: item.image_url
      ? [{ assetId: item.id, url: item.image_url, kind: "image" }]
      : [],
    tags: item.tags ?? [],
    confidence: item.confidence ?? 0.9,
    review: {},
    audit: { createdAt: item.start_time, updatedAt: item.start_time },
  };
}

// ---------------------------------------------------------------------------
// EventService
// ---------------------------------------------------------------------------

class EventService {
  private readonly allEvents: NightlifeItem[] = [...SEEDED_EVENTS];

  // ------------------------------------------------------------------
  // Map feed — uses MapFeedQuery contract
  // ------------------------------------------------------------------

  async fetchMapFeed(query: MapFeedQuery): Promise<MapFeedResponse> {
    await this._simulateLatency();
    const { bounds, window, filters } = query;

    const filtered = this.allEvents
      .filter((e) => this._inBounds(e.latitude, e.longitude, bounds))
      .filter((e) =>
        this._inTimeWindow(e.start_time, window.startUtc, window.endUtc),
      )
      .filter(
        (e) =>
          !filters?.categories ||
          filters.categories.includes(e.category as any),
      )
      .filter((e) => (e.confidence ?? 0.9) >= (filters?.minConfidence ?? 0));

    const events = filtered.map((e) => toMapCard(nightlifeItemToAggregate(e)));
    return { events, totalCount: events.length };
  }

  // ------------------------------------------------------------------
  // Temporal feed seam — requests canonical time-window from backend
  // ------------------------------------------------------------------

  async fetchTemporalFeed(temporalQuery: any): Promise<MapFeedResponse> {
    try {
      const resp = await fetch("/api/temporal/window", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(temporalQuery),
      });

      if (!resp.ok) throw new Error("temporal endpoint error");
      const windowPayload = await resp.json();

      // windowPayload should be { startUtc, endUtc, timezone }
      const window = {
        startUtc: windowPayload.startUtc ?? windowPayload.StartUtc,
        endUtc: windowPayload.endUtc ?? windowPayload.EndUtc,
      } as any;

      // Forward to existing map feed contract using a small MapFeedQuery shim
      const query = {
        bounds: { minLat: -90, minLng: -180, maxLat: 90, maxLng: 180 },
        window,
        filters: {},
      };
      return this.fetchMapFeed(query as MapFeedQuery);
    } catch (e) {
      // Fallback to local behavior when backend is unreachable
      return this.fetchMapFeed({
        bounds: { minLat: -90, minLng: -180, maxLat: 90, maxLng: 180 },
        window: {
          startUtc: new Date().toISOString(),
          endUtc: new Date(Date.now() + 3600000).toISOString(),
        },
        filters: {},
      } as any);
    }
  }

  // ------------------------------------------------------------------
  // Calendar feed — uses CalendarFeedQuery contract
  // ------------------------------------------------------------------

  async fetchCalendarFeed(
    query: CalendarFeedQuery,
  ): Promise<CalendarFeedResponse> {
    await this._simulateLatency();
    const { window, filters, pagination } = query;
    const page = pagination?.page ?? 1;
    const pageSize = pagination?.pageSize ?? 50;

    const filtered = this.allEvents
      .filter((e) =>
        this._inTimeWindow(e.start_time, window.startUtc, window.endUtc),
      )
      .filter(
        (e) =>
          !filters?.categories ||
          filters.categories.includes(e.category as any),
      )
      .sort(
        (a, b) =>
          new Date(a.start_time).getTime() - new Date(b.start_time).getTime(),
      );

    const totalCount = filtered.length;
    const start = (page - 1) * pageSize;
    const items = filtered
      .slice(start, start + pageSize)
      .map((e) => toCalendarProjection(nightlifeItemToAggregate(e)));

    return {
      items,
      totalCount,
      page,
      pageSize,
      hasNextPage: start + pageSize < totalCount,
    };
  }

  // ------------------------------------------------------------------
  // Event detail
  // ------------------------------------------------------------------

  async fetchEventDetail(
    query: EventDetailQuery,
  ): Promise<EventDetailResponse> {
    await this._simulateLatency();
    const item = this.allEvents.find((e) => e.id === query.eventId);
    if (!item) return { event: null };
    return { event: toDetailProjection(nightlifeItemToAggregate(item)) };
  }

  // ------------------------------------------------------------------
  // Legacy compat — used by existing components during migration
  // ------------------------------------------------------------------

  /** @deprecated Use fetchMapFeed() with a MapFeedQuery instead. */
  async fetchEventsInBounds(bounds: GeoBoundingBox): Promise<NightlifeItem[]> {
    const filtered = this.allEvents.filter((e) =>
      this._inBounds(e.latitude, e.longitude, bounds),
    );
    return filtered;
  }

  // ------------------------------------------------------------------
  // Private helpers
  // ------------------------------------------------------------------

  private async _simulateLatency(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, 100));
  }

  private _inBounds(lat: number, lng: number, bounds: GeoBoundingBox): boolean {
    return (
      lat >= bounds.minLat &&
      lat <= bounds.maxLat &&
      lng >= bounds.minLng &&
      lng <= bounds.maxLng
    );
  }

  private _inTimeWindow(
    startTime: string,
    windowStart: string,
    windowEnd: string,
  ): boolean {
    const t = new Date(startTime).getTime();
    return (
      t >= new Date(windowStart).getTime() && t < new Date(windowEnd).getTime()
    );
  }
}

export const eventService = new EventService();

// Re-export contract types for convenience
export type {
  GeoBoundingBox,
  MapFeedQuery,
  CalendarFeedQuery,
  EventDetailQuery,
} from "@/domains/query/contracts";
