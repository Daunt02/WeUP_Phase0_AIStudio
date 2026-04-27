<template>
  <div class="filter-panel">
    <q-select
      :model-value="preset"
      :options="presetOptions"
      emit-value
      map-options
      dense
      outlined
      label="Time window"
      @update:model-value="emit('update:preset', $event)"
    />

    <q-input
      :model-value="timezone"
      dense
      outlined
      label="Timezone"
      hint="Required for server-side preset resolution"
      @update:model-value="emit('update:timezone', String($event))"
    />

    <q-input
      v-if="isCustomPreset"
      :model-value="customStartLocal"
      type="datetime-local"
      dense
      outlined
      label="Custom start"
      @update:model-value="emit('update:customStartLocal', String($event))"
    />

    <q-input
      v-if="isCustomPreset"
      :model-value="customEndLocal"
      type="datetime-local"
      dense
      outlined
      label="Custom end"
      @update:model-value="emit('update:customEndLocal', String($event))"
    />

    <q-toggle
      :model-value="includeSavedOnly"
      label="Saved only"
      color="primary"
      @update:model-value="emit('update:includeSavedOnly', Boolean($event))"
    />

    <q-btn
      color="primary"
      :loading="isLoading"
      :disable="Boolean(validationError)"
      label="Refresh"
      @click="emit('refresh')"
    />
  </div>

  <q-banner v-if="validationError" class="validation-banner" rounded>
    {{ validationError }}
  </q-banner>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { TimeWindowPreset } from "../contracts/time-window.contracts";
import { isCustomRangePreset } from "../contracts/temporal-query.contracts";

const emit = defineEmits<{
  (event: "update:preset", value: TimeWindowPreset): void;
  (event: "update:timezone", value: string): void;
  (event: "update:customStartLocal", value: string): void;
  (event: "update:customEndLocal", value: string): void;
  (event: "update:includeSavedOnly", value: boolean): void;
  (event: "refresh"): void;
}>();

const presetOptions = [
  { label: "Now", value: TimeWindowPreset.Now },
  { label: "Tonight", value: TimeWindowPreset.Tonight },
  { label: "Tomorrow", value: TimeWindowPreset.Tomorrow },
  { label: "This Weekend", value: TimeWindowPreset.ThisWeekend },
  { label: "Next 24 Hours", value: TimeWindowPreset.Next24Hours },
  { label: "Next 48 Hours", value: TimeWindowPreset.Next48Hours },
  { label: "Custom Range", value: TimeWindowPreset.CustomRange },
];

const props = defineProps<{
  preset: TimeWindowPreset;
  timezone: string;
  customStartLocal: string;
  customEndLocal: string;
  includeSavedOnly: boolean;
  isLoading: boolean;
  validationError: string | null;
}>();

const isCustomPreset = computed(() => isCustomRangePreset(props.preset));
</script>

<style scoped>
.filter-panel {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 12px;
  align-items: end;
}

.validation-banner {
  margin-top: 12px;
  background: #fff3cd;
  color: #6b4f00;
}
</style>
