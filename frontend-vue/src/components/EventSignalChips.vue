<!--
WEUP-SYNTH:
source=components/EventSignalModal.tsx
destination=frontend-vue/src/components/EventSignalChips.vue
mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
notes=Signal states derived deterministically from EventDetailDto fields with
cited sources (G3 chain). The source modal's AI-generated insight prose and
flyer-search-term generation are BLOCKED_WITH_REASON (no signal-generation
endpoint exists); the fake checkout/purchase flow and door codes are excluded
by prototype isolation. Signal chips are informational; they never override
canonical event identity, saved state, coordinates, or provenance.
-->
<template>
  <div class="signals-section">
    <div class="signals-title">Signal state</div>
    <div class="signals-grid">
      <div
        v-for="signal in signals"
        :key="signal.label"
        class="signal-chip"
        :class="{ 'signal-unavailable': signal.unavailable }"
        :title="signal.sourceCitation"
      >
        <span class="signal-label">{{ signal.label }}</span>
        <span v-if="!signal.unavailable" class="signal-value">
          {{ signal.value }}
          <span v-if="signal.barValue != null" class="signal-bar">
            <span class="signal-bar-fill" :style="{ width: `${signal.barValue}%` }" />
          </span>
        </span>
        <span v-else class="signal-value-unavailable">unavailable</span>
        <span class="signal-source mono">{{ signal.sourceCitation }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import type {
  EventDetailDto,
  SaveSessionKind,
} from "../contracts/event-detail.contracts";

export interface TemporalSignalContext {
  readonly mode: string;
  readonly dayLabel: string | null;
  readonly overlapNote: boolean;
}

const props = defineProps<{
  event: EventDetailDto;
  sessionKind: SaveSessionKind | null;
  temporal: TemporalSignalContext | null;
  visibleEventCount: number | null;
}>();

interface SignalChip {
  readonly label: string;
  readonly value: string;
  readonly unavailable: boolean;
  readonly sourceCitation: string;
  readonly barValue: number | null;
}

const signals = computed<SignalChip[]>(() => {
  const event = props.event;

  const chips: SignalChip[] = [
    {
      label: "Signal strength",
      value: `${Math.round(event.confidence * 100)}%`,
      unavailable: false,
      sourceCitation: "src: EventDetailDto.confidence",
      barValue: Math.round(event.confidence * 100),
    },
    {
      label: "Status",
      value: event.status,
      unavailable: false,
      sourceCitation: "src: EventDetailDto.status",
      barValue: null,
    },
    {
      label: "Source",
      value: event.sourceKind,
      unavailable: false,
      sourceCitation: "src: EventDetailDto.sourceKind",
      barValue: null,
    },
    {
      label: "Category",
      value: event.categories[0] ?? event.category,
      unavailable: false,
      sourceCitation: "src: EventDetailDto.categories",
      barValue: null,
    },
    {
      label: "Media",
      value:
        event.mediaRefs.length === 0
          ? "no media refs"
          : `${event.mediaRefs.length} media ref${event.mediaRefs.length === 1 ? "" : "s"}${event.flyerImageUrl ? " · flyer" : ""}`,
      unavailable: event.mediaRefs.length === 0 && !event.flyerImageUrl,
      sourceCitation: "src: EventDetailDto.mediaRefs",
      barValue: null,
    },
    {
      label: "Media policy",
      value: event.status === "NEEDS_REVIEW" ? "awaiting signal" : "approved",
      unavailable: false,
      sourceCitation: "src: EventDetailDto.status",
      barValue: null,
    },
    {
      label: "Save state",
      value: event.savedByCurrentUser
        ? `saved${props.sessionKind ? ` (${props.sessionKind})` : ""}`
        : "not saved",
      unavailable: false,
      sourceCitation: "src: saved-state authority",
      barValue: null,
    },
  ];

  if (props.temporal) {
    const temporal = props.temporal;
    chips.push({
      label: "Temporal",
      value: temporal.dayLabel
        ? `${temporal.mode} · ${temporal.dayLabel}`
        : temporal.mode,
      unavailable: false,
      sourceCitation: "src: temporal singleton",
      barValue: null,
    });
    if (temporal.overlapNote) {
      chips.push({
        label: "Temporal note",
        value:
          "00:00–03:00 Houston overlap: pill and backend can name different spans",
        unavailable: false,
        sourceCitation: "src: G7 temporal arbitration",
        barValue: null,
      });
    }
  } else {
    chips.push({
      label: "Temporal",
      value: "",
      unavailable: true,
      sourceCitation: "src: temporal singleton (not attached)",
      barValue: null,
    });
  }

  if (props.visibleEventCount != null) {
    chips.push({
      label: "In current window",
      value: `${props.visibleEventCount} event${props.visibleEventCount === 1 ? "" : "s"}`,
      unavailable: false,
      sourceCitation: "src: discovery.visibleEventIds",
      barValue: null,
    });
  }

  return chips;
});
</script>

<style scoped>
.signals-section {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.signals-title {
  font-size: 0.75rem;
  font-weight: 700;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  color: #6b7280;
}

.signals-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.signal-chip {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 8px 12px;
  border-radius: 9999px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  background: rgba(0, 255, 156, 0.1);
  min-width: 0;
}

.signal-label {
  font-size: 0.65rem;
  font-weight: 700;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: rgba(255, 255, 255, 0.45);
}

.signal-value {
  font-size: 0.8rem;
  font-weight: 600;
  color: #ffffff;
  display: flex;
  align-items: center;
  gap: 8px;
}

.signal-value-unavailable {
  font-size: 0.8rem;
  font-style: italic;
  color: rgba(255, 255, 255, 0.4);
}

.signal-unavailable {
  background: rgba(255, 255, 255, 0.03);
}

.signal-source {
  font-size: 0.65rem;
  color: rgba(255, 255, 255, 0.3);
}

.signal-bar {
  display: inline-block;
  width: 64px;
  height: 6px;
  border-radius: 9999px;
  background: rgba(255, 255, 255, 0.1);
  overflow: hidden;
}

.signal-bar-fill {
  display: block;
  height: 100%;
  background: #00ff9c;
  border-radius: 9999px;
}

.mono {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
}
</style>
