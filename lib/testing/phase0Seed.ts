import rawSeed from "@/seed/phase0-dataset.json";
import { NightlifeItem } from "@/types";

type RawSeed = typeof rawSeed;
type RawEvent = RawSeed["events"][number];

const venueById = new Map(
  rawSeed.venues.map((venue) => [venue.venueId, venue]),
);

export const phase0Seed = rawSeed;
export const PHASE0_FIXED_NOW = rawSeed.meta.fixedNow;
export const PRIMARY_EVENT_ID = "evt-sf-midnight-groove";
export const SEEDED_USER_EMAIL = "camille+phase0@weup.test";
export const SEEDED_MODERATOR_EMAIL = "moderator+phase0@weup.test";

function toNightlifeItem(event: RawEvent): NightlifeItem {
  const venue = venueById.get(event.venueId);
  if (!venue) {
    throw new Error(
      `[phase0Seed] Missing venue '${event.venueId}' for event '${event.eventId}'.`,
    );
  }

  const source =
    event.sourceKind === "manual_submission"
      ? "manual"
      : event.sourceKind === "scraped_venue_page"
        ? "scraped"
        : "api";

  return {
    id: event.eventId,
    title: event.title,
    description: event.description,
    venue_name: venue.name,
    address: venue.address,
    latitude: venue.latitude,
    longitude: venue.longitude,
    start_time: event.startsAtUtc,
    end_time: event.endsAtUtc,
    category: event.category,
    price_tier: event.priceTier,
    source,
    image_url: event.imageUrl,
    status: event.status as NightlifeItem["status"],
    confidence: event.confidence,
    neighborhood: event.neighborhood,
    energyLevel: event.energyLevel,
    tags: [...event.tags],
    coordinates: {
      lat: venue.latitude,
      lng: venue.longitude,
    },
  };
}

export const SEEDED_EVENTS: NightlifeItem[] =
  rawSeed.events.map(toNightlifeItem);

export function getSeedEvent(eventId: string): NightlifeItem | undefined {
  return SEEDED_EVENTS.find((event) => event.id === eventId);
}

export function getDeterministicDraft(
  source: "UPLOAD" | "LINK" | "MANUAL" | "CENTER",
  mapCenter: { lat: number; lng: number },
): Partial<NightlifeItem> {
  const midnightGroove = getSeedEvent(PRIMARY_EVENT_ID) ?? SEEDED_EVENTS[0];
  const rooftopSignals =
    getSeedEvent("evt-sf-rooftop-signals") ??
    SEEDED_EVENTS[1] ??
    midnightGroove;

  switch (source) {
    case "UPLOAD":
      return {
        id: "draft-upload-neon-market",
        title: "Neon Market After Hours",
        description:
          "Deterministic upload extraction preview for release validation.",
        venue_name: midnightGroove.venue_name,
        address: midnightGroove.address,
        latitude: midnightGroove.latitude,
        longitude: midnightGroove.longitude,
        start_time: "2026-04-13T03:30:00Z",
        end_time: "2026-04-13T07:00:00Z",
        category: "nightlife",
        price_tier: "$$",
        source: "manual",
        image_url:
          "https://picsum.photos/seed/weup-upload-neon-market/800/1200",
        status: "NEEDS_REVIEW",
        confidence: 0.91,
        tags: ["After Hours", "House"],
      };
    case "LINK":
      return {
        id: "draft-link-rooftop-session",
        title: "Rooftop Signals Late Session",
        description:
          "Deterministic pasted-link extraction preview for release validation.",
        venue_name: rooftopSignals.venue_name,
        address: rooftopSignals.address,
        latitude: rooftopSignals.latitude,
        longitude: rooftopSignals.longitude,
        start_time: "2026-04-12T00:00:00Z",
        end_time: "2026-04-12T03:00:00Z",
        category: "rooftop",
        price_tier: "$$$",
        source: "manual",
        image_url:
          "https://picsum.photos/seed/weup-link-rooftop-session/800/1200",
        status: "PUBLISHED",
        confidence: 0.94,
        tags: ["Rooftop", "Cocktails"],
      };
    case "CENTER":
      return {
        id: "draft-center-seeded-signal",
        title: "Seeded Map Center Signal",
        description:
          "Deterministic center-drop draft used for manual validation flows.",
        venue_name: "Map Center",
        address: "Centered on current map viewport",
        latitude: mapCenter.lat,
        longitude: mapCenter.lng,
        start_time: rawSeed.meta.fixedNow,
        end_time: "2026-04-11T20:00:00Z",
        category: "nightlife",
        price_tier: "$$",
        source: "manual",
        image_url: "https://picsum.photos/seed/weup-center-signal/800/1200",
        status: "DRAFT",
        confidence: 0.5,
        tags: ["Draft"],
      };
    case "MANUAL":
    default:
      return {
        id: "draft-manual-seeded-signal",
        title: "",
        description: "",
        venue_name: "",
        address: "",
        latitude: mapCenter.lat,
        longitude: mapCenter.lng,
        start_time: rawSeed.meta.fixedNow,
        end_time: "2026-04-11T20:00:00Z",
        category: "nightlife",
        price_tier: "$$",
        source: "manual",
        image_url: "https://picsum.photos/seed/weup-manual-signal/800/1200",
        status: "DRAFT",
        confidence: 0,
        tags: [],
      };
  }
}
