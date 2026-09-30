/**
 * WEUP-SYNTH:
 * source=app/api/gemini/route.ts (action: assistantChat)
 * destination=frontend-vue/src/services/assistantTransport.ts
 * mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
 * notes=Thin browser-side adapter for the canonical server-side Gemini route.
 *   The Gemini API key lives ONLY in the Next route (server side); this
 *   module never holds a key, never calls Google APIs directly, and posts
 *   only { action, payload } envelopes. Transport boundary only — the
 *   assistant's intelligence always comes from the server response or a real
 *   error; there are no canned fallback replies here.
 */

/**
 * Canonical action contract from app/api/gemini/route.ts.
 *
 * Request:  POST {base}/api/gemini
 *   body: { action: "assistantChat",
 *           payload: { message, persona, center, radius, history } }
 *
 * Success:  { result: { text: string, suggestedEvent?: {...} } }
 * Failure:  { error: string } with a non-2xx status, or a thrown transport
 *           error (unreachable host, network failure, malformed JSON).
 *
 * When no server-side GEMINI_API_KEY is configured the route responds 500
 * with error "GEMINI_API_KEY or NEXT_PUBLIC_GEMINI_API_KEY is missing..." —
 * that surfaces through ApiRequestError below and must be rendered as an
 * honest error state, never replaced with canned assistant prose.
 */

export type AssistantPersona = "auto" | "concierge" | "animal";

export interface AssistantChatHistoryTurn {
  readonly role: "user" | "model";
  readonly parts: ReadonlyArray<{ readonly text: string }>;
}

export interface AssistantSuggestedEvent {
  readonly title: string;
  readonly venue_name: string;
  readonly category: string;
  readonly description?: string;
  readonly address?: string;
}

export interface AssistantChatResult {
  /** Conversational reply produced server-side (by Gemini). */
  readonly text: string;
  /**
   * Optional event reference synthesized by the assistant. This is a
   * SUGGESTION ONLY: it is not a canonical event, it must never be written
   * to any store, and selecting it may only drive the canonical
   * selectEvent(eventId) path when it resolves to a known canonical event.
   */
  readonly suggestedEvent?: AssistantSuggestedEvent;
}

export interface AssistantChatInput {
  readonly message: string;
  readonly persona: AssistantPersona;
  readonly center: { readonly lat: number; readonly lng: number };
  readonly radius: number;
  readonly history: readonly AssistantChatHistoryTurn[];
}

const API_BASE_URL =
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? "";

export class ApiRequestError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "ApiRequestError";
    this.status = status;
  }
}

function buildJsonHeaders(): HeadersInit {
  return {
    Accept: "application/json",
    "Content-Type": "application/json",
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

/**
 * Strictly parse the assistantChat result envelope. Throws on anything that
 * is not a well-formed server response — the caller must surface that as an
 * honest error, never synthesize a reply.
 */
export function parseAssistantChatResponse(body: unknown): AssistantChatResult {
  if (!isRecord(body)) {
    throw new Error("Assistant response was not a JSON object.");
  }

  if (typeof body.error === "string" && body.error.trim().length > 0) {
    throw new Error(`Assistant request failed: ${body.error}`);
  }

  const result = body.result;
  if (!isRecord(result)) {
    throw new Error("Assistant response contained no result envelope.");
  }

  if (typeof result.text !== "string" || result.text.trim().length === 0) {
    throw new Error("Assistant response contained no reply text.");
  }

  let suggestedEvent: AssistantSuggestedEvent | undefined;
  if (result.suggestedEvent !== undefined && result.suggestedEvent !== null) {
    if (!isRecord(result.suggestedEvent)) {
      throw new Error("Assistant suggested event was malformed.");
    }
    const candidate = result.suggestedEvent;
    if (
      typeof candidate.title !== "string" ||
      typeof candidate.venue_name !== "string" ||
      typeof candidate.category !== "string"
    ) {
      throw new Error(
        "Assistant suggested event is missing title, venue_name, or category.",
      );
    }
    suggestedEvent = {
      title: candidate.title,
      venue_name: candidate.venue_name,
      category: candidate.category,
      ...(typeof candidate.description === "string"
        ? { description: candidate.description }
        : {}),
      ...(typeof candidate.address === "string"
        ? { address: candidate.address }
        : {}),
    };
  }

  return { text: result.text, suggestedEvent };
}

/**
 * Post an assistantChat request to the canonical server route.
 *
 * Contract traceability: mirrors components/SpikeAssistantPanel.tsx
 * handleSend, which posted action "assistantChat" with payload
 * { message, persona, center, radius, history }.
 */
export async function postAssistantChat(
  input: AssistantChatInput,
): Promise<AssistantChatResult> {
  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}/api/gemini`, {
      method: "POST",
      headers: buildJsonHeaders(),
      body: JSON.stringify({
        action: "assistantChat",
        payload: {
          message: input.message,
          persona: input.persona,
          center: { lat: input.center.lat, lng: input.center.lng },
          radius: input.radius,
          history: input.history,
        },
      }),
    });
  } catch (cause) {
    throw new Error(
      `Assistant route is unreachable: ${
        cause instanceof Error ? cause.message : "network failure"
      }`,
    );
  }

  let body: unknown;
  try {
    body = await response.json();
  } catch {
    throw new Error(
      `Assistant route returned an unreadable response (status ${response.status}).`,
    );
  }

  if (!response.ok) {
    const detail =
      isRecord(body) && typeof body.error === "string" && body.error
        ? body.error
        : response.statusText || "unknown error";
    throw new ApiRequestError(
      `Assistant request failed (${response.status}). ${detail}`.trim(),
      response.status,
    );
  }

  return parseAssistantChatResponse(body);
}
