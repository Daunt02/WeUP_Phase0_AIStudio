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
