<template>
  <q-card
    flat
    bordered
    class="calendar-event-card"
    data-plane="z2"
    :data-state="isSelected ? 'selected' : 'idle'"
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
      <div class="card-plane card-plane-metadata">
        <div class="card-time">{{ event.displayTimeLabel }}</div>
        <div class="card-taxonomy">
          {{ event.source.primaryCategory }}
        </div>
      </div>
      <div class="card-plane card-plane-identity">
        <div class="card-title">{{ event.source.title }}</div>
      </div>
      <div class="card-plane card-plane-description">
        <div class="card-meta">{{ event.secondaryLabel }}</div>
      </div>
      <q-chip
        v-if="event.source.savedByCurrentUser"
        dense
        square
        color="red-1"
        text-color="red-9"
        class="saved-chip card-plane card-plane-action"
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
/* WEUP-2.5D (D09): layered signal-card surface. Base elevation comes from
   the global [data-plane="z2"] rule; selected/focused glow from the global
   [data-state] rules; transitions from the token motion grammar. */
.calendar-event-card {
  cursor: pointer;
  border-radius: var(--weup-radius-card);
  border-color: rgba(15, 23, 42, 0.14);
  background: rgba(255, 255, 255, 0.94);
}

.calendar-event-card:hover {
  box-shadow: var(--weup-elevation-3);
}

.calendar-event-card:focus-visible {
  outline: 2px solid rgba(37, 99, 235, 0.65);
  outline-offset: 2px;
}

.calendar-event-card.is-selected {
  border-color: rgba(37, 99, 235, 0.5);
}

/* Semantic internal planes (mission §IX): metadata / identity /
   description / action. Separators are structural, not decorative. */
.card-plane + .card-plane {
  border-top: 1px solid rgba(15, 23, 42, 0.08);
  padding-top: var(--weup-spacing-1);
  margin-top: var(--weup-spacing-1);
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
