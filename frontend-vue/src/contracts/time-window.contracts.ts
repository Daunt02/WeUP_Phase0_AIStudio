export enum TimeWindowPreset {
  Now = "now",
  Tonight = "tonight",
  Tomorrow = "tomorrow",
  ThisWeekend = "thisWeekend",
  Custom = "custom",
}

/**
 * Canonical temporal contract shared by the map feed and downstream calendar overlays.
 * The frontend transports preset identity and timezone only; the backend owns window expansion.
 */
export interface TimeWindowFilterDto {
  readonly preset: TimeWindowPreset;
  readonly timezone: string;
  readonly customStartUtc?: string;
  readonly customEndUtc?: string;
}

export interface MapFeedFilterState {
  preset: TimeWindowPreset;
  timezone: string;
  customStartLocal: string;
  customEndLocal: string;
  includeSavedOnly: boolean;
}

export interface CalendarOverlayTemporalQueryDto extends TimeWindowFilterDto {}
