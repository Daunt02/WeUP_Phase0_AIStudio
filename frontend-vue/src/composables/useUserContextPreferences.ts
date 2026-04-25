import { computed, ref, type ComputedRef } from "vue";
import type { DiscoveryFilterState } from "./useDiscoveryState";
import type {
  EventMapFeedQueryDto,
  EventMapItemDto,
} from "../contracts/map-feed.contracts";
import type { TimeWindowPreset } from "../contracts/time-window.contracts";
import {
  DISCOVERY_CANONICAL_CATEGORY_CODES,
  DISCOVERY_CANONICAL_DISTRICT_CODES,
  PREFERENCE_OWNER_MODES,
  type CanonicalCategoryCode,
  type CanonicalDistrictCode,
  type LastUsedMapStateDto,
  type PreferenceOwnerDto,
  type SavedCountSummaryDto,
  type UpsertUserDiscoveryContextRequest,
  type UserDiscoveryContextDto,
} from "../contracts/user-context.contracts";

const STORAGE_KEY = "weup.discovery.user-context.v1";
const SCHEMA_VERSION = 1;

const canonicalDistrictSet = new Set<string>(
  DISCOVERY_CANONICAL_DISTRICT_CODES,
);
const canonicalCategorySet = new Set<string>(
  DISCOVERY_CANONICAL_CATEGORY_CODES,
);

const defaultAnonymousOwner: PreferenceOwnerDto = {
  mode: PREFERENCE_OWNER_MODES.anonymous,
  userId: null,
  anonymousSessionId: "anon-local",
};

function nowIso(): string {
  return new Date().toISOString();
}

function toUniqueStableArray<T extends string>(values: readonly T[]): T[] {
  return Array.from(new Set(values)).sort((left, right) =>
    left.localeCompare(right, "en", { sensitivity: "base" }),
  ) as T[];
}

function normalizeCanonicalDistricts(
  values: readonly string[] | undefined,
): CanonicalDistrictCode[] {
  if (!values) {
    return [];
  }

  const normalized = values
    .map((value) => value.trim())
    .filter((value): value is CanonicalDistrictCode =>
      canonicalDistrictSet.has(value),
    );

  return toUniqueStableArray(normalized);
}

function normalizeCanonicalCategories(
  values: readonly string[] | undefined,
): CanonicalCategoryCode[] {
  if (!values) {
    return [];
  }

  const normalized = values
    .map((value) => value.trim())
    .filter((value): value is CanonicalCategoryCode =>
      canonicalCategorySet.has(value),
    );

  return toUniqueStableArray(normalized);
}

function normalizeTemporalPresets(
  presets: readonly TimeWindowPreset[] | undefined,
): TimeWindowPreset[] {
  if (!presets) {
    return [];
  }

  return toUniqueStableArray([...presets]);
}

function isBrowser(): boolean {
  return typeof window !== "undefined";
}

function readPersistedContext(): UserDiscoveryContextDto | null {
  if (!isBrowser()) {
    return null;
  }

  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return null;
    }

    const parsed = JSON.parse(raw) as UserDiscoveryContextDto;
    if (!parsed || parsed.version !== SCHEMA_VERSION || !parsed.owner) {
      return null;
    }

    return {
      owner: parsed.owner,
      preferredDistrictCodes: normalizeCanonicalDistricts(
        parsed.preferredDistrictCodes,
      ),
      preferredCategoryCodes: normalizeCanonicalCategories(
        parsed.preferredCategoryCodes,
      ),
      preferredTemporalPresets: normalizeTemporalPresets(
        parsed.preferredTemporalPresets,
      ),
      lastUsedMapState: parsed.lastUsedMapState ?? null,
      savedCountSummary: parsed.savedCountSummary ?? null,
      updatedAtUtc: parsed.updatedAtUtc ?? nowIso(),
      version: SCHEMA_VERSION,
    };
  } catch {
    return null;
  }
}

function persistContext(context: UserDiscoveryContextDto): void {
  if (!isBrowser()) {
    return;
  }

  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(context));
  } catch {
    // Best-effort persistence only. Preference persistence cannot block discovery UX.
  }
}

function createDefaultContext(
  owner: PreferenceOwnerDto,
): UserDiscoveryContextDto {
  return {
    owner,
    preferredDistrictCodes: [],
    preferredCategoryCodes: [],
    preferredTemporalPresets: [],
    lastUsedMapState: null,
    savedCountSummary: null,
    updatedAtUtc: nowIso(),
    version: SCHEMA_VERSION,
  };
}

export interface UseUserContextPreferencesOptions {
  owner?: PreferenceOwnerDto;
}

export interface UseUserContextPreferencesReturn {
  context: ComputedRef<UserDiscoveryContextDto>;
  ownershipMode: ComputedRef<PreferenceOwnerDto["mode"]>;
  preferredDistrictCodes: ComputedRef<CanonicalDistrictCode[]>;
  preferredCategoryCodes: ComputedRef<CanonicalCategoryCode[]>;
  preferredTemporalPresets: ComputedRef<TimeWindowPreset[]>;
  lastUsedMapState: ComputedRef<LastUsedMapStateDto | null>;
  savedCountSummary: ComputedRef<SavedCountSummaryDto | null>;

  setOwner: (owner: PreferenceOwnerDto) => void;
  updateFromMapDiscoveryFilters: (filters: DiscoveryFilterState) => void;
  updatePreferredTemporalPreset: (preset: TimeWindowPreset) => void;
  updateLastUsedMapStateFromQuery: (query: EventMapFeedQueryDto) => void;
  updateSavedCountSummaryFromItems: (items: EventMapItemDto[]) => void;
  applyPatch: (patch: UpsertUserDiscoveryContextRequest) => void;

  toMapDiscoveryFilterPatch: () => Partial<DiscoveryFilterState>;
  toCalendarOverlayFilterPatch: () => Pick<
    EventMapFeedQueryDto,
    "district" | "categories"
  >;
  toSavedEventsSurfaceSummary: () => SavedCountSummaryDto | null;
}

export function useUserContextPreferences(
  options: UseUserContextPreferencesOptions = {},
): UseUserContextPreferencesReturn {
  const owner = options.owner ?? defaultAnonymousOwner;
  const persisted = readPersistedContext();
  const isOwnerMatch =
    persisted?.owner.mode === owner.mode &&
    persisted.owner.userId === owner.userId &&
    persisted.owner.anonymousSessionId === owner.anonymousSessionId;

  const _context = ref<UserDiscoveryContextDto>(
    persisted && isOwnerMatch ? persisted : createDefaultContext(owner),
  );

  function commit(next: UserDiscoveryContextDto): void {
    _context.value = next;
    persistContext(next);
  }

  function setOwner(nextOwner: PreferenceOwnerDto): void {
    const current = _context.value;
    const isSameOwner =
      current.owner.mode === nextOwner.mode &&
      current.owner.userId === nextOwner.userId &&
      current.owner.anonymousSessionId === nextOwner.anonymousSessionId;

    if (isSameOwner) {
      return;
    }

    // Ownership boundary: preference streams are isolated between anonymous and authenticated contexts.
    const persisted = readPersistedContext();
    if (
      persisted &&
      persisted.owner.mode === nextOwner.mode &&
      persisted.owner.userId === nextOwner.userId &&
      persisted.owner.anonymousSessionId === nextOwner.anonymousSessionId
    ) {
      commit(persisted);
      return;
    }

    commit(createDefaultContext(nextOwner));
  }

  function applyPatch(patch: UpsertUserDiscoveryContextRequest): void {
    const current = _context.value;
    const next: UserDiscoveryContextDto = {
      owner: patch.owner,
      preferredDistrictCodes:
        patch.preferredDistrictCodes !== undefined
          ? normalizeCanonicalDistricts(patch.preferredDistrictCodes)
          : current.preferredDistrictCodes,
      preferredCategoryCodes:
        patch.preferredCategoryCodes !== undefined
          ? normalizeCanonicalCategories(patch.preferredCategoryCodes)
          : current.preferredCategoryCodes,
      preferredTemporalPresets:
        patch.preferredTemporalPresets !== undefined
          ? normalizeTemporalPresets(patch.preferredTemporalPresets)
          : current.preferredTemporalPresets,
      lastUsedMapState:
        patch.lastUsedMapState !== undefined
          ? patch.lastUsedMapState
          : current.lastUsedMapState,
      savedCountSummary:
        patch.savedCountSummary !== undefined
          ? patch.savedCountSummary
          : current.savedCountSummary,
      updatedAtUtc: nowIso(),
      version: SCHEMA_VERSION,
    };

    commit(next);
  }

  function updateFromMapDiscoveryFilters(filters: DiscoveryFilterState): void {
    // Canonical taxonomy boundary: only known district/category codes are persisted.
    const districtCodes = normalizeCanonicalDistricts(
      filters.district ? [filters.district] : [],
    );
    const categoryCodes = normalizeCanonicalCategories(filters.categories);

    applyPatch({
      owner: _context.value.owner,
      preferredDistrictCodes: districtCodes,
      preferredCategoryCodes: categoryCodes,
    });
  }

  function updatePreferredTemporalPreset(preset: TimeWindowPreset): void {
    const current = _context.value.preferredTemporalPresets;
    applyPatch({
      owner: _context.value.owner,
      preferredTemporalPresets: toUniqueStableArray([...current, preset]),
    });
  }

  function updateLastUsedMapStateFromQuery(query: EventMapFeedQueryDto): void {
    applyPatch({
      owner: _context.value.owner,
      lastUsedMapState: {
        bbox: query.bbox,
        centerLat: null,
        centerLng: null,
        zoom: null,
        capturedAtUtc: nowIso(),
      },
    });
  }

  function updateSavedCountSummaryFromItems(items: EventMapItemDto[]): void {
    const savedInWindow = items.filter(
      (item) => item.savedByCurrentUser,
    ).length;
    const existingTotal =
      _context.value.savedCountSummary?.totalSavedEvents ?? 0;

    applyPatch({
      owner: _context.value.owner,
      savedCountSummary: {
        totalSavedEvents: Math.max(existingTotal, savedInWindow),
        savedEventsInCurrentMapWindow: savedInWindow,
        capturedAtUtc: nowIso(),
      },
    });
  }

  const context = computed(() => _context.value);
  const ownershipMode = computed(() => _context.value.owner.mode);
  const preferredDistrictCodes = computed(
    () => _context.value.preferredDistrictCodes,
  );
  const preferredCategoryCodes = computed(
    () => _context.value.preferredCategoryCodes,
  );
  const preferredTemporalPresets = computed(
    () => _context.value.preferredTemporalPresets,
  );
  const lastUsedMapState = computed(() => _context.value.lastUsedMapState);
  const savedCountSummary = computed(() => _context.value.savedCountSummary);

  function toMapDiscoveryFilterPatch(): Partial<DiscoveryFilterState> {
    return {
      district: _context.value.preferredDistrictCodes[0],
      categories:
        _context.value.preferredCategoryCodes.length > 0
          ? [..._context.value.preferredCategoryCodes]
          : undefined,
    };
  }

  function toCalendarOverlayFilterPatch(): Pick<
    EventMapFeedQueryDto,
    "district" | "categories"
  > {
    return {
      district: _context.value.preferredDistrictCodes[0],
      categories:
        _context.value.preferredCategoryCodes.length > 0
          ? [..._context.value.preferredCategoryCodes]
          : undefined,
    };
  }

  function toSavedEventsSurfaceSummary(): SavedCountSummaryDto | null {
    return _context.value.savedCountSummary;
  }

  return {
    context,
    ownershipMode,
    preferredDistrictCodes,
    preferredCategoryCodes,
    preferredTemporalPresets,
    lastUsedMapState,
    savedCountSummary,

    setOwner,
    updateFromMapDiscoveryFilters,
    updatePreferredTemporalPreset,
    updateLastUsedMapStateFromQuery,
    updateSavedCountSummaryFromItems,
    applyPatch,

    toMapDiscoveryFilterPatch,
    toCalendarOverlayFilterPatch,
    toSavedEventsSurfaceSummary,
  };
}
