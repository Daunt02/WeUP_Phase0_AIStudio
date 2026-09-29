<!--
  WEUP-SYNTH (G7 — Temporal):
  source=components/CulturalCalendar.tsx
  destination=frontend-vue/src/components/CalendarEventGrid.vue
  mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
  notes=CulturalCalendar merged as an alternate day-column projection of the
  same calendarFeedQuery items: 7-day strip with event-day dots, hour-scale
  day columns, date selection driving the canonical temporal authority via the
  date-select emit, and the Tonight scrub-cursor marker. Masonry mode is
  unchanged (default). No local filtering fork: date selection always routes
  through useTemporalNavigation.selectHoustonDay.
-->
<template>
  <div class="calendar-grid-wrap">
    <q-banner v-if="error" rounded class="error-banner">
      {{ error }}
    </q-banner>

    <q-banner
      v-if="degradedReason && !isLoading"
      rounded
      class="degraded-banner"
    >
      {{ degradedReason }}
    </q-banner>

    <q-inner-loading :showing="isLoading">
      <div class="loading-state">
        <q-spinner-dots size="36px" color="primary" />
        <div class="loading-copy">Refreshing calendar projection...</div>
      </div>
    </q-inner-loading>

    <div
      v-if="!isLoading && layout.dayBuckets.length === 0 && layoutMode === 'masonry'"
      class="empty-state"
    >
      No events in this map window for the selected temporal preset.
    </div>

    <!-- WEUP-SYNTH (G7 — CulturalCalendar): day-column projection -->
    <div
      v-else-if="layoutMode === 'day-column'"
      class="day-columns"
      :class="[
        `phase-${transitionPhase ?? 'idle'}`,
        { 'defer-selection-motion': deferSelectionMotion },
      ]"
    >
      <div v-if="!isLoading && columnDays.length === 0" class="empty-state">
        No events in this map window for the selected temporal preset.
      </div>
      <section
        v-for="day in columnDays"
        :key="day.key"
        class="day-column"
        :class="{ 'is-active-day': day.key === activeDayKey }"
      >
        <button
          type="button"
          class="day-column-header"
          :aria-pressed="day.key === activeDayKey"
          :aria-label="`Select ${day.label}`"
          @click="$emit('date-select', day.key)"
        >
          <span class="day-column-name">{{ day.dayName }}</span>
          <span class="day-column-number">{{ day.dayNumber }}</span>
          <span
            v-if="dayEventCounts[day.key] > 0"
            class="day-column-dot"
            aria-hidden="true"
          ></span>
        </button>

        <div class="hour-rows">
          <div
            v-for="hourGroup in dayHourGroups[day.key] ?? []"
            :key="hourGroup.hour"
            class="hour-row"
          >
            <div class="hour-gutter">
              <span class="hour-label">{{ formatHourLabel(hourGroup.hour) }}</span>
              <span
                v-if="cursorHourByDay[day.key] === hourGroup.hour"
                class="cursor-marker"
                title="Scrub cursor"
              >
                <span class="cursor-dot"></span>
                <span class="cursor-label">{{ scrubCursorHourLabel }}</span>
              </span>
            </div>
            <div class="hour-events">
              <CalendarEventCard
                v-for="event in hourGroup.events"
                :key="event.eventId"
                :event="event"
                :is-selected="event.eventId === selectedEventId"
                @select-event="$emit('select-event', $event)"
              />
            </div>
          </div>
          <div
            v-if="(dayHourGroups[day.key] ?? []).length === 0"
            class="day-column-empty"
          >
            No signals
          </div>
        </div>
      </section>
    </div>

    <div
      v-else
      class="group-list"
      :class="[
        `phase-${transitionPhase ?? 'idle'}`,
        { 'defer-selection-motion': deferSelectionMotion },
      ]"
    >
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
import type {
  CalendarTimeBucket,
  PackedCalendarEvent,
} from "../composables/useCalendarMasonryLayout";
import type { CalendarTransitionPhase } from "../composables/useCalendarTransitionState";
import { computeCalendarMasonryLayout } from "../composables/useCalendarMasonryLayout";
import {
  HOUSTON_TIMEZONE,
  formatHoustonTimeShort,
  getUpcomingWeek,
  houstonDayKey,
  houstonHourOfDay,
  type HoustonWeekDay,
} from "../utils/houstonTime";
import { computed } from "vue";
import CalendarEventCard from "./CalendarEventCard.vue";

const props = defineProps<{
  items: CalendarEventItemDto[];
  selectedEventId: string | null;
  isLoading?: boolean;
  error?: string | null;
  degradedReason?: string | null;
  transitionPhase?: CalendarTransitionPhase;
  deferSelectionMotion?: boolean;
  /**
   * WEUP-SYNTH (G7 — CulturalCalendar): alternate projection.
   * "masonry" (default, unchanged) or "day-column" (7-day strip + hour-scale
   * day columns merged from CulturalCalendar).
   */
  layoutMode?: "masonry" | "day-column";
  /** Houston day key ("YYYY-MM-DD") of the selected day, if any. */
  activeDayKey?: string | null;
  /** Scrub cursor instant (Tonight mode) for the cursor marker. */
  scrubCursorUtc?: string | null;
}>();

defineEmits<{
  (event: "select-event", eventId: string): void;
  /**
   * WEUP-SYNTH (G7 — CulturalCalendar): date selection. The parent routes this
   * through useTemporalNavigation.selectHoustonDay — the grid never builds its
   * own query.
   */
  (event: "date-select", dayKey: string): void;
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

// ── WEUP-SYNTH (G7 — CulturalCalendar): day-column projection ────────────────
// Alternate projection of the same calendarFeedQuery items: 7-day strip with
// event-day dots, hour-scale day columns, Houston-day date selection.

interface DayHourGroup {
  hour: number;
  events: PackedCalendarEvent[];
}

function packDayColumnEvent(item: CalendarEventItemDto): PackedCalendarEvent {
  const startMs = Date.parse(item.startUtc);
  const endMs = item.endUtc ? Date.parse(item.endUtc) : startMs;
  return {
    eventId: item.eventId,
    source: item,
    startUtc: item.startUtc,
    endUtc: item.endUtc ?? item.startUtc,
    startMs,
    endMs: Number.isNaN(endMs) ? startMs : endMs,
    startMinuteOfDay: houstonHourOfDay(item.startUtc) * 60,
    durationMinutes: 90,
    columnIndex: 0,
    rowSpan: 1,
    displayTimeLabel: formatHoustonTimeShort(new Date(item.startUtc)),
    secondaryLabel: item.venueName ?? "",
  };
}

/** Day columns: the upcoming week plus any extra Houston days present in items. */
const columnDays = computed<readonly HoustonWeekDay[]>(() => {
  const week = getUpcomingWeek();
  const weekKeys = new Set(week.map((day) => day.key));
  const extras = new Map<string, HoustonWeekDay>();
  for (const item of props.items) {
    const key = houstonDayKey(item.startUtc);
    if (!weekKeys.has(key) && !extras.has(key)) {
      const date = new Date(item.startUtc);
      const dayNumber = Number(
        new Intl.DateTimeFormat("en-US", {
          timeZone: HOUSTON_TIMEZONE,
          day: "numeric",
        }).format(date),
      );
      const dayName = new Intl.DateTimeFormat("en-US", {
        timeZone: HOUSTON_TIMEZONE,
        weekday: "short",
      })
        .format(date)
        .toUpperCase();
      extras.set(key, {
        key,
        dayName,
        label: key,
        dayNumber,
      });
    }
  }
  return [...week, ...[...extras.values()].sort((a, b) => (a.key < b.key ? -1 : 1))];
});

const packedDayColumnEvents = computed<PackedCalendarEvent[]>(() =>
  [...props.items]
    .map(packDayColumnEvent)
    .sort((a, b) => a.startMs - b.startMs),
);

const dayEventCounts = computed<Record<string, number>>(() => {
  const counts: Record<string, number> = {};
  for (const event of packedDayColumnEvents.value) {
    const key = houstonDayKey(event.startUtc);
    counts[key] = (counts[key] ?? 0) + 1;
  }
  return counts;
});

const dayHourGroups = computed<Record<string, DayHourGroup[]>>(() => {
  const byDay = new Map<string, Map<number, PackedCalendarEvent[]>>();
  for (const event of packedDayColumnEvents.value) {
    const key = houstonDayKey(event.startUtc);
    const hour = houstonHourOfDay(event.startUtc);
    if (!byDay.has(key)) {
      byDay.set(key, new Map());
    }
    const hours = byDay.get(key) as Map<number, PackedCalendarEvent[]>;
    if (!hours.has(hour)) {
      hours.set(hour, []);
    }
    (hours.get(hour) as PackedCalendarEvent[]).push(event);
  }
  const result: Record<string, DayHourGroup[]> = {};
  for (const [key, hours] of byDay) {
    result[key] = [...hours.entries()]
      .sort((a, b) => a[0] - b[0])
      .map(([hour, events]) => ({ hour, events }));
  }
  return result;
});

/** Scrub-cursor hour per day column (Tonight mode only). */
const cursorHourByDay = computed<Record<string, number>>(() => {
  if (!props.scrubCursorUtc) {
    return {};
  }
  const key = houstonDayKey(props.scrubCursorUtc);
  return { [key]: houstonHourOfDay(props.scrubCursorUtc) };
});

const scrubCursorHourLabel = computed(() => {
  if (!props.scrubCursorUtc) {
    return "";
  }
  return formatHourLabel(houstonHourOfDay(props.scrubCursorUtc));
});

function formatHourLabel(hour: number): string {
  if (hour === 0) return "12AM";
  if (hour < 12) return `${hour}AM`;
  if (hour === 12) return "12PM";
  return `${hour - 12}PM`;
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

.degraded-banner {
  margin-bottom: 8px;
  background: #fff7ed;
  color: #9a3412;
}

.loading-state {
  display: grid;
  gap: 8px;
  justify-items: center;
}

.loading-copy {
  font-size: 12px;
  color: #334155;
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

/* ── WEUP-SYNTH (G7 — CulturalCalendar): day-column projection ────────────
   Hour-scale/day-column grammar merged from CulturalCalendar as an alternate
   projection of calendarFeedQuery. G4 tokens: pill day selectors, mono
   labels, brand-accent active day and scrub-cursor marker. */

.day-columns {
  display: grid;
  grid-auto-flow: column;
  grid-auto-columns: minmax(220px, 1fr);
  gap: 10px;
  overflow-x: auto;
  padding-bottom: 8px;
}

.day-column {
  background: rgba(255, 255, 255, 0.92);
  border-radius: 10px;
  border: 1px solid rgba(15, 23, 42, 0.08);
  display: flex;
  flex-direction: column;
  min-height: 120px;
}

.day-column.is-active-day {
  border-color: rgba(0, 255, 156, 0.55);
  box-shadow: 0 0 0 1px rgba(0, 255, 156, 0.35);
}

.day-column-header {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 10px 8px;
  background: transparent;
  border: none;
  border-bottom: 1px solid rgba(15, 23, 42, 0.08);
  cursor: pointer;
  border-radius: 10px 10px 0 0;
  transition: background 160ms ease;
}

.day-column-header:hover {
  background: rgba(15, 23, 42, 0.04);
}

.day-column-header:focus-visible {
  outline: 2px solid #00ff9c;
  outline-offset: -2px;
}

.day-column-name {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 10px;
  font-weight: 900;
  letter-spacing: 0.2em;
  color: #475569;
}

.day-column-number {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border-radius: 9999px;
  background: rgba(15, 23, 42, 0.05);
  border: 1px solid rgba(15, 23, 42, 0.1);
  font-size: 12px;
  font-weight: 800;
  color: #0f172a;
  transition: all 200ms ease;
}

.is-active-day .day-column-number {
  background: #0f172a;
  color: #fff;
  border-color: #0f172a;
}

.day-column-dot {
  width: 6px;
  height: 6px;
  border-radius: 9999px;
  background: rgba(0, 255, 156, 0.7);
}

.hour-rows {
  display: flex;
  flex-direction: column;
  padding: 8px;
  gap: 8px;
}

.hour-row {
  display: grid;
  grid-template-columns: 52px 1fr;
  gap: 8px;
  align-items: start;
}

.hour-gutter {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 4px;
  padding-top: 10px;
}

.hour-label {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 10px;
  font-weight: 700;
  color: #64748b;
}

.cursor-marker {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.cursor-dot {
  width: 8px;
  height: 8px;
  border-radius: 9999px;
  background: #00ff9c;
  box-shadow: 0 0 8px rgba(0, 255, 156, 0.8);
}

.cursor-label {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 9px;
  font-weight: 900;
  letter-spacing: 0.1em;
  color: #059669;
}

.hour-events {
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-width: 0;
}

.day-column-empty {
  padding: 16px 8px;
  text-align: center;
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 10px;
  letter-spacing: 0.3em;
  text-transform: uppercase;
  color: rgba(15, 23, 42, 0.25);
}

/* Keep motion finite: only short opacity shifts to preserve map orientation. */
.group-list.phase-filter-refresh {
  opacity: 0.88;
}

.group-list.defer-selection-motion :deep(.calendar-event-card) {
  transition: none;
}

@media (prefers-reduced-motion: reduce) {
  .group-list {
    transition: none;
  }
}
</style>
