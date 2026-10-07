/**
 * WEUP-SYNTH:
 * source=components/SpikeAssistantPanel.tsx
 * destination=frontend-vue/src/composables/useAssistantSession.ts
 * mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
 * notes=Assistant session state for the Spike co-pilot surface. Personas are
 *   pure UI state (animal / concierge / auto) and must never invent profile
 *   identity. AI suggestions are references only: they are resolved against
 *   canonical map-feed items and may only drive discovery.selectEvent — the
 *   assistant never writes events, saved state, or temporal state. The Gemini
 *   response path goes through services/assistantTransport.ts (server route
 *   only); failures surface as honest system entries, never canned prose.
 */
import { computed, ref } from "vue";
import type {
  AssistantChatHistoryTurn,
  AssistantChatInput,
  AssistantChatResult,
  AssistantPersona,
  AssistantSuggestedEvent,
} from "../services/assistantTransport";
import type { EventMapItemDto } from "../contracts/map-feed.contracts";

export type AssistantLogSender = "user" | "assistant" | "system";

export interface AssistantChatLogEntry {
  readonly sender: AssistantLogSender;
  readonly text: string;
  /** Local HH:MM, presentation only. */
  readonly timestamp: string;
  /**
   * Present only on system entries that carry an AI event suggestion.
   * The suggestion is a reference, never a canonical event.
   */
  readonly suggestion?: AssistantSuggestedEvent;
}

export type AnimalSubTab = "DIALOGUE" | "CONSOLE";

export interface AssistantSuggestionOutcome {
  readonly kind: "resolved" | "unresolved";
  readonly eventId: string | null;
}

export type AssistantTransportFn = (
  input: AssistantChatInput,
) => Promise<AssistantChatResult>;

function formatTimestamp(date: Date): string {
  return date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

function nowTimestamp(): string {
  return formatTimestamp(new Date());
}

const INITIAL_GREETING =
  "RUFF! Active Duty Operator! Spike online and connected to WeUP Signal Grid. State coordinates calibrated. Ask me about local hot spots or calibrate the solar radar directly!";

/**
 * Suggestion chips from the source panel. They only fill the input box —
 * sending them still goes through the live Gemini transport.
 */
export const ASSISTANT_SUGGESTION_PROMPTS = [
  { icon: "auto_awesome", label: "Suggest Hotspots", prompt: "🐶 Spike, suggest concrete dynamic nightlife near me!" },
  { icon: "tune", label: "Calibrate Radar", prompt: "🚗 Spike, calibrate the radar system range!" },
  { icon: "explore", label: "Find Rooftops", prompt: "🔮 Scan Sector 4 and check for Rooftops" },
] as const;

export const ASSISTANT_CATEGORIES = [
  "nightlife",
  "lounge",
  "concert",
  "private",
  "restaurant",
  "rooftop",
  "startup",
] as const;

function normalizeForMatch(value: string): string {
  return value.trim().toLowerCase();
}

/**
 * Resolve an AI suggestion to a canonical eventId. Match on normalized
 * title equality, or on title+venue equality. Returns null when the
 * suggestion does not correspond to any known canonical event — the caller
 * must render that honestly rather than fabricate an event.
 */
export function resolveSuggestionToCanonicalId(
  suggestion: AssistantSuggestedEvent,
  items: readonly EventMapItemDto[],
): string | null {
  const title = normalizeForMatch(suggestion.title);
  const venue = normalizeForMatch(suggestion.venue_name);

  const titleMatch = items.find(
    (item) => normalizeForMatch(item.title) === title,
  );
  if (titleMatch) {
    return titleMatch.eventId;
  }

  const venueMatch = items.find(
    (item) =>
      normalizeForMatch(item.title) === title &&
      normalizeForMatch(item.venueName) === venue,
  );
  return venueMatch ? venueMatch.eventId : null;
}

export interface UseAssistantSessionOptions {
  /** Live transport. Inject a mock in tests; production passes postAssistantChat. */
  transport: AssistantTransportFn;
}

export function useAssistantSession(options: UseAssistantSessionOptions) {
  const { transport } = options;

  const activePersona = ref<AssistantPersona>("animal");
  const animalSubTab = ref<AnimalSubTab>("DIALOGUE");
  const inputText = ref("");
  const chatLog = ref<AssistantChatLogEntry[]>([
    { sender: "assistant", text: INITIAL_GREETING, timestamp: "09:44" },
  ]);
  const isThinking = ref(false);
  const isSpeaking = ref(false);

  // ── Calibration deck (auto / animal unified console) ────────────────────
  const signalIntensity = ref(82);
  const pulseRate = ref(5.7); // Hz
  const coreTemp = ref(41.2); // Celsius
  const calibrationActive = ref(false);

  // ── Scan context (assistant session only; the canonical feed owns its ──
  //    own filters. radius is passed to the Gemini payload as source context.)
  const scanRadiusKm = ref(5);

  // Latest AI suggestion awaiting operator action. Single-flight by design:
  // a new assistant reply replaces the previous pending suggestion.
  const pendingSuggestion = ref<AssistantSuggestedEvent | null>(null);

  const canSend = computed(
    () => inputText.value.trim().length > 0 && !isThinking.value,
  );

  function appendLog(entry: Omit<AssistantChatLogEntry, "timestamp">): void {
    chatLog.value = [...chatLog.value, { ...entry, timestamp: nowTimestamp() }];
  }

  function setPersona(persona: AssistantPersona): void {
    activePersona.value = persona;
  }

  function setAnimalSubTab(tab: AnimalSubTab): void {
    animalSubTab.value = tab;
  }

  function fillInputFromSuggestion(prompt: string): void {
    inputText.value = prompt;
  }

  /**
   * Source behavior: recalibration bumps the deck stats, appends a system
   * alert, and releases the calibration lock after 1500ms (terminating).
   */
  function calibrate(): void {
    if (calibrationActive.value) {
      return;
    }
    calibrationActive.value = true;
    signalIntensity.value = 100;
    pulseRate.value = 9.8;
    coreTemp.value = 38.4;
    appendLog({
      sender: "system",
      text: `SYSTEM_ALERT: Core radar recalibrated. Frequency locked at 9.8Hz. Range index: ${scanRadiusKm.value}km.`,
    });
    setTimeout(() => {
      calibrationActive.value = false;
    }, 1500);
  }

  /**
   * Source behavior: the animal persona's BARK_FEEDBACK is a local feedback
   * loop, not a Gemini turn. It appends a local assistant entry only — it
   * never pretends to route through the server.
   */
  function barkFeedback(): void {
    appendLog({
      sender: "assistant",
      text: "RUFF! BARK! *Spike panting enthusiastically* Let's track some grid vibrations!",
    });
    isSpeaking.value = true;
    setTimeout(() => {
      isSpeaking.value = false;
    }, 2000);
  }

  function buildHistory(): AssistantChatHistoryTurn[] {
    // System entries are session telemetry, not dialogue — they are
    // excluded from the model history (the source mapped everything
    // non-user to "model"; excluding system noise is stricter).
    return chatLog.value
      .filter((entry) => entry.sender !== "system")
      .slice(-5)
      .map((entry) => ({
        role: entry.sender === "user" ? "user" : ("model" as const),
        parts: [{ text: entry.text }],
      }));
  }

  /**
   * Send the current input through the live Gemini transport.
   *
   * Never fakes a reply: on transport failure the thread gains an honest
   * system error entry and no assistant message. The send path stays live
   * — the input clears only after a real attempt.
   */
  async function sendMessage(center: {
    readonly lat: number;
    readonly lng: number;
  }): Promise<void> {
    const message = inputText.value.trim();
    if (message.length === 0 || isThinking.value) {
      return;
    }

    inputText.value = "";
    appendLog({ sender: "user", text: message });
    isThinking.value = true;
    isSpeaking.value = false;

    try {
      const result = await transport({
        message,
        persona: activePersona.value,
        center,
        radius: scanRadiusKm.value,
        history: buildHistory(),
      });

      isThinking.value = false;
      isSpeaking.value = true;

      appendLog({ sender: "assistant", text: result.text });

      if (result.suggestedEvent) {
        pendingSuggestion.value = result.suggestedEvent;
        appendLog({
          sender: "system",
          text: `SIGNAL_FOUND: Detected event "${result.suggestedEvent.title}" at ${result.suggestedEvent.venue_name}. Tap the suggestion card to resolve it against the live feed.`,
          suggestion: result.suggestedEvent,
        });
      }

      setTimeout(() => {
        isSpeaking.value = false;
      }, 3000);
    } catch (cause) {
      isThinking.value = false;
      const reason =
        cause instanceof Error && cause.message ? cause.message : "Timeout";
      // Honest failure: no offline cache exists, so none is claimed.
      appendLog({
        sender: "system",
        text: `ROUTING_RETRY: Assistant request failed: ${reason}. No cached fallback available — retry when the route is reachable.`,
      });
    }
  }

  /**
   * Operator tapped the pending suggestion card. Resolve it against the
   * canonical feed items; a resolved id is handed to discovery.selectEvent
   * by the caller. An unresolved suggestion is reported honestly — it is
   * never injected as an event.
   */
  function resolvePendingSuggestion(
    items: readonly EventMapItemDto[],
  ): AssistantSuggestionOutcome {
    const suggestion = pendingSuggestion.value;
    if (!suggestion) {
      return { kind: "unresolved", eventId: null };
    }

    const eventId = resolveSuggestionToCanonicalId(suggestion, items);
    if (eventId) {
      appendLog({
        sender: "system",
        text: `SIGNAL_LOCK: "${suggestion.title}" resolved to a live event. Opening event detail.`,
      });
      pendingSuggestion.value = null;
      return { kind: "resolved", eventId };
    }

    appendLog({
      sender: "system",
      text: `SIGNAL_UNRESOLVED: "${suggestion.title}" has no match in the current feed. The suggestion stays a reference — it is not a confirmed event.`,
    });
    return { kind: "unresolved", eventId: null };
  }

  function dismissSuggestion(): void {
    pendingSuggestion.value = null;
  }

  return {
    activePersona,
    animalSubTab,
    inputText,
    chatLog,
    isThinking,
    isSpeaking,
    signalIntensity,
    pulseRate,
    coreTemp,
    calibrationActive,
    scanRadiusKm,
    pendingSuggestion,
    canSend,
    setPersona,
    setAnimalSubTab,
    fillInputFromSuggestion,
    calibrate,
    barkFeedback,
    sendMessage,
    resolvePendingSuggestion,
    dismissSuggestion,
  };
}
