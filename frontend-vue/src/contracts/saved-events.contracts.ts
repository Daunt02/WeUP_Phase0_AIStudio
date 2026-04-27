import type { SaveSessionKind } from "./event-detail.contracts";
import type { EventMapItemDto } from "./map-feed.contracts";

export type SavedEventResolutionStatus = "resolved" | "missing-or-deleted";

/** Backend DTO: WeUP.Contracts.Saves.SavedEventDto */
export interface SavedEventDto {
  readonly eventId: string;
  readonly savedAt: string;
  readonly resolutionStatus: SavedEventResolutionStatus;
  readonly resolutionMessage: string | null;
  readonly canonicalEvent: EventMapItemDto | null;
}

/** Backend DTO: WeUP.Contracts.Saves.SavedEventsResponse */
export interface SavedEventsResponseDto {
  readonly items: SavedEventDto[];
  readonly totalCount: number;
  readonly page: number;
  readonly pageSize: number;
  readonly hasNextPage: boolean;
  readonly resolvedCount: number;
  readonly missingOrDeletedCount: number;
  readonly retrievedAtUtc: string;
  readonly sourceProjection: "canonical-event-map-v1";
}

export interface SavedEventsQueryDto {
  readonly page?: number;
  readonly pageSize?: number;
}

export interface SavedEventsSurfaceSnapshot {
  readonly sessionKind: SaveSessionKind;
  readonly savedCountBadgeValue: number;
  readonly savedEventIds: string[];
  readonly resolvedItems: SavedEventDto[];
  readonly missingOrDeletedItems: SavedEventDto[];
  readonly markerItems: EventMapItemDto[];
}
