<template>
  <q-btn
    :loading="isPending"
    :disable="isPending"
    :outline="!saved"
    :color="saved ? 'accent' : 'primary'"
    :icon="saved ? 'bookmark' : 'bookmark_add'"
    :label="saved ? 'Saved' : 'Save'"
    @click="toggle"
  />
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useSavedEventState } from "../composables/useSavedEventState";

interface Props {
  eventId: string;
}

const props = defineProps<Props>();
const savedState = useSavedEventState();
const saved = ref(false);
const error = ref<string | null>(null);

const isPending = computed(() => {
  return Boolean(savedState.pendingByEventId.value[props.eventId]);
});

onMounted(async () => {
  const resolved = await savedState.resolveSavedState(props.eventId);
  saved.value = resolved.saved;
});

async function toggle(): Promise<void> {
  error.value = null;

  try {
    await savedState.mutateSavedState(
      props.eventId,
      !saved.value,
      (nextSaved) => {
        // UI is optimistic first, then reconciled against persistence result.
        saved.value = nextSaved;
      },
    );
  } catch (cause) {
    error.value =
      cause instanceof Error ? cause.message : "Failed to update saved state.";
  }
}
</script>
