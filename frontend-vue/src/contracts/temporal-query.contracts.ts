import type { TimeWindowPreset } from "./time-window.contracts";

/**
 * M8-P39 canonical temporal query contract.
 *
 * Ownership boundary:
 * - Frontend sends temporal intent only.
 * - Backend owns final window resolution for all preset semantics.
 * - fromUtc/toUtc are only explicit range hints, never frontend-derived preset expansion.
 */
export interface TemporalQueryDto {
  readonly preset: TimeWindowPreset;
  readonly fromUtc?: string;
  readonly toUtc?: string;
  readonly marketTimezone: string;
  readonly referenceInstantUtc?: string;
  /** Server-computed helper on the C# DTO; optional in request payloads. */
  readonly isCustomRange?: boolean;
}

/**
 * Centralized custom-range predicate used by all temporal consumers.
 * This prevents duplicated preset branching across components.
 */
export function isCustomRangePreset(preset: TimeWindowPreset): boolean {
  return preset === "customRange" || preset === "custom";
}
