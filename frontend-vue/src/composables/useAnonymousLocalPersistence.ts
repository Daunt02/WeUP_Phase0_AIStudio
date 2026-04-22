import type {
  AnonymousDiscoveryContext,
  AnonymousSavedState,
  AnonymousTemporalFilterSnapshot,
  LocalStorageEnvelope,
} from "../contracts/anonymous-local-persistence.contracts";

const STORAGE_SCHEMA = "weup.anonymous.local" as const;
const STORAGE_VERSION = 1;

/**
 * Storage key strategy:
 * - Namespace all keys under weup.anonymous.* for explicit session scope.
 * - Key names are semantic and stable across minor payload shape updates.
 * - Versioning lives in envelope.version, not in key suffixes.
 */
export const ANONYMOUS_STORAGE_KEYS = {
  savedState: "weup.anonymous.saved-state",
  discoveryContext: "weup.anonymous.discovery-context",
} as const;

const LEGACY_ANONYMOUS_SAVED_EVENTS_KEY = "weup.saved-events.anon.v1";

const DEFAULT_TTL_MS = {
  savedState: 30 * 24 * 60 * 60 * 1000,
  discoveryContext: 7 * 24 * 60 * 60 * 1000,
} as const;

type AnyEnvelope = LocalStorageEnvelope<unknown>;

function isBrowser(): boolean {
  return typeof window !== "undefined";
}

function nowIso(): string {
  return new Date().toISOString();
}

function expiresAtIso(ttlMs: number): string {
  return new Date(Date.now() + ttlMs).toISOString();
}

function isPlainRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function parseEnvelope(value: unknown): AnyEnvelope | null {
  if (!isPlainRecord(value)) {
    return null;
  }

  const payload = value.payload;

  if (
    value.schema !== STORAGE_SCHEMA ||
    typeof value.version !== "number" ||
    typeof value.createdAtUtc !== "string" ||
    typeof value.updatedAtUtc !== "string" ||
    typeof value.expiresAtUtc !== "string" ||
    payload === undefined
  ) {
    return null;
  }

  return {
    schema: STORAGE_SCHEMA,
    version: value.version,
    createdAtUtc: value.createdAtUtc,
    updatedAtUtc: value.updatedAtUtc,
    expiresAtUtc: value.expiresAtUtc,
    payload,
  };
}

function isEnvelopeExpired(envelope: AnyEnvelope): boolean {
  const expiry = Date.parse(envelope.expiresAtUtc);
  if (Number.isNaN(expiry)) {
    return true;
  }

  return expiry <= Date.now();
}

function normalizeEventId(value: unknown): string {
  return typeof value === "string" ? value.trim() : "";
}

function normalizeSavedEventIds(input: unknown): string[] {
  if (!Array.isArray(input)) {
    return [];
  }

  const ids = input
    .map((entry) => normalizeEventId(entry))
    .filter((entry) => entry.length > 0);

  return Array.from(new Set(ids));
}

function toSavedStatePayload(value: unknown): AnonymousSavedState | null {
  if (!isPlainRecord(value)) {
    return null;
  }

  if (
    !Array.isArray(value.savedEventIds) ||
    typeof value.updatedAtUtc !== "string"
  ) {
    return null;
  }

  const savedEventIds = normalizeSavedEventIds(value.savedEventIds);
  if (savedEventIds.length !== value.savedEventIds.length) {
    return null;
  }

  return {
    savedEventIds,
    updatedAtUtc: value.updatedAtUtc,
  };
}

function toTemporalFilterSnapshot(
  value: unknown,
): AnonymousTemporalFilterSnapshot | null {
  if (!isPlainRecord(value)) {
    return null;
  }

  if (typeof value.preset !== "string" || typeof value.timezone !== "string") {
    return null;
  }

  return {
    preset: value.preset as AnonymousTemporalFilterSnapshot["preset"],
    timezone: value.timezone,
    ...(typeof value.customStartUtc === "string"
      ? { customStartUtc: value.customStartUtc }
      : {}),
    ...(typeof value.customEndUtc === "string"
      ? { customEndUtc: value.customEndUtc }
      : {}),
  };
}

function toDiscoveryContextPayload(
  value: unknown,
): AnonymousDiscoveryContext | null {
  if (!isPlainRecord(value) || typeof value.updatedAtUtc !== "string") {
    return null;
  }

  const lastViewedDistrict =
    typeof value.lastViewedDistrict === "string" &&
    value.lastViewedDistrict.trim()
      ? value.lastViewedDistrict.trim()
      : undefined;

  let lastTemporalFilter: AnonymousDiscoveryContext["lastTemporalFilter"];

  if (value.lastTemporalFilter !== undefined) {
    const temporal = toTemporalFilterSnapshot(value.lastTemporalFilter);
    if (!temporal) {
      return null;
    }
    lastTemporalFilter = temporal;
  }

  let recentMapViewport: AnonymousDiscoveryContext["recentMapViewport"];

  if (value.recentMapViewport !== undefined) {
    if (!isPlainRecord(value.recentMapViewport)) {
      return null;
    }

    if (
      typeof value.recentMapViewport.bbox !== "string" ||
      typeof value.recentMapViewport.capturedAtUtc !== "string"
    ) {
      return null;
    }

    recentMapViewport = {
      bbox: value.recentMapViewport.bbox,
      capturedAtUtc: value.recentMapViewport.capturedAtUtc,
    };
  }

  return {
    updatedAtUtc: value.updatedAtUtc,
    ...(lastViewedDistrict !== undefined ? { lastViewedDistrict } : {}),
    ...(lastTemporalFilter !== undefined ? { lastTemporalFilter } : {}),
    ...(recentMapViewport !== undefined ? { recentMapViewport } : {}),
  };
}

function makeEnvelope<TPayload>(
  payload: TPayload,
  ttlMs: number,
  createdAtUtc?: string,
): LocalStorageEnvelope<TPayload> {
  const now = nowIso();
  return {
    schema: STORAGE_SCHEMA,
    version: STORAGE_VERSION,
    createdAtUtc: createdAtUtc ?? now,
    updatedAtUtc: now,
    expiresAtUtc: expiresAtIso(ttlMs),
    payload,
  };
}

function safeGetRaw(key: string): string | null {
  if (!isBrowser()) {
    return null;
  }

  try {
    return window.localStorage.getItem(key);
  } catch {
    return null;
  }
}

function safeRemove(key: string): void {
  if (!isBrowser()) {
    return;
  }

  try {
    window.localStorage.removeItem(key);
  } catch {
    // Best-effort cleanup only.
  }
}

function safeSet<TPayload>(
  key: string,
  envelope: LocalStorageEnvelope<TPayload>,
): void {
  if (!isBrowser()) {
    return;
  }

  try {
    window.localStorage.setItem(key, JSON.stringify(envelope));
  } catch {
    // Local persistence cannot block primary UX flows.
  }
}

function migrateSavedStateFromLegacyKey(): AnonymousSavedState | null {
  const raw = safeGetRaw(LEGACY_ANONYMOUS_SAVED_EVENTS_KEY);
  if (!raw) {
    return null;
  }

  try {
    const parsed = JSON.parse(raw);
    const ids = normalizeSavedEventIds(parsed);
    safeRemove(LEGACY_ANONYMOUS_SAVED_EVENTS_KEY);

    return {
      savedEventIds: ids,
      updatedAtUtc: nowIso(),
    };
  } catch {
    safeRemove(LEGACY_ANONYMOUS_SAVED_EVENTS_KEY);
    return null;
  }
}

function readTypedEnvelope<TPayload>(
  key: string,
  parsePayload: (value: unknown) => TPayload | null,
): LocalStorageEnvelope<TPayload> | null {
  const raw = safeGetRaw(key);
  if (!raw) {
    return null;
  }

  let parsedRaw: unknown;
  try {
    parsedRaw = JSON.parse(raw);
  } catch {
    // Corrupt JSON must be discarded to keep future reads deterministic.
    safeRemove(key);
    return null;
  }

  const envelope = parseEnvelope(parsedRaw);
  if (!envelope) {
    safeRemove(key);
    return null;
  }

  // Versioning rule:
  // - Same version: validate payload and continue.
  // - Unknown version: discard (no hidden shape assumptions).
  if (envelope.version !== STORAGE_VERSION) {
    safeRemove(key);
    return null;
  }

  if (isEnvelopeExpired(envelope)) {
    safeRemove(key);
    return null;
  }

  const payload = parsePayload(envelope.payload);
  if (!payload) {
    safeRemove(key);
    return null;
  }

  return {
    schema: STORAGE_SCHEMA,
    version: STORAGE_VERSION,
    createdAtUtc: envelope.createdAtUtc,
    updatedAtUtc: envelope.updatedAtUtc,
    expiresAtUtc: envelope.expiresAtUtc,
    payload,
  };
}

export interface AnonymousLocalPersistence {
  readonly storageKeys: typeof ANONYMOUS_STORAGE_KEYS;
  getSavedState: () => AnonymousSavedState;
  setSavedEventIds: (eventIds: string[]) => AnonymousSavedState;
  addSavedEventId: (eventId: string) => AnonymousSavedState;
  removeSavedEventId: (eventId: string) => AnonymousSavedState;
  getDiscoveryContext: () => AnonymousDiscoveryContext;
  setDiscoveryContext: (
    partial: Partial<Omit<AnonymousDiscoveryContext, "updatedAtUtc">>,
  ) => AnonymousDiscoveryContext;
  clearAnonymousState: () => void;
}

export function useAnonymousLocalPersistence(): AnonymousLocalPersistence {
  function getSavedState(): AnonymousSavedState {
    const current = readTypedEnvelope(
      ANONYMOUS_STORAGE_KEYS.savedState,
      toSavedStatePayload,
    );

    if (current) {
      return current.payload;
    }

    const migrated = migrateSavedStateFromLegacyKey();
    if (migrated) {
      safeSet(
        ANONYMOUS_STORAGE_KEYS.savedState,
        makeEnvelope(migrated, DEFAULT_TTL_MS.savedState),
      );
      return migrated;
    }

    return {
      savedEventIds: [],
      updatedAtUtc: nowIso(),
    };
  }

  function setSavedEventIds(eventIds: string[]): AnonymousSavedState {
    const nextPayload: AnonymousSavedState = {
      savedEventIds: normalizeSavedEventIds(eventIds),
      updatedAtUtc: nowIso(),
    };

    if (nextPayload.savedEventIds.length === 0) {
      safeRemove(ANONYMOUS_STORAGE_KEYS.savedState);
      return nextPayload;
    }

    const previous = readTypedEnvelope(
      ANONYMOUS_STORAGE_KEYS.savedState,
      toSavedStatePayload,
    );

    safeSet(
      ANONYMOUS_STORAGE_KEYS.savedState,
      makeEnvelope(
        nextPayload,
        DEFAULT_TTL_MS.savedState,
        previous?.createdAtUtc,
      ),
    );

    return nextPayload;
  }

  function addSavedEventId(eventId: string): AnonymousSavedState {
    const normalized = normalizeEventId(eventId);
    const current = getSavedState();
    if (!normalized) {
      return current;
    }

    return setSavedEventIds([...current.savedEventIds, normalized]);
  }

  function removeSavedEventId(eventId: string): AnonymousSavedState {
    const normalized = normalizeEventId(eventId);
    const current = getSavedState();
    if (!normalized) {
      return current;
    }

    return setSavedEventIds(
      current.savedEventIds.filter((savedId) => savedId !== normalized),
    );
  }

  function getDiscoveryContext(): AnonymousDiscoveryContext {
    const envelope = readTypedEnvelope(
      ANONYMOUS_STORAGE_KEYS.discoveryContext,
      toDiscoveryContextPayload,
    );

    if (envelope) {
      return envelope.payload;
    }

    return {
      updatedAtUtc: nowIso(),
    };
  }

  function setDiscoveryContext(
    partial: Partial<Omit<AnonymousDiscoveryContext, "updatedAtUtc">>,
  ): AnonymousDiscoveryContext {
    const current = getDiscoveryContext();
    const nextPayload: AnonymousDiscoveryContext = {
      ...current,
      ...partial,
      updatedAtUtc: nowIso(),
    };

    const previous = readTypedEnvelope(
      ANONYMOUS_STORAGE_KEYS.discoveryContext,
      toDiscoveryContextPayload,
    );

    safeSet(
      ANONYMOUS_STORAGE_KEYS.discoveryContext,
      makeEnvelope(
        nextPayload,
        DEFAULT_TTL_MS.discoveryContext,
        previous?.createdAtUtc,
      ),
    );

    return nextPayload;
  }

  function clearAnonymousState(): void {
    safeRemove(ANONYMOUS_STORAGE_KEYS.savedState);
    safeRemove(ANONYMOUS_STORAGE_KEYS.discoveryContext);
    safeRemove(LEGACY_ANONYMOUS_SAVED_EVENTS_KEY);
  }

  return {
    storageKeys: ANONYMOUS_STORAGE_KEYS,
    getSavedState,
    setSavedEventIds,
    addSavedEventId,
    removeSavedEventId,
    getDiscoveryContext,
    setDiscoveryContext,
    clearAnonymousState,
  };
}
