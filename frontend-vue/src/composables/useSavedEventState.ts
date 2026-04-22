import { computed, ref } from "vue";
import type { SavedStateDto } from "../contracts/event-detail.contracts";
import {
  ApiRequestError,
  fetchSavedState,
  saveEvent,
  unsaveEvent,
} from "../services/eventDetailService";
import { useAnonymousLocalPersistence } from "./useAnonymousLocalPersistence";
import { useSaveStateMigration } from "./useSaveStateMigration";

type SavedStateCache = Record<string, SavedStateDto>;

function normalizeEventId(eventId: string): string {
  return eventId.trim();
}

function buildAnonymousSavedState(
  eventId: string,
  saved: boolean,
): SavedStateDto {
  return {
    eventId,
    saved,
    sessionKind: "anonymous",
    persistenceSource: "anonymous-local",
  };
}

export function useSavedEventState() {
  const anonymousLocalPersistence = useAnonymousLocalPersistence();
  const saveStateMigration = useSaveStateMigration();

  const savedStateByEventId = ref<SavedStateCache>({});
  const pendingByEventId = ref<Record<string, boolean>>({});

  const hasPendingMutation = computed(() => {
    return Object.values(pendingByEventId.value).some(Boolean);
  });

  function getCachedSavedState(eventId: string): SavedStateDto | null {
    const normalizedEventId = normalizeEventId(eventId);
    if (!normalizedEventId) {
      return null;
    }

    return savedStateByEventId.value[normalizedEventId] ?? null;
  }

  function setSavedStateCache(nextState: SavedStateDto): void {
    savedStateByEventId.value = {
      ...savedStateByEventId.value,
      [nextState.eventId]: nextState,
    };
  }

  function setPending(eventId: string, pending: boolean): void {
    pendingByEventId.value = {
      ...pendingByEventId.value,
      [eventId]: pending,
    };
  }

  async function resolveSavedState(eventId: string): Promise<SavedStateDto> {
    const normalizedEventId = normalizeEventId(eventId);
    if (!normalizedEventId) {
      throw new Error(
        "Canonical eventId is required for saved-state resolution.",
      );
    }

    const cached = getCachedSavedState(normalizedEventId);
    if (cached) {
      return cached;
    }

    try {
      const remote = await fetchSavedState(normalizedEventId);
      setSavedStateCache(remote);
      return remote;
    } catch (cause) {
      if (cause instanceof ApiRequestError && cause.status === 401) {
        const localSaved = anonymousLocalPersistence
          .getSavedState()
          .savedEventIds.includes(normalizedEventId);
        const anonymousState = buildAnonymousSavedState(
          normalizedEventId,
          localSaved,
        );

        setSavedStateCache(anonymousState);
        return anonymousState;
      }

      throw cause;
    }
  }

  async function mutateSavedState(
    eventId: string,
    targetSaved: boolean,
    applyUiSavedState: (saved: boolean) => void,
  ): Promise<SavedStateDto> {
    const normalizedEventId = normalizeEventId(eventId);
    if (!normalizedEventId) {
      throw new Error("Canonical eventId is required for save-state mutation.");
    }

    if (saveStateMigration.isEventLockedForMigration(normalizedEventId)) {
      throw new Error(
        "Save-state migration is in progress for this event. Retry after migration completes.",
      );
    }

    const previousState = await resolveSavedState(normalizedEventId);

    // Optimistic mutation is applied first, but must be reconciled with persistence.
    const optimisticState: SavedStateDto = {
      ...previousState,
      saved: targetSaved,
    };
    setSavedStateCache(optimisticState);
    applyUiSavedState(optimisticState.saved);
    setPending(normalizedEventId, true);

    try {
      let resolvedState: SavedStateDto;

      if (previousState.sessionKind === "anonymous") {
        if (targetSaved) {
          anonymousLocalPersistence.addSavedEventId(normalizedEventId);
        } else {
          anonymousLocalPersistence.removeSavedEventId(normalizedEventId);
        }
        resolvedState = buildAnonymousSavedState(
          normalizedEventId,
          targetSaved,
        );
      } else {
        try {
          const response = targetSaved
            ? await saveEvent({ eventId: normalizedEventId })
            : await unsaveEvent({ eventId: normalizedEventId });

          resolvedState = {
            eventId: response.eventId,
            saved: response.saved,
            sessionKind: "authenticated",
            persistenceSource: "backend",
          };
        } catch (cause) {
          if (cause instanceof ApiRequestError && cause.status === 401) {
            if (targetSaved) {
              anonymousLocalPersistence.addSavedEventId(normalizedEventId);
            } else {
              anonymousLocalPersistence.removeSavedEventId(normalizedEventId);
            }
            resolvedState = buildAnonymousSavedState(
              normalizedEventId,
              targetSaved,
            );
          } else {
            throw cause;
          }
        }
      }

      setSavedStateCache(resolvedState);
      applyUiSavedState(resolvedState.saved);
      return resolvedState;
    } catch (cause) {
      // Roll back optimistic UI when persistence fails.
      setSavedStateCache(previousState);
      applyUiSavedState(previousState.saved);
      throw cause;
    } finally {
      setPending(normalizedEventId, false);
    }
  }

  return {
    savedStateByEventId,
    pendingByEventId,
    hasPendingMutation,
    isMigrationInFlight: saveStateMigration.isMigrationInFlight,
    getCachedSavedState,
    resolveSavedState,
    mutateSavedState,
  };
}
