/**
 * WeUP Phase 0 — Event UI Projections
 *
 * Derive lean, display-only view models from EventAggregate.
 * Components depend only on these types — NOT on EventAggregate directly.
 *
 * M4-P17: Projections are derived from two sources:
 *   (a) Local EventAggregate (seed / offline)
 *   (b) Backend API DTOs from domains/event/apiContracts.ts
 * Adapter functions for (b) are provided as fromXxxDto() below.
 */

import type {
  EventMapCardDto,
  EventCalendarCardDto,
  EventDetailDto,
} from "./apiContracts";
import { EventAggregate, EventCategory, EventStatus } from "./types";

// ---------------------------------------------------------------------------
// Shared display helpers
// ---------------------------------------------------------------------------

export interface PriceTierDisplay {
  label: string; // '$' | '$$' | '$$$' | 'FREE'
}

// ---------------------------------------------------------------------------
// Map card projection
// ---------------------------------------------------------------------------

export interface EventMapCardProjection {
  id: string;
  title: string;
  venueName: string;
  category: EventCategory;
  lat: number;
  lng: number;
  thumbnailUrl: string | null;
  status: EventStatus;
  confidence: number;
}

// ---------------------------------------------------------------------------
// Calendar list projection
// ---------------------------------------------------------------------------

export interface EventCalendarProjection {
  id: string;
  title: string;
  venueName: string;
  category: EventCategory;
  startUtc: string;
  endUtc: string | null;
  timezone: string;
  thumbnailUrl: string | null;
  status: EventStatus;
}

// ---------------------------------------------------------------------------
// Detail view projection
// ---------------------------------------------------------------------------

export interface EventDetailProjection {
  id: string;
  title: string;
  description: string | null;
  venueName: string;
  address: string; // single display string from AddressSnapshot
  lat: number;
  lng: number;
  category: EventCategory;
  startUtc: string;
  endUtc: string | null;
  timezone: string;
  mediaRefs: Array<{ url: string; kind: "image" | "video" | "poster" }>;
  tags: string[];
  status: EventStatus;
  confidence: number;
  sourceKind: string; // human-readable source summary
}

// ---------------------------------------------------------------------------
// Mappers
// ---------------------------------------------------------------------------

function primaryThumbnail(event: EventAggregate): string | null {
  const poster = event.mediaRefs.find((m) => m.kind === "poster");
  const image = event.mediaRefs.find((m) => m.kind === "image");
  return (poster ?? image)?.url ?? null;
}

function addressDisplay(event: EventAggregate): string {
  const a = event.address;
  const parts = [a.line1, a.city, a.state].filter(Boolean);
  return parts.join(", ");
}

function sourceKindDisplay(event: EventAggregate): string {
  if (event.sourceRefs.length === 0) return "unknown";
  return event.sourceRefs[0].kind.replace(/_/g, " ");
}

export function toMapCard(event: EventAggregate): EventMapCardProjection {
  return {
    id: event.id,
    title: event.canonicalTitle,
    venueName: event.venue.name,
    category: event.category,
    lat: event.geo.lat,
    lng: event.geo.lng,
    thumbnailUrl: primaryThumbnail(event),
    status: event.status,
    confidence: event.confidence,
  };
}

export function toCalendarProjection(
  event: EventAggregate,
): EventCalendarProjection {
  return {
    id: event.id,
    title: event.canonicalTitle,
    venueName: event.venue.name,
    category: event.category,
    startUtc: event.timeRange.startUtc,
    endUtc: event.timeRange.endUtc,
    timezone: event.timezone,
    thumbnailUrl: primaryThumbnail(event),
    status: event.status,
  };
}

export function toDetailProjection(
  event: EventAggregate,
): EventDetailProjection {
  return {
    id: event.id,
    title: event.canonicalTitle,
    description: event.canonicalDescription,
    venueName: event.venue.name,
    address: addressDisplay(event),
    lat: event.geo.lat,
    lng: event.geo.lng,
    category: event.category,
    startUtc: event.timeRange.startUtc,
    endUtc: event.timeRange.endUtc,
    timezone: event.timezone,
    mediaRefs: event.mediaRefs.map((m) => ({ url: m.url, kind: m.kind })),
    tags: event.tags,
    status: event.status,
    confidence: event.confidence,
    sourceKind: sourceKindDisplay(event),
  };
}

// ---------------------------------------------------------------------------
// M4-P17: DTO -> Projection adapters
//
// These bridge backend API DTOs (apiContracts.ts) to display projections.
// Used by services when data originates from the API rather than seed data.
// ---------------------------------------------------------------------------

/** Adapt a backend EventMapCardDto to the EventMapCardProjection used by map components. */
export function fromMapCardDto(dto: EventMapCardDto): EventMapCardProjection {
  return {
    id: dto.id,
    title: dto.title,
    venueName: dto.venueName,
    category: dto.category as EventCategory,
    lat: dto.lat,
    lng: dto.lng,
    thumbnailUrl: dto.thumbnailUrl,
    status: dto.status as EventStatus,
    confidence: dto.confidence,
  };
}

/** Adapt a backend EventCalendarCardDto to the EventCalendarProjection used by calendar components. */
export function fromCalendarCardDto(
  dto: EventCalendarCardDto,
): EventCalendarProjection {
  return {
    id: dto.id,
    title: dto.title,
    venueName: dto.venueName,
    category: dto.category as EventCategory,
    startUtc: dto.startUtc,
    endUtc: dto.endUtc,
    timezone: dto.timezone,
    thumbnailUrl: dto.thumbnailUrl,
    status: dto.status as EventStatus,
  };
}

/** Adapt a backend EventDetailDto to the EventDetailProjection used by detail components. */
export function fromDetailDto(dto: EventDetailDto): EventDetailProjection {
  return {
    id: dto.id,
    title: dto.title,
    description: dto.description,
    venueName: dto.venueName,
    address: dto.address,
    lat: dto.lat,
    lng: dto.lng,
    category: dto.category as EventCategory,
    startUtc: dto.startUtc,
    endUtc: dto.endUtc,
    timezone: dto.timezone,
    mediaRefs: (dto.mediaRefs ?? []).map((m) => ({
      url: m.url,
      kind: m.kind as "image" | "video" | "poster",
    })),
    tags: dto.tags ?? [],
    status: dto.status as EventStatus,
    confidence: dto.confidence,
    sourceKind: dto.sourceKind,
  };
}
