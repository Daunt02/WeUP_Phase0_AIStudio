import type {
  EventMapFeedQueryDto,
  EventMapItemDto,
  EventMapMarkerState,
} from "./map-feed.contracts";

/**
 * Deterministic finite states for the map calendar overlay shell.
 * These are UI states only; event identity remains canonical via eventId.
 */
export type CalendarOverlayLayerState =
  | "closed"
  | "partial"
  | "expanded"
  | "event-selected";

/**
 * Query contract for the temporal overlay projection endpoint.
 * Field-compatible with EventMapFeedQueryDto to prevent map/calendar drift.
 */
export interface EventCalendarFeedQueryDto {
  readonly bbox: string;
  readonly preset: EventMapFeedQueryDto["preset"];
  /** Legacy alias. Prefer marketTimezone. */
  readonly timezone: EventMapFeedQueryDto["timezone"];
  readonly marketTimezone?: EventMapFeedQueryDto["marketTimezone"];
  readonly fromUtc?: EventMapFeedQueryDto["fromUtc"];
  readonly toUtc?: EventMapFeedQueryDto["toUtc"];
  readonly referenceInstantUtc?: EventMapFeedQueryDto["referenceInstantUtc"];
  readonly customStartUtc?: EventMapFeedQueryDto["customStartUtc"];
  readonly customEndUtc?: EventMapFeedQueryDto["customEndUtc"];
  readonly district?: EventMapFeedQueryDto["district"];
  readonly categories?: EventMapFeedQueryDto["categories"];
  readonly includeSavedOnly?: EventMapFeedQueryDto["includeSavedOnly"];
}

/**
 * Calendar temporal item. Shared attributes mirror EventMapItemDto.
 */
export interface CalendarEventItemDto {
  readonly eventId: EventMapItemDto["eventId"];
  readonly title: EventMapItemDto["title"];
  readonly startUtc: EventMapItemDto["startUtc"];
  readonly endUtc: EventMapItemDto["endUtc"];
  readonly timezone: string;
  readonly venueName: EventMapItemDto["venueName"];
  readonly district: EventMapItemDto["district"];
  readonly primaryCategory: EventMapItemDto["primaryCategory"];
  readonly savedByCurrentUser: EventMapItemDto["savedByCurrentUser"];
  readonly markerState: EventMapMarkerState;
  readonly thumbnailUrl: string | null;
}

export interface EventCalendarFeedV1ResponseDto {
  readonly items: CalendarEventItemDto[];
  readonly totalCount: number;
  readonly projection: "temporal_grid_v1";
}

/**
 * Shared synchronization bundle between map and calendar overlay.
 * This keeps temporal filters and viewport identity in lockstep.
 */
export interface CalendarOverlayMapSyncState {
  readonly latestMapFeedQuery: EventMapFeedQueryDto | null;
  readonly selectedEventId: string | null;
}
