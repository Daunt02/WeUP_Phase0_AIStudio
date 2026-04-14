import type { NightlifeItem } from "@/types";
import type {
  GhostEventDraft,
  GhostDraftPatch,
  RuntimeEventProjection,
  SubmissionDraftProjection,
} from "@/features/world/runtimeTypes";

const LEGACY_STATUS: NightlifeItem["status"][] = [
  "DRAFT",
  "NEEDS_REVIEW",
  "PUBLISHED",
  "ARCHIVED",
];

function toLegacyStatus(
  status: RuntimeEventProjection["status"],
): NightlifeItem["status"] {
  if (!status) return undefined;
  return LEGACY_STATUS.includes(status as NightlifeItem["status"])
    ? (status as NightlifeItem["status"])
    : "NEEDS_REVIEW";
}

/**
 * Legacy prototype adapter boundary.
 * Keep all NightlifeItem conversions isolated to this module.
 */
export function fromLegacyNightlifeItem(
  event: NightlifeItem,
): SubmissionDraftProjection {
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
    category: event.category as SubmissionDraftProjection["category"],
    priceTier: event.price_tier,
    source: event.source,
    imageUrl: event.image_url,
    status: event.status,
    confidence: event.confidence,
    tags: event.tags,
    neighborhood: event.neighborhood,
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
    status: toLegacyStatus(event.status),
    confidence: event.confidence,
    tags: event.tags,
    neighborhood: event.neighborhood,
  };
}

export function toLegacyDraftPatch(
  patch: GhostDraftPatch,
): Partial<NightlifeItem> {
  const ghost = patch as GhostEventDraft;

  return {
    id: ghost.id,
    title: ghost.title,
    description: ghost.description,
    venue_name: ghost.venueName,
    address: ghost.address,
    latitude: ghost.latitude,
    longitude: ghost.longitude,
    start_time: ghost.startTime,
    end_time: ghost.endTime,
    category: ghost.category,
    price_tier: ghost.priceTier,
    image_url: ghost.imageUrl,
    status: toLegacyStatus(ghost.status),
    confidence: ghost.confidence,
    tags: ghost.tags,
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
