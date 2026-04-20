/**
 * Moderation Service — M3-P15
 * Client for the moderation queue REST API.
 * Backend surface: /api/moderation/queue, /api/moderation/reviews/{id}/*
 */

import { toApiUrl } from "@/services/apiBase";
import { getAuthHeader } from "@/services/auth";

// ---------------------------------------------------------------------------
// Enums — mirror WeUP.Contracts.Moderation
// ---------------------------------------------------------------------------

export type ModerationItemKind =
  | "CandidateReview"
  | "DedupeReview"
  | "IngestionFailure"
  | "PublishBlocked";

export type ModerationItemStatus = "Open" | "InReview" | "Resolved" | "Closed";

export type ModerationReviewStatus =
  | "NEEDS_REVIEW"
  | "APPROVED"
  | "REJECTED"
  | "CHANGES_REQUESTED";

export type ModerationReviewUrgency = "Low" | "Normal" | "High" | "Critical";

export type ConfidenceBucket = "High" | "Medium" | "Low";

export type DuplicateSeverity = "None" | "Possible" | "Probable" | "Definite";

// ---------------------------------------------------------------------------
// DTO shapes
// ---------------------------------------------------------------------------

export interface CandidateSnapshotDto {
  title: string | null;
  venueName: string | null;
  address: string | null;
  startUtc: string | null;
  endUtc: string | null;
  timezone: string | null;
  category: string | null;
  description: string | null;
  tags: string[] | null;
  sourceKind: string;
  sourceRef: string;
}

export interface ProvenanceSummaryDto {
  sourceKind: string;
  sourceRef: string;
  ingestionJobId: string | null;
  evidenceRefs: string[];
  submittedAt: string;
}

export interface ConfidenceSummaryDto {
  extraction: number;
  geocode: number;
  temporal: number;
  venueMatch: number;
  dupeRisk: number;
  aggregate: number;
  bucket: ConfidenceBucket;
  reviewBlockers: string[];
}

export interface DedupeSummaryDto {
  existingEventId: string | null;
  existingEventTitle: string | null;
  matchScore: number;
  severity: DuplicateSeverity;
  matchReasons: string[];
}

export interface IngestionJobSummaryDto {
  jobId: string;
  status: string;
  failureReason: string | null;
  createdAt: string;
  completedAt: string | null;
}

export interface ModerationQueueItemDto {
  itemId: string;
  kind: ModerationItemKind;
  status: ModerationItemStatus;
  reviewStatus: ModerationReviewStatus;
  eventId: string | null;
  candidateId: string | null;
  sourceKind: string;
  confidence: number;
  blockerReasons: string[];
  evidenceAvailable: boolean;
  urgency: ModerationReviewUrgency;
  createdAt: string;
  updatedAt: string;
  candidate: CandidateSnapshotDto | null;
  provenance: ProvenanceSummaryDto;
  confidenceSummary: ConfidenceSummaryDto;
  dedupeMatch: DedupeSummaryDto | null;
  ingestionJob: IngestionJobSummaryDto | null;
  reviewReasons: string[];
  assignedReviewerId: string | null;
}

export interface ModerationQueueResponse {
  items: ModerationQueueItemDto[];
  totalCount: number;
  nextCursor: string | null;
  previousCursor: string | null;
}

export interface ModerationStatsDto {
  totalOpen: number;
  totalInReview: number;
  totalResolved: number;
  candidateReviewOpen: number;
  dedupeReviewOpen: number;
  ingestionFailureOpen: number;
  publishBlockedOpen: number;
  highConfidenceOpen: number;
  mediumConfidenceOpen: number;
  lowConfidenceOpen: number;
  asOf: string;
}

export interface ConfidenceVector {
  extraction: number;
  geocode: number;
  temporal: number;
  venueMatch: number;
  dupeRisk: number;
  aggregate: number;
  reviewConfidence: number;
  bucket: ConfidenceBucket;
}

export interface ModerationEvidenceBundle {
  queueItemId: string;
  eventId: string | null;
  candidateId: string | null;
  sourceKind: string;
  sourceRef: string;
  evidenceRefs: string[];
  confidence: ConfidenceVector;
  blockerReasons: string[];
  sourceRefs: string[];
  ocrText: string | null;
  rawExtractionText: string | null;
  resolutionExplanation: string | null;
  retrievedAtUtc: string;
}

export interface ReviewHistoryItemDto {
  itemId: string;
  itemKind: string;
  action: string;
  actorId: string;
  note: string | null;
  previousStatus: string;
  nextStatus: string;
  timestamp: string;
}

export interface ReviewHistoryResponse {
  items: ReviewHistoryItemDto[];
  totalCount: number;
  nextCursor: string | null;
}

// ---------------------------------------------------------------------------
// Query filters
// ---------------------------------------------------------------------------

export interface ModerationQueueFilters {
  status?: ModerationItemStatus;
  kind?: ModerationItemKind;
  confidenceBucket?: ConfidenceBucket;
  pageSize?: number;
  cursor?: string;
}

// ---------------------------------------------------------------------------
// API calls
// ---------------------------------------------------------------------------

function buildQueueUrl(filters: ModerationQueueFilters): string {
  const params = new URLSearchParams();
  if (filters.status) params.set("status", filters.status);
  if (filters.kind) params.set("kind", filters.kind);
  if (filters.confidenceBucket)
    params.set("confidenceBucket", filters.confidenceBucket);
  if (filters.pageSize) params.set("pageSize", String(filters.pageSize));
  if (filters.cursor) params.set("cursor", filters.cursor);
  const qs = params.toString();
  return toApiUrl(`/api/moderation/queue${qs ? `?${qs}` : ""}`);
}

export async function getModerationQueue(
  filters: ModerationQueueFilters = {},
  signal?: AbortSignal,
): Promise<ModerationQueueResponse> {
  const res = await fetch(buildQueueUrl(filters), {
    headers: getAuthHeader(),
    signal,
  });
  if (!res.ok) throw new Error(`Moderation queue fetch failed: ${res.status}`);
  return res.json();
}

export async function getModerationItem(
  itemId: string,
  signal?: AbortSignal,
): Promise<ModerationQueueItemDto> {
  const res = await fetch(toApiUrl(`/api/moderation/queue/${itemId}`), {
    headers: getAuthHeader(),
    signal,
  });
  if (!res.ok) throw new Error(`Moderation item fetch failed: ${res.status}`);
  return res.json();
}

export async function getModerationEvidence(
  itemId: string,
  signal?: AbortSignal,
): Promise<ModerationEvidenceBundle> {
  const res = await fetch(
    toApiUrl(`/api/moderation/queue/${itemId}/evidence`),
    {
      headers: getAuthHeader(),
      signal,
    },
  );
  if (!res.ok) throw new Error(`Evidence fetch failed: ${res.status}`);
  return res.json();
}

export async function getModerationHistory(
  itemId: string,
  signal?: AbortSignal,
): Promise<ReviewHistoryResponse> {
  const res = await fetch(
    toApiUrl(`/api/moderation/reviews/${itemId}/history`),
    {
      headers: getAuthHeader(),
      signal,
    },
  );
  if (!res.ok) throw new Error(`History fetch failed: ${res.status}`);
  return res.json();
}

export async function getModerationStats(
  signal?: AbortSignal,
): Promise<ModerationStatsDto> {
  const res = await fetch(toApiUrl("/api/moderation/stats"), {
    headers: getAuthHeader(),
    signal,
  });
  if (!res.ok) throw new Error(`Stats fetch failed: ${res.status}`);
  return res.json();
}

// ---------------------------------------------------------------------------
// Review actions
// ---------------------------------------------------------------------------

async function postReviewAction(
  itemId: string,
  action: string,
  body: Record<string, unknown>,
): Promise<{ success: boolean; message?: string }> {
  const res = await fetch(
    toApiUrl(`/api/moderation/reviews/${itemId}/${action}`),
    {
      method: "POST",
      headers: { "Content-Type": "application/json", ...getAuthHeader() },
      body: JSON.stringify(body),
    },
  );
  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new Error(`Action '${action}' failed (${res.status}): ${text}`);
  }
  return res.json().catch(() => ({ success: true }));
}

export function approveItem(itemId: string, actorId: string, comment?: string) {
  return postReviewAction(itemId, "approve", {
    actorId,
    comment: comment ?? "",
    reasons: [],
    correlationId: crypto.randomUUID(),
  });
}

export function rejectItem(
  itemId: string,
  actorId: string,
  comment: string,
  reasons: string[],
) {
  return postReviewAction(itemId, "reject", {
    actorId,
    comment,
    reasons,
    correlationId: crypto.randomUUID(),
  });
}

export function requestChanges(
  itemId: string,
  actorId: string,
  comment: string,
  reasons: string[],
) {
  return postReviewAction(itemId, "request-changes", {
    actorId,
    comment,
    reasons,
    correlationId: crypto.randomUUID(),
  });
}

export function archiveItem(itemId: string, actorId: string, comment?: string) {
  return postReviewAction(itemId, "archive", {
    actorId,
    comment: comment ?? "",
    correlationId: crypto.randomUUID(),
  });
}
