<template>
  <q-card
    flat
    bordered
    class="calendar-event-card"
    :class="{
      'is-selected': isSelected,
      'is-saved': event.source.savedByCurrentUser,
    }"
    :style="{
      gridColumn: `${event.columnIndex + 1}`,
      gridRow: `span ${event.rowSpan}`,
    }"
    @click="$emit('select-event', event.eventId)"
  >
    <q-card-section class="card-body">
      <div class="card-time">{{ event.displayTimeLabel }}</div>
      <div class="card-title">{{ event.source.title }}</div>
      <div class="card-meta">{{ event.secondaryLabel }}</div>
      <div class="card-taxonomy">
        {{ event.source.primaryCategory }}
      </div>
      <q-chip
        v-if="event.source.savedByCurrentUser"
        dense
        square
        color="red-1"
        text-color="red-9"
        class="saved-chip"
      >
        Saved
      </q-chip>
    </q-card-section>
  </q-card>
</template>

<script setup lang="ts">
import type { PackedCalendarEvent } from "../composables/useCalendarMasonryLayout";

/**
 * Event card contract for calendar masonry rows.
 * eventId remains canonical and is surfaced unchanged in selection emits.
 */
defineProps<{
  event: PackedCalendarEvent;
  isSelected: boolean;
}>();

defineEmits<{
  (event: "select-event", eventId: string): void;
}>();
</script>

<style scoped>
.calendar-event-card {
  cursor: pointer;
  border-radius: 10px;
  border-color: rgba(15, 23, 42, 0.14);
  background: rgba(255, 255, 255, 0.94);
  transition:
    transform 120ms ease,
    box-shadow 120ms ease,
    border-color 120ms ease;
}

.calendar-event-card:hover {
  transform: translateY(-1px);
  box-shadow: 0 8px 16px rgba(15, 23, 42, 0.14);
}

.calendar-event-card.is-selected {
  border-color: rgba(37, 99, 235, 0.5);
  box-shadow: 0 0 0 2px rgba(37, 99, 235, 0.2);
}

.calendar-event-card.is-saved {
  border-left: 4px solid rgba(185, 28, 28, 0.7);
}

.card-body {
  padding: 8px;
  display: grid;
  gap: 4px;
}

.card-time {
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.02em;
  color: #0f172a;
}

.card-title {
  font-size: 13px;
  line-height: 1.25;
  font-weight: 700;
  color: #0f172a;
}

.card-meta {
  font-size: 11px;
  color: #475569;
}

.card-taxonomy {
  font-size: 11px;
  color: #1d4ed8;
  text-transform: capitalize;
}

.saved-chip {
  justify-self: start;
}
</style>
