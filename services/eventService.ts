/**
 * WeUP Phase 0 — Event Service (frontend API adapter)
 *
 * Thin adapter between UI contracts and backend event endpoints.
 * Production behavior is API-backed with no local mock fallback.
 */

import {
  MapFeedQuery,
  CalendarFeedQuery,
  EventDetailQuery,
  EventFilters,
  MapFeedResponse,
  CalendarFeedResponse,
  EventDetailResponse,
} from "@/domains/query/contracts";
import { EventCalendarProjection, EventDetailProjection, EventMapCardProjection } from "@/domains/event/projections";
import { toApiUrl } from "@/services/apiBase";

// ---------------------------------------------------------------------------
// EventService
// ---------------------------------------------------------------------------

interface MapFeedApiRequest {
  bounds: MapFeedQuery["bounds"];
  window: MapFeedQuery["window"];
  categories?: EventFilters["categories"];
  districtCode?: string;
  minConfidence: number;
  sort: string;
}

interface CalendarFeedApiRequest {
  window: CalendarFeedQuery["window"];
  categories?: EventFilters["categories"];
  districtCode?: string;
  minConfidence: number;
  sort: string;
  page: number;
  pageSize: number;
}

interface EventDetailApiResponse {
  event?: EventDetailProjection | null;
  Event?: EventDetailProjection | null;
}

function normalizeMapCard(item: Partial<EventMapCardProjection>): EventMapCardProjection {
  return {
    id: item.id ?? "",
    title: item.title ?? "",
    venueName: item.venueName ?? "",
    category: (item.category ?? "other") as EventMapCardProjection["category"],
    lat: item.lat ?? 0,
    lng: item.lng ?? 0,
    thumbnailUrl: item.thumbnailUrl ?? null,
    status: (item.status ?? "PUBLISHED") as EventMapCardProjection["status"],
    confidence: item.confidence ?? 0,
  };
}

function normalizeCalendarItem(
  item: Partial<EventCalendarProjection>,
): EventCalendarProjection {
  return {
    id: item.id ?? "",
    title: item.title ?? "",
    venueName: item.venueName ?? "",
    category: (item.category ?? "other") as EventCalendarProjection["category"],
    startUtc: item.startUtc ?? new Date(0).toISOString(),
    endUtc: item.endUtc ?? null,
    timezone: item.timezone ?? "UTC",
    thumbnailUrl: item.thumbnailUrl ?? null,
    status: (item.status ?? "PUBLISHED") as EventCalendarProjection["status"],
  };
}

async function fetchJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(toApiUrl(path), {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers ?? {}),
    },
  });

  if (!response.ok) {
    let detail = "";
    try {
      detail = await response.text();
    } catch {
      detail = "";
    }

    throw new Error(
      `[eventService] ${response.status} ${response.statusText}${detail ? ` - ${detail}` : ""}`,
    );
  }

  return (await response.json()) as T;
}

function toMapFeedApiRequest(query: MapFeedQuery): MapFeedApiRequest {
  return {
    bounds: query.bounds,
    window: query.window,
    categories: query.filters?.categories,
    districtCode: query.filters?.districtCode,
    minConfidence: query.filters?.minConfidence ?? 0,
    sort: query.sort ?? "start_time_asc",
  };
}

function toCalendarFeedApiRequest(query: CalendarFeedQuery): CalendarFeedApiRequest {
  return {
    window: query.window,
    categories: query.filters?.categories,
    districtCode: query.filters?.districtCode,
    minConfidence: query.filters?.minConfidence ?? 0,
    sort: query.sort ?? "start_time_asc",
    page: query.pagination?.page ?? 1,
    pageSize: query.pagination?.pageSize ?? 50,
  };
}

class EventService {

  // ------------------------------------------------------------------
  // Map feed — uses MapFeedQuery contract
  // ------------------------------------------------------------------

  async fetchMapFeed(query: MapFeedQuery): Promise<MapFeedResponse> {
    const payload = await fetchJson<{
      events?: EventMapCardProjection[];
      Events?: EventMapCardProjection[];
      totalCount?: number;
      TotalCount?: number;
    }>("/api/events/map", {
      method: "POST",
      body: JSON.stringify(toMapFeedApiRequest(query)),
    });

    const items = payload.events ?? payload.Events ?? [];
    return {
      events: items.map((item) => normalizeMapCard(item)),
      totalCount: payload.totalCount ?? payload.TotalCount ?? items.length,
    };
  }

  // ------------------------------------------------------------------
  // Calendar feed — uses CalendarFeedQuery contract
  // ------------------------------------------------------------------

  async fetchCalendarFeed(
    query: CalendarFeedQuery,
  ): Promise<CalendarFeedResponse> {
    const payload = await fetchJson<{
      items?: EventCalendarProjection[];
      Items?: EventCalendarProjection[];
      totalCount?: number;
      TotalCount?: number;
      page?: number;
      Page?: number;
      pageSize?: number;
      PageSize?: number;
      hasNextPage?: boolean;
      HasNextPage?: boolean;
    }>("/api/events/calendar", {
      method: "POST",
      body: JSON.stringify(toCalendarFeedApiRequest(query)),
    });

    const items = payload.items ?? payload.Items ?? [];
    const page = payload.page ?? payload.Page ?? query.pagination?.page ?? 1;
    const pageSize =
      payload.pageSize ?? payload.PageSize ?? query.pagination?.pageSize ?? 50;
    const totalCount = payload.totalCount ?? payload.TotalCount ?? items.length;

    return {
      items: items.map((item) => normalizeCalendarItem(item)),
      totalCount,
      page,
      pageSize,
      hasNextPage: payload.hasNextPage ?? payload.HasNextPage ?? page * pageSize < totalCount,
    };
  }

  // ------------------------------------------------------------------
  // Event detail
  // ------------------------------------------------------------------

  async fetchEventDetail(
    query: EventDetailQuery,
  ): Promise<EventDetailResponse> {
    const payload = await fetchJson<EventDetailApiResponse>(
      `/api/events/${encodeURIComponent(query.eventId)}`,
      { method: "GET" },
    );

    return {
      event: payload.event ?? payload.Event ?? null,
    };
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
