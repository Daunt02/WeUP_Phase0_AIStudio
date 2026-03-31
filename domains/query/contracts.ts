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

import { EventCategory } from '@/domains/event/types';
import {
  EventMapCardProjection,
  EventCalendarProjection,
  EventDetailProjection,
} from '@/domains/event/projections';

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
  if (bb.minLat >= bb.maxLat) errors.push('minLat must be less than maxLat');
  if (bb.minLng >= bb.maxLng) errors.push('minLng must be less than maxLng');
  if (bb.minLat < -90 || bb.maxLat > 90) errors.push('Latitude must be in range [-90, 90]');
  if (bb.minLng < -180 || bb.maxLng > 180) errors.push('Longitude must be in range [-180, 180]');
  const latSpan = bb.maxLat - bb.minLat;
  const lngSpan = bb.maxLng - bb.minLng;
  if (latSpan > 10 || lngSpan > 10) errors.push('Bounding box span exceeds 10 degrees — overly broad query not allowed');
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

/** Named temporal presets — mapped to concrete TimeWindows at query time. */
export type TemporalPreset =
  | 'NOW'            // next 2 hours from now
  | 'TONIGHT'        // 6 PM – 3 AM local
  | 'TOMORROW'       // midnight to midnight next local day
  | 'THIS_WEEKEND'   // Friday 6 PM – Sunday midnight local
  | 'NEXT_7_DAYS'    // rolling 7-day window
  | 'CUSTOM';        // caller supplies TimeWindow directly

/**
 * Resolve a TemporalPreset to a concrete UTC TimeWindow.
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
  if (preset === 'CUSTOM') {
    if (!customWindow) throw new Error('customWindow is required for CUSTOM preset');
    return customWindow;
  }

  const nowMs = new Date(now).getTime();

  // Helper — local midnight in UTC for a given day offset
  function localMidnightUtc(dayOffset: number): Date {
    // Use Intl to find local midnight
    const d = new Date(nowMs + dayOffset * 86_400_000);
    const localDateStr = d.toLocaleDateString('en-CA', { timeZone: timezone }); // YYYY-MM-DD
    return new Date(`${localDateStr}T00:00:00`);
  }

  // Helper — local hour in UTC
  function localHourUtc(dayOffset: number, hour: number): string {
    const midnight = localMidnightUtc(dayOffset);
    return new Date(midnight.getTime() + hour * 3_600_000).toISOString();
  }

  switch (preset) {
    case 'NOW':
      return {
        startUtc: now,
        endUtc: new Date(nowMs + 2 * 3_600_000).toISOString(),
        timezone,
      };
    case 'TONIGHT':
      return {
        startUtc: localHourUtc(0, 18),  // 6 PM local today
        endUtc: localHourUtc(1, 3),     // 3 AM local tomorrow
        timezone,
      };
    case 'TOMORROW':
      return {
        startUtc: localHourUtc(1, 0),
        endUtc: localHourUtc(2, 0),
        timezone,
      };
    case 'THIS_WEEKEND': {
      // Friday 6 PM through Sunday midnight
      const day = new Date(now).getDay(); // 0=Sun … 6=Sat
      const daysToFriday = day <= 5 ? (5 - day) : 6; // next Friday
      return {
        startUtc: localHourUtc(daysToFriday, 18),
        endUtc: localHourUtc(daysToFriday + 2, 24),
        timezone,
      };
    }
    case 'NEXT_7_DAYS':
      return {
        startUtc: now,
        endUtc: new Date(nowMs + 7 * 86_400_000).toISOString(),
        timezone,
      };
  }
}

export function normalizeTimeWindow(window: TimeWindow): TimeWindow {
  const start = new Date(window.startUtc);
  const end = new Date(window.endUtc);
  if (isNaN(start.getTime())) throw new Error('Invalid startUtc');
  if (isNaN(end.getTime())) throw new Error('Invalid endUtc');
  if (start >= end) throw new Error('startUtc must be before endUtc');
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
  | 'start_time_asc'
  | 'start_time_desc'
  | 'proximity'
  | 'freshness'
  | 'confidence'
  | 'featured'; // placeholder seam for sponsored/editorial

// ---------------------------------------------------------------------------
// Shared filter options
// ---------------------------------------------------------------------------

export interface EventFilters {
  categories?: EventCategory[];
  /** District / neighborhood code. */
  districtCode?: string;
  /** Minimum confidence threshold (0–1). */
  minConfidence?: number;
  /** Only return events from these source kinds. */
  sourceKinds?: string[];
}

// ---------------------------------------------------------------------------
// Pagination
// ---------------------------------------------------------------------------

export interface PaginationParams {
  page: number;   // 1-based
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

export interface MapFeedResponse {
  events: EventMapCardProjection[];
  totalCount: number;
}

export function buildMapFeedQuery(
  bounds: GeoBoundingBox,
  preset: TemporalPreset,
  timezone: string,
  filters?: EventFilters,
): MapFeedQuery {
  if (!isValidBoundingBox(bounds)) {
    const { errors } = validateBoundingBox(bounds);
    throw new Error(`Invalid bounding box: ${errors.join('; ')}`);
  }
  return {
    bounds,
    window: resolveTemporalPreset(preset, timezone),
    filters,
    sort: 'start_time_asc',
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
    sort: 'start_time_asc',
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
