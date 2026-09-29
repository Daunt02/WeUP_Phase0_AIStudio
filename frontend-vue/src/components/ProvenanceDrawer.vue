<!--
WEUP-SYNTH:
source=components/ReceiptDrawer.tsx
destination=frontend-vue/src/components/ProvenanceDrawer.vue
mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
notes=Rebuilt per mission section 22 as a first-class provenance surface
exposing SOURCE -> INGESTION JOB -> EXTRACTION -> NORMALIZATION ->
CONFIDENCE -> ISSUES -> CANONICAL EVENT -> UI PROJECTION. The source
ReceiptDrawer's purchase-key visuals, pseudo-QR code, and "device key
activated" copy are explicitly excluded (G4 receipt primitive; prototype
isolation: fabricated keys forbidden, decorative pseudo-QR forbidden).
Stubbed backend stages are labeled PENDING, never rendered as success.
-->
<template>
  <div v-if="isOpen" class="provenance-drawer-root" role="dialog" aria-modal="true" aria-label="Event provenance">
    <div class="drawer-dimmer" @click="close" />

    <div class="drawer-sheet">
      <div class="drawer-handle" />

      <div class="drawer-header">
        <div>
          <div class="drawer-eyebrow">Provenance</div>
          <div class="drawer-title">Event provenance chain</div>
          <div v-if="activeEventId" class="drawer-subtitle mono">
            {{ activeEventId }}
          </div>
        </div>
        <button
          type="button"
          class="drawer-close"
          aria-label="Close provenance drawer"
          @click="close"
        >
          <q-icon name="close" size="20px" />
        </button>
      </div>

      <div class="drawer-body">
        <div v-if="isLoadingJob" class="stage-stack">
          <q-skeleton v-for="index in 4" :key="index" type="rect" height="72px" />
        </div>

        <q-banner v-else-if="jobError" rounded class="drawer-error">
          {{ jobError }}
        </q-banner>

        <div v-else class="stage-stack">
          <div
            v-for="(stage, index) in stages"
            :key="stage.stage"
            class="stage-row stage-entrance"
            :style="{ animationDelay: `${index * 60}ms` }"
          >
            <button
              type="button"
              class="stage-toggle"
              :aria-expanded="expandedStage === stage.stage"
              @click="toggleStage(stage.stage)"
            >
              <span class="stage-status-dot" :class="`status-${stage.status}`" />
              <span class="stage-text">
                <span class="stage-index mono">{{ String(index + 1).padStart(2, "0") }}</span>
                <span class="stage-name">{{ stage.title }}</span>
              </span>
              <span class="stage-badge" :class="`status-${stage.status}`">
                {{ stageStatusLabel(stage.status) }}
              </span>
              <q-icon
                :name="expandedStage === stage.stage ? 'expand_less' : 'expand_more'"
                size="18px"
                class="stage-chevron"
              />
            </button>

            <div class="stage-summary">{{ stage.summary }}</div>

            <div v-if="expandedStage === stage.stage" class="stage-details">
              <div
                v-for="(detail, detailIndex) in stage.details"
                :key="`${stage.stage}-detail-${detailIndex}`"
                class="detail-row"
              >
                <span class="detail-label">{{ detail.label }}</span>
                <span class="detail-value" :class="{ mono: detail.mono }">
                  {{ detail.value }}
                </span>
              </div>
            </div>
          </div>
        </div>

        <div class="drawer-footer-note">
          Backend-owned stages marked PENDING are stubbed extraction or
          normalization — they are reported as pending, never as complete.
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from "vue";
import type {
  ProvenanceStageDto,
  ProvenanceStageKind,
  ProvenanceStageStatus,
} from "../contracts/ingestion.contracts";

const props = defineProps<{
  isOpen: boolean;
  activeEventId: string | null;
  stages: ProvenanceStageDto[];
  isLoadingJob: boolean;
  jobError: string | null;
}>();

const emit = defineEmits<{
  (event: "update:isOpen", value: boolean): void;
}>();

const expandedStage = ref<ProvenanceStageKind | null>(null);

watch(
  () => props.isOpen,
  (open) => {
    if (!open) {
      expandedStage.value = null;
    }
  },
);

function toggleStage(stage: ProvenanceStageKind): void {
  expandedStage.value = expandedStage.value === stage ? null : stage;
}

function close(): void {
  emit("update:isOpen", false);
}

function stageStatusLabel(status: ProvenanceStageStatus): string {
  switch (status) {
    case "complete":
      return "Complete";
    case "pending":
      return "Pending";
    case "failed":
      return "Failed";
    case "unavailable":
      return "Unavailable";
  }
}
</script>

<style scoped>
.provenance-drawer-root {
  position: fixed;
  inset: 0;
  z-index: 6100;
}

.drawer-dimmer {
  position: absolute;
  inset: 0;
  background: rgba(0, 0, 0, 0.7);
}

.drawer-sheet {
  position: absolute;
  inset-inline: 0;
  bottom: 0;
  max-height: 92vh;
  display: flex;
  flex-direction: column;
  background: rgba(10, 10, 10, 0.92);
  backdrop-filter: blur(24px);
  -webkit-backdrop-filter: blur(24px);
  border-top: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 18px 18px 0 0;
  box-shadow: 0 -20px 50px rgba(0, 0, 0, 0.8);
  animation: drawer-slide-up 400ms ease;
  overflow: hidden;
}

@media (min-width: 768px) {
  .drawer-sheet {
    inset-inline: auto;
    right: 24px;
    bottom: 24px;
    width: min(520px, calc(100vw - 48px));
    max-height: 85vh;
    border: 1px solid rgba(255, 255, 255, 0.1);
    border-radius: 18px;
  }
}

@keyframes drawer-slide-up {
  from {
    transform: translateY(100%);
  }
  to {
    transform: translateY(0);
  }
}

.drawer-handle {
  width: 48px;
  height: 4px;
  border-radius: 9999px;
  background: rgba(255, 255, 255, 0.1);
  margin: 16px auto 4px;
  flex-shrink: 0;
}

.drawer-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  padding: 16px 20px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  background: rgba(0, 0, 0, 0.3);
  flex-shrink: 0;
}

.drawer-eyebrow {
  font-size: 0.7rem;
  font-weight: 700;
  letter-spacing: 0.3em;
  text-transform: uppercase;
  color: #00ff9c;
}

.drawer-title {
  margin-top: 4px;
  font-size: 1.1rem;
  font-weight: 700;
  color: #ffffff;
}

.drawer-subtitle {
  margin-top: 4px;
  font-size: 0.75rem;
  color: rgba(255, 255, 255, 0.4);
  word-break: break-all;
}

.drawer-close {
  width: 44px;
  height: 44px;
  border-radius: 9999px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  background: rgba(255, 255, 255, 0.05);
  color: rgba(255, 255, 255, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: background-color 0.2s ease, color 0.2s ease;
}

.drawer-close:hover {
  background: rgba(255, 255, 255, 0.1);
  color: #ffffff;
}

.drawer-body {
  flex: 1;
  overflow-y: auto;
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.stage-stack {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.stage-row {
  border: 1px solid rgba(255, 255, 255, 0.05);
  border-radius: 16px;
  background: rgba(255, 255, 255, 0.02);
  padding: 12px 14px;
}

.stage-entrance {
  animation: stage-fade-in 300ms ease both;
}

@keyframes stage-fade-in {
  from {
    opacity: 0;
    transform: translateY(8px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.stage-toggle {
  width: 100%;
  display: flex;
  align-items: center;
  gap: 10px;
  background: none;
  border: none;
  padding: 0;
  cursor: pointer;
  color: inherit;
  text-align: left;
}

.stage-status-dot {
  width: 10px;
  height: 10px;
  border-radius: 9999px;
  flex-shrink: 0;
}

.stage-status-dot.status-complete {
  background: #00ff9c;
}

.stage-status-dot.status-pending {
  background: #f59e0b;
}

.stage-status-dot.status-failed {
  background: #f87171;
}

.stage-status-dot.status-unavailable {
  background: rgba(255, 255, 255, 0.25);
}

.stage-text {
  display: flex;
  align-items: baseline;
  gap: 8px;
  flex: 1;
  min-width: 0;
}

.stage-index {
  font-size: 0.7rem;
  color: rgba(255, 255, 255, 0.25);
}

.stage-name {
  font-size: 0.9rem;
  font-weight: 700;
  color: #ffffff;
}

.stage-badge {
  font-size: 0.65rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.12em;
  padding: 4px 10px;
  border-radius: 9999px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  flex-shrink: 0;
}

.stage-badge.status-complete {
  color: #00ff9c;
  border-color: rgba(0, 255, 156, 0.3);
  background: rgba(0, 255, 156, 0.08);
}

.stage-badge.status-pending {
  color: #fbbf24;
  border-color: rgba(245, 158, 11, 0.3);
  background: rgba(245, 158, 11, 0.08);
}

.stage-badge.status-failed {
  color: #f87171;
  border-color: rgba(248, 113, 113, 0.3);
  background: rgba(248, 113, 113, 0.08);
}

.stage-badge.status-unavailable {
  color: rgba(255, 255, 255, 0.5);
}

.stage-chevron {
  color: rgba(255, 255, 255, 0.4);
  flex-shrink: 0;
}

.stage-summary {
  margin-top: 8px;
  font-size: 0.8rem;
  color: rgba(255, 255, 255, 0.6);
  line-height: 1.5;
}

.stage-details {
  margin-top: 10px;
  padding-top: 10px;
  border-top: 1px solid rgba(255, 255, 255, 0.05);
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.detail-row {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.detail-label {
  font-size: 0.65rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.12em;
  color: rgba(255, 255, 255, 0.35);
}

.detail-value {
  font-size: 0.8rem;
  color: rgba(255, 255, 255, 0.8);
  line-height: 1.5;
  word-break: break-word;
}

.mono {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
}

.drawer-error {
  background: rgba(248, 113, 113, 0.08);
  color: #fecaca;
  border: 1px solid rgba(248, 113, 113, 0.2);
}

.drawer-footer-note {
  font-size: 0.72rem;
  line-height: 1.5;
  color: rgba(255, 255, 255, 0.35);
  border-top: 1px dashed rgba(255, 255, 255, 0.1);
  padding-top: 12px;
}

@media (prefers-reduced-motion: reduce) {
  .drawer-sheet,
  .stage-entrance {
    animation: none;
  }
}
</style>
