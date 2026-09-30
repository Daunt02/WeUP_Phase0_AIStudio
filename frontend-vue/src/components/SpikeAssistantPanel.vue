<!--
  WEUP-SYNTH:
  source=components/SpikeAssistantPanel.tsx
  destination=frontend-vue/src/components/SpikeAssistantPanel.vue
  mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
  notes=Server-side Gemini only (services/assistantTransport.ts posts to the
    canonical /api/gemini route; no key in browser code). AI suggestions are
    selection references only — resolvePendingSuggestion matches them against
    canonical feed items and emits select-event(eventId); nothing is written
    to event, saved, or temporal stores. Persona state is UI-only. The
    decorative avatar blink loop from the source is dropped per the G4 motion
    rules (only state-bearing, terminating motion); the BARK_FEEDBACK local
    feedback loop and calibration console are retained as observable behavior.
-->
<template>
  <Transition name="assistant-backdrop">
    <div
      v-if="modelValue"
      class="assistant-backdrop"
      @click="emit('update:modelValue', false)"
      aria-hidden="true"
    />
  </Transition>

  <Transition name="assistant-panel">
    <!-- WEUP-2.5D (D13): Z4 command plane — the operational command surface,
         visually unmistakable as authority, not content. data-state reflects
         the EXISTING session execution state only (isThinking); no new
         execution paths are created here. -->
    <section
      v-if="modelValue"
      class="assistant-panel"
      data-plane="z4"
      :data-state="session.isThinking.value ? 'actionable' : 'visible'"
      role="dialog"
      aria-modal="true"
      aria-label="WeUP co-pilot assistant"
    >
      <div class="assistant-glow" aria-hidden="true" />

      <!-- HEADER BANNER -->
      <header class="assistant-header">
        <div class="assistant-title-row">
          <div class="assistant-mark">
            <q-icon name="smart_toy" class="assistant-mark-icon" />
          </div>
          <div>
            <h2 class="assistant-title">
              WeUP co-pilot assistant
              <span class="assistant-version">Spike_V0.9</span>
            </h2>
            <p class="assistant-subtitle">
              Active radar coordinate scanner &amp; concierge controller
            </p>
          </div>
        </div>

        <div
          class="persona-row"
          role="tablist"
          aria-label="Assistant persona"
        >
          <button
            type="button"
            role="tab"
            :aria-selected="session.activePersona.value === 'auto'"
            class="persona-chip"
            :class="{ 'is-active': session.activePersona.value === 'auto' }"
            @click="session.setPersona('auto')"
          >
            <q-icon name="directions_car" class="persona-icon" />
            miniAuto
          </button>
          <button
            type="button"
            role="tab"
            :aria-selected="session.activePersona.value === 'concierge'"
            class="persona-chip"
            :class="{ 'is-active': session.activePersona.value === 'concierge' }"
            @click="session.setPersona('concierge')"
          >
            <q-icon name="support_agent" class="persona-icon" />
            miniConcierge
          </button>
          <button
            type="button"
            role="tab"
            :aria-selected="session.activePersona.value === 'animal'"
            class="persona-chip persona-chip-animal"
            :class="{ 'is-active': session.activePersona.value === 'animal' }"
            @click="session.setPersona('animal')"
          >
            <q-icon name="pets" class="persona-icon" />
            miniAnimal
          </button>
        </div>

        <button
          type="button"
          class="assistant-close"
          aria-label="Close assistant"
          @click="emit('update:modelValue', false)"
        >
          <q-icon name="close" />
        </button>
      </header>

      <div class="assistant-body">
        <!-- LEFT: calibration deck (auto + animal personas) -->
        <aside
          v-if="showDeck"
          class="assistant-deck"
          :class="{ 'deck-mobile-hidden': session.activePersona.value === 'animal' && session.animalSubTab.value === 'DIALOGUE' }"
          aria-label="Calibration console"
        >
          <div class="deck-scroll">
            <div class="deck-block">
              <div class="deck-row-label">
                <span>Calibration command</span>
                <span class="deck-accent">{{ session.signalIntensity.value }}% lock</span>
              </div>
              <div class="deck-card deck-card-row">
                <div class="deck-card-lead">
                  <q-icon name="tune" class="deck-icon" />
                  <div>
                    <p class="deck-card-title">Grid sync optimization</p>
                    <p class="deck-card-meta">
                      Current: {{ session.pulseRate.value }}Hz // Temp:
                      {{ session.coreTemp.value }}°C
                    </p>
                  </div>
                </div>
                <button
                  type="button"
                  class="deck-sync"
                  :disabled="session.calibrationActive.value"
                  @click="session.calibrate()"
                >
                  <q-icon
                    name="refresh"
                    :class="{ 'is-spinning': session.calibrationActive.value }"
                  />
                  Sync
                </button>
              </div>
            </div>

            <div class="deck-block">
              <div class="deck-row-label">
                <span>Sector scan range</span>
                <span class="deck-accent">{{ session.scanRadiusKm.value }} km</span>
              </div>
              <div class="deck-card">
                <div class="range-ends">
                  <span>Close range</span>
                  <span>Metro wide</span>
                </div>
                <input
                  v-model.number="session.scanRadiusKm.value"
                  type="range"
                  min="1"
                  max="20"
                  step="1"
                  class="range-input"
                  aria-label="Scan range in kilometers"
                />
                <p class="deck-footnote">
                  *Adjusts the radar scanning field bounds. Higher range
                  increases processing latency.
                </p>
              </div>
            </div>

            <div class="deck-block">
              <div class="deck-row-label">
                <span>Signal frequencies filter</span>
                <span>{{ activeCategories.length }} locked</span>
              </div>
              <div class="category-chips">
                <button
                  v-for="category in ASSISTANT_CATEGORIES"
                  :key="category"
                  type="button"
                  class="category-chip"
                  :class="{ 'is-active': activeCategories.includes(category) }"
                  :aria-pressed="activeCategories.includes(category)"
                  @click="emit('toggle-category', category)"
                >
                  <span
                    class="category-dot"
                    :class="{ 'is-active': activeCategories.includes(category) }"
                    aria-hidden="true"
                  />
                  {{ category }}
                </button>
              </div>
            </div>

            <div class="deck-metrics">
              <div class="metric">
                <span class="metric-label">WLLS rhythm level</span>
                <p class="metric-value">
                  <q-icon name="wifi" class="metric-icon metric-icon-live" />
                  Stable_flux
                </p>
              </div>
              <div class="metric">
                <span class="metric-label">Energy feed rate</span>
                <p class="metric-value">
                  <q-icon name="bolt" class="metric-icon metric-icon-energy" />
                  974.2 GigaHz
                </p>
              </div>
            </div>
          </div>

          <p class="deck-footer">
            Operator diagnostic panel v0.9 // System variables fully synced
            with primary telemetry plane.
          </p>
        </aside>

        <!-- RIGHT: dialogue console -->
        <div
          class="assistant-dialogue"
          :class="{
            'dialogue-full': session.activePersona.value === 'auto',
            'dialogue-mobile-console': session.activePersona.value === 'animal' && session.animalSubTab.value === 'CONSOLE',
          }"
        >
          <!-- Mobile sub-tabs for the unified animal persona -->
          <div
            v-if="session.activePersona.value === 'animal'"
            class="animal-subtabs"
            role="tablist"
            aria-label="Animal persona view"
          >
            <button
              type="button"
              role="tab"
              :aria-selected="session.animalSubTab.value === 'DIALOGUE'"
              class="animal-subtab"
              :class="{ 'is-active': session.animalSubTab.value === 'DIALOGUE' }"
              @click="session.setAnimalSubTab('DIALOGUE')"
            >
              Dialogue chat
            </button>
            <button
              type="button"
              role="tab"
              :aria-selected="session.animalSubTab.value === 'CONSOLE'"
              class="animal-subtab"
              :class="{ 'is-active': session.animalSubTab.value === 'CONSOLE' }"
              @click="session.setAnimalSubTab('CONSOLE')"
            >
              Calibration console
            </button>
          </div>

          <!-- Visual plane strip -->
          <div class="visual-plane">
            <div class="visual-plane-label">
              <span class="visual-plane-key">A.I_Visualplane:</span>
              <span class="visual-plane-name">{{ visualPlaneName }}</span>
            </div>
            <button
              v-if="session.activePersona.value === 'animal'"
              type="button"
              class="bark-button"
              @click="session.barkFeedback()"
            >
              Bark_feedback
            </button>
          </div>

          <!-- Persona avatar stage -->
          <div class="avatar-stage" aria-hidden="true">
            <Transition name="avatar-swap" mode="out-in">
              <div
                v-if="session.activePersona.value === 'auto'"
                key="auto"
                class="avatar avatar-auto"
              >
                <svg class="avatar-svg" viewBox="0 0 100 100" fill="none">
                  <path
                    d="M 15,65 Q 15,55 25,50 L 35,38 Q 42,35 50,35 Q 58,35 65,38 L 75,50 Q 85,55 85,65 Q 85,73 75,75 L 25,75 Q 15,73 15,65 Z"
                    stroke="currentColor"
                    stroke-width="1.5"
                  />
                  <circle cx="33" cy="74" r="8" stroke="currentColor" stroke-width="1.5" />
                  <circle cx="67" cy="74" r="8" stroke="currentColor" stroke-width="1.5" />
                  <line x1="25" y1="58" x2="75" y2="58" stroke="currentColor" stroke-width="1" stroke-dasharray="3 3" />
                  <circle cx="50" cy="50" r="1.5" fill="currentColor" />
                </svg>
              </div>
              <div
                v-else-if="session.activePersona.value === 'concierge'"
                key="concierge"
                class="avatar avatar-concierge"
                :class="{ 'is-thinking': session.isThinking.value }"
              >
                <q-icon name="radar" class="concierge-orb" />
              </div>
              <div
                v-else
                key="animal"
                class="avatar avatar-animal"
                :class="{ 'is-speaking': session.isSpeaking.value }"
              >
                <svg class="avatar-svg" viewBox="0 0 100 100" fill="none" xmlns="http://www.w3.org/2000/svg">
                  <polygon points="15,40 25,20 40,35" fill="currentColor" fill-opacity="0.08" stroke="currentColor" stroke-width="1.5" />
                  <polygon points="85,40 75,20 60,35" fill="currentColor" fill-opacity="0.08" stroke="currentColor" stroke-width="1.5" />
                  <path d="M 25,45 Q 20,60 30,75 Q 50,85 70,75 Q 80,60 75,45 Q 50,38 25,45 Z" fill="#060606" stroke="currentColor" stroke-width="2" />
                  <path d="M 30,42 L 42,48 M 70,42 L 58,48" stroke="currentColor" stroke-width="1" stroke-opacity="0.4" />
                  <path d="M 33,60 Q 50,55 67,60 Q 72,70 65,77 Q 50,80 35,77 Q 28,70 33,60 Z" fill="#0b0b0b" stroke="currentColor" stroke-width="1.5" />
                  <path d="M 28,75 Q 50,88 72,75" stroke="#ff0055" stroke-width="2.5" />
                  <circle cx="36" cy="79" r="1" fill="#00FF9C" />
                  <circle cx="50" cy="82" r="1" fill="#00FF9C" />
                  <circle cx="64" cy="79" r="1" fill="#00FF9C" />
                  <circle cx="38" cy="50" r="3.5" fill="currentColor" />
                  <circle cx="62" cy="50" r="3.5" fill="currentColor" />
                  <circle cx="38" cy="50" r="6" stroke="currentColor" stroke-width="0.5" stroke-dasharray="2 1" />
                  <circle cx="62" cy="50" r="6" stroke="currentColor" stroke-width="0.5" stroke-dasharray="2 1" />
                  <polygon points="46,57 54,57 50,62" fill="currentColor" />
                  <path d="M 45,62 Q 50,66 55,62 M 50,62 L 50,72" stroke="currentColor" stroke-width="1" />
                </svg>
              </div>
            </Transition>
          </div>

          <!-- Chat thread -->
          <div ref="scrollRef" class="chat-log" role="log" aria-live="polite">
            <template v-for="(entry, index) in session.chatLog.value" :key="index">
              <div v-if="entry.sender === 'system'" class="log-system">
                <q-icon name="tune" class="log-system-icon" />
                <div class="log-system-body">
                  <p class="log-timestamp">{{ entry.timestamp }}</p>
                  <p class="log-system-text">{{ entry.text }}</p>
                </div>
              </div>
              <div v-else class="log-message" :class="{ 'is-self': entry.sender === 'user' }">
                <div class="log-avatar">
                  <q-icon :name="entry.sender === 'user' ? 'tune' : 'smart_toy'" />
                </div>
                <div class="log-content">
                  <div class="log-meta">
                    <span class="log-sender">{{ senderLabel(entry.sender) }}</span>
                    <span class="log-timestamp">{{ entry.timestamp }}</span>
                  </div>
                  <div class="log-bubble">{{ entry.text }}</div>
                </div>
              </div>
            </template>

            <div v-if="session.isThinking.value" class="log-thinking" aria-label="Assistant is thinking">
              <div class="log-avatar">
                <q-icon name="smart_toy" class="thinking-icon" />
              </div>
              <div class="thinking-bubble">
                <span class="thinking-dot" />
                <span class="thinking-dot" />
                <span class="thinking-dot" />
              </div>
            </div>
          </div>

          <!-- Pending suggestion card: actionable, labeled, never a dead chip -->
          <div v-if="session.pendingSuggestion.value" class="suggestion-wrap">
            <button
              type="button"
              class="suggestion-card"
              @click="onSuggestionTap"
            >
              <span class="suggestion-label">AI suggested — not a confirmed event</span>
              <span class="suggestion-title">{{ session.pendingSuggestion.value.title }}</span>
              <span class="suggestion-meta">
                {{ session.pendingSuggestion.value.venue_name }}
                · {{ session.pendingSuggestion.value.category }}
              </span>
              <span v-if="session.pendingSuggestion.value.description" class="suggestion-desc">
                {{ session.pendingSuggestion.value.description }}
              </span>
            </button>
            <button
              type="button"
              class="suggestion-dismiss"
              aria-label="Dismiss suggestion"
              @click="session.dismissSuggestion()"
            >
              <q-icon name="close" />
            </button>
          </div>

          <!-- Input bar -->
          <div class="input-bar">
            <div class="suggestion-chips">
              <button
                v-for="chip in ASSISTANT_SUGGESTION_PROMPTS"
                :key="chip.label"
                type="button"
                class="suggestion-chip"
                @click="session.fillInputFromSuggestion(chip.prompt)"
              >
                <q-icon :name="chip.icon" class="suggestion-chip-icon" />
                {{ chip.label }}
              </button>
            </div>

            <form class="input-form" @submit.prevent="onSend">
              <input
                v-model="session.inputText.value"
                type="text"
                class="input-field"
                :placeholder="
                  session.activePersona.value === 'auto'
                    ? 'System diagnostic tuning commands only...'
                    : 'Ask your WeUP co-pilot...'
                "
                :disabled="session.activePersona.value === 'auto' || session.isThinking.value"
                :aria-label="
                  session.activePersona.value === 'auto'
                    ? 'Diagnostic input disabled in auto persona'
                    : 'Message the co-pilot'
                "
              />
              <button
                type="submit"
                class="send-button"
                :disabled="!session.canSend.value || session.activePersona.value === 'auto'"
                aria-label="Send message"
              >
                <q-icon name="send" />
              </button>
            </form>
          </div>
        </div>
      </div>
    </section>
  </Transition>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from "vue";
import {
  ASSISTANT_CATEGORIES,
  ASSISTANT_SUGGESTION_PROMPTS,
  useAssistantSession,
  type AssistantLogSender,
} from "../composables/useAssistantSession";
import { postAssistantChat } from "../services/assistantTransport";
import type { EventMapItemDto } from "../contracts/map-feed.contracts";

interface MapCenter {
  readonly lat: number;
  readonly lng: number;
}

const props = defineProps<{
  modelValue: boolean;
  mapCenter: MapCenter;
  activeCategories: readonly string[];
  mapItems: readonly EventMapItemDto[];
}>();

const emit = defineEmits<{
  (event: "update:modelValue", visible: boolean): void;
  (event: "toggle-category", category: string): void;
  (event: "select-event", eventId: string): void;
}>();

const session = useAssistantSession({ transport: postAssistantChat });
const scrollRef = ref<HTMLElement | null>(null);

const showDeck = computed(
  () =>
    session.activePersona.value === "auto" ||
    session.activePersona.value === "animal",
);

const visualPlaneName = computed(() => {
  switch (session.activePersona.value) {
    case "auto":
      return "MINIAUTO_SCANNER_BLUEPRINT";
    case "concierge":
      return "MINICONCIERGE_ENERGY_WAVE";
    case "animal":
      return "MINIANIMAL_SPIKE_AVATAR";
  }
});

function senderLabel(sender: AssistantLogSender): string {
  if (sender === "user") {
    return "Operator";
  }
  return session.activePersona.value === "animal" ? "Spike_AI" : "Concierge_AI";
}

function scrollToBottom(): void {
  nextTick(() => {
    const el = scrollRef.value;
    if (el) {
      el.scrollTop = el.scrollHeight;
    }
  });
}

watch(
  () => [session.chatLog.value.length, session.isThinking.value],
  scrollToBottom,
);

function onSend(): void {
  void session.sendMessage(props.mapCenter).then(scrollToBottom);
}

/**
 * The suggestion card is always actionable: a canonical match drives the
 * shared selection path (EventDetailModal opens via App.vue wiring); an
 * unresolved suggestion appends an honest system entry inside the session —
 * never a dead chip, never a fabricated event.
 */
function onSuggestionTap(): void {
  const outcome = session.resolvePendingSuggestion(props.mapItems);
  scrollToBottom();
  if (outcome.kind === "resolved" && outcome.eventId) {
    emit("select-event", outcome.eventId);
  }
}

/**
 * WEUP-2.5D (D17): expose the thinking flag (read-only) so the shell's
 * interaction state machine can coordinate the Z4 command state. No new
 * execution path — the session remains the sole owner of its state.
 */
defineExpose({
  isAssistantThinking: computed(() => session.isThinking.value),
});
</script>

<style scoped>
/* ── Tokens (G4 UI system; accent inherited from the source grammar) ── */
.assistant-panel {
  --assistant-accent: #00ff9c;
  --assistant-ink: #ffffff;
  --assistant-dim: rgba(255, 255, 255, 0.6);
  --assistant-faint: rgba(255, 255, 255, 0.3);
  --assistant-line: rgba(255, 255, 255, 0.1);
  --assistant-card: rgba(255, 255, 255, 0.03);
}

/* ── Backdrop ── */
.assistant-backdrop {
  position: fixed;
  inset: 0;
  z-index: 49;
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(12px);
  -webkit-backdrop-filter: blur(12px);
}
.assistant-backdrop-enter-active,
.assistant-backdrop-leave-active {
  transition: opacity 0.3s ease;
}
.assistant-backdrop-enter-from,
.assistant-backdrop-leave-to {
  opacity: 0;
}

/* ── Panel frame ── */
/* WEUP-2.5D (D13): Z4 authority boundary — elevation.4 + command glow from
   the token system. The global [data-plane="z4"] rule agrees; this scoped
   rule carries the layout specifics. */
.assistant-panel {
  position: fixed;
  inset-inline: 0;
  bottom: 0;
  top: 8%;
  z-index: 50;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: rgba(5, 5, 5, 0.95);
  border-top: 1px solid var(--assistant-line);
  border-radius: 18px 18px 0 0;
  box-shadow: var(--weup-elevation-4), var(--weup-glow-command);
  color: var(--assistant-ink);
}
.assistant-panel-enter-active,
.assistant-panel-leave-active {
  transition:
    transform 0.4s ease,
    opacity 0.4s ease;
}
.assistant-panel-enter-from,
.assistant-panel-leave-to {
  transform: translateY(100%);
  opacity: 0;
}
@media (min-width: 768px) {
  .assistant-panel {
    inset-inline: auto;
    right: 0;
    top: 0;
    width: min(460px, 42vw);
    border-top: none;
    border-left: 1px solid var(--assistant-line);
    border-radius: 0;
  }
  .assistant-panel-enter-from,
  .assistant-panel-leave-to {
    transform: translateX(100%);
    opacity: 0;
  }
}

.assistant-glow {
  position: absolute;
  top: 0;
  inset-inline: 0;
  height: 2px;
  background: linear-gradient(
    to right,
    transparent,
    var(--assistant-accent),
    transparent
  );
  opacity: 0.45;
  pointer-events: none;
}

/* ── Header ── */
.assistant-header {
  position: relative;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 1rem;
  padding: 1.25rem 1.75rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  background: rgba(0, 0, 0, 0.4);
  flex-shrink: 0;
}
.assistant-title-row {
  display: flex;
  align-items: center;
  gap: 1rem;
}
.assistant-mark {
  width: 3rem;
  height: 3rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 1rem;
  background: rgba(0, 255, 156, 0.1);
  border: 1px solid rgba(0, 255, 156, 0.2);
}
.assistant-mark-icon {
  font-size: 1.5rem;
  color: var(--assistant-accent);
}
.assistant-title {
  margin: 0;
  font-size: 1.25rem;
  font-weight: 900;
  font-style: italic;
  text-transform: uppercase;
  letter-spacing: -0.02em;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}
.assistant-version {
  font-size: 0.5rem;
  font-style: normal;
  font-family: ui-monospace, monospace;
  padding: 0.125rem 0.5rem;
  border-radius: 0.375rem;
  background: rgba(0, 255, 156, 0.1);
  color: var(--assistant-accent);
  border: 1px solid rgba(0, 255, 156, 0.2);
  text-transform: uppercase;
}
.assistant-subtitle {
  margin: 0.25rem 0 0;
  font-size: 0.5rem;
  font-family: ui-monospace, monospace;
  color: var(--assistant-faint);
  letter-spacing: 0.2em;
  text-transform: uppercase;
}

.persona-row {
  display: flex;
  gap: 0.25rem;
  padding: 0.25rem;
  border-radius: 1rem;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid var(--assistant-line);
}
.persona-chip {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.5rem 1rem;
  border-radius: 0.75rem;
  border: 1px solid transparent;
  background: transparent;
  color: var(--assistant-dim);
  font-size: 0.5625rem;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.1em;
  cursor: pointer;
  transition:
    color 0.3s,
    background 0.3s,
    border-color 0.3s;
}
.persona-chip:hover {
  color: var(--assistant-ink);
}
.persona-chip.is-active {
  background: rgba(0, 255, 156, 0.1);
  color: var(--assistant-accent);
  border-color: rgba(0, 255, 156, 0.2);
}
.persona-chip-animal.is-active {
  background: var(--assistant-accent);
  color: #000;
  border-color: var(--assistant-accent);
}
.persona-icon {
  font-size: 0.875rem;
}

.assistant-close {
  position: absolute;
  right: 1.5rem;
  top: 1.5rem;
  width: 2.5rem;
  height: 2.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 9999px;
  border: none;
  background: rgba(255, 255, 255, 0.05);
  color: var(--assistant-dim);
  font-size: 1.25rem;
  cursor: pointer;
  transition: background 0.2s, color 0.2s;
}
.assistant-close:hover {
  background: rgba(255, 255, 255, 0.1);
  color: var(--assistant-ink);
}

/* ── Body layout ── */
.assistant-body {
  flex: 1;
  min-height: 0;
  display: grid;
  grid-template-columns: 1fr;
  overflow: hidden;
}
@media (min-width: 768px) {
  .assistant-body {
    grid-template-columns: 5fr 7fr;
  }
}

/* ── Deck ── */
.assistant-deck {
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  padding: 2rem;
  background: rgba(0, 0, 0, 0.2);
  border-right: 1px solid rgba(255, 255, 255, 0.05);
  min-height: 0;
}
@media (max-width: 767px) {
  .assistant-deck.deck-mobile-hidden {
    display: none;
  }
}
.deck-scroll {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  overflow-y: auto;
  min-height: 0;
  scrollbar-width: none;
}
.deck-scroll::-webkit-scrollbar {
  display: none;
}
.deck-block {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}
.deck-row-label {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.5625rem;
  font-family: ui-monospace, monospace;
  text-transform: uppercase;
  letter-spacing: 0.2em;
  color: var(--assistant-faint);
}
.deck-accent {
  color: var(--assistant-accent);
  font-weight: 900;
}
.deck-card {
  padding: 1.25rem;
  border-radius: 1.5rem;
  background: var(--assistant-card);
  border: 1px solid rgba(255, 255, 255, 0.05);
}
.deck-card-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}
.deck-card-lead {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}
.deck-icon {
  font-size: 1.25rem;
  color: var(--assistant-accent);
}
.deck-card-title {
  margin: 0;
  font-size: 0.75rem;
  font-weight: 700;
  text-transform: uppercase;
  font-style: italic;
}
.deck-card-meta {
  margin: 0.125rem 0 0;
  font-size: 0.5rem;
  font-family: ui-monospace, monospace;
  color: var(--assistant-faint);
}
.deck-sync {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  height: 2.5rem;
  padding: 0 1rem;
  border-radius: 0.75rem;
  border: 1px solid var(--assistant-line);
  background: rgba(255, 255, 255, 0.05);
  color: var(--assistant-ink);
  font-size: 0.5rem;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.15em;
  cursor: pointer;
  transition: transform 0.15s, background 0.2s;
}
.deck-sync:hover:not(:disabled) {
  background: rgba(255, 255, 255, 0.1);
}
.deck-sync:active:not(:disabled) {
  transform: scale(0.95);
}
.deck-sync:disabled {
  opacity: 0.7;
  cursor: default;
}
.is-spinning {
  animation: deck-spin 1.5s linear infinite;
  color: var(--assistant-accent);
}
@keyframes deck-spin {
  to {
    transform: rotate(360deg);
  }
}
.range-ends {
  display: flex;
  justify-content: space-between;
  font-size: 0.6875rem;
  font-family: ui-monospace, monospace;
  color: rgba(255, 255, 255, 0.5);
  margin-bottom: 0.75rem;
}
.range-input {
  width: 100%;
  accent-color: var(--assistant-accent);
  cursor: pointer;
}
.deck-footnote {
  margin: 0.75rem 0 0;
  font-size: 0.4375rem;
  font-family: ui-monospace, monospace;
  text-transform: uppercase;
  letter-spacing: 0.15em;
  line-height: 1.6;
  color: rgba(255, 255, 255, 0.2);
}
.category-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}
.category-chip {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  height: 2.25rem;
  padding: 0 1rem;
  border-radius: 9999px;
  border: 1px solid rgba(255, 255, 255, 0.05);
  background: rgba(255, 255, 255, 0.05);
  color: rgba(255, 255, 255, 0.6);
  font-size: 0.53125rem;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  cursor: pointer;
  transition: all 0.2s;
}
.category-chip:hover {
  background: rgba(255, 255, 255, 0.1);
}
.category-chip.is-active {
  background: var(--assistant-accent);
  border-color: var(--assistant-accent);
  color: #000;
  box-shadow: 0 0 15px rgba(0, 255, 156, 0.25);
}
.category-dot {
  width: 0.375rem;
  height: 0.375rem;
  border-radius: 9999px;
  background: rgba(255, 255, 255, 0.2);
}
.category-dot.is-active {
  background: #000;
}
.deck-metrics {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
  padding: 1.25rem;
  border-radius: 1.5rem;
  background: rgba(0, 255, 156, 0.02);
  border: 1px solid rgba(0, 255, 156, 0.1);
}
.metric-label {
  font-size: 0.46875rem;
  font-family: ui-monospace, monospace;
  text-transform: uppercase;
  letter-spacing: 0.15em;
  color: rgba(255, 255, 255, 0.2);
}
.metric-value {
  margin: 0.25rem 0 0;
  display: flex;
  align-items: center;
  gap: 0.375rem;
  font-size: 0.875rem;
  font-weight: 900;
  font-style: italic;
  text-transform: uppercase;
  letter-spacing: 0.1em;
}
.metric-icon {
  font-size: 0.875rem;
}
.metric-icon-live {
  color: var(--assistant-accent);
}
.metric-icon-energy {
  color: #facc15;
}
.deck-footer {
  margin: 1rem 0 0;
  padding-top: 1rem;
  border-top: 1px solid rgba(255, 255, 255, 0.05);
  font-size: 0.4375rem;
  font-family: ui-monospace, monospace;
  text-transform: uppercase;
  letter-spacing: 0.15em;
  line-height: 1.6;
  color: rgba(255, 255, 255, 0.2);
}
@media (max-width: 767px) {
  .deck-footer {
    display: none;
  }
}

/* ── Dialogue column ── */
.assistant-dialogue {
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  min-height: 0;
}
@media (min-width: 768px) {
  .assistant-dialogue {
    grid-column: 2;
  }
  .assistant-dialogue.dialogue-full {
    grid-column: 1 / -1;
  }
}
@media (max-width: 767px) {
  .assistant-dialogue.dialogue-mobile-console {
    display: none;
  }
}

.animal-subtabs {
  display: flex;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  background: rgba(0, 0, 0, 0.1);
  flex-shrink: 0;
}
@media (min-width: 768px) {
  .animal-subtabs {
    display: none;
  }
}
.animal-subtab {
  flex: 1;
  padding: 1rem;
  background: transparent;
  border: none;
  border-bottom: 2px solid transparent;
  color: var(--assistant-dim);
  font-size: 0.5625rem;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.15em;
  cursor: pointer;
}
.animal-subtab.is-active {
  color: var(--assistant-accent);
  border-bottom-color: var(--assistant-accent);
}

.visual-plane {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 1rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  background: rgba(0, 0, 0, 0.3);
  flex-shrink: 0;
}
.visual-plane-label {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}
.visual-plane-key {
  font-size: 0.5rem;
  font-family: ui-monospace, monospace;
  text-transform: uppercase;
  letter-spacing: 0.2em;
  color: var(--assistant-faint);
}
.visual-plane-name {
  font-size: 0.5625rem;
  font-weight: 900;
  font-style: italic;
  text-transform: uppercase;
  letter-spacing: 0.15em;
  color: var(--assistant-accent);
}
.bark-button {
  padding: 0.25rem 0.75rem;
  border-radius: 0.375rem;
  border: 1px solid rgba(0, 255, 156, 0.2);
  background: rgba(0, 255, 156, 0.1);
  color: var(--assistant-accent);
  font-size: 0.5rem;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.15em;
  cursor: pointer;
  transition: background 0.2s;
}
.bark-button:hover {
  background: rgba(0, 255, 156, 0.2);
}

/* ── Avatar stage ── */
.avatar-stage {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1.25rem 1.5rem;
  background: rgba(0, 0, 0, 0.4);
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  flex-shrink: 0;
}
.avatar {
  position: relative;
  width: 10rem;
  height: 10rem;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--assistant-accent);
  transition: transform 0.4s ease;
}
.avatar-swap-enter-active,
.avatar-swap-leave-active {
  transition:
    opacity 0.25s ease,
    transform 0.25s ease;
}
.avatar-swap-enter-from,
.avatar-swap-leave-to {
  opacity: 0;
  transform: scale(0.8);
}
.avatar-svg {
  width: 9rem;
  height: 9rem;
  filter: drop-shadow(0 0 15px rgba(0, 255, 156, 0.25));
}
.avatar-concierge {
  border-radius: 9999px;
  border: 1px solid rgba(0, 255, 156, 0.2);
  background: rgba(0, 255, 156, 0.1);
  box-shadow: 0 0 30px rgba(0, 255, 156, 0.2);
}
.avatar-concierge.is-thinking {
  border-color: rgba(0, 255, 156, 0.6);
  box-shadow: 0 0 45px rgba(0, 255, 156, 0.35);
}
.concierge-orb {
  font-size: 1.75rem;
  color: var(--assistant-accent);
}
.avatar-animal.is-speaking {
  transform: scale(1.04);
}

/* ── Chat thread ── */
.chat-log {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  background: rgba(0, 0, 0, 0.1);
  scrollbar-width: none;
}
.chat-log::-webkit-scrollbar {
  display: none;
}
.log-system {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 0.75rem;
  border-radius: 1rem;
  background: rgba(239, 68, 68, 0.05);
  border: 1px solid rgba(239, 68, 68, 0.1);
}
.log-system-icon {
  color: #ef4444;
  font-size: 1rem;
  flex-shrink: 0;
  margin-top: 0.125rem;
}
.log-system-text {
  margin: 0;
  font-size: 0.625rem;
  font-family: ui-monospace, monospace;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: #f87171;
  line-height: 1.5;
}
.log-message {
  display: flex;
  align-items: flex-start;
  gap: 1rem;
}
.log-message.is-self {
  flex-direction: row-reverse;
}
.log-avatar {
  width: 2rem;
  height: 2rem;
  border-radius: 0.75rem;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  border: 1px solid rgba(255, 255, 255, 0.1);
  background: rgba(255, 255, 255, 0.05);
  color: rgba(255, 255, 255, 0.5);
  font-size: 1rem;
}
.log-message.is-self .log-avatar {
  background: rgba(0, 255, 156, 0.1);
  border-color: rgba(0, 255, 156, 0.2);
  color: var(--assistant-accent);
}
.log-content {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  max-width: 80%;
}
.log-meta {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.log-message.is-self .log-meta {
  flex-direction: row-reverse;
}
.log-sender {
  font-size: 0.5rem;
  font-family: ui-monospace, monospace;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.15em;
  color: var(--assistant-faint);
}
.log-timestamp {
  margin: 0;
  font-size: 0.4375rem;
  font-family: ui-monospace, monospace;
  color: rgba(255, 255, 255, 0.15);
}
.log-bubble {
  padding: 1rem;
  border-radius: 1.5rem;
  font-size: 0.6875rem;
  line-height: 1.6;
  letter-spacing: 0.02em;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.05);
  color: rgba(255, 255, 255, 0.85);
}
.log-message.is-self .log-bubble {
  background: var(--assistant-accent);
  color: #000;
  font-weight: 600;
  border-top-right-radius: 0;
}
.log-message:not(.is-self) .log-bubble {
  border-top-left-radius: 0;
}
.log-thinking {
  display: flex;
  align-items: flex-start;
  gap: 1rem;
}
.thinking-icon {
  color: var(--assistant-accent);
  animation: deck-spin 1.2s linear infinite;
}
.thinking-bubble {
  display: flex;
  align-items: center;
  gap: 0.25rem;
  padding: 1rem;
  border-radius: 1.5rem;
  border-top-left-radius: 0;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.05);
}
.thinking-dot {
  width: 0.375rem;
  height: 0.375rem;
  border-radius: 9999px;
  background: var(--assistant-accent);
  animation: thinking-bounce 0.8s ease-in-out infinite;
}
.thinking-dot:nth-child(2) {
  animation-delay: 0.15s;
}
.thinking-dot:nth-child(3) {
  animation-delay: 0.3s;
}
@keyframes thinking-bounce {
  0%,
  100% {
    transform: translateY(0);
    opacity: 0.5;
  }
  50% {
    transform: translateY(-0.25rem);
    opacity: 1;
  }
}

/* ── Suggestion card ── */
.suggestion-wrap {
  display: flex;
  align-items: stretch;
  gap: 0.5rem;
  padding: 0 1.5rem;
  flex-shrink: 0;
}
.suggestion-card {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.25rem;
  text-align: left;
  padding: 0.875rem 1rem;
  border-radius: 1rem;
  border: 1px solid rgba(0, 255, 156, 0.25);
  background: rgba(0, 255, 156, 0.06);
  color: var(--assistant-ink);
  cursor: pointer;
  transition:
    background 0.2s,
    transform 0.15s;
}
.suggestion-card:hover {
  background: rgba(0, 255, 156, 0.12);
}
.suggestion-card:active {
  transform: scale(0.99);
}
.suggestion-label {
  font-size: 0.5rem;
  font-family: ui-monospace, monospace;
  text-transform: uppercase;
  letter-spacing: 0.15em;
  color: var(--assistant-accent);
}
.suggestion-title {
  font-size: 0.8125rem;
  font-weight: 800;
}
.suggestion-meta {
  font-size: 0.625rem;
  color: var(--assistant-dim);
}
.suggestion-desc {
  font-size: 0.625rem;
  color: rgba(255, 255, 255, 0.45);
  line-height: 1.5;
}
.suggestion-dismiss {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2.75rem;
  border-radius: 1rem;
  border: 1px solid var(--assistant-line);
  background: rgba(255, 255, 255, 0.05);
  color: var(--assistant-dim);
  font-size: 1rem;
  cursor: pointer;
}
.suggestion-dismiss:hover {
  color: var(--assistant-ink);
  background: rgba(255, 255, 255, 0.1);
}

/* ── Input bar ── */
.input-bar {
  padding: 1.5rem;
  border-top: 1px solid rgba(255, 255, 255, 0.05);
  background: rgba(0, 0, 0, 0.4);
  display: flex;
  flex-direction: column;
  gap: 1rem;
  flex-shrink: 0;
}
.suggestion-chips {
  display: flex;
  gap: 0.5rem;
  overflow-x: auto;
  padding-bottom: 0.25rem;
  scrollbar-width: none;
}
.suggestion-chips::-webkit-scrollbar {
  display: none;
}
.suggestion-chip {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: 0.25rem;
  height: 1.75rem;
  padding: 0 0.75rem;
  border-radius: 9999px;
  border: 1px solid rgba(255, 255, 255, 0.05);
  background: rgba(255, 255, 255, 0.05);
  color: rgba(255, 255, 255, 0.55);
  font-size: 0.5rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  cursor: pointer;
  transition: all 0.2s;
}
.suggestion-chip:hover {
  background: rgba(255, 255, 255, 0.1);
  border-color: rgba(255, 255, 255, 0.1);
}
.suggestion-chip-icon {
  font-size: 0.625rem;
  color: var(--assistant-accent);
}
.input-form {
  position: relative;
  display: flex;
  align-items: center;
}
.input-field {
  width: 100%;
  height: 3.5rem;
  padding: 0 3.5rem 0 1.25rem;
  border-radius: 1rem;
  border: 1px solid var(--assistant-line);
  background: rgba(255, 255, 255, 0.05);
  color: var(--assistant-ink);
  font-size: 0.75rem;
  font-family: ui-monospace, monospace;
  outline: none;
  transition: border-color 0.2s;
}
.input-field::placeholder {
  color: rgba(255, 255, 255, 0.2);
}
.input-field:focus {
  border-color: var(--assistant-accent);
}
.input-field:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
.send-button {
  position: absolute;
  right: 0.75rem;
  width: 2.5rem;
  height: 2.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 0.75rem;
  border: none;
  background: var(--assistant-accent);
  color: #000;
  font-size: 1rem;
  cursor: pointer;
  transition:
    transform 0.15s,
    opacity 0.2s;
}
.send-button:hover:not(:disabled) {
  transform: scale(1.05);
}
.send-button:active:not(:disabled) {
  transform: scale(0.95);
}
.send-button:disabled {
  opacity: 0.3;
  cursor: default;
}

/* ── Reduced motion: terminate all transitions/animations ── */
@media (prefers-reduced-motion: reduce) {
  .assistant-backdrop-enter-active,
  .assistant-backdrop-leave-active,
  .assistant-panel-enter-active,
  .assistant-panel-leave-active,
  .avatar-swap-enter-active,
  .avatar-swap-leave-active {
    transition: none;
  }
  .is-spinning,
  .thinking-icon,
  .thinking-dot {
    animation: none;
  }
}
</style>
