import type { TimeWindowPreset } from "./time-window.contracts";

export const PREFERENCE_OWNER_MODES = {
  anonymous: "anonymous",
  authenticated: "authenticated",
} as const;

export type PreferenceOwnerMode =
  (typeof PREFERENCE_OWNER_MODES)[keyof typeof PREFERENCE_OWNER_MODES];

/**
 * Canonical district taxonomy codes for MVP v1.0.
 * Preferences must store codes only, never display labels.
 */
export const DISCOVERY_CANONICAL_DISTRICT_CODES = [
  "mission",
  "soma",
  "north-beach",
  "uptown",
  "jack-london",
  "temescal",
] as const;

/**
 * Canonical category taxonomy codes for MVP v1.0.
 */
export const DISCOVERY_CANONICAL_CATEGORY_CODES = [
  "nightlife",
  "rooftop",
  "concert",
  "music",
  "food",
  "community",
] as const;

export type CanonicalDistrictCode =
  (typeof DISCOVERY_CANONICAL_DISTRICT_CODES)[number];

export type CanonicalCategoryCode =
  (typeof DISCOVERY_CANONICAL_CATEGORY_CODES)[number];

/**
 * Explicit ownership envelope so anonymous and authenticated context
 * can never be merged implicitly.
 */
export interface PreferenceOwnerDto {
  readonly mode: PreferenceOwnerMode;
  readonly userId: string | null;
  readonly anonymousSessionId: string | null;
}

export interface LastUsedMapStateDto {
  readonly bbox: string;
  readonly centerLat: number | null;
  readonly centerLng: number | null;
  readonly zoom: number | null;
  readonly capturedAtUtc: string;
}

export interface SavedCountSummaryDto {
  readonly totalSavedEvents: number;
  readonly savedEventsInCurrentMapWindow: number;
  readonly capturedAtUtc: string;
}

export interface UserDiscoveryContextDto {
  readonly owner: PreferenceOwnerDto;
  readonly preferredDistrictCodes: CanonicalDistrictCode[];
  readonly preferredCategoryCodes: CanonicalCategoryCode[];
  readonly preferredTemporalPresets: TimeWindowPreset[];
  readonly lastUsedMapState: LastUsedMapStateDto | null;
  readonly savedCountSummary: SavedCountSummaryDto | null;
  readonly updatedAtUtc: string;
  readonly version: number;
}

export interface UpsertUserDiscoveryContextRequest {
  readonly owner: PreferenceOwnerDto;
  readonly preferredDistrictCodes?: CanonicalDistrictCode[];
  readonly preferredCategoryCodes?: CanonicalCategoryCode[];
  readonly preferredTemporalPresets?: TimeWindowPreset[];
  readonly lastUsedMapState?: LastUsedMapStateDto | null;
  readonly savedCountSummary?: SavedCountSummaryDto | null;
}
