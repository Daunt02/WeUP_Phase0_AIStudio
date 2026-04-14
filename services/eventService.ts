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

import phase0Seed from "@/seed/phase0-dataset.json";
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

type SeedVenue = (typeof phase0Seed.venues)[number];
type SeedEvent = (typeof phase0Seed.events)[number];

const venueById = new Map<string, SeedVenue>(
  phase0Seed.venues.map((venue) => [venue.venueId, venue]),
);

function seedSourceToKind(src: string): SourceKind {
  switch (src) {
    case "manual_submission":
    case "flyer_upload":
    case "pasted_url":
    case "scraped_venue_page":
    case "external_feed":
      return src;
    default:
      return "manual_submission";
  }
}

function seedEventToAggregate(event: SeedEvent): EventAggregate {
  const venue = venueById.get(event.venueId);
  if (!venue) {
    throw new Error(
      `[eventService] Missing venue '${event.venueId}' for event '${event.eventId}'.`,
    );
  }

  return {
    id: event.eventId,
    status: (event.status as EventStatus) ?? "PUBLISHED",
    canonicalTitle: event.title,
    canonicalDescription: event.description ?? null,
    category: event.category as EventAggregate["category"],
    venue: { venueId: venue.venueId, name: venue.name },
    address: {
      line1: venue.address,
      city: venue.districtCode,
      country: "US",
      raw: venue.address,
    },
    geo: { lat: venue.latitude, lng: venue.longitude },
    timeRange: { startUtc: event.startsAtUtc, endUtc: event.endsAtUtc ?? null },
    timezone: "America/Los_Angeles",
    sourceRefs: [
      {
        kind: seedSourceToKind(event.sourceKind),
        ref: event.eventId,
        ingestedAt: event.startsAtUtc,
      },
    ],
    mediaRefs: event.imageUrl
      ? [{ assetId: event.eventId, url: event.imageUrl, kind: "image" }]
      : [],
    tags: event.tags ?? [],
    confidence: event.confidence ?? 0.9,
    review: {},
    audit: { createdAt: event.startsAtUtc, updatedAt: event.startsAtUtc },
  };
}

// ---------------------------------------------------------------------------
// EventService
// ---------------------------------------------------------------------------

class EventService {
  private readonly allEvents: EventAggregate[] =
    phase0Seed.events.map(seedEventToAggregate);

  // ------------------------------------------------------------------
  // Map feed — uses MapFeedQuery contract
  // ------------------------------------------------------------------

  async fetchMapFeed(query: MapFeedQuery): Promise<MapFeedResponse> {
    await this._simulateLatency();
    const { bounds, window, filters } = query;

    const filtered = this.allEvents
      .filter((e) => this._inBounds(e.geo.lat, e.geo.lng, bounds))
      .filter((e) =>
        this._inTimeWindow(
          e.timeRange.startUtc,
          window.startUtc,
          window.endUtc,
        ),
      )
      .filter(
        (e) => !filters?.categories || filters.categories.includes(e.category),
      )
      .filter((e) => e.confidence >= (filters?.minConfidence ?? 0));

    const events = filtered.map(toMapCard);
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
        this._inTimeWindow(
          e.timeRange.startUtc,
          window.startUtc,
          window.endUtc,
        ),
      )
      .filter(
        (e) => !filters?.categories || filters.categories.includes(e.category),
      )
      .sort(
        (a, b) =>
          new Date(a.timeRange.startUtc).getTime() -
          new Date(b.timeRange.startUtc).getTime(),
      );

    const totalCount = filtered.length;
    const start = (page - 1) * pageSize;
    const items = filtered
      .slice(start, start + pageSize)
      .map(toCalendarProjection);

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
    return { event: toDetailProjection(item) };
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
