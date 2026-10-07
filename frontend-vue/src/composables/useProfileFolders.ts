/**
 * WEUP-SYNTH:
 * source=components/ProfilePanel.tsx (folders) via G2_CONTRACT_ARBITRATION.json
 *   ProfileState.folders -> ADAPT_TO_CONTRACTS (PROTOTYPE_LOCAL)
 * destination=frontend-vue/src/composables/useProfileFolders.ts
 * mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
 * notes=The saves backend contract has NO folder concept
 *   (G2 arbitration: SaveActionResponse.folder -> DEPRECATED -> dropped).
 *   Folders are therefore prototype-local: browser-localStorage only,
 *   explicitly labeled non-persistent in the UI, never sent to any backend,
 *   never merged into the canonical saved state (useSavedEventState /
 *   useSavedEventsCollection own saved truth). Folder membership maps
 *   canonical eventIds -> folder names; counts always derive from the
 *   canonical saved list so folders cannot fabricate saved events.
 */

import { computed, reactive } from "vue";

const STORAGE_KEY = "weup.profile.folders.v1";
const MAX_FOLDERS = 20;
const MAX_NAME_LENGTH = 48;

export interface ProfileFolderDto {
  readonly name: string;
}

interface ProfileFolderStore {
  folderNames: string[];
  assignments: Record<string, string>;
}

function isBrowser(): boolean {
  return typeof window !== "undefined";
}

function normalizeName(name: string): string {
  return name.trim().slice(0, MAX_NAME_LENGTH);
}

function readPersisted(): ProfileFolderStore {
  if (!isBrowser()) {
    return { folderNames: [], assignments: {} };
  }

  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return { folderNames: [], assignments: {} };
    }

    const parsed = JSON.parse(raw) as {
      folderNames?: unknown;
      assignments?: unknown;
    };

    const folderNames = Array.isArray(parsed.folderNames)
      ? parsed.folderNames
          .filter((entry): entry is string => typeof entry === "string")
          .map(normalizeName)
          .filter((entry) => entry.length > 0)
          .slice(0, MAX_FOLDERS)
      : [];

    const assignments: Record<string, string> = {};
    if (parsed.assignments && typeof parsed.assignments === "object") {
      for (const [eventId, folderName] of Object.entries(parsed.assignments)) {
        if (
          typeof eventId === "string" &&
          typeof folderName === "string" &&
          folderNames.includes(normalizeName(folderName))
        ) {
          assignments[eventId.trim()] = normalizeName(folderName);
        }
      }
    }

    return { folderNames, assignments };
  } catch {
    return { folderNames: [], assignments: {} };
  }
}

function persist(store: ProfileFolderStore): void {
  if (!isBrowser()) {
    return;
  }

  try {
    window.localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({
        folderNames: store.folderNames,
        assignments: store.assignments,
      }),
    );
  } catch {
    // Best-effort prototype persistence; folder failure cannot block saves.
  }
}

const _store = reactive<ProfileFolderStore>(readPersisted());

export interface ProfileFolderView extends ProfileFolderDto {
  /** Count derived from the canonical saved list (honest, not stored). */
  readonly savedCount: number;
}

export function useProfileFolders() {
  const folderNames = computed(() => _store.folderNames.slice());

  /**
   * Folder -> folder name assignment for an event. null when unfiled.
   */
  function folderNameFor(eventId: string): string | null {
    const trimmed = eventId.trim();
    return trimmed ? (_store.assignments[trimmed] ?? null) : null;
  }

  /**
   * Create a folder. Throws when invalid, duplicate, or at capacity — the
   * caller surfaces this as a UI error (no fake success per §31).
   */
  function createFolder(name: string): string {
    const normalized = normalizeName(name);
    if (normalized.length === 0) {
      throw new Error("Folder name cannot be empty.");
    }
    if (_store.folderNames.includes(normalized)) {
      throw new Error(`Folder "${normalized}" already exists.`);
    }
    if (_store.folderNames.length >= MAX_FOLDERS) {
      throw new Error(`Folder limit reached (${MAX_FOLDERS}).`);
    }

    _store.folderNames = [..._store.folderNames, normalized];
    persist(_store);
    return normalized;
  }

  function deleteFolder(name: string): void {
    const normalized = normalizeName(name);
    _store.folderNames = _store.folderNames.filter(
      (entry) => entry !== normalized,
    );

    for (const [eventId, folderName] of Object.entries(_store.assignments)) {
      if (folderName === normalized) {
        delete _store.assignments[eventId];
      }
    }
    persist(_store);
  }

  /**
   * Assign a canonical saved event to a folder (or unfile it with null).
   * This is a view-model mapping only — it never mutates saved state.
   */
  function assignEventToFolder(
    eventId: string,
    folderName: string | null,
  ): void {
    const trimmed = eventId.trim();
    if (!trimmed) {
      throw new Error("Canonical eventId is required for folder assignment.");
    }

    if (folderName === null) {
      delete _store.assignments[trimmed];
      persist(_store);
      return;
    }

    const normalized = normalizeName(folderName);
    if (!_store.folderNames.includes(normalized)) {
      throw new Error(`Unknown folder "${normalized}".`);
    }

    _store.assignments = { ..._store.assignments, [trimmed]: normalized };
    persist(_store);
  }

  /**
   * Folder list with counts derived from the canonical saved ids passed in.
   * Counts never exceed the canonical truth.
   */
  function foldersWithSavedCounts(
    canonicalSavedEventIds: readonly string[],
  ): ProfileFolderView[] {
    const savedSet = new Set(
      canonicalSavedEventIds.map((id) => id.trim()).filter(Boolean),
    );

    return _store.folderNames.map((name) => {
      let count = 0;
      for (const [eventId, folderName] of Object.entries(_store.assignments)) {
        if (folderName === name && savedSet.has(eventId)) {
          count += 1;
        }
      }
      return { name, savedCount: count };
    });
  }

  return {
    folderNames,
    folderNameFor,
    createFolder,
    deleteFolder,
    assignEventToFolder,
    foldersWithSavedCounts,
    prototypeLabel: "Prototype — folders are browser-local only",
  };
}

/** Test-only reset to the persisted prototype store. */
export function resetProfileFoldersForTests(): void {
  _store.folderNames = [];
  _store.assignments = {};
  if (typeof window !== "undefined") {
    try {
      window.localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Best-effort cleanup.
    }
  }
}
