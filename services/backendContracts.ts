import type {
  CalendarFeedResponse,
  EventDetailResponse,
  GeoBoundingBox,
  MapFeedResponse,
  TimeWindow,
} from "@/domains/query/contracts";
import type { DraftSubmissionRequest } from "@/features/world/runtimeTypes";

export type {
  GeoBoundingBox,
  TimeWindow,
  MapFeedResponse,
  CalendarFeedResponse,
  EventDetailResponse,
  DraftSubmissionRequest,
};

export type SubmissionStatus =
  | "Draft"
  | "SubmittedForReview"
  | "ChangesRequested"
  | "Approved"
  | "Rejected";

export interface SavedEventDto {
  eventId: string;
  title: string;
  venueName: string;
  startUtc: string;
  thumbnailUrl: string | null;
  savedAt: string;
}

export interface SavedEventsResponse {
  items: SavedEventDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  hasNextPage: boolean;
}

export interface SaveEventRequestDto {
  eventId: string;
}

export interface UnsaveEventRequestDto {
  eventId: string;
}

export interface SaveEventResponseDto {
  eventId: string;
  saved: boolean;
  message: string;
}

export interface SavedStateDto {
  eventId: string;
  saved: boolean;
  sessionKind: string;
  persistenceSource: string;
}

export interface SaveStateLocalTemporalFilterDto {
  preset: string | null;
  timezone: string | null;
  customStartUtc: string | null;
  customEndUtc: string | null;
}

export interface SaveStateLocalMapViewportDto {
  bbox: string | null;
  capturedAtUtc: string | null;
}

export interface SaveStateLocalDiscoveryContextDto {
  lastViewedDistrict: string | null;
  lastTemporalFilter: SaveStateLocalTemporalFilterDto | null;
  recentMapViewport: SaveStateLocalMapViewportDto | null;
  updatedAtUtc: string | null;
}

export interface SaveStateMigrationRequestDto {
  localSavedEventIds: string[];
  localDiscoveryContext: SaveStateLocalDiscoveryContextDto | null;
  clientMigrationKey: string | null;
}

export interface SaveStateMigrationCountsDto {
  receivedLocalSavedCount: number;
  distinctLocalSavedCount: number;
  duplicateCollapsedCount: number;
  migratedCount: number;
  alreadySavedCount: number;
  invalidLocalIdCount: number;
  missingLocalIdCount: number;
}

export interface SaveStateMigrationItemResultDto {
  eventId: string;
  outcome: string;
  message: string;
}

export interface SaveStateDiscoveryContextMigrationResultDto {
  status: string;
  applied: boolean;
  message: string;
  resolvedPreferredTimezone: string | null;
}

export interface SaveStateMigrationResultDto {
  status: "completed" | "completed-with-issues";
  migrationDirection: "anonymous-to-authenticated";
  ownership: "authenticated-user";
  clientMigrationKey: string | null;
  processedAtUtc: string;
  counts: SaveStateMigrationCountsDto;
  itemResults: SaveStateMigrationItemResultDto[];
  retainedLocalSavedEventIds: string[];
  discoveryContext: SaveStateDiscoveryContextMigrationResultDto;
}

export interface SubmissionDto {
  submissionId: string;
  submittedByUserId: string;
  status: SubmissionStatus;
  title: string | null;
  venueName: string | null;
  address: string | null;
  startUtc: string | null;
  endUtc: string | null;
  timezone: string | null;
  category: string | null;
  description: string | null;
  tags: string[] | null;
  flyerAssetIds: string[] | null;
  reviewNote: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface SubmissionListResponse {
  items: SubmissionDto[];
  totalCount: number;
}

export interface SubmitForReviewResponse {
  submissionId: string;
  status: SubmissionStatus;
  message: string;
}

export interface UserPreferencesDto {
  userId: string;
  preferredCategories: string[];
  homeRadiusMeters: number;
  notifyOnNewEvents: boolean;
  notifyOnSaveReminders: boolean;
  preferredTimeZone: string | null;
  lastKnownMapCenterLat: number | null;
  lastKnownMapCenterLng: number | null;
  updatedAt: string;
}

export interface UpdatePreferencesRequest {
  preferredCategories?: string[];
  homeRadiusMeters?: number;
  notifyOnNewEvents?: boolean;
  notifyOnSaveReminders?: boolean;
  preferredTimeZone?: string | null;
  lastKnownMapCenterLat?: number | null;
  lastKnownMapCenterLng?: number | null;
}
