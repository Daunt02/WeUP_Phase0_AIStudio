import { getAuthHeader } from "@/services/auth";
import { toApiUrl } from "@/services/apiBase";
import type { SavedEventsResponse } from "@/services/backendContracts";

type SavedEventsApiPayload = SavedEventsResponse & {
  Items?: Array<{ eventId?: string }>;
  HasNextPage?: boolean;
};

function extractSavedIds(payload: SavedEventsApiPayload): string[] {
  const items = payload.items ?? payload.Items ?? [];

  return Array.from(
    new Set(
      items
        .map((item) => {
          const id = item.eventId;
          return typeof id === "string" ? id.trim() : "";
        })
        .filter((id) => id.length > 0),
    ),
  );
}

function hasNextPage(payload: SavedEventsApiPayload): boolean {
  return Boolean(payload.hasNextPage ?? payload.HasNextPage ?? false);
}

function buildAuthHeaders(): Record<string, string> {
  return {
    "Content-Type": "application/json",
    ...getAuthHeader(),
  };
}

export async function listSavedEventIds(): Promise<string[]> {
  const headers = buildAuthHeaders();

  if (!headers.Authorization) {
    throw new Error("Authenticated save session is required.");
  }

  const collected: string[] = [];
  let page = 1;
  let hasNext = true;

  while (hasNext && page <= 20) {
    const response = await fetch(
      toApiUrl(`/api/users/me/saves?page=${page}&pageSize=100`),
      { headers },
    );

    if (!response.ok) {
      throw new Error(
        `Save list request failed with status ${response.status}.`,
      );
    }

    const payload = (await response.json()) as SavedEventsApiPayload;
    collected.push(...extractSavedIds(payload));

    hasNext = hasNextPage(payload);
    page += 1;
  }

  return Array.from(new Set(collected));
}

export async function setSavedEvent(
  eventId: string,
  saved: boolean,
): Promise<void> {
  const headers = buildAuthHeaders();

  if (!headers.Authorization) {
    throw new Error("Authenticated save session is required.");
  }

  const response = await fetch(
    toApiUrl(`/api/users/me/saves/${encodeURIComponent(eventId)}`),
    {
      method: saved ? "POST" : "DELETE",
      headers,
    },
  );

  if (!response.ok) {
    throw new Error(`Save request failed with status ${response.status}.`);
  }
}
