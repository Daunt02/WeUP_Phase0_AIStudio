<template>
  <div class="temporal-navigation-controls">
    <!-- Container: responsive for mobile and desktop -->
    <q-card flat bordered class="controls-card">
      <q-card-section class="controls-section">
        <!-- Preset buttons row -->
        <div class="presets-row">
          <q-btn
            v-for="preset in presetOptions"
            :key="preset.value"
            :label="preset.label"
            :outline="preset.value !== activePreset"
            :unelevated="preset.value === activePreset"
            :color="preset.value === activePreset ? 'primary' : 'grey-8'"
            size="sm"
            class="preset-btn"
            @click="onSelectPreset(preset.value)"
            :aria-pressed="preset.value === activePreset"
            :aria-label="`Navigate to ${preset.label}`"
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
import type { TimeWindowPreset } from "../contracts/time-window.contracts";
import { TimeWindowPreset } from "../contracts/time-window.contracts";
import type { UseTemporalNavigation } from "../composables/useTemporalNavigation";

const isDevelopment = import.meta.env.DEV;

interface TemporalNavigationControlsProps {
  temporal: UseTemporalNavigation;
}

const props = defineProps<TemporalNavigationControlsProps>();

// Preset options for buttons
const presetOptions = [
  { value: TimeWindowPreset.Now, label: "Now" },
  { value: TimeWindowPreset.Tonight, label: "Tonight" },
  { value: TimeWindowPreset.Tomorrow, label: "Tomorrow" },
  { value: TimeWindowPreset.ThisWeekend, label: "This Weekend" },
  { value: TimeWindowPreset.Custom, label: "Custom" },
] as const;

// Scrubbing state
const isScrubbing = ref(false);

// Computed properties from temporal state
const activePreset = computed(() => props.temporal.preset.value);
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
  const preset = currentPreset.value;
  const offset = stepOffset.value;

  let base = "";
  switch (preset) {
    case TimeWindowPreset.Now:
      base = "Now";
      break;
    case TimeWindowPreset.Tonight:
      base = "Tonight";
      break;
    case TimeWindowPreset.Tomorrow:
      base = "Tomorrow";
      break;
    case TimeWindowPreset.ThisWeekend:
      base = "This Weekend";
      break;
    case TimeWindowPreset.Custom:
      base = "Custom Range";
      break;
  }

  if (offset !== 0) {
    const suffix = offset > 0 ? `+${offset}d` : `${offset}d`;
    return `${base} ${suffix}`;
  }

  return base;
});

// Event handlers

function onSelectPreset(preset: TimeWindowPreset): void {
  props.temporal.selectPreset(preset);
}

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
