/**
 * WeUP Phase 0 — Temporal and Geospatial Query Contracts
 *
 * Canonical request/response contracts for:
 *  - Map viewport feed
 *  - Calendar / date-window feed
 *  - Event detail lookup
 *  - Saved-event query seam
 *
 * The frontend uses these types now. The future .NET backend implements
 * endpoints that accept / return these exact shapes.
 */

import { EventCategory } from "@/domains/event/types";
import {
  EventMapCardProjection,
  EventCalendarProjection,
  EventDetailProjection,
} from "@/domains/event/projections";

// ---------------------------------------------------------------------------
// Geospatial primitives
// ---------------------------------------------------------------------------

export interface GeoBoundingBox {
  minLat: number;
  maxLat: number;
  minLng: number;
  maxLng: number;
}

/** Validation result for a bounding box. */
export interface BoundingBoxValidation {
  valid: boolean;
  errors: string[];
}

export function validateBoundingBox(bb: GeoBoundingBox): BoundingBoxValidation {
  const errors: string[] = [];
  if (bb.minLat >= bb.maxLat) errors.push("minLat must be less than maxLat");
  if (bb.minLng >= bb.maxLng) errors.push("minLng must be less than maxLng");
  if (bb.minLat < -90 || bb.maxLat > 90)
    errors.push("Latitude must be in range [-90, 90]");
  if (bb.minLng < -180 || bb.maxLng > 180)
    errors.push("Longitude must be in range [-180, 180]");
  const latSpan = bb.maxLat - bb.minLat;
  const lngSpan = bb.maxLng - bb.minLng;
  if (latSpan > 5 || lngSpan > 5)
    errors.push(
      "Bounding box span exceeds 5 degrees — overly broad query not allowed",
    );
  if (latSpan * lngSpan > 8)
    errors.push(
      "Bounding box area exceeds 8 square degrees — overly broad query not allowed",
    );
  return { valid: errors.length === 0, errors };
}

export function isValidBoundingBox(bb: GeoBoundingBox): boolean {
  return validateBoundingBox(bb).valid;
}

// ---------------------------------------------------------------------------
// Temporal primitives
// ---------------------------------------------------------------------------

/**
 * A resolved time window expressed in UTC.
 * All temporal presets expand into a TimeWindow before querying.
 */
export interface TimeWindow {
  /** Inclusive lower bound, ISO 8601 UTC. */
  startUtc: string;
  /** Exclusive upper bound, ISO 8601 UTC. */
  endUtc: string;
  /** IANA timezone used to derive the window. */
  timezone: string;
}

/**
 * Authoritative backend-resolved temporal window.
 *
 * Unlike TimeWindow, this shape carries preset-resolution provenance so
 * map/calendar/detail/filter surfaces can stay aligned without frontend
 * reinterpretation of temporal semantics.
 */
export interface BackendResolvedTimeWindow extends TimeWindow {
  /** Backend preset identity that produced the UTC bounds. */
  sourcePreset: string;
  /** Human-readable label returned by backend (e.g. "Tonight"). */
  presetLabel: string;
  /** UTC instant used by backend as reference "now" for expansion. */
  resolutionInstantUtc: string;
}

/** Named temporal presets — mapped to concrete TimeWindows at query time. */
export type TemporalPreset =
  | "TODAY" // local market day 00:00 -> 00:00 next day
  | "TONIGHT" // local nightlife window 18:00 -> 03:00 next day
  | "WEEKEND" // Friday 18:00 -> Monday 00:00 local
  | "NEXT_7_DAYS" // rolling 7-day window
  | "CUSTOM"; // caller supplies TimeWindow directly

/**
 * Resolve a TemporalPreset to a concrete UTC TimeWindow.
 *
 * @deprecated Backend preset expansion is authoritative. Use backend-resolved
 * windows whenever possible and pass those through buildMapFeedQueryFromResolvedWindow
 * or buildCalendarFeedQueryFromResolvedWindow.
 * @param preset - named preset
 * @param timezone - IANA timezone for the launch market (e.g. "America/Chicago")
 * @param now - current time as ISO string; defaults to Date.now()
 * @param customWindow - required when preset === 'CUSTOM'
 */
export function resolveTemporalPreset(
  preset: TemporalPreset,
  timezone: string,
  now: string = new Date().toISOString(),
  customWindow?: TimeWindow,
): TimeWindow {
  if (preset === "CUSTOM") {
    if (!customWindow)
      throw new Error("customWindow is required for CUSTOM preset");
    return normalizeTimeWindow(customWindow);
  }

  const nowDate = new Date(now);
  if (Number.isNaN(nowDate.getTime())) {
    throw new Error("Invalid now timestamp");
  }

  const nowParts = getZonedDateParts(nowDate, timezone);

  function localDateShifted(dayOffset: number): {
    year: number;
    month: number;
    day: number;
  } {
    const shifted = new Date(
      Date.UTC(nowParts.year, nowParts.month - 1, nowParts.day + dayOffset),
    );
    return {
      year: shifted.getUTCFullYear(),
      month: shifted.getUTCMonth() + 1,
      day: shifted.getUTCDate(),
    };
  }

  function localBoundaryIso(dayOffset: number, hour: number): string {
    const shifted = localDateShifted(dayOffset);
    return zonedLocalTimeToUtcIso(
      {
        year: shifted.year,
        month: shifted.month,
        day: shifted.day,
        hour,
        minute: 0,
        second: 0,
      },
      timezone,
    );
  }

  function dayOfWeekAtOffset(dayOffset: number): number {
    const shifted = localDateShifted(dayOffset);
    return new Date(
      Date.UTC(shifted.year, shifted.month - 1, shifted.day),
    ).getUTCDay();
  }

  switch (preset) {
    case "TODAY":
      return {
        startUtc: localBoundaryIso(0, 0),
        endUtc: localBoundaryIso(1, 0),
        timezone,
      };
    case "TONIGHT":
      return {
        startUtc: localBoundaryIso(0, 18),
        endUtc: localBoundaryIso(1, 3),
        timezone,
      };
    case "WEEKEND": {
      // Weekend canonical window: Friday 18:00 through Monday 00:00 local.
      const localToday = dayOfWeekAtOffset(0);
      const friday = 5;
      const daysToFriday = (friday - localToday + 7) % 7;
      return {
        startUtc: localBoundaryIso(daysToFriday, 18),
        endUtc: localBoundaryIso(daysToFriday + 3, 0),
        timezone,
      };
    }
    case "NEXT_7_DAYS":
      return {
        startUtc: nowDate.toISOString(),
        endUtc: new Date(nowDate.getTime() + 7 * 86_400_000).toISOString(),
        timezone,
      };
  }
}

function getZonedDateParts(
  date: Date,
  timezone: string,
): {
  year: number;
  month: number;
  day: number;
  hour: number;
  minute: number;
  second: number;
} {
  const formatter = new Intl.DateTimeFormat("en-US", {
    timeZone: timezone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false,
  });
  const parts = formatter.formatToParts(date);
  const pick = (type: string): number => {
    const value = parts.find((part) => part.type === type)?.value;
    if (!value) {
      throw new Error(`Unable to resolve ${type} in timezone ${timezone}`);
    }
    return Number.parseInt(value, 10);
  };

  return {
    year: pick("year"),
    month: pick("month"),
    day: pick("day"),
    hour: pick("hour"),
    minute: pick("minute"),
    second: pick("second"),
  };
}

function zonedLocalTimeToUtcIso(
  local: {
    year: number;
    month: number;
    day: number;
    hour: number;
    minute: number;
    second: number;
  },
  timezone: string,
): string {
  const targetUtcMs = Date.UTC(
    local.year,
    local.month - 1,
    local.day,
    local.hour,
    local.minute,
    local.second,
  );
  let guessUtcMs = targetUtcMs;

  // Iteratively converge on the UTC instant that renders as the target local time in this timezone.
  for (let i = 0; i < 4; i += 1) {
    const rendered = getZonedDateParts(new Date(guessUtcMs), timezone);
    const renderedUtcMs = Date.UTC(
      rendered.year,
      rendered.month - 1,
      rendered.day,
      rendered.hour,
      rendered.minute,
      rendered.second,
    );
    guessUtcMs += targetUtcMs - renderedUtcMs;
  }

  return new Date(guessUtcMs).toISOString();
}

export function normalizeTimeWindow(window: TimeWindow): TimeWindow {
  const start = new Date(window.startUtc);
  const end = new Date(window.endUtc);
  if (isNaN(start.getTime())) throw new Error("Invalid startUtc");
  if (isNaN(end.getTime())) throw new Error("Invalid endUtc");
  if (start >= end) throw new Error("startUtc must be before endUtc");
  return {
    startUtc: start.toISOString(),
    endUtc: end.toISOString(),
    timezone: window.timezone,
  };
}

// ---------------------------------------------------------------------------
// Sort options
// ---------------------------------------------------------------------------

export type EventSortOption =
  | "start_time_asc"
  | "start_time_desc"
  | "proximity"
  | "freshness"
  | "confidence"
  | "featured"; // placeholder seam for sponsored/editorial

// ---------------------------------------------------------------------------
// Shared filter options
// ---------------------------------------------------------------------------

export interface EventFilters {
  categories?: EventCategory[];
  /** District / neighborhood code. */
  districtCode?: string;
  /** Canonical locality filter dimensions. */
  locality?: LocalityFilter;
  /** Minimum confidence threshold (0–1). */
  minConfidence?: number;
  /** Only return events from these source kinds. */
  sourceKinds?: string[];
}

export interface LocalityFilter {
  marketCode?: string;
  districtCode?: string;
  neighborhoodCode?: string;
}

// ---------------------------------------------------------------------------
// Pagination
// ---------------------------------------------------------------------------

export interface PaginationParams {
  page: number; // 1-based
  pageSize: number;
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  hasNextPage: boolean;
}

// ---------------------------------------------------------------------------
// Map feed
// ---------------------------------------------------------------------------

export interface MapFeedQuery {
  bounds: GeoBoundingBox;
  window: TimeWindow;
  filters?: EventFilters;
  sort?: EventSortOption;
}

/**
 * Canonical map query shape when using backend-resolved temporal windows.
 */
export interface MapFeedResolvedWindowQuery extends Omit<
  MapFeedQuery,
  "window"
> {
  window: BackendResolvedTimeWindow;
}

export interface MapFeedResponse {
  events: EventMapCardProjection[];
  totalCount: number;
  clusters?: EventMapClusterProjection[];
  queryMode?: "bounding_box" | "cluster_aggregation";
}

export interface EventMapClusterProjection {
  clusterId: string;
  centerLat: number;
  centerLng: number;
  count: number;
  eventIds: string[];
}

export function buildMapFeedQuery(
  bounds: GeoBoundingBox,
  preset: TemporalPreset,
  timezone: string,
  filters?: EventFilters,
): MapFeedQuery {
  if (!isValidBoundingBox(bounds)) {
    const { errors } = validateBoundingBox(bounds);
    throw new Error(`Invalid bounding box: ${errors.join("; ")}`);
  }
  return {
    bounds,
    window: resolveTemporalPreset(preset, timezone),
    filters,
    sort: "start_time_asc",
  };
}

export function buildMapFeedQueryFromResolvedWindow(
  bounds: GeoBoundingBox,
  window: BackendResolvedTimeWindow,
  filters?: EventFilters,
): MapFeedResolvedWindowQuery {
  if (!isValidBoundingBox(bounds)) {
    const { errors } = validateBoundingBox(bounds);
    throw new Error(`Invalid bounding box: ${errors.join("; ")}`);
  }

  return {
    bounds,
    window: {
      ...normalizeTimeWindow(window),
      sourcePreset: window.sourcePreset,
      presetLabel: window.presetLabel,
      resolutionInstantUtc: new Date(window.resolutionInstantUtc).toISOString(),
    },
    filters,
    sort: "start_time_asc",
  };
}

// ---------------------------------------------------------------------------
// Calendar feed
// ---------------------------------------------------------------------------

export interface CalendarFeedQuery {
  window: TimeWindow;
  filters?: EventFilters;
  sort?: EventSortOption;
  pagination?: PaginationParams;
}

/**
 * Canonical calendar query shape when using backend-resolved temporal windows.
 */
export interface CalendarFeedResolvedWindowQuery extends Omit<
  CalendarFeedQuery,
  "window"
> {
  window: BackendResolvedTimeWindow;
}

export interface CalendarFeedResponse extends PaginatedResponse<EventCalendarProjection> {}

export function buildCalendarFeedQuery(
  preset: TemporalPreset,
  timezone: string,
  filters?: EventFilters,
  pagination?: PaginationParams,
): CalendarFeedQuery {
  return {
    window: resolveTemporalPreset(preset, timezone),
    filters,
    sort: "start_time_asc",
    pagination: pagination ?? { page: 1, pageSize: 50 },
  };
}

export function buildCalendarFeedQueryFromResolvedWindow(
  window: BackendResolvedTimeWindow,
  filters?: EventFilters,
  pagination?: PaginationParams,
): CalendarFeedResolvedWindowQuery {
  return {
    window: {
      ...normalizeTimeWindow(window),
      sourcePreset: window.sourcePreset,
      presetLabel: window.presetLabel,
      resolutionInstantUtc: new Date(window.resolutionInstantUtc).toISOString(),
    },
    filters,
    sort: "start_time_asc",
    pagination: pagination ?? { page: 1, pageSize: 50 },
  };
}

// ---------------------------------------------------------------------------
// Event detail
// ---------------------------------------------------------------------------

export interface EventDetailQuery {
  eventId: string;
}

export interface EventDetailResponse {
  event: EventDetailProjection | null;
}

// ---------------------------------------------------------------------------
// Saved events
// ---------------------------------------------------------------------------

export interface SavedEventQuery {
  userId: string;
  pagination?: PaginationParams;
}

export interface SavedEventResponse extends PaginatedResponse<EventCalendarProjection> {}
