<!--
  WEUP-SYNTH:
  source=components/FlyerSkeleton.tsx
  destination=frontend-vue/src/components/FlyerSkeleton.vue
  mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
  notes=Stage-bound skeleton: every visible stage derives from the ACTUAL
    ingestion job lifecycle observed while polling (mission sections 12, 13).
    No arbitrary animation stands in for processing — when the job reaches a
    terminal status the skeleton resolves. Shimmer is a live-processing
    indicator only and terminates with the job; prefers-reduced-motion
    disables it (mission section 19).
-->
<template>
  <div
    class="flyer-skeleton"
    role="status"
    data-plane="z2"
    :aria-label="ariaLabel"
    data-testid="flyer-skeleton"
  >
    <div class="skeleton-card" aria-hidden="true">
      <div class="skeleton-shimmer" />
      <div class="skeleton-slots">
        <div class="slot-badge" />
        <div class="slot-title" />
        <div class="slot-sub" />
        <div class="slot-row">
          <div class="slot-cell" />
          <div class="slot-cell" />
        </div>
      </div>
    </div>

    <ol class="stage-list">
      <li
        v-for="entry in stageEntries"
        :key="entry.status"
        class="stage-item"
        :class="`stage-${entry.state}`"
        :aria-current="entry.state === 'active' ? 'step' : undefined"
      >
        <span class="stage-dot" />
        <span class="stage-label">{{ entry.label }}</span>
      </li>
    </ol>

    <p v-if="jobId" class="job-id" data-testid="skeleton-job-id">
      JOB {{ jobId }}
    </p>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import {
  INGESTION_STATUS_FLOW,
  ingestionStatusLabel,
  type IngestionJobStatus,
} from "../contracts/ingestion.contracts";

const props = defineProps<{
  /** The lifecycle status most recently observed on the polled job. */
  currentStatus: IngestionJobStatus | null;
  /**
   * The statuses actually observed on this job (ledger maintained by
   * useIngestionWizard). Stages never observed stay "pending" — they are
   * not inferred as done merely because a later stage is active.
   */
  observedStatuses?: ReadonlyArray<IngestionJobStatus> | null;
  /** Job id under observation (rendered for traceability). */
  jobId?: string | null;
}>();

type StageState = "done" | "active" | "pending";

const stageEntries = computed(() => {
  const observed = new Set<IngestionJobStatus>(props.observedStatuses ?? []);
  return INGESTION_STATUS_FLOW.map((status) => {
    let state: StageState = "pending";
    if (observed.has(status)) {
      state = status === props.currentStatus ? "active" : "done";
    }
    return { status, label: ingestionStatusLabel(status), state };
  });
});

const ariaLabel = computed(() => {
  if (!props.currentStatus) {
    return "Ingestion is starting";
  }
  return `Ingestion stage: ${ingestionStatusLabel(props.currentStatus)}`;
});
</script>

<style scoped>
.flyer-skeleton {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.skeleton-card {
  position: relative;
  width: 100%;
  aspect-ratio: 4 / 5;
  max-height: 320px;
  border-radius: 24px;
  overflow: hidden;
  background: rgba(255, 255, 255, 0.02);
  border: 1px solid rgba(255, 255, 255, 0.06);
  display: flex;
  flex-direction: column;
  justify-content: flex-end;
  padding: 24px;
}

.skeleton-shimmer {
  position: absolute;
  inset: 0;
  background: rgba(0, 255, 156, 0.02);
  animation: skeleton-pulse var(--weup-motion-processing-duration)
    var(--weup-motion-processing-easing) infinite;
  pointer-events: none;
}

.skeleton-slots {
  position: relative;
  z-index: 1;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.slot-badge,
.slot-title,
.slot-sub,
.slot-cell {
  background: rgba(255, 255, 255, 0.06);
  border-radius: var(--weup-radius-pill);
  animation: slot-pulse var(--weup-motion-slot-duration)
    var(--weup-motion-processing-easing) infinite;
}

.slot-badge {
  width: 80px;
  height: 16px;
}

.slot-title {
  width: 66%;
  height: 32px;
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.1);
}

.slot-sub {
  width: 50%;
  height: 20px;
  border-radius: 8px;
}

.slot-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
  padding-top: 4px;
}

.slot-cell {
  height: 40px;
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.03);
}

@keyframes skeleton-pulse {
  0%,
  100% {
    opacity: 0.3;
  }
  50% {
    opacity: 0.7;
  }
}

@keyframes slot-pulse {
  0%,
  100% {
    opacity: 0.4;
  }
  50% {
    opacity: 0.8;
  }
}

.stage-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.stage-item {
  display: flex;
  align-items: center;
  gap: 12px;
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 11px;
  letter-spacing: 0.18em;
  text-transform: uppercase;
}

.stage-dot {
  width: 10px;
  height: 10px;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.12);
  flex-shrink: 0;
}

.stage-label {
  color: rgba(255, 255, 255, 0.35);
}

/* WEUP-2.5D (D09): stage states are observed lifecycle facts (mission
   §12/13). Active stage carries signal glow; done stages keep the signal
   color without glow — temporal activity, not decoration. */
.stage-done .stage-dot {
  background: var(--weup-glow-signal-color);
}

.stage-done .stage-label {
  color: rgba(255, 255, 255, 0.75);
}

.stage-active .stage-dot {
  background: var(--weup-glow-signal-color);
  box-shadow: var(--weup-glow-signal);
  animation: skeleton-pulse var(--weup-motion-slot-duration)
    var(--weup-motion-processing-easing) infinite;
}

.stage-active .stage-label {
  color: var(--weup-glow-signal-color);
}

.job-id {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 10px;
  letter-spacing: 0.2em;
  color: rgba(255, 255, 255, 0.3);
  margin: 0;
  word-break: break-all;
}

@media (prefers-reduced-motion: reduce) {
  .skeleton-shimmer,
  .slot-badge,
  .slot-title,
  .slot-sub,
  .slot-cell,
  .stage-active .stage-dot {
    animation: none;
  }
}
</style>
