<!--
  WEUP-SYNTH (G9 — Saved/Profile):
  source=components/SavedEvents.tsx
  destination=frontend-vue/src/components/SavedEventsPanel.vue
  mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
  notes=G9 completion of the G6-mounted shell: restyled to the dark G4
    surface grammar (light #fffdf8 surface replaced per mission §18), and
    enriched with the real per-item actions from the source:
    - unsave (canonical saved-state mutation, real backend/anonymous-local)
    - view signal -> shared selection -> canonical event detail modal
    - prototype-local folder assignment (G2 arbitration: no backend folder
      concept; labeled non-persistent)
    All counts/session read from the canonical useSavedEventsCollection
    surfaceSnapshot. No fake EXPORT_ITINERARY: the source's footer export
    button has no backend counterpart and is excluded by §31.
-->
<template>
  <div class="saved-events-panel" data-plane="z1">
    <div class="panel-header">
      <div class="header-copy">
        <div class="title-row">
          <div class="panel-title">Shortlist</div>
          <q-badge
            class="count-badge"
            :class="{ 'has-count': savedCountBadgeValue > 0 }"
            rounded
          >
            {{ savedCountBadgeValue }}
          </q-badge>
        </div>
        <div class="panel-meta">Curated cultural signals</div>
        <div class="panel-submeta">
          {{ resolvedCount }} resolved
          <span v-if="missingOrDeletedCount > 0">
            · {{ missingOrDeletedCount }} missing or deleted
          </span>
          <span>· {{ sessionKindLabel }}</span>
        </div>
      </div>

      <q-btn
        flat
        dense
        color="primary"
        icon="refresh"
        :loading="isRefreshing"
        aria-label="Refresh saved events"
        @click="$emit('refresh')"
      />
    </div>

    <q-banner v-if="degradedReason" class="state-banner degraded" rounded>
      {{ degradedReason }}
    </q-banner>

    <q-banner v-if="error" class="state-banner error" rounded>
      {{ error }}
    </q-banner>

    <div v-if="isLoading" class="state-block">
      <q-skeleton dark type="text" width="40%" />
      <q-skeleton dark type="rect" height="88px" class="q-mt-sm" />
      <q-skeleton dark type="rect" height="88px" class="q-mt-sm" />
    </div>

    <div v-else-if="items.length === 0 && !error" class="state-block empty">
      <div class="empty-icon">
        <q-icon name="bookmark_add" size="44px" />
      </div>
      <div class="empty-title">No signals archived</div>
      <div class="empty-copy">
        Explore the city grid and bookmark nightlife signals to build your
        cultural itinerary.
      </div>
    </div>

    <q-list v-else separator class="saved-list">
      <q-item
        v-for="item in items"
        :key="`${item.eventId}:${item.savedAt}`"
        clickable
        :disable="item.canonicalEvent === null"
        :active="item.canonicalEvent?.eventId === selectedEventId"
        active-class="is-selected"
        @click="onSelect(item)"
      >
        <q-item-section>
          <q-item-label class="item-title">
            {{ item.canonicalEvent?.title ?? item.eventId }}
          </q-item-label>
          <q-item-label caption class="item-caption">
            {{ item.canonicalEvent?.venueName ?? "Missing canonical event" }}
          </q-item-label>
          <q-item-label caption class="item-caption">
            {{ item.canonicalEvent?.district ? `${item.canonicalEvent.district} · ` : "" }}{{ formatTimestamp(item.savedAt) }}
          </q-item-label>
          <q-item-label
            v-if="item.resolutionMessage"
            caption
            class="resolution-copy"
          >
            {{ item.resolutionMessage }}
          </q-item-label>

          <div v-if="item.canonicalEvent" class="item-actions">
            <q-btn
              flat
              dense
              no-caps
              size="sm"
              color="accent"
              label="View signal"
              class="view-signal-btn"
              @click.stop="onSelect(item)"
            />
            <q-select
              v-if="folderNames.length > 0"
              dense
              outlined
              dark
              :model-value="folderNameByEventId[item.eventId] ?? null"
              :options="folderOptions"
              emit-value
              map-options
              clearable
              label="Folder"
              class="folder-select"
              popup-content-class="folder-popup"
              @click.stop
              @update:model-value="
                $emit('assign-folder', {
                  eventId: item.eventId,
                  folderName: $event,
                })
              "
            >
              <template #prepend>
                <span class="folder-prototype-dot" title="Prototype-local" />
              </template>
            </q-select>
            <q-btn
              flat
              dense
              round
              size="sm"
              color="grey-6"
              icon="delete"
              aria-label="Remove saved event"
              class="unsave-btn"
              @click.stop="$emit('unsave-event', item.eventId)"
            />
          </div>
        </q-item-section>

        <q-item-section side top>
          <q-chip
            :color="item.canonicalEvent ? 'positive' : 'warning'"
            text-color="black"
            square
            dense
            class="resolution-chip"
          >
            {{ item.canonicalEvent ? "Resolved" : "Missing" }}
          </q-chip>
          <q-chip
            v-if="folderNameByEventId[item.eventId]"
            square
            dense
            class="folder-chip"
          >
            {{ folderNameByEventId[item.eventId] }}
          </q-chip>
        </q-item-section>
      </q-item>
    </q-list>

    <div v-if="items.length > 0" class="panel-footer">
      <span class="footer-label">Total signals</span>
      <span class="footer-value">{{ savedCountBadgeValue }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import type { SaveSessionKind } from "../contracts/event-detail.contracts";
import type { SavedEventDto } from "../contracts/saved-events.contracts";

const props = withDefaults(
  defineProps<{
    items: SavedEventDto[];
    savedCountBadgeValue: number;
    resolvedCount: number;
    missingOrDeletedCount: number;
    sessionKind: SaveSessionKind;
    isLoading: boolean;
    isRefreshing?: boolean;
    degradedReason?: string | null;
    error?: string | null;
    selectedEventId?: string | null;
    /** Prototype-local folder names for the assignment control. */
    folderNames?: readonly string[];
    /** Prototype-local folder assignment by canonical eventId. */
    folderNameByEventId?: Record<string, string>;
  }>(),
  {
    isRefreshing: false,
    degradedReason: null,
    error: null,
    selectedEventId: null,
    folderNames: () => [],
    folderNameByEventId: () => ({}),
  },
);

const emit = defineEmits<{
  (event: "refresh"): void;
  (event: "select-event", eventId: string): void;
  (event: "unsave-event", eventId: string): void;
  (
    event: "assign-folder",
    payload: { eventId: string; folderName: string | null },
  ): void;
}>();

const sessionKindLabel = computed(() => {
  return props.sessionKind === "authenticated"
    ? "authenticated sync"
    : "anonymous local";
});

const folderOptions = computed(() =>
  props.folderNames.map((name) => ({ label: name, value: name })),
);

function formatTimestamp(value: string): string {
  const parsed = Date.parse(value);
  if (Number.isNaN(parsed)) {
    return value;
  }

  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(parsed);
}

function onSelect(item: SavedEventDto): void {
  if (!item.canonicalEvent) {
    return;
  }

  emit("select-event", item.canonicalEvent.eventId);
}
</script>

<style scoped>
/* G4 dark surface grammar: #050505 substrate, accent #00FF9C, mono captions.
   Card primitive: rounded-2xl rows, saved indicator state color. */
/* WEUP-2.5D (D10): curated-signal surface. Raised above pure structure
   (elevation.2) — the shortlist is working state, not chrome. The count
   badge glows only when the count is real and non-zero. */
.saved-events-panel {
  display: flex;
  flex-direction: column;
  min-height: 240px;
  color: #fff;
  background: transparent;
  box-shadow: var(--weup-elevation-2);
}

.panel-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  padding: 16px 16px 12px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
}

.header-copy {
  min-width: 0;
}

.title-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.panel-title {
  color: #fff;
  font-size: 22px;
  font-weight: 800;
  text-transform: uppercase;
  font-style: italic;
  letter-spacing: -0.02em;
}

.count-badge {
  background: #00ff9c;
  color: #000;
  font-weight: 800;
  font-size: 12px;
}

.count-badge.has-count {
  box-shadow: var(--weup-glow-signal);
}

.panel-meta {
  margin-top: 4px;
  color: rgba(255, 255, 255, 0.3);
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.3em;
}

.panel-submeta {
  margin-top: 4px;
  color: rgba(255, 255, 255, 0.45);
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 10px;
  letter-spacing: 0.06em;
}

.state-banner {
  margin: 12px 16px 0;
}

.state-banner.degraded {
  background: rgba(251, 191, 36, 0.08);
  color: #fbbf24;
  border: 1px solid rgba(251, 191, 36, 0.25);
}

.state-banner.error {
  background: rgba(248, 113, 113, 0.08);
  color: #f87171;
  border: 1px solid rgba(248, 113, 113, 0.25);
}

.state-block {
  padding: 16px;
}

.state-block.empty {
  text-align: center;
  padding: 40px 20px;
}

.empty-icon {
  color: rgba(255, 255, 255, 0.12);
}

.empty-title {
  margin-top: 12px;
  color: #fff;
  font-size: 18px;
  font-weight: 800;
  text-transform: uppercase;
  font-style: italic;
}

.empty-copy {
  margin-top: 8px;
  color: rgba(255, 255, 255, 0.35);
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.12em;
  line-height: 1.7;
}

.saved-list {
  padding: 8px;
}

.saved-list :deep(.q-item) {
  border-radius: 16px;
  margin-bottom: 8px;
  background: rgba(255, 255, 255, 0.02);
  border: 1px solid rgba(255, 255, 255, 0.05);
  transition:
    background-color 0.4s ease,
    border-color 0.4s ease;
}

.saved-list :deep(.q-item:hover) {
  background: rgba(255, 255, 255, 0.05);
  border-color: rgba(255, 255, 255, 0.1);
}

.saved-list :deep(.q-item.is-selected) {
  border-color: rgba(0, 255, 156, 0.35);
  background: rgba(0, 255, 156, 0.04);
}

.item-title {
  color: #fff;
  font-weight: 700;
  font-size: 15px;
}

.item-caption {
  color: rgba(255, 255, 255, 0.45);
}

.resolution-copy {
  color: #fbbf24;
}

.item-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 8px;
}

.view-signal-btn {
  font-weight: 800;
  font-size: 10px;
  letter-spacing: 0.2em;
  text-transform: uppercase;
}

.folder-select {
  width: 140px;
}

.folder-select :deep(.q-field__control) {
  min-height: 32px;
  font-size: 11px;
}

.folder-prototype-dot {
  width: 8px;
  height: 8px;
  border-radius: 999px;
  background: #fbbf24;
  flex-shrink: 0;
}

.unsave-btn:hover {
  color: #f87171;
}

.resolution-chip {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 9px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.1em;
}

.folder-chip {
  margin-top: 4px;
  background: rgba(251, 191, 36, 0.08);
  border: 1px solid rgba(251, 191, 36, 0.3);
  color: #fbbf24;
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 9px;
  text-transform: uppercase;
  letter-spacing: 0.1em;
}

.panel-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px;
  border-top: 1px solid rgba(255, 255, 255, 0.05);
  background: rgba(0, 0, 0, 0.4);
}

.footer-label {
  color: rgba(255, 255, 255, 0.3);
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.2em;
}

.footer-value {
  color: #00ff9c;
  font-size: 22px;
  font-weight: 800;
  font-style: italic;
}
</style>
