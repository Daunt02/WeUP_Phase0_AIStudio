/**
 * WeUP Phase 0 — Backend API DTOs (M4-P17)
 *
 * These interfaces are the typed wire-format representation of what the .NET
 * backend actually returns from its event endpoints. They mirror the C# records
 * defined in WeUP.Contracts.Events exactly.
 *
 * FIELD VISIBILITY CLASSIFICATION:
 *
 *   FRONTEND-SAFE  — safe to expose from any authenticated event endpoint
 *   OPERATIONAL    — moderation / operator endpoints only (requires moderator auth)
 *
 * Mapping layer (frontend):
 *   apiDto -> projection: done in domains/event/projections.ts via toXxx() helpers.
 *   Components depend on projections, NOT directly on these DTO types.
 *
 * Serialization rules (must match backend):
 *   - Dates: ISO 8601 UTC strings formatted as "yyyy-MM-ddTHH:mm:ssZ"
 *   - Enums: uppercase string for frontend-visible lifecycle status (e.g. "PUBLISHED")
 *           PascalCase string for operational DTOs (e.g., "Approved", "Low")
 *   - Nullable fields: null (not undefined) when absent
 *   - Arrays: never null — use empty array []
 */

// ---------------------------------------------------------------------------
// Map card DTO (FRONTEND-SAFE)
// Endpoint: POST /api/events/map → MapFeedResponse.events[]
// ---------------------------------------------------------------------------

export interface EventMapCardDto {
  /** Canonical event identity. */
  readonly id: string;
  readonly title: string;
  readonly venueName: string;
  readonly category: string;
  readonly lat: number;
  readonly lng: number;
  readonly thumbnailUrl: string | null;
  /** Uppercase lifecycle string: "DRAFT" | "NEEDS_REVIEW" | "APPROVED" | "PUBLISHED" | "REJECTED" | "ARCHIVED" */
  readonly status: string;
  readonly confidence: number;
}

// ---------------------------------------------------------------------------
// Canonical map feed v1 DTOs (FRONTEND-SAFE)
// Endpoint: GET /api/events/map-feed/v1
// ---------------------------------------------------------------------------

export type EventMapMarkerState =
  | "default"
  | "selected"
  | "saved"
  | "low-confidence-hidden";

export interface EventMapItemDto {
  /** Canonical event identity used as the single source for marker selection. */
  readonly eventId: string;
  readonly title: string;
  /** ISO 8601 UTC. */
  readonly startUtc: string;
  /** ISO 8601 UTC. null when no explicit end time exists. */
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

/**
 * Canonical temporal presets for map discovery and calendar overlays.
 * These values must mirror WeUP.Contracts.Events.TimeWindowPreset exactly.
 */
export enum TimeWindowPreset {
  Now = "now",
  Tonight = "tonight",
  Tomorrow = "tomorrow",
  ThisWeekend = "thisWeekend",
  Custom = "custom",
}

export interface EventMapFeedQueryDto {
  /** Comma-separated format: minLng,minLat,maxLng,maxLat */
  readonly bbox: string;
  /** Backend-resolved preset identity for deterministic map/calendar window parity. */
  readonly preset: TimeWindowPreset;
  /** Required IANA or Windows timezone identifier used by backend preset expansion. */
  readonly timezone: string;
  /** Required when preset=custom. Must be earlier than customEndUtc. */
  readonly customStartUtc?: string;
  /** Required when preset=custom. Must be later than customStartUtc. */
  readonly customEndUtc?: string;
  readonly district?: string;
  readonly categories?: string[];
  readonly includeSavedOnly?: boolean;
}

export interface EventMapFeedV1ResponseDto {
  readonly events: EventMapItemDto[];
  readonly totalCount: number;
  readonly clusters: EventMapFeedClusterDto[];
  readonly densityControl: EventMapDensityControlDto;
  readonly clusterStrategy: EventMapClusterStrategy;
}

// ---------------------------------------------------------------------------
// Calendar card DTO (FRONTEND-SAFE)
// Endpoint: POST /api/events/calendar → CalendarFeedResponse.items[]
// ---------------------------------------------------------------------------

export interface EventCalendarCardDto {
  /** Canonical event identity — same id as EventMapCardDto and EventDetailDto. */
  readonly id: string;
  readonly title: string;
  readonly venueName: string;
  readonly category: string;
  /** ISO 8601 UTC. */
  readonly startUtc: string;
  /** ISO 8601 UTC. null for open-ended events. */
  readonly endUtc: string | null;
  /** IANA timezone identifier (e.g. "America/Chicago"). */
  readonly timezone: string;
  readonly thumbnailUrl: string | null;
  /** Uppercase lifecycle string. */
  readonly status: string;
}

// ---------------------------------------------------------------------------
// Event detail DTO (FRONTEND-SAFE)
// Endpoint: GET /api/events/{id} → EventDetailResponse.event
// ---------------------------------------------------------------------------

export interface MediaRefDto {
  readonly url: string;
  /** "image" | "video" | "poster" */
  readonly kind: string;
}

export interface EventDetailDto {
  /** Canonical event identity. */
  readonly id: string;
  readonly title: string;
  readonly description: string | null;
  readonly venueName: string;
  /** Display address: "Line1, City, State" — no raw/internal address. */
  readonly address: string;
  readonly lat: number;
  readonly lng: number;
  readonly category: string;
  /** ISO 8601 UTC. */
  readonly startUtc: string;
  /** ISO 8601 UTC. null for open-ended events. */
  readonly endUtc: string | null;
  /** IANA timezone identifier. */
  readonly timezone: string;
  /** Never null — use [] when no media. */
  readonly mediaRefs: MediaRefDto[];
  /** Never null — use [] when no tags. */
  readonly tags: string[];
  /** Uppercase lifecycle string. */
  readonly status: string;
  readonly confidence: number;
  /** Human-readable source kind label (e.g. "flyer upload"). */
  readonly sourceKind: string;
  /** Canonical aggregate version — use for optimistic client behaviour. Always >= 1. */
  readonly version: number;
  /** Last classified change type (MinorMetadataUpdate | MaterialEventChange | StatusTransition | MergeLineageUpdate). null if no history. */
  readonly lastChangeType: string | null;
  /** Concurrency token for conditional update calls. */
  readonly concurrencyToken: string | null;
}

// ---------------------------------------------------------------------------
// Event moderation DTO (OPERATIONAL — moderator auth required)
// Endpoint: GET /api/moderation/events/{id} (future; currently via queue item)
// ---------------------------------------------------------------------------

export interface EventMergeLineageSummaryDto {
  readonly parentCanonicalEventId: string | null;
  readonly mergedEventCount: number;
  /** ISO 8601 UTC. */
  readonly lastMergedAtUtc: string | null;
}

export interface EventModerationDto {
  /** Canonical event identity. */
  readonly canonicalEventId: string;
  readonly title: string;
  readonly category: string;
  readonly venueName: string;
  /** ISO 8601 UTC. */
  readonly startUtc: string;
  /** ISO 8601 UTC. */
  readonly endUtc: string | null;
  /** IANA timezone identifier. */
  readonly timezone: string;
  /** PascalCase — matches EventLifecycleStatus enum: "Draft" | "Candidate" | "Reviewed" | "Approved" | "Rejected" | "Published" | "Cancelled" | "Archived" */
  readonly lifecycleStatus: string;
  /** PascalCase — matches EventModerationStatus enum: "Unreviewed" | "InReview" | "Approved" | "Rejected" */
  readonly moderationStatus: string;
  /** PascalCase — matches EventPublishStatus enum. */
  readonly publishStatus: string;
  /** PascalCase — matches EventRiskLevel enum: "Low" | "Medium" | "High" | "Restricted" */
  readonly riskLevel: string;
  readonly confidenceScore: number;
  /** Aggregate version for optimistic concurrency. */
  readonly version: number;
  /** ISO 8601 UTC. */
  readonly updatedAtUtc: string;
  /** Last classified change type. null if no version history exists. */
  readonly lastChangeType: string | null;
  /** True when the last change requires moderator confirmation before publish. */
  readonly hasPendingReview: boolean;
  /** Concurrency token for conditional mutation requests. */
  readonly concurrencyToken: string | null;
  /** null if event has not been through merge. */
  readonly mergeLineage: EventMergeLineageSummaryDto | null;
}

// ---------------------------------------------------------------------------
// Publish eligibility DTO (OPERATIONAL — moderator/operator auth required)
// Endpoint: GET /api/events/{id}/publish-eligibility
// ---------------------------------------------------------------------------

export interface PublishBlockerSummaryDto {
  /** Stable code string — matches PublishBlockerCode enum name. */
  readonly code: string;
  readonly message: string;
  readonly isHardBlock: boolean;
  /** The specific field that caused the blocker, if applicable. */
  readonly field: string | null;
}

export interface EligibilityConfidenceSummaryDto {
  readonly aggregate: number;
  /** "High" | "Medium" | "Low" */
  readonly band: string;
  readonly extraction: number;
  readonly geocode: number;
  readonly temporal: number;
  readonly venueMatch: number;
  readonly dupeRisk: number;
  readonly meetsAutoPublishThreshold: boolean;
  readonly requiresManualReview: boolean;
}

export interface EligibilityFieldCompletenessSummaryDto {
  readonly isComplete: boolean;
  /** Never null — use [] when none missing. */
  readonly missingRequiredFields: string[];
  /** 0.0 – 1.0 */
  readonly completenessRatio: number;
}

export interface EventPublishEligibilityDto {
  /** Canonical event identity. */
  readonly canonicalEventId: string;
  readonly eligible: boolean;
  /** "AutoPublishable" | "ManualReviewRequired" | "Blocked" */
  readonly eligibilityBand: string;
  /** "AutoPublish" | "RouteToManualReview" | "BlockPublish" */
  readonly recommendedAction: string;
  /** Never null — use [] when no blockers. */
  readonly blockers: PublishBlockerSummaryDto[];
  readonly confidenceSummary: EligibilityConfidenceSummaryDto;
  readonly fieldCompleteness: EligibilityFieldCompletenessSummaryDto;
  /** Never null — use [] when no notes. */
  readonly notes: string[];
}
