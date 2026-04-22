"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { getAuthHeader, getCurrentUserProfile } from "@/services/auth";
import type { SaveStateMigrationResultDto } from "@/services/backendContracts";
import {
  listSavedEventIds,
  migrateAnonymousSaveState,
  setSavedEvent,
} from "@/services/saveService";

const LOCAL_ANON_SAVES_KEY = "weup.saved-events.anon.v1";

export type SaveAuthoritySessionKind = "anonymous" | "authenticated";

export interface SaveAuthorityState {
  sessionKind: SaveAuthoritySessionKind;
  savedEventIds: string[];
  loading: boolean;
  syncing: boolean;
  error: string | null;
  lastMigrationResult: SaveStateMigrationResultDto | null;
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

export function useSavedEventsAuthority() {
  const [state, setState] = useState<SaveAuthorityState>({
    sessionKind: "anonymous",
    savedEventIds: [],
    loading: true,
    syncing: false,
    error: null,
    lastMigrationResult: null,
  });

  const refresh = useCallback(async () => {
    const tokenHeaders = getAuthHeader();
    if (!tokenHeaders.Authorization) {
      setState((current) => ({
        ...current,
        sessionKind: "anonymous",
        savedEventIds: readAnonymousSavedEventIds(),
        loading: false,
        lastMigrationResult: null,
      }));
      return;
    }

    const profile = await getCurrentUserProfile();
    if (!profile) {
      setState((current) => ({
        ...current,
        sessionKind: "anonymous",
        savedEventIds: readAnonymousSavedEventIds(),
        loading: false,
        lastMigrationResult: null,
      }));
      return;
    }

    setState((current) => ({
      ...current,
      sessionKind: "authenticated",
      loading: true,
      syncing: true,
      error: null,
    }));

    try {
      const localIds = readAnonymousSavedEventIds();
      const migrationResult = await migrateAnonymousSaveState({
        localSavedEventIds: localIds,
        localDiscoveryContext: null,
        clientMigrationKey: `save-authority-${Date.now()}`,
      });

      // Only after confirmed migration response do we rewrite local anonymous state.
      writeAnonymousSavedEventIds(migrationResult.retainedLocalSavedEventIds);

      const remoteIds = await listSavedEventIds();

      setState((current) => ({
        ...current,
        sessionKind: "authenticated",
        savedEventIds: Array.from(new Set(remoteIds)),
        loading: false,
        syncing: false,
        lastMigrationResult: migrationResult,
        error:
          migrationResult.status === "completed-with-issues"
            ? "Save migration completed with issues. Check migration result for details."
            : null,
      }));
    } catch (error) {
      setState((current) => ({
        ...current,
        sessionKind: "anonymous",
        savedEventIds: readAnonymousSavedEventIds(),
        loading: false,
        syncing: false,
        lastMigrationResult: null,
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

  const toggleSavedEvent = useCallback(
    async (eventId: string) => {
      const normalizedId = eventId.trim();
      if (!normalizedId) return;

      if (state.syncing) {
        return;
      }

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
        await setSavedEvent(normalizedId, !currentlySaved);

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
      lastMigrationResult: state.lastMigrationResult,
      isSaved,
      toggleSavedEvent,
      refresh,
      clearError,
    }),
    [
      state.sessionKind,
      state.savedEventIds,
      state.loading,
      state.syncing,
      state.error,
      state.lastMigrationResult,
      isSaved,
      toggleSavedEvent,
      refresh,
      clearError,
    ],
  );
}
