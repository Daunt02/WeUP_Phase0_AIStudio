<template>
  <div class="overlay-anchor" :class="anchorClass">
    <q-btn
      v-if="layer === 'closed'"
      class="overlay-open-btn"
      color="primary"
      icon="event"
      label="Calendar"
      unelevated
      @click="$emit('set-layer', 'partial')"
    />

    <transition name="overlay-slide">
      <q-card
        v-if="layer !== 'closed'"
        flat
        bordered
        class="overlay-card"
        :style="{ height: overlayHeight }"
      >
        <CalendarTimelineHeader
          :layer="layer"
          :total-count="items.length"
          @set-layer="$emit('set-layer', $event)"
        />

        <CalendarEventGrid
          :items="items"
          :selected-event-id="selectedEventId"
          :is-loading="isLoading"
          :error="error"
          @select-event="$emit('select-event', $event)"
        />
      </q-card>
    </transition>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import type {
  CalendarEventItemDto,
  CalendarOverlayLayerState,
} from "../contracts/calendar-overlay.contracts";
import CalendarEventGrid from "./CalendarEventGrid.vue";
import CalendarTimelineHeader from "./CalendarTimelineHeader.vue";

const props = defineProps<{
  layer: CalendarOverlayLayerState;
  overlayHeight: string;
  selectedEventId: string | null;
  items: CalendarEventItemDto[];
  isLoading?: boolean;
  error?: string | null;
}>();

defineEmits<{
  (event: "set-layer", layer: CalendarOverlayLayerState): void;
  (event: "select-event", eventId: string): void;
}>();

const anchorClass = computed(() => {
  return props.layer === "closed" ? "is-closed" : "is-open";
});
</script>

<style scoped>
.overlay-anchor {
  position: absolute;
  left: 16px;
  right: 16px;
  bottom: 16px;
  z-index: 20;
  pointer-events: none;
}

.overlay-anchor.is-open {
  top: 84px;
}

.overlay-open-btn,
.overlay-card {
  pointer-events: auto;
}

.overlay-open-btn {
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.2);
}

.overlay-card {
  width: 100%;
  display: flex;
  flex-direction: column;
  background: linear-gradient(
    180deg,
    rgba(255, 255, 255, 0.96) 0%,
    rgba(241, 245, 249, 0.94) 100%
  );
  backdrop-filter: blur(8px);
  border: 1px solid rgba(15, 23, 42, 0.12);
  border-radius: 14px;
  overflow: hidden;
  box-shadow: 0 20px 44px rgba(15, 23, 42, 0.18);
}

.overlay-slide-enter-active,
.overlay-slide-leave-active {
  transition:
    transform 220ms ease,
    opacity 220ms ease;
}

.overlay-slide-enter-from,
.overlay-slide-leave-to {
  transform: translateY(20px);
  opacity: 0;
}

@media (max-width: 768px) {
  .overlay-anchor {
    left: 8px;
    right: 8px;
    bottom: 8px;
  }

  .overlay-anchor.is-open {
    top: 72px;
  }
}
</style>
