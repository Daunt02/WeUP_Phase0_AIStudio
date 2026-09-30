import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  resolveSuggestionToCanonicalId,
  useAssistantSession,
} from "../composables/useAssistantSession";
import type { AssistantSuggestedEvent } from "../services/assistantTransport";
import type { EventMapItemDto } from "../contracts/map-feed.contracts";

function createMapItem(overrides: Partial<EventMapItemDto> = {}): EventMapItemDto {
  return {
    eventId: "evt-1",
    title: "Neon Rooftop Set",
    startUtc: "2026-09-30T01:00:00Z",
    endUtc: null,
    latitude: 29.76,
    longitude: -95.36,
    venueName: "Skyline Deck",
    district: "Downtown",
    primaryCategory: "rooftop",
    savedByCurrentUser: false,
    markerState: "default",
    ...overrides,
  };
}

const CENTER = { lat: 29.76, lng: -95.36 };

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe("useAssistantSession — persona state is pure UI state", () => {
  it("defaults to the animal persona with the source greeting", () => {
    const session = useAssistantSession({
      transport: vi.fn().mockRejectedValue(new Error("unused")),
    });

    expect(session.activePersona.value).toBe("animal");
    expect(session.chatLog.value).toHaveLength(1);
    expect(session.chatLog.value[0].sender).toBe("assistant");
    expect(session.chatLog.value[0].text).toContain("RUFF!");
  });

  it("switching personas changes UI state only — no transport call, no log entry", () => {
    const transport = vi.fn().mockRejectedValue(new Error("unused"));
    const session = useAssistantSession({ transport });

    session.setPersona("concierge");
    session.setPersona("auto");

    expect(session.activePersona.value).toBe("auto");
    expect(transport).not.toHaveBeenCalled();
    expect(session.chatLog.value).toHaveLength(1);
  });

  it("suggestion chips fill the input without sending", () => {
    const session = useAssistantSession({
      transport: vi.fn().mockRejectedValue(new Error("unused")),
    });

    session.fillInputFromSuggestion("Calibrate the radar");
    expect(session.inputText.value).toBe("Calibrate the radar");
    expect(session.chatLog.value).toHaveLength(1);
  });
});

describe("useAssistantSession — send path", () => {
  it("sends message/persona/center/radius/history and appends the real reply", async () => {
    const transport = vi.fn().mockResolvedValue({
      text: "RUFF! Locked onto Skyline Deck.",
    });
    const session = useAssistantSession({ transport });
    session.inputText.value = "Find rooftops";

    const pending = session.sendMessage(CENTER);
    expect(session.isThinking.value).toBe(true);
    await pending;

    expect(transport).toHaveBeenCalledTimes(1);
    expect(transport.mock.calls[0][0]).toMatchObject({
      message: "Find rooftops",
      persona: "animal",
      center: CENTER,
      radius: 5,
    });
    expect(session.isThinking.value).toBe(false);
    const log = session.chatLog.value;
    expect(log[1].sender).toBe("user");
    expect(log[1].text).toBe("Find rooftops");
    expect(log[2].sender).toBe("assistant");
    expect(log[2].text).toBe("RUFF! Locked onto Skyline Deck.");
    expect(session.pendingSuggestion.value).toBeNull();
  });

  it("stores a structured suggestion and announces it as a system entry", async () => {
    const suggestion: AssistantSuggestedEvent = {
      title: "Neon Rooftop Set",
      venue_name: "Skyline Deck",
      category: "rooftop",
    };
    const transport = vi.fn().mockResolvedValue({
      text: "One target acquired.",
      suggestedEvent: suggestion,
    });
    const session = useAssistantSession({ transport });
    session.inputText.value = "Find rooftops";

    await session.sendMessage(CENTER);

    expect(session.pendingSuggestion.value).toEqual(suggestion);
    const systemEntry = session.chatLog.value.find(
      (entry) => entry.sender === "system" && entry.suggestion,
    );
    expect(systemEntry).toBeDefined();
    expect(systemEntry?.text).toContain("SIGNAL_FOUND");
  });

  it("never sends empty input and never double-sends while thinking", async () => {
    let releaseTransport!: (value: unknown) => void;
    const transport = vi
      .fn()
      .mockImplementation(
        () => new Promise((resolve) => (releaseTransport = resolve)),
      );
    const session = useAssistantSession({ transport });

    session.inputText.value = "   ";
    await session.sendMessage(CENTER);
    expect(transport).not.toHaveBeenCalled();

    session.inputText.value = "hello";
    const first = session.sendMessage(CENTER);
    const second = session.sendMessage(CENTER);
    await second;
    expect(transport).toHaveBeenCalledTimes(1);
    releaseTransport({ text: "ok" });
    await first;
    expect(session.isThinking.value).toBe(false);
  });

  it("shows an HONEST error entry on failure — no fake assistant prose", async () => {
    const transport = vi
      .fn()
      .mockRejectedValue(
        new Error(
          "Assistant request failed (500). GEMINI_API_KEY or NEXT_PUBLIC_GEMINI_API_KEY is missing.",
        ),
      );
    const session = useAssistantSession({ transport });
    session.inputText.value = "Find rooftops";

    await session.sendMessage(CENTER);

    const log = session.chatLog.value;
    expect(log).toHaveLength(3);
    expect(log[2].sender).toBe("system");
    expect(log[2].text).toContain("ROUTING_RETRY");
    expect(log[2].text).toContain("GEMINI_API_KEY");
    // No assistant message may be fabricated on failure.
    expect(log.some((entry) => entry.sender === "assistant" && entry !== log[0])).toBe(
      false,
    );
    expect(session.isThinking.value).toBe(false);
  });
});

describe("useAssistantSession — calibration deck", () => {
  it("recalibrates stats, logs the alert, and releases the lock after 1500ms", async () => {
    const session = useAssistantSession({
      transport: vi.fn().mockRejectedValue(new Error("unused")),
    });

    session.calibrate();

    expect(session.calibrationActive.value).toBe(true);
    expect(session.signalIntensity.value).toBe(100);
    expect(session.pulseRate.value).toBe(9.8);
    expect(session.coreTemp.value).toBe(38.4);
    const alert = session.chatLog.value.at(-1);
    expect(alert?.sender).toBe("system");
    expect(alert?.text).toContain("SYSTEM_ALERT");

    await vi.advanceTimersByTimeAsync(1500);
    expect(session.calibrationActive.value).toBe(false);
  });

  it("ignores recalibration while a calibration is active", () => {
    const session = useAssistantSession({
      transport: vi.fn().mockRejectedValue(new Error("unused")),
    });

    session.calibrate();
    const logLength = session.chatLog.value.length;
    session.calibrate();
    expect(session.chatLog.value).toHaveLength(logLength);
  });
});

describe("useAssistantSession — suggestion resolution", () => {
  const suggestion: AssistantSuggestedEvent = {
    title: "Neon Rooftop Set",
    venue_name: "Skyline Deck",
    category: "rooftop",
  };

  it("resolves a suggestion to a canonical eventId by title match", () => {
    expect(
      resolveSuggestionToCanonicalId(suggestion, [createMapItem()]),
    ).toBe("evt-1");
  });

  it("matches case-insensitively", () => {
    expect(
      resolveSuggestionToCanonicalId(
        { ...suggestion, title: "neon ROOFTOP set" },
        [createMapItem()],
      ),
    ).toBe("evt-1");
  });

  it("returns null when the suggestion is not a known canonical event", () => {
    expect(
      resolveSuggestionToCanonicalId(suggestion, [
        createMapItem({ eventId: "evt-9", title: "Different Event" }),
      ]),
    ).toBeNull();
  });

  it("resolvePendingSuggestion drives canonical selection and clears the card", async () => {
    const transport = vi.fn().mockResolvedValue({
      text: "One target acquired.",
      suggestedEvent: suggestion,
    });
    const session = useAssistantSession({ transport });
    session.inputText.value = "Find rooftops";
    await session.sendMessage(CENTER);

    const outcome = session.resolvePendingSuggestion([createMapItem()]);

    expect(outcome).toEqual({ kind: "resolved", eventId: "evt-1" });
    expect(session.pendingSuggestion.value).toBeNull();
    const last = session.chatLog.value.at(-1);
    expect(last?.text).toContain("SIGNAL_LOCK");
  });

  it("reports unresolved suggestions honestly without inventing an event", async () => {
    const transport = vi.fn().mockResolvedValue({
      text: "One target acquired.",
      suggestedEvent: suggestion,
    });
    const session = useAssistantSession({ transport });
    session.inputText.value = "Find rooftops";
    await session.sendMessage(CENTER);

    const outcome = session.resolvePendingSuggestion([
      createMapItem({ eventId: "evt-9", title: "Different Event" }),
    ]);

    expect(outcome).toEqual({ kind: "unresolved", eventId: null });
    // The pending suggestion stays visible so the operator can dismiss it.
    expect(session.pendingSuggestion.value).not.toBeNull();
    const last = session.chatLog.value.at(-1);
    expect(last?.text).toContain("SIGNAL_UNRESOLVED");
  });
});
