import type { TimeWindowPreset } from "./time-window.contracts";

/**
 * Lightweight anonymous saved-state cache.
 * This is convenience-only UI state, never backend source-of-record state.
 */
export interface AnonymousSavedState {
  readonly savedEventIds: string[];
  readonly updatedAtUtc: string;
}

export interface AnonymousTemporalFilterSnapshot {
  readonly preset: TimeWindowPreset;
  readonly timezone: string;
  readonly customStartUtc?: string;
  readonly customEndUtc?: string;
}

export interface AnonymousMapViewportSnapshot {
  /**
   * Canonical bbox representation: minLng,minLat,maxLng,maxLat.
   */
  readonly bbox: string;
  readonly capturedAtUtc: string;
}

/**
 * Anonymous discovery context for UX continuity only.
 * It must not be treated as canonical truth.
 */
export interface AnonymousDiscoveryContext {
  readonly lastViewedDistrict?: string;
  readonly lastTemporalFilter?: AnonymousTemporalFilterSnapshot;
  readonly recentMapViewport?: AnonymousMapViewportSnapshot;
  readonly updatedAtUtc: string;
}

/**
 * Strict browser storage envelope for all anonymous persisted payloads.
 * Version is required so incompatible shapes can be safely discarded/migrated.
 */
export interface LocalStorageEnvelope<TPayload> {
  readonly schema: "weup.anonymous.local";
  readonly version: number;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string;
  readonly expiresAtUtc: string;
  readonly payload: TPayload;
}
