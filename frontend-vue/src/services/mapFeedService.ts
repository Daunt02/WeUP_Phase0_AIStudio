import type {
  EventMapFeedQueryDto,
  EventMapFeedV1ResponseDto,
} from "../contracts/map-feed.contracts";
import { isCustomRangePreset } from "../contracts/temporal-query.contracts";

const API_BASE_URL =
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? "";

function buildMapFeedQueryString(query: EventMapFeedQueryDto): string {
  const params = new URLSearchParams();
  const marketTimezone = query.marketTimezone ?? query.timezone;
  const fromUtc = query.fromUtc ?? query.customStartUtc;
  const toUtc = query.toUtc ?? query.customEndUtc;

  params.set("bbox", query.bbox);
  params.set("preset", query.preset);
  params.set("marketTimezone", marketTimezone);
  params.set("timezone", marketTimezone);

  if (fromUtc) {
    params.set("fromUtc", fromUtc);
    params.set("customStartUtc", fromUtc);
  }

  if (toUtc) {
    params.set("toUtc", toUtc);
    params.set("customEndUtc", toUtc);
  }

  if (query.referenceInstantUtc) {
    params.set("referenceInstantUtc", query.referenceInstantUtc);
  }

  if (query.district) {
    params.set("district", query.district);
  }

  if (query.categories && query.categories.length > 0) {
    for (const category of query.categories) {
      params.append("categories", category);
    }
  }

  params.set("includeSavedOnly", String(Boolean(query.includeSavedOnly)));
  return params.toString();
}

function ensureQueryContract(query: EventMapFeedQueryDto): void {
  const marketTimezone = query.marketTimezone ?? query.timezone;
  const fromUtc = query.fromUtc ?? query.customStartUtc;
  const toUtc = query.toUtc ?? query.customEndUtc;

  if (!query.bbox || query.bbox.split(",").length !== 4) {
    throw new Error(
      "EventMapFeedQueryDto.bbox must be minLng,minLat,maxLng,maxLat.",
    );
  }

  if (!marketTimezone.trim()) {
    throw new Error(
      "TemporalQueryDto.marketTimezone is required (timezone is accepted as a legacy alias).",
    );
  }

  if (isCustomRangePreset(query.preset)) {
    if (!fromUtc || !toUtc) {
      throw new Error("Provide fromUtc and toUtc when preset is custom range.");
    }

    if (fromUtc >= toUtc) {
      throw new Error("fromUtc must be earlier than toUtc.");
    }
  }
}

export async function fetchEventMapFeed(
  query: EventMapFeedQueryDto,
  init?: RequestInit,
): Promise<EventMapFeedV1ResponseDto> {
  ensureQueryContract(query);

  const queryString = buildMapFeedQueryString(query);
  const response = await fetch(
    `${API_BASE_URL}/api/events/map-feed/v1?${queryString}`,
    {
      method: "GET",
      headers: {
        Accept: "application/json",
        ...(init?.headers ?? {}),
      },
      ...init,
    },
  );

  if (!response.ok) {
    const detail = await response.text().catch(() => "");
    throw new Error(
      `Map feed request failed (${response.status}). ${detail}`.trim(),
    );
  }

  return (await response.json()) as EventMapFeedV1ResponseDto;
}
