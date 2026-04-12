import { EventCategory, EventStatus } from "@/domains/event/types";
import type { GeoBoundingBox } from "@/domains/query/contracts";
import type { EventMapCardProjection } from "@/domains/event/projections";
import type { NightlifeItem } from "@/types";

export type SelectedEventId = string;
export type SaveToggleEventId = SelectedEventId;

export type MapCenter = {
  lat: number;
  lng: number;
};

export type MapBounds = GeoBoundingBox | null;

export type TemporalPresetSelection =
  | "NOW"
  | "6PM"
  | "9PM"
  | "MIDNIGHT"
  | "3AM"
  | "FRI"
  | "SAT"
  | "SUN";

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

export function fromLegacyNightlifeItem(
  event: NightlifeItem,
): RuntimeEventProjection {
  return {
    id: event.id,
    title: event.title,
    description: event.description,
    venueName: event.venue_name,
    address: event.address,
    latitude: event.latitude,
    longitude: event.longitude,
    startTime: event.start_time,
    endTime: event.end_time,
    category: event.category,
    priceTier: event.price_tier,
    source: event.source,
    imageUrl: event.image_url,
    status: event.status,
    confidence: event.confidence,
    tags: event.tags,
    neighborhood: event.neighborhood,
  };
}

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

export function toLegacyNightlifeItem(
  event: RuntimeEventProjection,
): NightlifeItem {
  return {
    id: event.id,
    title: event.title,
    description: event.description,
    venue_name: event.venueName,
    address: event.address,
    latitude: event.latitude,
    longitude: event.longitude,
    start_time: event.startTime,
    end_time: event.endTime,
    category: event.category,
    price_tier: event.priceTier,
    source: event.source,
    image_url: event.imageUrl,
    status: event.status,
    confidence: event.confidence,
    tags: event.tags,
    neighborhood: event.neighborhood,
  };
}

export function toLegacyDraftPatch(
  patch: GhostDraftPatch,
): Partial<NightlifeItem> {
  return {
    id: patch.id,
    title: patch.title,
    description: patch.description,
    venue_name: patch.venueName,
    address: patch.address,
    latitude: patch.latitude,
    longitude: patch.longitude,
    start_time: patch.startTime,
    end_time: patch.endTime,
    category: patch.category,
    price_tier: patch.priceTier,
    image_url: patch.imageUrl,
    status: patch.status,
    confidence: patch.confidence,
    tags: patch.tags,
  };
}

export function fromLegacyDraftPatch(
  patch: Partial<NightlifeItem>,
): GhostDraftPatch {
  return {
    id: patch.id,
    title: patch.title,
    description: patch.description,
    venueName: patch.venue_name,
    address: patch.address,
    latitude: patch.latitude,
    longitude: patch.longitude,
    startTime: patch.start_time,
    endTime: patch.end_time,
    category: patch.category,
    priceTier: patch.price_tier,
    imageUrl: patch.image_url,
    status: patch.status,
    confidence: patch.confidence,
    tags: patch.tags,
  };
}
