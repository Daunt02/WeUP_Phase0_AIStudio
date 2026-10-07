<template>
  <div class="timeline-header" data-plane="z1">
    <div class="left">
      <q-chip color="blue-1" text-color="blue-10" square>
        Temporal Overlay
      </q-chip>
      <div class="meta">
        <div class="state-label">{{ stateLabel }}</div>
        <div class="count-label weup-telemetry">
          {{ totalCount }} events in current map window
        </div>
        <div v-if="isFilterRefreshPending" class="status-label">
          Updating temporal/filter projection...
        </div>
      </div>
    </div>

    <div class="actions">
      <!-- WEUP-SYNTH (G7 — CulturalCalendar): projection toggle -->
      <q-btn
        flat
        dense
        :color="layoutMode === 'masonry' ? 'primary' : 'grey-8'"
        icon="view_module"
        label="Grid"
        :aria-pressed="layoutMode === 'masonry'"
        @click="$emit('layout-mode-change', 'masonry')"
      />
      <q-btn
        flat
        dense
        :color="layoutMode === 'day-column' ? 'primary' : 'grey-8'"
        icon="view_column"
        label="Days"
        :aria-pressed="layoutMode === 'day-column'"
        @click="$emit('layout-mode-change', 'day-column')"
      />
      <q-btn
        flat
        dense
        color="primary"
        icon="calendar_view_day"
        label="Partial"
        @click="$emit('set-layer', 'partial')"
      />
      <q-btn
        flat
        dense
        color="primary"
        icon="calendar_view_month"
        label="Expanded"
        @click="$emit('set-layer', 'expanded')"
      />
      <q-btn
        flat
        dense
        color="grey-8"
        icon="close"
        label="Close"
        @click="$emit('set-layer', 'closed')"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import type { CalendarOverlayLayerState } from "../contracts/calendar-overlay.contracts";
import type { CalendarTransitionPhase } from "../composables/useCalendarTransitionState";
import type { CalendarLayoutMode } from "./CalendarOverlayShell.vue";

const props = withDefaults(
  defineProps<{
    layer: CalendarOverlayLayerState;
    totalCount: number;
    isFilterRefreshPending?: boolean;
    transitionPhase?: CalendarTransitionPhase;
    /** WEUP-SYNTH (G7): calendar projection mode. */
    layoutMode?: CalendarLayoutMode;
  }>(),
  {
    layoutMode: "masonry",
  },
);

defineEmits<{
  (event: "set-layer", layer: CalendarOverlayLayerState): void;
  /** WEUP-SYNTH (G7): calendar projection mode toggle. */
  (event: "layout-mode-change", mode: CalendarLayoutMode): void;
}>();

const stateLabel = computed(() => {
  if (props.layer === "closed") {
    return "Closed";
  }
  if (props.layer === "partial") {
    return "Partial Overlay";
  }
  if (props.layer === "expanded") {
    return "Expanded Overlay";
  }
  if (props.transitionPhase === "event-select") {
    return "Event Focus";
  }
  return "Event Selected";
});
</script>

<style scoped>
.timeline-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 12px;
  border-bottom: 1px solid rgba(15, 23, 42, 0.08);
}

.left {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
}

.meta {
  min-width: 0;
}

.state-label {
  font-weight: 600;
  color: #0f172a;
}

.count-label {
  color: #334155;
  font-size: 12px;
  white-space: nowrap;
  text-overflow: ellipsis;
  overflow: hidden;
  max-width: 48vw;
}

.status-label {
  color: #1d4ed8;
  font-size: 11px;
  font-weight: 600;
}

.actions {
  display: flex;
  align-items: center;
  gap: 4px;
}

@media (max-width: 860px) {
  .timeline-header {
    flex-direction: column;
    align-items: stretch;
  }

  .actions {
    justify-content: flex-end;
  }
}
</style>
