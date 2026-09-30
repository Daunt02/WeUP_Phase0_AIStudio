import { afterEach, describe, expect, it, vi } from "vitest";
import {
  ApiRequestError,
  parseAssistantChatResponse,
  postAssistantChat,
  type AssistantChatInput,
} from "../services/assistantTransport";

const INPUT: AssistantChatInput = {
  message: "Suggest nightlife near me",
  persona: "animal",
  center: { lat: 29.76, lng: -95.36 },
  radius: 5,
  history: [],
};

function mockFetchOnce(
  response: Partial<Omit<Response, "body">> & { jsonBody?: unknown },
): ReturnType<typeof vi.fn> {
  const fetchMock = vi.fn().mockResolvedValue({
    ok: response.ok ?? true,
    status: response.status ?? 200,
    statusText: response.statusText ?? "",
    json:
      response.jsonBody !== undefined
        ? async () => response.jsonBody
        : async () => {
            throw new Error("Invalid JSON");
          },
  } as unknown as Response);
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("assistantTransport — canonical /api/gemini contract", () => {
  it("posts the action/payload envelope without any API key in the browser", async () => {
    const fetchMock = mockFetchOnce({
      jsonBody: { result: { text: "RUFF! Here is a spot." } },
    });

    await postAssistantChat(INPUT);

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url.endsWith("/api/gemini")).toBe(true);
    expect(init.method).toBe("POST");
    const body = JSON.parse(init.body as string) as Record<string, unknown>;
    expect(body.action).toBe("assistantChat");
    expect(body.payload).toMatchObject({
      message: INPUT.message,
      persona: INPUT.persona,
      center: { lat: 29.76, lng: -95.36 },
      radius: 5,
      history: [],
    });
    // No credential material may cross the transport boundary.
    const serialized = JSON.stringify(body).toLowerCase();
    expect(serialized).not.toContain("gemini_api_key");
    expect(serialized).not.toContain("api_key");
    expect(serialized).not.toContain("bearer");
  });

  it("parses a successful structured response with a suggested event", async () => {
    mockFetchOnce({
      jsonBody: {
        result: {
          text: "WOOF! Found one.",
          suggestedEvent: {
            title: "Neon Rooftop Set",
            venue_name: "Skyline Deck",
            category: "rooftop",
            description: "Sunset DJ set.",
            address: "100 Main St",
          },
        },
      },
    });

    const result = await postAssistantChat(INPUT);

    expect(result.text).toBe("WOOF! Found one.");
    expect(result.suggestedEvent).toMatchObject({
      title: "Neon Rooftop Set",
      venue_name: "Skyline Deck",
      category: "rooftop",
    });
  });

  it("surfaces the server error envelope instead of a fake reply", async () => {
    mockFetchOnce({ jsonBody: { error: "Something broke upstream" } });

    await expect(postAssistantChat(INPUT)).rejects.toThrow(
      "Assistant request failed: Something broke upstream",
    );
  });

  it("throws ApiRequestError on non-2xx with the server detail (e.g. missing key)", async () => {
    mockFetchOnce({
      ok: false,
      status: 500,
      statusText: "Internal Server Error",
      jsonBody: {
        error:
          "GEMINI_API_KEY or NEXT_PUBLIC_GEMINI_API_KEY is missing. Please add it to your environment variables.",
      },
    });

    const failure = await postAssistantChat(INPUT).catch((e) => e);
    expect(failure).toBeInstanceOf(ApiRequestError);
    expect((failure as ApiRequestError).status).toBe(500);
    expect((failure as Error).message).toContain("GEMINI_API_KEY");
  });

  it("surfaces unreachable-route failures honestly", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockRejectedValue(new TypeError("Failed to fetch")),
    );

    await expect(postAssistantChat(INPUT)).rejects.toThrow(
      /Assistant route is unreachable/,
    );
  });

  it("rejects malformed JSON bodies", async () => {
    mockFetchOnce({ jsonBody: undefined });

    await expect(postAssistantChat(INPUT)).rejects.toThrow(
      /unreadable response/,
    );
  });
});

describe("parseAssistantChatResponse — structured response handling", () => {
  it("accepts text-only results", () => {
    expect(
      parseAssistantChatResponse({ result: { text: "Hello operator." } }),
    ).toEqual({ text: "Hello operator.", suggestedEvent: undefined });
  });

  it("rejects results with no reply text", () => {
    expect(() => parseAssistantChatResponse({ result: {} })).toThrow(
      /no reply text/,
    );
    expect(() => parseAssistantChatResponse({ result: { text: "  " } })).toThrow(
      /no reply text/,
    );
  });

  it("rejects malformed suggested events", () => {
    expect(() =>
      parseAssistantChatResponse({
        result: {
          text: "ok",
          suggestedEvent: { title: "No venue" },
        },
      }),
    ).toThrow(/missing title, venue_name, or category/);
  });

  it("rejects non-object envelopes", () => {
    expect(() => parseAssistantChatResponse(null)).toThrow(/not a JSON object/);
    expect(() => parseAssistantChatResponse({ result: null })).toThrow(
      /no result envelope/,
    );
  });
});
