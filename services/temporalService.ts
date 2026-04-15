/**
 * Temporal Query Service — P21
 * Maps canonical temporal presets (TODAY, TONIGHT, WEEKEND) to backend time windows
 */

import { getAuthHeader } from "./auth";

export interface TemporalPresetRequest {
  preset: string; // Today, Tonight, Weekend, Next7Days
  marketTimezone?: string; // e.g., "America/Los_Angeles"
}

export interface TemporalQueryResponse {
  preset: string;
  presetLabel: string;
  timeWindowStart: string; // ISO 8601
  timeWindowEnd: string; // ISO 8601
  timezone: string;
  events: Array<any>; // Future: EventDto[]
  count: number;
}

export interface PresetDto {
  value: number;
  name: string;
  label: string;
}

export interface PresetListResponse {
  presets: PresetDto[];
}

/**
 * Query events within a temporal preset window
 */
export async function getEventsAtTime(
  request: TemporalPresetRequest,
  signal?: AbortSignal,
): Promise<TemporalQueryResponse> {
  const response = await fetch("/api/temporal/events-at-time", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...getAuthHeader(),
    },
    body: JSON.stringify(request),
    signal,
  });

  if (!response.ok) {
    throw new Error(`Temporal query failed: ${response.statusText}`);
  }

  return response.json();
}

/**
 * List all available temporal presets with labels
 */
export async function getTemporalPresets(
  signal?: AbortSignal,
): Promise<PresetListResponse> {
  const response = await fetch("/api/temporal/presets", {
    method: "GET",
    headers: getAuthHeader(),
    signal,
  });

  if (!response.ok) {
    throw new Error(`Failed to fetch temporal presets: ${response.statusText}`);
  }

  return response.json();
}

/**
 * Preset label map for UI display
 */
export const PRESET_LABELS: Record<string, string> = {
  Today: "TODAY",
  Tonight: "TONIGHT",
  Weekend: "WEEKEND",
  Next7Days: "NEXT 7 DAYS",
};
