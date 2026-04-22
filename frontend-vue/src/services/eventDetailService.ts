import type {
  EventDetailDto,
  EventDetailResponse,
  SaveEventRequestDto,
  SaveEventResponseDto,
  SavedStateDto,
  UnsaveEventRequestDto,
} from "../contracts/event-detail.contracts";

const API_BASE_URL =
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? "";

function buildErrorMessage(
  operation: string,
  status: number,
  detail: string,
): string {
  return `${operation} failed (${status}). ${detail}`.trim();
}

export class ApiRequestError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "ApiRequestError";
    this.status = status;
  }
}

function buildJsonHeaders(init?: RequestInit): HeadersInit {
  return {
    Accept: "application/json",
    "Content-Type": "application/json",
    ...(init?.headers ?? {}),
  };
}

export type EventShareResult = "shared" | "copied" | "opened" | "dismissed";

function buildShareUrl(eventId: string): string {
  if (typeof window === "undefined") {
    return `/events/${encodeURIComponent(eventId)}`;
  }

  return new URL(
    `/events/${encodeURIComponent(eventId)}`,
    window.location.origin,
  ).toString();
}

function buildShareText(event: EventDetailDto): string {
  return `${event.title} at ${event.venueName}`;
}

export async function shareEventDetail(
  event: EventDetailDto,
): Promise<EventShareResult> {
  const url = buildShareUrl(event.id);
  const title = event.title;
  const text = buildShareText(event);

  if (
    typeof navigator !== "undefined" &&
    typeof navigator.share === "function"
  ) {
    try {
      await navigator.share({ title, text, url });
      return "shared";
    } catch (cause) {
      if (cause instanceof DOMException && cause.name === "AbortError") {
        return "dismissed";
      }
    }
  }

  if (typeof navigator !== "undefined" && navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(url);
    return "copied";
  }

  if (typeof window !== "undefined") {
    const mailtoUrl = new URL("mailto:");
    mailtoUrl.searchParams.set("subject", title);
    mailtoUrl.searchParams.set("body", `${text}\n\n${url}`);
    window.open(mailtoUrl.toString(), "_blank", "noopener,noreferrer");
    return "opened";
  }

  throw new Error("No share target is available in the current environment.");
}

export async function fetchEventDetail(
  eventId: string,
  init?: RequestInit,
): Promise<EventDetailResponse> {
  const response = await fetch(
    `${API_BASE_URL}/api/events/${encodeURIComponent(eventId)}`,
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
    throw new ApiRequestError(
      buildErrorMessage("Event detail request", response.status, detail),
      response.status,
    );
  }

  return (await response.json()) as EventDetailResponse;
}

export async function saveEvent(
  request: SaveEventRequestDto,
  init?: RequestInit,
): Promise<SaveEventResponseDto> {
  const response = await fetch(`${API_BASE_URL}/api/users/me/saves`, {
    method: "POST",
    headers: buildJsonHeaders(init),
    body: JSON.stringify(request),
    ...init,
  });

  if (!response.ok) {
    const detail = await response.text().catch(() => "");
    throw new ApiRequestError(
      buildErrorMessage("Save request", response.status, detail),
      response.status,
    );
  }

  return (await response.json()) as SaveEventResponseDto;
}

export async function unsaveEvent(
  request: UnsaveEventRequestDto,
  init?: RequestInit,
): Promise<SaveEventResponseDto> {
  const response = await fetch(`${API_BASE_URL}/api/users/me/saves/unsave`, {
    method: "POST",
    headers: buildJsonHeaders(init),
    body: JSON.stringify(request),
    ...init,
  });

  if (!response.ok) {
    const detail = await response.text().catch(() => "");
    throw new ApiRequestError(
      buildErrorMessage("Unsave request", response.status, detail),
      response.status,
    );
  }

  return (await response.json()) as SaveEventResponseDto;
}

export async function fetchSavedState(
  eventId: string,
  init?: RequestInit,
): Promise<SavedStateDto> {
  const response = await fetch(
    `${API_BASE_URL}/api/users/me/saves/${encodeURIComponent(eventId)}/state`,
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
    throw new ApiRequestError(
      buildErrorMessage("Saved state request", response.status, detail),
      response.status,
    );
  }

  return (await response.json()) as SavedStateDto;
}
