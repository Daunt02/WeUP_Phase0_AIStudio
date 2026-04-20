export type EventMapMarkerState =
  | "default"
  | "selected"
  | "saved"
  | "low-confidence-hidden";

/** Backend DTO: WeUP.Contracts.Events.EventMapItemDto */
export interface EventMapItemDto {
  readonly eventId: string;
  readonly title: string;
  readonly startUtc: string;
  readonly endUtc: string | null;
  readonly latitude: number;
  readonly longitude: number;
  readonly venueName: string;
  readonly district: string | null;
  readonly primaryCategory: string;
  readonly savedByCurrentUser: boolean;
  readonly markerState: EventMapMarkerState;
}

/** Backend DTO: WeUP.Contracts.Events.EventMapFeedV1ResponseDto */
export interface EventMapFeedV1ResponseDto {
  readonly events: EventMapItemDto[];
  readonly totalCount: number;
}

/** Backend query contract: WeUP.Contracts.Events.EventMapFeedQueryDto */
export interface EventMapFeedQueryDto {
  readonly bbox: string;
  readonly timeWindowPreset?: "today" | "tonight" | "weekend" | "next7days";
  readonly fromUtc?: string;
  readonly toUtc?: string;
  readonly district?: string;
  readonly categories?: string[];
  readonly includeSavedOnly?: boolean;
}

export interface EventMapMarkerViewModel extends EventMapItemDto {
  /**
   * Effective marker state is derived on the client for deterministic selection.
   * Canonical identity resolution must always use eventId.
   */
  readonly effectiveMarkerState: Exclude<
    EventMapMarkerState,
    "low-confidence-hidden"
  >;
}
