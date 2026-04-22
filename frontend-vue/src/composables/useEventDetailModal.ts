import { computed, ref, watch, type Ref } from "vue";
import type {
  EventDetailDto,
  SavedStateDto,
} from "../contracts/event-detail.contracts";
import { fetchEventDetail } from "../services/eventDetailService";

interface SavedStateAuthority {
  resolveSavedState: (eventId: string) => Promise<SavedStateDto>;
  mutateSavedState: (
    eventId: string,
    targetSaved: boolean,
    applyUiSavedState: (saved: boolean) => void,
  ) => Promise<SavedStateDto>;
}

export function useEventDetailModal(
  selectedEventId: Ref<string | null> = ref<string | null>(null),
  savedStateAuthority?: SavedStateAuthority,
) {
  const eventDetail = ref<EventDetailDto | null>(null);
  const isLoading = ref(false);
  const isSavePending = ref(false);
  const error = ref<string | null>(null);
  const activeRequestId = ref(0);

  const isOpen = computed(() => selectedEventId.value !== null);

  // Selection is the shared source of truth for both the map highlight and the
  // modal. Marker clicks set selectedEventId; clearing the modal resets it.
  watch(
    selectedEventId,
    async (eventId) => {
      activeRequestId.value += 1;
      const requestId = activeRequestId.value;

      if (!eventId) {
        eventDetail.value = null;
        error.value = null;
        isLoading.value = false;
        return;
      }

      if (eventDetail.value?.id === eventId) {
        error.value = null;
        return;
      }

      isLoading.value = true;
      error.value = null;

      try {
        const response = await fetchEventDetail(eventId);

        if (
          requestId !== activeRequestId.value ||
          selectedEventId.value !== eventId
        ) {
          return;
        }

        if (!response.event) {
          throw new Error(
            "Event detail was not found for the selected eventId.",
          );
        }

        let resolvedDetail = response.event;
        if (savedStateAuthority) {
          const savedState =
            await savedStateAuthority.resolveSavedState(eventId);

          if (
            requestId !== activeRequestId.value ||
            selectedEventId.value !== eventId
          ) {
            return;
          }

          resolvedDetail = {
            ...resolvedDetail,
            savedByCurrentUser: savedState.saved,
          };
        }

        eventDetail.value = resolvedDetail;
      } catch (cause) {
        if (
          requestId !== activeRequestId.value ||
          selectedEventId.value !== eventId
        ) {
          return;
        }

        eventDetail.value = null;
        error.value =
          cause instanceof Error
            ? cause.message
            : "Failed to load event detail.";
      } finally {
        if (
          requestId === activeRequestId.value &&
          selectedEventId.value === eventId
        ) {
          isLoading.value = false;
        }
      }
    },
    { immediate: true },
  );

  function closeModal(): void {
    activeRequestId.value += 1;
    selectedEventId.value = null;
    eventDetail.value = null;
    error.value = null;
    isLoading.value = false;
  }

  async function toggleSavedState(): Promise<void> {
    const detail = eventDetail.value;
    if (!detail) {
      return;
    }

    isSavePending.value = true;
    error.value = null;

    try {
      const targetSavedState = !detail.savedByCurrentUser;

      const applyUiSavedState = (saved: boolean): void => {
        if (eventDetail.value?.id !== detail.id) {
          return;
        }

        eventDetail.value = {
          ...eventDetail.value,
          savedByCurrentUser: saved,
        };
      };

      if (savedStateAuthority) {
        await savedStateAuthority.mutateSavedState(
          detail.id,
          targetSavedState,
          applyUiSavedState,
        );
      } else {
        applyUiSavedState(targetSavedState);
      }

      if (eventDetail.value?.id !== detail.id) {
        return;
      }
    } catch (cause) {
      error.value =
        cause instanceof Error ? cause.message : "Failed to update save state.";
    } finally {
      isSavePending.value = false;
    }
  }

  return {
    selectedEventId,
    eventDetail,
    isOpen,
    isLoading,
    isSavePending,
    error,
    closeModal,
    toggleSavedState,
  };
}
