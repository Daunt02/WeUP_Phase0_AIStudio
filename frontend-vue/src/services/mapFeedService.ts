import type {
  EventMapFeedQueryDto,
  EventMapFeedV1ResponseDto,
} from "../contracts/map-feed.contracts";

const API_BASE_URL =
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? "";

function buildMapFeedQueryString(query: EventMapFeedQueryDto): string {
  const params = new URLSearchParams();

  params.set("bbox", query.bbox);

  if (query.timeWindowPreset) {
    params.set("timeWindowPreset", query.timeWindowPreset);
  }

  if (query.fromUtc) {
    params.set("fromUtc", query.fromUtc);
  }

  if (query.toUtc) {
    params.set("toUtc", query.toUtc);
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
  if (!query.bbox || query.bbox.split(",").length !== 4) {
    throw new Error(
      "EventMapFeedQueryDto.bbox must be minLng,minLat,maxLng,maxLat.",
    );
  }

  if (!query.timeWindowPreset && (!query.fromUtc || !query.toUtc)) {
    throw new Error("Provide timeWindowPreset or both fromUtc and toUtc.");
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
