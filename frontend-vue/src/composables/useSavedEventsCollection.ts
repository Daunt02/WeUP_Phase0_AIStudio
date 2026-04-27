import { computed, ref, watch, type Ref } from "vue";
import type {
  EventDetailDto,
  SavePersistenceSource,
  SaveSessionKind,
  SavedStateDto,
} from "../contracts/event-detail.contracts";
import type { EventMapItemDto } from "../contracts/map-feed.contracts";
import type {
  SavedEventDto,
  SavedEventsQueryDto,
  SavedEventsResponseDto,
  SavedEventsSurfaceSnapshot,
} from "../contracts/saved-events.contracts";
import {
  ApiRequestError,
  fetchEventDetail,
} from "../services/eventDetailService";
import { fetchSavedEvents } from "../services/savedEventsService";
import { useAnonymousLocalPersistence } from "./useAnonymousLocalPersistence";

interface SavedStateCollectionAuthority {
  readonly mutationRevision: Ref<number>;
  readonly primeSavedStates: (states: readonly SavedStateDto[]) => void;
}

function toCanonicalMapItem(detail: EventDetailDto): EventMapItemDto {
  return {
    eventId: detail.id,
    title: detail.title,
    startUtc: detail.startUtc,
    endUtc: detail.endUtc,
    latitude: detail.lat,
    longitude: detail.lng,
    venueName: detail.venueName,
    district: null,
    primaryCategory: detail.category,
    savedByCurrentUser: true,
    markerState: "saved",
  };
}

function createSavedState(
  eventId: string,
  sessionKind: SaveSessionKind,
  persistenceSource: SavePersistenceSource,
): SavedStateDto {
  return {
    eventId,
    saved: true,
    sessionKind,
    persistenceSource,
  };
}

async function resolveAnonymousSavedItem(
  eventId: string,
  savedAt: string,
): Promise<{ item: SavedEventDto; degradedReason: string | null }> {
  try {
    const response = await fetchEventDetail(eventId);

    if (!response.event) {
      return {
        item: {
          eventId,
          savedAt,
          resolutionStatus: "missing-or-deleted",
          resolutionMessage:
            "Saved event no longer resolves to a canonical event.",
          canonicalEvent: null,
        },
        degradedReason: null,
      };
    }

    return {
      item: {
        eventId,
        savedAt,
        resolutionStatus: "resolved",
        resolutionMessage: null,
        canonicalEvent: toCanonicalMapItem(response.event),
      },
      degradedReason: null,
    };
  } catch (cause) {
    if (cause instanceof ApiRequestError && cause.status === 404) {
      return {
        item: {
          eventId,
          savedAt,
          resolutionStatus: "missing-or-deleted",
          resolutionMessage:
            "Saved event was deleted or is no longer publicly available.",
          canonicalEvent: null,
        },
        degradedReason: null,
      };
    }

    return {
      item: {
        eventId,
        savedAt,
        resolutionStatus: "missing-or-deleted",
        resolutionMessage:
          "Canonical event data could not be refreshed. Showing the saved reference only.",
        canonicalEvent: null,
      },
      degradedReason:
        cause instanceof Error
          ? cause.message
          : "Anonymous saved events could not be fully refreshed.",
    };
  }
}

async function buildAnonymousSavedEventsResponse(
  query: Required<SavedEventsQueryDto>,
  savedEventIds: readonly string[],
  savedAt: string,
): Promise<SavedEventsResponseDto & { degradedReason: string | null }> {
  const start = (query.page - 1) * query.pageSize;
  const pageItems = savedEventIds.slice(start, start + query.pageSize);
  const resolvedResults = await Promise.all(
    pageItems.map((eventId) => resolveAnonymousSavedItem(eventId, savedAt)),
  );

  const items = resolvedResults.map((result) => result.item);
  const resolvedCount = items.filter(
    (item) => item.canonicalEvent !== null,
  ).length;
  const degradedReason =
    resolvedResults.find((result) => result.degradedReason)?.degradedReason ??
    null;

  return {
    items,
    totalCount: savedEventIds.length,
    page: query.page,
    pageSize: query.pageSize,
    hasNextPage: start + items.length < savedEventIds.length,
    resolvedCount,
    missingOrDeletedCount: items.length - resolvedCount,
    retrievedAtUtc: new Date().toISOString(),
    sourceProjection: "canonical-event-map-v1",
    degradedReason,
  };
}

export function useSavedEventsCollection(
  savedStateAuthority?: SavedStateCollectionAuthority,
  initialQuery: SavedEventsQueryDto = {},
) {
  const anonymousLocalPersistence = useAnonymousLocalPersistence();
  const page = ref(Math.max(1, initialQuery.page ?? 1));
  const pageSize = ref(Math.min(100, Math.max(1, initialQuery.pageSize ?? 50)));

  const response = ref<SavedEventsResponseDto | null>(null);
  const sessionKind = ref<SaveSessionKind>("anonymous");
  const isLoading = ref(false);
  const isRefreshing = ref(false);
  const error = ref<string | null>(null);
  const degradedReason = ref<string | null>(null);
  let activeRequestId = 0;

  function primeResolvedSavedStates(
    items: readonly SavedEventDto[],
    nextSessionKind: SaveSessionKind,
  ): void {
    if (!savedStateAuthority) {
      return;
    }

    const persistenceSource: SavePersistenceSource =
      nextSessionKind === "authenticated" ? "backend" : "anonymous-local";

    savedStateAuthority.primeSavedStates(
      items
        .filter((item) => item.canonicalEvent !== null)
        .map((item) =>
          createSavedState(item.eventId, nextSessionKind, persistenceSource),
        ),
    );
  }

  async function loadSavedEvents(): Promise<void> {
    const requestId = ++activeRequestId;
    const hasExistingData = response.value !== null;

    if (hasExistingData) {
      isRefreshing.value = true;
    } else {
      isLoading.value = true;
    }

    error.value = null;

    const query = {
      page: page.value,
      pageSize: pageSize.value,
    } satisfies Required<SavedEventsQueryDto>;

    try {
      const remote = await fetchSavedEvents(query);
      if (requestId !== activeRequestId) {
        return;
      }

      response.value = remote;
      sessionKind.value = "authenticated";
      degradedReason.value =
        remote.missingOrDeletedCount > 0
          ? "Some saved references no longer resolve to active canonical events."
          : null;
      primeResolvedSavedStates(remote.items, "authenticated");
      return;
    } catch (cause) {
      if (!(cause instanceof ApiRequestError) || cause.status !== 401) {
        if (requestId !== activeRequestId) {
          return;
        }

        const message =
          cause instanceof Error
            ? cause.message
            : "Saved events could not be loaded.";

        if (response.value !== null) {
          degradedReason.value = message;
        } else {
          error.value = message;
        }
        return;
      }
      const anonymousSavedState = anonymousLocalPersistence.getSavedState();
      const fallback = await buildAnonymousSavedEventsResponse(
        query,
        anonymousSavedState.savedEventIds,
        anonymousSavedState.updatedAtUtc,
      );

      if (requestId !== activeRequestId) {
        return;
      }

      response.value = fallback;
      sessionKind.value = "anonymous";
      degradedReason.value = fallback.degradedReason;
      primeResolvedSavedStates(fallback.items, "anonymous");
    } finally {
      if (requestId === activeRequestId) {
        isLoading.value = false;
        isRefreshing.value = false;
      }
    }
  }

  const items = computed(() => response.value?.items ?? []);
  const totalCount = computed(() => response.value?.totalCount ?? 0);
  const resolvedCount = computed(() => response.value?.resolvedCount ?? 0);
  const missingOrDeletedCount = computed(
    () => response.value?.missingOrDeletedCount ?? 0,
  );
  const hasNextPage = computed(() => response.value?.hasNextPage ?? false);
  const savedEventIds = computed(() => items.value.map((item) => item.eventId));
  const markerItems = computed(() => {
    return items.value
      .map((item) => item.canonicalEvent)
      .filter((item): item is EventMapItemDto => item !== null);
  });
  const surfaceSnapshot = computed<SavedEventsSurfaceSnapshot>(() => ({
    sessionKind: sessionKind.value,
    savedCountBadgeValue: totalCount.value,
    savedEventIds: [...savedEventIds.value],
    resolvedItems: items.value.filter((item) => item.canonicalEvent !== null),
    missingOrDeletedItems: items.value.filter(
      (item) => item.canonicalEvent === null,
    ),
    markerItems: [...markerItems.value],
  }));

  watch(
    [page, pageSize],
    () => {
      void loadSavedEvents();
    },
    { immediate: true },
  );

  watch(
    () => savedStateAuthority?.mutationRevision.value,
    (nextRevision, previousRevision) => {
      if (
        nextRevision === undefined ||
        previousRevision === undefined ||
        nextRevision === previousRevision
      ) {
        return;
      }

      // Save-state mutations must round-trip back into the saved panel/count surfaces
      // so the list, badge, and map markers converge on the same canonical truth.
      void loadSavedEvents();
    },
  );

  return {
    page,
    pageSize,
    sessionKind,
    response,
    items,
    totalCount,
    resolvedCount,
    missingOrDeletedCount,
    hasNextPage,
    savedEventIds,
    markerItems,
    surfaceSnapshot,
    isLoading,
    isRefreshing,
    error,
    degradedReason,
    loadSavedEvents,
  };
}
