<template>
  <div class="calendar-grid-wrap">
    <q-banner v-if="error" rounded class="error-banner">
      {{ error }}
    </q-banner>

    <q-inner-loading :showing="isLoading">
      <q-spinner-dots size="36px" color="primary" />
    </q-inner-loading>

    <div
      v-if="!isLoading && layout.dayBuckets.length === 0"
      class="empty-state"
    >
      No events in this map window for the selected temporal preset.
    </div>

    <div v-else class="group-list">
      <section
        v-for="day in layout.dayBuckets"
        :key="day.dayKey"
        class="day-group"
      >
        <div class="day-header">
          <span>{{ day.displayLabel }}</span>
          <q-badge color="blue-grey-2" text-color="blue-grey-10" rounded>
            {{ day.totalCount }}
          </q-badge>
        </div>

        <div class="bucket-list">
          <article
            v-for="bucket in day.buckets"
            :key="bucket.bucketKey"
            class="time-bucket"
            :class="`density-${bucket.densityLevel}`"
          >
            <div class="bucket-header">
              <div class="bucket-time">{{ bucket.displayLabel }}</div>
              <div class="bucket-meta">
                <span>{{ bucket.visibleCount }} visible</span>
                <span v-if="bucket.overflowCount > 0">
                  • +{{ bucket.overflowCount }} overflow</span
                >
              </div>
            </div>

            <div class="masonry-grid" :style="gridStyle(bucket)">
              <CalendarEventCard
                v-for="event in bucket.visibleEvents"
                :key="event.eventId"
                :event="event"
                :is-selected="event.eventId === selectedEventId"
                @select-event="$emit('select-event', $event)"
              />
            </div>

            <div v-if="bucket.overflowEvents.length > 0" class="overflow-panel">
              <div class="overflow-title">
                Dense period: {{ bucket.overflowCount }} more events kept in
                overflow to preserve click target clarity.
              </div>
              <div class="overflow-list">
                <q-btn
                  v-for="overflowEvent in bucket.overflowEvents"
                  :key="overflowEvent.eventId"
                  dense
                  unelevated
                  color="grey-2"
                  text-color="grey-9"
                  class="overflow-chip"
                  :label="overflowEvent.source.title"
                  @click="$emit('select-event', overflowEvent.eventId)"
                />
              </div>
            </div>
          </article>
        </div>
      </section>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { CalendarEventItemDto } from "../contracts/calendar-overlay.contracts";
import type { CalendarTimeBucket } from "../composables/useCalendarMasonryLayout";
import { computeCalendarMasonryLayout } from "../composables/useCalendarMasonryLayout";
import { computed } from "vue";
import CalendarEventCard from "./CalendarEventCard.vue";

const props = defineProps<{
  items: CalendarEventItemDto[];
  selectedEventId: string | null;
  isLoading?: boolean;
  error?: string | null;
}>();

defineEmits<{
  (event: "select-event", eventId: string): void;
}>();

const layout = computed(() => {
  return computeCalendarMasonryLayout(props.items, {
    bucketMinutes: 60,
    maxColumnsPerBucket: 4,
    denseThreshold: 8,
    overloadedThreshold: 12,
    maxVisibleInDenseBucket: 8,
    maxVisibleInOverloadedBucket: 6,
  });
});

function gridStyle(bucket: CalendarTimeBucket): Record<string, string> {
  const visibleColumns = bucket.visibleEvents.reduce((max, event) => {
    return Math.max(max, event.columnIndex + 1);
  }, 1);

  return {
    gridTemplateColumns: `repeat(${visibleColumns}, minmax(0, 1fr))`,
  };
}
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
  border: 1px solid rgba(15, 23, 42, 0.08);
}

.day-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 10px;
  font-weight: 700;
  color: #0f172a;
  border-bottom: 1px solid rgba(15, 23, 42, 0.08);
}

.bucket-list {
  display: grid;
  gap: 10px;
  padding: 10px;
}

.time-bucket {
  border: 1px solid rgba(15, 23, 42, 0.1);
  border-radius: 10px;
  background: rgba(248, 250, 252, 0.9);
  padding: 8px;
  display: grid;
  gap: 8px;
}

.time-bucket.density-dense,
.time-bucket.density-overloaded {
  border-color: rgba(30, 64, 175, 0.3);
}

.error-banner {
  margin-bottom: 8px;
  background: #fee2e2;
  color: #7f1d1d;
}

.bucket-header {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 10px;
}

.bucket-time {
  font-size: 12px;
  font-weight: 700;
  color: #0f172a;
}

.bucket-meta {
  font-size: 11px;
  color: #475569;
}

.masonry-grid {
  display: grid;
  grid-auto-rows: 7px;
  gap: 8px;
}

.overflow-panel {
  border-top: 1px dashed rgba(15, 23, 42, 0.18);
  padding-top: 8px;
  display: grid;
  gap: 6px;
}

.overflow-title {
  font-size: 11px;
  color: #334155;
}

.overflow-list {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.overflow-chip {
  text-transform: none;
  max-width: 240px;
}

.empty-state {
  text-align: center;
  color: #475569;
  padding: 24px 12px;
}
</style>
