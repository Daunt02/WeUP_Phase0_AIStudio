<!--
  WEUP-SYNTH (G7 — Temporal):
  sources=[components/TimelineControl.tsx, components/CulturalCalendar.tsx]
  destination=frontend-vue/src/components/TemporalNavigationControls.vue
  mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
  notes=TimelineControl merged: hold-to-scrub glass pill with label arbitration
  (NOW/6PM/9PM/MIDNIGHT/3AM/FRI/SAT/SUN) and the six-mode inventory
  (NOW/TODAY/TONIGHT/TOMORROW/WEEKEND/CUSTOM). Every gesture drives the single
  temporal authority (useTemporalNavigation singleton); scrub/timeline
  interactions are real state transitions, never decorative animation.
  Pre-existing preset/step/scrub-track behavior is preserved and now shares
  the same authority.
-->
<template>
  <div class="temporal-navigation-controls">
    <!-- Container: responsive for mobile and desktop -->
    <q-card flat bordered class="controls-card">
      <q-card-section class="controls-section">
        <!-- WEUP-SYNTH (TimelineControl): hold-to-scrub glass pill -->
        <div class="scrub-pill-wrap">
          <div
            ref="pillRef"
            class="scrub-pill"
            :class="{ holding: isHolding }"
            role="slider"
            tabindex="0"
            aria-label="Temporal scrub control"
            :aria-valuetext="scrubReadout"
            @pointerdown="onPillPointerDown"
            @pointermove="onPillPointerMove"
            @pointerup="onPillPointerUp"
            @pointercancel="onPillPointerUp"
            @keydown.left.prevent="onPillKeyStep(-1)"
            @keydown.right.prevent="onPillKeyStep(1)"
          >
            <div class="pill-identity">
              <q-icon
                name="schedule"
                size="sm"
                class="pill-clock"
                :class="{ holding: isHolding }"
              />
              <div class="pill-labels">
                <span class="pill-state">{{
                  isHolding ? "EXPLORING FUTURE" : modeReadout
                }}</span>
                <span class="pill-time">{{ scrubReadout }}</span>
              </div>
            </div>

            <div v-if="isHolding" class="pill-scrub-labels">
              <span
                v-for="(label, index) in scrubLabels"
                :key="label"
                class="pill-scrub-label"
                :class="{ active: label === activeScrubLabel }"
                :style="{ opacity: labelOpacity(index) }"
              >
                {{ label }}
              </span>
            </div>

            <div v-if="isHolding" class="pill-progress-track">
              <div
                class="pill-progress"
                :style="{ width: pillProgressWidth }"
              ></div>
            </div>
          </div>
          <span v-if="!isHolding" class="pill-hint"
            >Hold + Slide to Scrub Time</span
          >
        </div>

        <!-- Mode buttons row: the six canonical temporal modes -->
        <div class="presets-row">
          <q-btn
            v-for="mode in modeOptions"
            :key="mode.value"
            :label="mode.label"
            :outline="mode.value !== activeMode"
            :unelevated="mode.value === activeMode"
            :color="mode.value === activeMode ? 'primary' : 'grey-8'"
            size="sm"
            class="preset-btn"
            @click="onSelectMode(mode.value)"
            :aria-pressed="mode.value === activeMode"
            :aria-label="`Navigate to ${mode.label}`"
          />
        </div>

        <!-- Temporal status display -->
        <div class="temporal-status">
          <div class="status-text">
            <span v-if="isValid" class="status-info">
              <q-icon name="schedule" size="sm" />
              {{ statusMessage }}
            </span>
            <span v-else class="status-error">
              <q-icon name="warning" size="sm" />
              {{ validationMessage }}
            </span>
          </div>

          <!-- Pending indicator -->
          <q-spinner
            v-if="hasPendingTemporalChange"
            color="primary"
            size="sm"
            class="pending-spinner"
          />
        </div>

        <!-- Date stepping controls -->
        <div class="stepping-row">
          <q-btn
            icon="navigate_before"
            flat
            dense
            size="md"
            class="step-btn"
            @click="onStepBackward"
            aria-label="Step backward 1 day"
          />

          <div class="step-display">
            <span v-if="hasDateOffset" class="offset-badge">
              {{ formatDateOffset }}
            </span>
            <span v-else class="status-normal">Today</span>
          </div>

          <q-btn
            icon="navigate_next"
            flat
            dense
            size="md"
            class="step-btn"
            @click="onStepForward"
            aria-label="Step forward 1 day"
          />
        </div>

        <!-- Timeline scrubber -->
        <div class="scrubber-section">
          <div
            class="scrubber-track"
            @mousedown="onScrubberMouseDown"
            @touchstart="onScrubberTouchStart"
            @click="onScrubberClick"
            role="slider"
            :aria-valuenow="Math.round(scrubPosition * 100)"
            :aria-valuemin="0"
            :aria-valuemax="100"
            :aria-label="currentPreset"
            :aria-disabled="!isValid"
          >
            <!-- Background track -->
            <div class="track-bg"></div>

            <!-- Progress fill -->
            <div
              class="track-progress"
              :style="{ width: `${scrubPosition * 100}%` }"
            ></div>

            <!-- Scrub handle -->
            <div
              class="scrub-handle"
              :class="{ active: isScrubbingActive }"
              :style="{ left: `${scrubPosition * 100}%` }"
              @mousedown.stop="onHandleMouseDown"
              @touchstart.stop="onHandleTouchStart"
            >
              <div class="handle-inner"></div>
              <div v-if="isScrubbingActive" class="scrub-tooltip">
                {{ formatScrubPosition }}
              </div>
            </div>

            <!-- Time markers (optional visual guides) -->
            <div class="time-markers">
              <span class="marker" style="left: 0%">Start</span>
              <span class="marker" style="left: 50%">Mid</span>
              <span class="marker" style="left: 100%">End</span>
            </div>
          </div>
        </div>

        <!-- Reset button -->
        <div class="reset-row">
          <q-btn
            label="Reset to Now"
            flat
            dense
            size="sm"
            color="negative"
            class="reset-btn"
            @click="onReset"
            aria-label="Reset temporal navigation to current time"
          />
        </div>

        <!-- Debug info (development only) -->
        <div v-if="isDevelopment" class="debug-info">
          <div class="debug-item">
            <span class="debug-label">Request Signature:</span>
            <span class="debug-value">{{ requestSignaturePreview }}</span>
          </div>
          <div class="debug-item">
            <span class="debug-label">Step Offset:</span>
            <span class="debug-value">{{ stepOffset }}</span>
          </div>
          <div class="debug-item">
            <span class="debug-label">Scrub Position:</span>
            <span class="debug-value"
              >{{ (scrubPosition * 100).toFixed(1) }}%</span
            >
          </div>
        </div>
      </q-card-section>
    </q-card>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import {
  TIMELINE_SCRUB_LABELS,
  type TemporalMode,
  type TimelineScrubLabel,
  type UseTemporalNavigation,
} from "../composables/useTemporalNavigation";

const isDevelopment = import.meta.env.DEV;

interface TemporalNavigationControlsProps {
  temporal: UseTemporalNavigation;
}

const props = defineProps<TemporalNavigationControlsProps>();

// WEUP-SYNTH (G7 — TimelineControl): the six canonical temporal modes.
// TODAY is a Houston-day CustomRange; the rest map 1:1 to backend presets.
const modeOptions: ReadonlyArray<{ value: TemporalMode; label: string }> = [
  { value: "NOW", label: "Now" },
  { value: "TODAY", label: "Today" },
  { value: "TONIGHT", label: "Tonight" },
  { value: "TOMORROW", label: "Tomorrow" },
  { value: "WEEKEND", label: "Weekend" },
  { value: "CUSTOM", label: "Custom" },
];

const scrubLabels = TIMELINE_SCRUB_LABELS;

// Scrubbing state (linear track)
const isScrubbing = ref(false);

// WEUP-SYNTH (G7 — TimelineControl): hold-to-scrub pill state
const pillRef = ref<HTMLElement | null>(null);
const isHolding = ref(false);
const dragX = ref(0);
const activeScrubLabel = ref<TimelineScrubLabel | null>(null);
const DRAG_LIMIT = 150;

// Computed properties from temporal state
const activeMode = computed<TemporalMode>(
  () => props.temporal.activeTemporalMode.value,
);
const currentPreset = computed(() => props.temporal.preset.value);
const scrubPosition = computed(() => props.temporal.scrubPosition.value);
const isScrubbingActive = computed(
  () => props.temporal.isScrubbingActive.value,
);
const stepOffset = computed(() => props.temporal.stepOffset.value);
const isValid = computed(() => props.temporal.isValid.value);
const validationMessage = computed(
  () => props.temporal.validationMessage.value,
);
const hasPendingTemporalChange = computed(
  () => props.temporal.hasPendingTemporalChange.value,
);
const requestSignature = computed(() => props.temporal.requestSignature.value);

// Format display values
const hasDateOffset = computed(() => stepOffset.value !== 0);
const formatDateOffset = computed(() => {
  const days = stepOffset.value;
  if (days === 0) return "Today";
  if (days === 1) return "Tomorrow";
  if (days === -1) return "Yesterday";
  const direction = days > 0 ? "+" : "";
  return `${direction}${days}d`;
});

const formatScrubPosition = computed(() => {
  const pct = Math.round(scrubPosition.value * 100);
  if (pct <= 25) return "Earlier";
  if (pct <= 50) return "Early";
  if (pct <= 75) return "Late";
  return "Latest";
});

const requestSignaturePreview = computed(() => {
  const sig = requestSignature.value;
  if (!sig) return "—";
  try {
    const parsed = JSON.parse(sig);
    return JSON.stringify(parsed, null, 0).substring(0, 60) + "...";
  } catch {
    return sig.substring(0, 60) + "...";
  }
});

const statusMessage = computed(() => {
  const mode = activeMode.value;
  const offset = stepOffset.value;
  const dayKey = props.temporal.activeDayKey.value;

  let base: string;
  switch (mode) {
    case "NOW":
      base = "Now";
      break;
    case "TODAY":
      base = dayKey ? `Today (${dayKey})` : "Today";
      break;
    case "TONIGHT":
      base = "Tonight";
      break;
    case "TOMORROW":
      base = "Tomorrow";
      break;
    case "WEEKEND":
      base = "Weekend";
      break;
    case "CUSTOM":
      base = dayKey ? `Custom (${dayKey})` : "Custom Range";
      break;
  }

  if (offset !== 0) {
    const suffix = offset > 0 ? `+${offset}d` : `${offset}d`;
    return `${base} ${suffix}`;
  }

  return base;
});

/**
 * WEUP-SYNTH (G7 — TimelineControl): pill readout state.
 * modeReadout names the active mode; scrubReadout shows the live scrub label
 * while holding, else the Tonight cursor label, else the mode.
 */
const modeReadout = computed(() => {
  const mode = activeMode.value;
  const dayKey = props.temporal.activeDayKey.value;
  if ((mode === "TODAY" || mode === "CUSTOM") && dayKey) {
    return dayKey;
  }
  return mode;
});

const scrubReadout = computed(() => {
  if (isHolding.value && activeScrubLabel.value) {
    return activeScrubLabel.value;
  }
  if (
    activeMode.value === "TONIGHT" &&
    props.temporal.scrubCursorLabel.value
  ) {
    return props.temporal.scrubCursorLabel.value as string;
  }
  return modeReadout.value;
});

// Event handlers

function onSelectMode(mode: TemporalMode): void {
  props.temporal.selectTemporalMode(mode);
}

/**
 * WEUP-SYNTH (G7 — TimelineControl): hold-to-scrub gesture. Press-and-hold
 * reveals the scrub labels; sliding maps pointer X to the nearest label and
 * every label change is a real temporal transition via scrubToLabel. Release
 * springs the drag offset back (finite, state-driven motion).
 */
function onPillPointerDown(event: PointerEvent): void {
  isHolding.value = true;
  dragX.value = 0;
  activeScrubLabel.value = null;
  try {
    pillRef.value?.setPointerCapture(event.pointerId);
  } catch {
    // Pointer capture is best-effort (jsdom / older engines).
  }
  event.preventDefault();
}

function onPillPointerMove(event: PointerEvent): void {
  if (!isHolding.value || !pillRef.value) {
    return;
  }
  const rect = pillRef.value.getBoundingClientRect();
  const centerX = rect.left + rect.width / 2;
  const delta = Math.max(
    -DRAG_LIMIT,
    Math.min(DRAG_LIMIT, event.clientX - centerX),
  );
  dragX.value = delta;
  const index = Math.round(
    ((delta + DRAG_LIMIT) / (2 * DRAG_LIMIT)) * (scrubLabels.length - 1),
  );
  const label =
    scrubLabels[Math.max(0, Math.min(scrubLabels.length - 1, index))];
  if (label !== activeScrubLabel.value) {
    activeScrubLabel.value = label;
    props.temporal.scrubToLabel(label);
  }
}

function onPillPointerUp(event: PointerEvent): void {
  if (!isHolding.value) {
    return;
  }
  isHolding.value = false;
  dragX.value = 0;
  activeScrubLabel.value = null;
  try {
    pillRef.value?.releasePointerCapture(event.pointerId);
  } catch {
    // Best-effort.
  }
}

/** Keyboard alternative to the hold-and-slide gesture. */
function onPillKeyStep(direction: -1 | 1): void {
  const current = activeScrubLabel.value
    ? scrubLabels.indexOf(activeScrubLabel.value)
    : direction > 0
      ? -1
      : scrubLabels.length;
  const next = Math.max(
    0,
    Math.min(scrubLabels.length - 1, current + direction),
  );
  const label = scrubLabels[next];
  activeScrubLabel.value = label;
  props.temporal.scrubToLabel(label);
}

/** Label opacity falls off with distance from the drag position (source grammar). */
function labelOpacity(index: number): number {
  const center = ((dragX.value + DRAG_LIMIT) / (2 * DRAG_LIMIT)) * (scrubLabels.length - 1);
  return Math.max(0.2, 1 - Math.abs(index - center) * 0.28);
}

const pillProgressWidth = computed(() => {
  return `${(Math.abs(dragX.value) / DRAG_LIMIT) * 50}%`;
});

function onStepForward(): void {
  props.temporal.stepDateTime(1, "day");
}

function onStepBackward(): void {
  props.temporal.stepDateTime(-1, "day");
}

function onReset(): void {
  props.temporal.resetToNow();
}

// Scrubber interaction handlers

function onScrubberClick(event: MouseEvent): void {
  const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
  const x = event.clientX - rect.left;
  const position = x / rect.width;
  props.temporal.updateScrubPosition(position);
}

function onHandleMouseDown(): void {
  isScrubbing.value = true;
  document.addEventListener("mousemove", handleMouseMove);
  document.addEventListener("mouseup", handleMouseUp);
}

function onScrubberMouseDown(event: MouseEvent): void {
  if ((event.target as HTMLElement).classList.contains("scrub-handle")) {
    return; // Let handle mousedown take precedence
  }
  isScrubbing.value = true;
  const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
  const x = event.clientX - rect.left;
  const position = x / rect.width;
  props.temporal.updateScrubPosition(position);
  document.addEventListener("mousemove", handleMouseMove);
  document.addEventListener("mouseup", handleMouseUp);
}

function handleMouseMove(event: MouseEvent): void {
  if (!isScrubbing.value) return;

  const scrubber = document.querySelector(
    ".scrubber-track",
  ) as HTMLElement | null;
  if (!scrubber) return;

  const rect = scrubber.getBoundingClientRect();
  const x = event.clientX - rect.left;
  const position = x / rect.width;
  props.temporal.updateScrubPosition(position);
}

function handleMouseUp(): void {
  isScrubbing.value = false;
  props.temporal.endScrubbing();
  document.removeEventListener("mousemove", handleMouseMove);
  document.removeEventListener("mouseup", handleMouseUp);
}

function onHandleTouchStart(): void {
  isScrubbing.value = true;
  document.addEventListener("touchmove", handleTouchMove);
  document.addEventListener("touchend", handleTouchEnd);
}

function onScrubberTouchStart(event: TouchEvent): void {
  if ((event.target as HTMLElement).classList.contains("scrub-handle")) {
    return; // Let handle touchstart take precedence
  }
  isScrubbing.value = true;
  const scrubber = event.currentTarget as HTMLElement;
  const rect = scrubber.getBoundingClientRect();
  const x = event.touches[0].clientX - rect.left;
  const position = x / rect.width;
  props.temporal.updateScrubPosition(position);
  document.addEventListener("touchmove", handleTouchMove);
  document.addEventListener("touchend", handleTouchEnd);
}

function handleTouchMove(event: TouchEvent): void {
  if (!isScrubbing.value) return;

  const scrubber = document.querySelector(
    ".scrubber-track",
  ) as HTMLElement | null;
  if (!scrubber) return;

  const rect = scrubber.getBoundingClientRect();
  const x = event.touches[0].clientX - rect.left;
  const position = x / rect.width;
  props.temporal.updateScrubPosition(position);
}

function handleTouchEnd(): void {
  isScrubbing.value = false;
  props.temporal.endScrubbing();
  document.removeEventListener("touchmove", handleTouchMove);
  document.removeEventListener("touchend", handleTouchEnd);
}
</script>

<style scoped>
/* ── WEUP-SYNTH (G7 — TimelineControl): hold-to-scrub glass pill ──────────
   Source grammar: bg-black/80 backdrop-blur-3xl border-white/10 rounded-full
   px-6 py-4, mono labels, #00FF9C active state, bottom scrub-progress bar.
   G4 tokens: pill radius, overlay elevation, mono typography, brand accent. */

.scrub-pill-wrap {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.5rem;
  user-select: none;
}

.scrub-pill {
  position: relative;
  display: flex;
  align-items: center;
  gap: 1rem;
  width: 100%;
  max-width: 90vw;
  padding: 1rem 1.5rem;
  background: rgba(0, 0, 0, 0.8);
  backdrop-filter: blur(24px);
  -webkit-backdrop-filter: blur(24px);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 9999px;
  box-shadow: 0 20px 50px rgba(0, 0, 0, 0.8);
  cursor: grab;
  touch-action: none;
  transition: border-color 0.5s ease;
  overflow: hidden;
}

.scrub-pill:hover {
  border-color: rgba(0, 255, 156, 0.4);
}

.scrub-pill.holding {
  cursor: grabbing;
  border-color: rgba(0, 255, 156, 0.4);
}

.scrub-pill:focus-visible {
  outline: 2px solid #00ff9c;
  outline-offset: 2px;
}

.pill-identity {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-shrink: 0;
}

.pill-clock {
  color: rgba(255, 255, 255, 0.4);
  transition:
    transform 0.2s ease,
    color 0.2s ease;
}

.pill-clock.holding {
  transform: scale(1.2);
  color: #00ff9c;
}

.pill-labels {
  display: flex;
  flex-direction: column;
  line-height: 1.1;
}

.pill-state {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 10px;
  font-weight: 900;
  letter-spacing: 0.3em;
  text-transform: uppercase;
  font-style: italic;
  color: rgba(255, 255, 255, 0.9);
  white-space: nowrap;
}

.pill-time {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 14px;
  font-weight: 900;
  color: #00ff9c;
  white-space: nowrap;
}

.pill-scrub-labels {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 0 0.5rem;
  overflow: hidden;
  animation: pill-labels-in 0.25s ease;
}

@keyframes pill-labels-in {
  from {
    opacity: 0;
    transform: translateX(-10px);
  }
  to {
    opacity: 1;
    transform: translateX(0);
  }
}

.pill-scrub-label {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.15em;
  color: rgba(255, 255, 255, 0.2);
  flex-shrink: 0;
  transition: color 0.15s ease;
}

.pill-scrub-label.active {
  color: #00ff9c;
}

.pill-progress-track {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  height: 2px;
  pointer-events: none;
}

.pill-progress {
  height: 100%;
  margin: 0 auto;
  background: rgba(0, 255, 156, 0.4);
  border-radius: 9999px;
  transition: width 0.1s ease-out;
}

.pill-hint {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 8px;
  letter-spacing: 0.4em;
  text-transform: uppercase;
  color: rgba(255, 255, 255, 0.2);
}

@media (prefers-reduced-motion: reduce) {
  .scrub-pill,
  .scrub-pill *,
  .pill-scrub-labels {
    transition: none !important;
    animation: none !important;
  }
}

@media (max-width: 512px) {
  .scrub-pill {
    padding: 0.75rem 1rem;
    gap: 0.75rem;
  }

  .pill-scrub-labels {
    gap: 0.6rem;
  }

  .pill-scrub-label {
    font-size: 8px;
  }
}

.temporal-navigation-controls {
  padding: 0.5rem;
  user-select: none;
}

.controls-card {
  background: rgba(0, 0, 0, 0.5);
  border: 1px solid rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(10px);
}

.controls-section {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding: 1rem;
}

/* Preset buttons row */
.presets-row {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.preset-btn {
  flex: 0 1 auto;
  font-size: 0.85rem;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

@media (max-width: 512px) {
  .preset-btn {
    flex: 1 1 calc(50% - 0.25rem);
  }
}

/* Temporal status */
.temporal-status {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem;
  background: rgba(33, 150, 243, 0.05);
  border-radius: 0.25rem;
  font-size: 0.85rem;
}

.status-text {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex: 1;
}

.status-info {
  color: #2196f3;
}

.status-error {
  color: #ff9800;
}

.status-normal {
  color: rgba(255, 255, 255, 0.7);
}

.pending-spinner {
  flex-shrink: 0;
}

/* Date stepping controls */
.stepping-row {
  display: flex;
  align-items: center;
  gap: 0;
}

.step-btn {
  color: rgba(255, 255, 255, 0.6);
  transition: color 0.2s;
}

.step-btn:hover {
  color: #2196f3;
}

.step-display {
  flex: 1;
  text-align: center;
  font-size: 0.95rem;
  font-weight: 600;
  padding: 0.5rem 1rem;
  color: rgba(255, 255, 255, 0.9);
}

.offset-badge {
  font-size: 0.8rem;
  background: rgba(255, 152, 0, 0.2);
  padding: 0.25rem 0.75rem;
  border-radius: 1rem;
  color: #ffb74d;
}

/* Scrubber section */
.scrubber-section {
  padding: 0.5rem 0;
}

.scrubber-track {
  position: relative;
  height: 40px;
  cursor: pointer;
  border-radius: 0.5rem;
  overflow: hidden;
  display: flex;
  align-items: center;
}

.track-bg {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 0.5rem;
  z-index: 1;
}

.track-progress {
  position: absolute;
  top: 0;
  left: 0;
  bottom: 0;
  background: linear-gradient(
    90deg,
    rgba(33, 150, 243, 0.3),
    rgba(33, 150, 243, 0.5)
  );
  border-radius: 0.5rem 0 0 0.5rem;
  transition: width 0.1s ease-out;
  z-index: 2;
}

.scrub-handle {
  position: absolute;
  width: 24px;
  height: 24px;
  background: #2196f3;
  border-radius: 50%;
  top: 50%;
  transform: translate(-50%, -50%);
  cursor: grab;
  transition:
    transform 0.2s,
    box-shadow 0.2s;
  z-index: 3;
  box-shadow: 0 2px 8px rgba(33, 150, 243, 0.3);
}

.scrub-handle:hover {
  transform: translate(-50%, -50%) scale(1.15);
  box-shadow: 0 4px 12px rgba(33, 150, 243, 0.5);
}

.scrub-handle.active {
  cursor: grabbing;
  transform: translate(-50%, -50%) scale(1.25);
  box-shadow: 0 6px 16px rgba(33, 150, 243, 0.7);
}

.handle-inner {
  width: 100%;
  height: 100%;
  border-radius: 50%;
  box-shadow: inset 0 0 0 2px rgba(255, 255, 255, 0.8);
}

.scrub-tooltip {
  position: absolute;
  bottom: 100%;
  left: 50%;
  transform: translateX(-50%);
  background: rgba(0, 0, 0, 0.9);
  color: #fff;
  padding: 0.25rem 0.75rem;
  border-radius: 0.25rem;
  font-size: 0.75rem;
  font-weight: 600;
  white-space: nowrap;
  margin-bottom: 0.5rem;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.time-markers {
  position: absolute;
  top: 100%;
  left: 0;
  right: 0;
  display: flex;
  justify-content: space-between;
  padding: 0.25rem 0;
  font-size: 0.65rem;
  color: rgba(255, 255, 255, 0.4);
  z-index: 1;
}

.marker {
  position: relative;
  transform: translateX(-50%);
}

/* Reset button row */
.reset-row {
  display: flex;
  justify-content: center;
}

.reset-btn {
  font-size: 0.8rem;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: #ff5252;
}

/* Debug info (development only) */
.debug-info {
  padding-top: 0.5rem;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
  font-size: 0.7rem;
  font-family: monospace;
  color: rgba(255, 255, 255, 0.5);
}

.debug-item {
  display: flex;
  justify-content: space-between;
  margin: 0.25rem 0;
  padding: 0.25rem;
}

.debug-label {
  color: rgba(33, 150, 243, 0.7);
}

.debug-value {
  color: rgba(76, 175, 80, 0.7);
  font-weight: 600;
}

/* Mobile responsive adjustments */
@media (max-width: 512px) {
  .controls-section {
    gap: 0.75rem;
    padding: 0.75rem;
  }

  .scrubber-track {
    height: 32px;
  }

  .scrub-handle {
    width: 20px;
    height: 20px;
  }

  .time-markers {
    display: none;
  }

  .temporal-status {
    font-size: 0.75rem;
  }

  .step-display {
    font-size: 0.85rem;
  }
}

@media (max-width: 320px) {
  .preset-btn {
    font-size: 0.7rem;
    padding: 0.25rem 0.5rem;
  }

  .step-display {
    padding: 0.25rem 0.5rem;
  }

  .offset-badge {
    font-size: 0.7rem;
  }
}
</style>
