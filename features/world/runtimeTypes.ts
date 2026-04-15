import { EventCategory, EventStatus } from "@/domains/event/types";
import type { GeoBoundingBox } from "@/domains/query/contracts";
import type { EventMapCardProjection } from "@/domains/event/projections";

export type SelectedEventId = string;
export type SaveToggleEventId = SelectedEventId;

export type MapCenter = {
  lat: number;
  lng: number;
};

export type MapBounds = GeoBoundingBox | null;

export type TemporalPresetSelection =
  | "Today"
  | "Tonight"
  | "Weekend"
  | "Next7Days";

export interface RuntimeEventProjection {
  id: SelectedEventId;
  title: string;
  description: string;
  venueName: string;
  address: string;
  latitude: number;
  longitude: number;
  startTime: string;
  endTime: string;
  category: EventCategory;
  priceTier: string;
  source: "manual" | "scraped" | "api";
  imageUrl: string;
  status?: EventStatus;
  confidence?: number;
  tags?: string[];
  neighborhood?: string;
}

export interface SubmissionDraftProjection extends RuntimeEventProjection {
  flyerAssetIds?: string[];
}

export interface GhostEventDraft {
  id?: string;
  latitude?: number;
  longitude?: number;
  title?: string;
  venueName?: string;
  address?: string;
  category?: EventCategory | string;
  startTime?: string;
  endTime?: string;
  description?: string;
  imageUrl?: string;
  priceTier?: string;
  tags?: string[];
  confidence?: number;
  status?: EventStatus;
}

export type GhostDraftPatch = Partial<GhostEventDraft>;

export function fromMapCardProjection(
  event: EventMapCardProjection,
): RuntimeEventProjection {
  return {
    id: event.id,
    title: event.title,
    description: "",
    venueName: event.venueName,
    address: "",
    latitude: event.lat,
    longitude: event.lng,
    startTime: new Date().toISOString(),
    endTime: new Date().toISOString(),
    category: event.category,
    priceTier: "$$",
    source: "api",
    imageUrl: event.thumbnailUrl || "",
    status: event.status,
    confidence: event.confidence,
    tags: [],
  };
}

export interface DraftSubmissionRequest {
  title?: string;
  venueName?: string;
  address?: string;
  startUtc?: string;
  endUtc?: string;
  timezone?: string;
  category?: string;
  description?: string;
  tags?: string[];
  flyerAssetIds?: string[];
}

export function toDraftSubmissionRequest(
  event: SubmissionDraftProjection,
): DraftSubmissionRequest {
  return {
    title: event.title,
    venueName: event.venueName,
    address: event.address,
    startUtc: event.startTime,
    endUtc: event.endTime,
    timezone: "America/Chicago",
    category: event.category,
    description: event.description,
    tags: event.tags,
    flyerAssetIds: event.flyerAssetIds,
  };
}
