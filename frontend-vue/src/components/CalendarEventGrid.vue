<template>
  <div class="calendar-grid-wrap">
    <q-banner v-if="error" rounded class="error-banner">
      {{ error }}
    </q-banner>

    <q-inner-loading :showing="isLoading">
      <q-spinner-dots size="36px" color="primary" />
    </q-inner-loading>

    <div v-if="!isLoading && grouped.length === 0" class="empty-state">
      No events in this map window for the selected temporal preset.
    </div>

    <div v-else class="group-list">
      <section v-for="group in grouped" :key="group.dateKey" class="day-group">
        <div class="day-header">{{ group.displayLabel }}</div>

        <q-list bordered separator class="event-list">
          <q-item
            v-for="item in group.items"
            :key="item.eventId"
            clickable
            :active="item.eventId === selectedEventId"
            active-class="event-active"
            @click="$emit('select-event', item.eventId)"
          >
            <q-item-section>
              <q-item-label class="event-title">{{ item.title }}</q-item-label>
              <q-item-label caption>
                {{ formatTime(item.startUtc) }}
                <span v-if="item.endUtc"> - {{ formatTime(item.endUtc) }}</span>
                • {{ item.venueName }}
              </q-item-label>
              <q-item-label caption>
                {{ item.primaryCategory }}
                <span v-if="item.district"> • {{ item.district }}</span>
              </q-item-label>
            </q-item-section>
            <q-item-section side>
              <q-chip
                v-if="item.savedByCurrentUser"
                dense
                square
                color="red-1"
                text-color="red-9"
              >
                Saved
              </q-chip>
            </q-item-section>
          </q-item>
        </q-list>
      </section>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import type { CalendarEventItemDto } from "../contracts/calendar-overlay.contracts";

const props = defineProps<{
  items: CalendarEventItemDto[];
  selectedEventId: string | null;
  isLoading?: boolean;
  error?: string | null;
}>();

defineEmits<{
  (event: "select-event", eventId: string): void;
}>();

type GroupedItems = {
  dateKey: string;
  displayLabel: string;
  items: CalendarEventItemDto[];
};

function toDateKey(isoUtc: string): string {
  return isoUtc.slice(0, 10);
}

function toDateLabel(isoUtc: string): string {
  const date = new Date(isoUtc);
  return new Intl.DateTimeFormat(undefined, {
    weekday: "short",
    month: "short",
    day: "numeric",
  }).format(date);
}

function formatTime(isoUtc: string): string {
  const date = new Date(isoUtc);
  return new Intl.DateTimeFormat(undefined, {
    hour: "numeric",
    minute: "2-digit",
  }).format(date);
}

const grouped = computed<GroupedItems[]>(() => {
  const buckets = new Map<string, GroupedItems>();

  for (const item of props.items) {
    const dateKey = toDateKey(item.startUtc);
    const existing = buckets.get(dateKey);

    if (existing) {
      existing.items.push(item);
      continue;
    }

    buckets.set(dateKey, {
      dateKey,
      displayLabel: toDateLabel(item.startUtc),
      items: [item],
    });
  }

  return [...buckets.values()]
    .sort((left, right) => left.dateKey.localeCompare(right.dateKey))
    .map((group) => ({
      ...group,
      items: [...group.items].sort((left, right) =>
        left.startUtc.localeCompare(right.startUtc),
      ),
    }));
});
</script>

<style scoped>
.calendar-grid-wrap {
  position: relative;
  height: 100%;
  overflow: auto;
  padding: 8px 12px 12px;
}

.group-list {
  display: grid;
  gap: 10px;
}

.day-group {
  background: rgba(255, 255, 255, 0.92);
  border-radius: 10px;
}

.day-header {
  padding: 8px 10px;
  font-weight: 700;
  color: #0f172a;
  border-bottom: 1px solid rgba(15, 23, 42, 0.08);
}

.event-list {
  border: none;
}

.event-title {
  font-weight: 600;
}

.event-active {
  background: rgba(37, 99, 235, 0.12);
}

.error-banner {
  margin-bottom: 8px;
  background: #fee2e2;
  color: #7f1d1d;
}

.empty-state {
  text-align: center;
  color: #475569;
  padding: 24px 12px;
}
</style>
