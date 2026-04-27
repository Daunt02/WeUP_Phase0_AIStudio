<template>
  <q-card flat bordered class="saved-events-panel">
    <q-card-section class="panel-header">
      <div class="header-copy">
        <div class="title-row">
          <div class="panel-title">Saved Events</div>
          <q-badge color="accent" text-color="white" rounded>
            {{ savedCountBadgeValue }}
          </q-badge>
        </div>

        <div class="panel-meta">
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
        label="Refresh"
        @click="$emit('refresh')"
      />
    </q-card-section>

    <q-banner v-if="degradedReason" class="state-banner degraded" rounded>
      {{ degradedReason }}
    </q-banner>

    <q-banner v-if="error" class="state-banner error" rounded>
      {{ error }}
    </q-banner>

    <q-card-section v-if="isLoading" class="state-block">
      <q-skeleton type="text" width="40%" />
      <q-skeleton type="rect" height="72px" class="q-mt-sm" />
      <q-skeleton type="rect" height="72px" class="q-mt-sm" />
    </q-card-section>

    <q-card-section
      v-else-if="items.length === 0 && !error"
      class="state-block empty"
    >
      <div class="empty-title">No saved events yet</div>
      <div class="empty-copy">
        Saved events will appear here once a canonical event is bookmarked.
      </div>
    </q-card-section>

    <q-list v-else separator>
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
          <q-item-label caption>
            {{ item.canonicalEvent?.venueName ?? "Missing canonical event" }}
          </q-item-label>
          <q-item-label caption>
            Saved {{ formatTimestamp(item.savedAt) }}
          </q-item-label>
          <q-item-label v-if="item.canonicalEvent" caption>
            Starts {{ formatTimestamp(item.canonicalEvent.startUtc) }}
          </q-item-label>
          <q-item-label
            v-if="item.resolutionMessage"
            caption
            class="resolution-copy"
          >
            {{ item.resolutionMessage }}
          </q-item-label>
        </q-item-section>

        <q-item-section side top>
          <q-chip
            :color="item.canonicalEvent ? 'green-1' : 'orange-1'"
            :text-color="item.canonicalEvent ? 'green-9' : 'orange-10'"
            square
            dense
          >
            {{ item.canonicalEvent ? "Resolved" : "Missing" }}
          </q-chip>
        </q-item-section>
      </q-item>
    </q-list>
  </q-card>
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
  }>(),
  {
    isRefreshing: false,
    degradedReason: null,
    error: null,
    selectedEventId: null,
  },
);

const emit = defineEmits<{
  (event: "refresh"): void;
  (event: "select-event", eventId: string): void;
}>();

const sessionKindLabel = computed(() => {
  return props.sessionKind === "authenticated"
    ? "authenticated sync"
    : "anonymous local";
});

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
.saved-events-panel {
  display: flex;
  flex-direction: column;
  min-height: 240px;
  background: #fffdf8;
}

.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
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
  font-size: 16px;
  font-weight: 700;
  color: #172033;
}

.panel-meta {
  margin-top: 4px;
  color: #526071;
  font-size: 12px;
}

.state-banner {
  margin: 0 16px 12px;
}

.state-banner.degraded {
  background: #fff7e6;
  color: #8a5300;
}

.state-banner.error {
  background: #fdecec;
  color: #991b1b;
}

.state-block {
  padding-top: 8px;
}

.state-block.empty {
  text-align: center;
  padding: 32px 20px;
}

.empty-title {
  font-size: 15px;
  font-weight: 700;
  color: #172033;
}

.empty-copy {
  margin-top: 8px;
  color: #5f6b7a;
}

.item-title {
  font-weight: 600;
  color: #172033;
}

.resolution-copy {
  color: #9a3412;
}

.is-selected {
  background: rgba(37, 99, 235, 0.08);
}
</style>
