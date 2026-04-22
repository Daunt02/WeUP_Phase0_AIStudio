import { computed, ref } from "vue";
import type {
  AnonymousDiscoveryContext,
  AnonymousSavedState,
} from "../contracts/anonymous-local-persistence.contracts";
import type {
  SaveStateLocalDiscoveryContextDto,
  SaveStateMigrationRequestDto,
  SaveStateMigrationResultDto,
} from "../contracts/event-detail.contracts";
import {
  ApiRequestError,
  migrateAnonymousSaveState,
} from "../services/eventDetailService";
import { useAnonymousLocalPersistence } from "./useAnonymousLocalPersistence";

function normalizeEventId(eventId: string): string {
  return eventId.trim();
}

function normalizeDistinctEventIds(eventIds: string[]): string[] {
  const normalized = eventIds
    .map((id) => normalizeEventId(id))
    .filter((id) => id.length > 0);

  return Array.from(new Set(normalized)).sort((left, right) =>
    left.localeCompare(right, "en", { sensitivity: "base" }),
  );
}

function toDiscoveryContextDto(
  context: AnonymousDiscoveryContext,
): SaveStateLocalDiscoveryContextDto | null {
  const hasPayload =
    Boolean(context.lastViewedDistrict) ||
    Boolean(context.lastTemporalFilter) ||
    Boolean(context.recentMapViewport);

  if (!hasPayload) {
    return null;
  }

  return {
    lastViewedDistrict: context.lastViewedDistrict ?? null,
    lastTemporalFilter: context.lastTemporalFilter
      ? {
          preset: context.lastTemporalFilter.preset,
          timezone: context.lastTemporalFilter.timezone,
          customStartUtc: context.lastTemporalFilter.customStartUtc ?? null,
          customEndUtc: context.lastTemporalFilter.customEndUtc ?? null,
        }
      : null,
    recentMapViewport: context.recentMapViewport
      ? {
          bbox: context.recentMapViewport.bbox,
          capturedAtUtc: context.recentMapViewport.capturedAtUtc,
        }
      : null,
    updatedAtUtc: context.updatedAtUtc ?? null,
  };
}

function buildMigrationRequest(
  savedState: AnonymousSavedState,
  discoveryContext: AnonymousDiscoveryContext,
  clientMigrationKey?: string,
): SaveStateMigrationRequestDto {
  return {
    localSavedEventIds: normalizeDistinctEventIds(savedState.savedEventIds),
    localDiscoveryContext: toDiscoveryContextDto(discoveryContext),
    clientMigrationKey: clientMigrationKey?.trim() || null,
  };
}

const isMigrationInFlight = ref(false);
const lockedEventIds = ref<string[]>([]);
const lastResult = ref<SaveStateMigrationResultDto | null>(null);
const lastError = ref<string | null>(null);

export function useSaveStateMigration() {
  const anonymousPersistence = useAnonymousLocalPersistence();

  const hasMigrationIssues = computed(() => {
    return lastResult.value?.status === "completed-with-issues";
  });

  function isEventLockedForMigration(eventId: string): boolean {
    const normalized = normalizeEventId(eventId);
    if (!normalized) {
      return false;
    }

    return lockedEventIds.value.includes(normalized);
  }

  async function triggerMigrationOnSignIn(
    clientMigrationKey?: string,
  ): Promise<SaveStateMigrationResultDto> {
    if (isMigrationInFlight.value) {
      throw new Error("Save-state migration already in progress.");
    }

    const localSavedState = anonymousPersistence.getSavedState();
    const localDiscoveryContext = anonymousPersistence.getDiscoveryContext();
    const request = buildMigrationRequest(
      localSavedState,
      localDiscoveryContext,
      clientMigrationKey,
    );

    // During migration, lock all local saved IDs to prevent concurrent duplicate toggles.
    lockedEventIds.value = [...request.localSavedEventIds];
    isMigrationInFlight.value = true;
    lastError.value = null;

    try {
      const result = await migrateAnonymousSaveState(request);

      // Local state is only changed after a confirmed migration response.
      // Any retained IDs are explicitly surfaced by backend conflict handling.
      anonymousPersistence.setSavedEventIds(result.retainedLocalSavedEventIds);
      lastResult.value = result;
      return result;
    } catch (cause) {
      if (cause instanceof ApiRequestError) {
        lastError.value = cause.message;
      } else if (cause instanceof Error) {
        lastError.value = cause.message;
      } else {
        lastError.value = "Save-state migration failed.";
      }
      throw cause;
    } finally {
      lockedEventIds.value = [];
      isMigrationInFlight.value = false;
    }
  }

  return {
    isMigrationInFlight,
    hasMigrationIssues,
    lastResult,
    lastError,
    isEventLockedForMigration,
    triggerMigrationOnSignIn,
  };
}
