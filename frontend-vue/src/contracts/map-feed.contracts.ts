import type { TimeWindowFilterDto } from "./time-window.contracts";
import type { TemporalQueryDto } from "./temporal-query.contracts";

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

export type EventMapClusterStrategy = "client_v1" | "server_v1";

export type EventMapClusterExpansionBehavior = "zoom_or_expand";

export interface EventMapFeedClusterDto {
  readonly clusterId: string;
  readonly centerLat: number;
  readonly centerLng: number;
  readonly count: number;
  readonly eventIds: string[];
  readonly savedCount: number;
}

export interface EventMapDensityControlDto {
  readonly clusteringEnabled: boolean;
  readonly activationVisibleEventCountThreshold: number;
  readonly activationMaxZoomInclusive: number;
  readonly clusterRadiusPixels: number;
  readonly clusterMaxZoomInclusive: number;
  readonly selectedMarkerBypassEnabled: boolean;
  readonly expansionBehavior: EventMapClusterExpansionBehavior;
}

/** Backend DTO: WeUP.Contracts.Events.EventMapFeedV1ResponseDto */
export interface EventMapFeedV1ResponseDto {
  readonly events: EventMapItemDto[];
  readonly totalCount: number;
  readonly clusters: EventMapFeedClusterDto[];
  readonly densityControl: EventMapDensityControlDto;
  readonly clusterStrategy: EventMapClusterStrategy;
}

/** Backend query contract: WeUP.Contracts.Events.EventMapFeedQueryDto */
export interface EventMapFeedQueryDto extends TimeWindowFilterDto {
  readonly bbox: string;
  /** Canonical temporal contract (preferred). Backend keeps preset resolution authority. */
  readonly marketTimezone?: TemporalQueryDto["marketTimezone"];
  readonly fromUtc?: TemporalQueryDto["fromUtc"];
  readonly toUtc?: TemporalQueryDto["toUtc"];
  readonly referenceInstantUtc?: TemporalQueryDto["referenceInstantUtc"];
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
