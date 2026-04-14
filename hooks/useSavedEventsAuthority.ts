"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { getAuthHeader } from "@/services/auth";
import { toApiUrl } from "@/services/apiBase";

const LOCAL_ANON_SAVES_KEY = "weup.saved-events.anon.v1";

export type SaveAuthoritySessionKind = "anonymous" | "authenticated";

export interface SaveAuthorityState {
  sessionKind: SaveAuthoritySessionKind;
  savedEventIds: string[];
  loading: boolean;
  syncing: boolean;
  error: string | null;
}

interface SavedEventsApiPayload {
  items?: Array<{ eventId?: string; EventId?: string }>;
  Items?: Array<{ eventId?: string; EventId?: string }>;
  hasNextPage?: boolean;
  HasNextPage?: boolean;
}

function normalizeEventIds(value: unknown): string[] {
  if (!Array.isArray(value)) return [];

  const ids = value
    .map((entry) => (typeof entry === "string" ? entry.trim() : ""))
    .filter((entry) => entry.length > 0);

  return Array.from(new Set(ids));
}

function readAnonymousSavedEventIds(): string[] {
  if (typeof window === "undefined") return [];

  try {
    const raw = localStorage.getItem(LOCAL_ANON_SAVES_KEY);
    if (!raw) return [];

    return normalizeEventIds(JSON.parse(raw));
  } catch {
    return [];
  }
}

function writeAnonymousSavedEventIds(ids: string[]) {
  if (typeof window === "undefined") return;

  try {
    const normalized = normalizeEventIds(ids);
    if (normalized.length === 0) {
      localStorage.removeItem(LOCAL_ANON_SAVES_KEY);
      return;
    }

    localStorage.setItem(LOCAL_ANON_SAVES_KEY, JSON.stringify(normalized));
  } catch {
    // Intentionally ignore storage failures; save authority still works in-memory.
  }
}

function extractSavedIds(payload: SavedEventsApiPayload): string[] {
  const items = payload.items ?? payload.Items ?? [];

  const ids = items
    .map((item) => {
      const id = item.eventId ?? item.EventId;
      return typeof id === "string" ? id.trim() : "";
    })
    .filter((id) => id.length > 0);

  return Array.from(new Set(ids));
}

function hasNextPage(payload: SavedEventsApiPayload): boolean {
  return Boolean(payload.hasNextPage ?? payload.HasNextPage ?? false);
}

async function fetchRemoteSavedEventIds(
  headers: Record<string, string>,
): Promise<string[]> {
  const collected: string[] = [];
  let page = 1;
  let hasNext = true;

  while (hasNext && page <= 20) {
    const response = await fetch(
      toApiUrl(`/api/users/me/saves?page=${page}&pageSize=100`),
      { headers },
    );

    if (!response.ok) {
      throw new Error(
        `Save list request failed with status ${response.status}.`,
      );
    }

    const payload = (await response.json()) as SavedEventsApiPayload;
    const pageIds = extractSavedIds(payload);
    collected.push(...pageIds);

    hasNext = hasNextPage(payload);
    page += 1;
  }

  return Array.from(new Set(collected));
}

async function persistRemoteSave(
  eventId: string,
  saved: boolean,
  headers: Record<string, string>,
): Promise<void> {
  const response = await fetch(
    toApiUrl(`/api/users/me/saves/${encodeURIComponent(eventId)}`),
    {
      method: saved ? "POST" : "DELETE",
      headers,
    },
  );

  if (!response.ok) {
    throw new Error(`Save request failed with status ${response.status}.`);
  }
}

export function useSavedEventsAuthority() {
  const [state, setState] = useState<SaveAuthorityState>({
    sessionKind: "anonymous",
    savedEventIds: [],
    loading: true,
    syncing: false,
    error: null,
  });

  const refresh = useCallback(async () => {
    const headers = getAuthHeader();

    if (!headers.Authorization) {
      setState((current) => ({
        ...current,
        sessionKind: "anonymous",
        savedEventIds: readAnonymousSavedEventIds(),
        loading: false,
      }));
      return;
    }

    setState((current) => ({
      ...current,
      sessionKind: "authenticated",
      loading: true,
      error: null,
    }));

    try {
      const remoteIds = await fetchRemoteSavedEventIds(headers);
      const remoteSet = new Set(remoteIds);
      const localIds = readAnonymousSavedEventIds();

      const migrationCandidates = localIds.filter((id) => !remoteSet.has(id));
      const failedMigrationIds: string[] = [];
      const migratedIds: string[] = [];

      for (const eventId of migrationCandidates) {
        try {
          await persistRemoteSave(eventId, true, headers);
          migratedIds.push(eventId);
        } catch {
          failedMigrationIds.push(eventId);
        }
      }

      writeAnonymousSavedEventIds(failedMigrationIds);

      setState((current) => ({
        ...current,
        sessionKind: "authenticated",
        savedEventIds: Array.from(new Set([...remoteIds, ...migratedIds])),
        loading: false,
        error:
          failedMigrationIds.length > 0
            ? "Some local saves could not be migrated and will retry later."
            : null,
      }));
    } catch (error) {
      setState((current) => ({
        ...current,
        sessionKind: "anonymous",
        savedEventIds: readAnonymousSavedEventIds(),
        loading: false,
        error:
          error instanceof Error
            ? error.message
            : "Unable to sync saved events.",
      }));
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const ingestLegacyLocalSaves = useCallback(
    (legacyIds: string[]) => {
      const normalized = normalizeEventIds(legacyIds);
      if (normalized.length === 0) return;

      const merged = Array.from(
        new Set([...readAnonymousSavedEventIds(), ...normalized]),
      );
      writeAnonymousSavedEventIds(merged);

      setState((current) => {
        if (current.sessionKind === "authenticated") {
          return current;
        }

        return {
          ...current,
          savedEventIds: Array.from(
            new Set([...current.savedEventIds, ...merged]),
          ),
        };
      });

      if (getAuthHeader().Authorization) {
        void refresh();
      }
    },
    [refresh],
  );

  const toggleSavedEvent = useCallback(
    async (eventId: string) => {
      const normalizedId = eventId.trim();
      if (!normalizedId) return;

      const headers = getAuthHeader();
      const hasAuthorization = Boolean(headers.Authorization);

      setState((current) => ({ ...current, syncing: true, error: null }));

      try {
        if (!hasAuthorization) {
          setState((current) => {
            const exists = current.savedEventIds.includes(normalizedId);
            const nextIds = exists
              ? current.savedEventIds.filter((id) => id !== normalizedId)
              : [...current.savedEventIds, normalizedId];

            writeAnonymousSavedEventIds(nextIds);

            return {
              ...current,
              sessionKind: "anonymous",
              savedEventIds: nextIds,
              syncing: false,
            };
          });
          return;
        }

        const currentlySaved = state.savedEventIds.includes(normalizedId);
        await persistRemoteSave(normalizedId, !currentlySaved, headers);

        setState((current) => {
          const exists = current.savedEventIds.includes(normalizedId);
          const nextIds = exists
            ? current.savedEventIds.filter((id) => id !== normalizedId)
            : [...current.savedEventIds, normalizedId];

          return {
            ...current,
            sessionKind: "authenticated",
            savedEventIds: nextIds,
            syncing: false,
          };
        });
      } catch (error) {
        setState((current) => ({
          ...current,
          syncing: false,
          error:
            error instanceof Error
              ? error.message
              : "Unable to update saved events.",
        }));
      }
    },
    [state.savedEventIds],
  );

  const isSaved = useCallback(
    (eventId: string | null | undefined) =>
      Boolean(eventId && state.savedEventIds.includes(eventId)),
    [state.savedEventIds],
  );

  const clearError = useCallback(() => {
    setState((current) => ({ ...current, error: null }));
  }, []);

  return useMemo(
    () => ({
      sessionKind: state.sessionKind,
      savedEventIds: state.savedEventIds,
      loading: state.loading,
      syncing: state.syncing,
      error: state.error,
      isSaved,
      toggleSavedEvent,
      refresh,
      clearError,
      ingestLegacyLocalSaves,
    }),
    [
      state.sessionKind,
      state.savedEventIds,
      state.loading,
      state.syncing,
      state.error,
      isSaved,
      toggleSavedEvent,
      refresh,
      clearError,
      ingestLegacyLocalSaves,
    ],
  );
}
