import type {
  SavedEventsQueryDto,
  SavedEventsResponseDto,
} from "../contracts/saved-events.contracts";
import { ApiRequestError } from "./eventDetailService";

const API_BASE_URL =
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? "";

function buildErrorMessage(
  operation: string,
  status: number,
  detail: string,
): string {
  return `${operation} failed (${status}). ${detail}`.trim();
}

function normalizeQuery(
  query: SavedEventsQueryDto = {},
): Required<SavedEventsQueryDto> {
  return {
    page: Math.max(1, query.page ?? 1),
    pageSize: Math.min(100, Math.max(1, query.pageSize ?? 50)),
  };
}

export async function fetchSavedEvents(
  query: SavedEventsQueryDto = {},
  init?: RequestInit,
): Promise<SavedEventsResponseDto> {
  const normalizedQuery = normalizeQuery(query);
  const params = new URLSearchParams({
    page: String(normalizedQuery.page),
    pageSize: String(normalizedQuery.pageSize),
  });

  const response = await fetch(`${API_BASE_URL}/api/users/me/saves?${params}`, {
    method: "GET",
    headers: {
      Accept: "application/json",
      ...(init?.headers ?? {}),
    },
    ...init,
  });

  if (!response.ok) {
    const detail = await response.text().catch(() => "");
    throw new ApiRequestError(
      buildErrorMessage("Saved events request", response.status, detail),
      response.status,
    );
  }

  return (await response.json()) as SavedEventsResponseDto;
}
