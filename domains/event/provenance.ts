export enum SourceType {
  ManualSubmission = "manual_submission",
  FlyerUpload = "flyer_upload",
  PastedURL = "pasted_url",
  ScrapedVenuePage = "scraped_venue_page",
  ExternalFeed = "external_feed",
}

export type EvidenceType = "image" | "ocr" | "url" | "api_response" | "other";

export interface EvidenceReference {
  id: string; // unique id for the evidence (uuid)
  type: EvidenceType;
  uri?: string; // where the evidence is stored or accessible
  snapshotAt?: string; // ISO timestamp of when the evidence was captured
  checksum?: string; // optional checksum for immutable evidence
  page?: number; // for multi-page documents
  excerpt?: string; // a short text excerpt useful for review
}

export interface Provenance {
  id: string; // unique provenance id (uuid)
  sourceType: SourceType;
  sourceId?: string; // optional id inside the source (e.g., feed id, file id)
  receivedAt: string; // ISO timestamp when system ingested/observed it
  receivedBy?: string; // actor that ingested it (system/service/user id)
  rawEvidence?: EvidenceReference[]; // pointers to raw evidence artifacts
  originalPayloadSummary?: string; // short summary of original payload for tracing
  ingestionMethod?: string; // e.g., "ocr", "manual_form", "scraper-v2", "api-poll"
  trustScore?: number; // 0..1 heuristic assigned at ingestion based on source reliability
}

// Per-field or grouped confidences. Values are 0..1 where 1 is highest confidence.
export interface ConfidenceMap {
  extraction?: number; // confidence of parsed fields from the input (overall)
  geocode?: number; // confidence of resolved location
  moderation?: number; // automated moderation confidence
  datetime?: number; // confidence in parsed dates/times
  title?: number; // confidence in parsed title/name
  [key: string]: number | undefined;
}

export enum ReviewState {
  Pending = "pending",
  Approved = "approved",
  Rejected = "rejected",
  NeedsClarification = "needs_clarification",
}

export interface ReviewNote {
  id: string;
  by?: string; // reviewer id
  at: string; // ISO timestamp
  note: string;
  severity?: "info" | "warning" | "critical";
}

export interface ReviewMetadata {
  state: ReviewState;
  reviewedBy?: string;
  reviewedAt?: string;
  notes?: ReviewNote[];
  evidenceRefs?: string[]; // evidence ids that were used during review
  autoPublishDecision?: boolean; // explicit decision flag if automated publish was performed
  manualReviewRequired?: boolean; // computed or asserted flag
}

export interface EventProvenanceBundle {
  provenance: Provenance;
  confidences?: ConfidenceMap;
  review?: ReviewMetadata;
}

// -------------------------------
// Utility functions
// -------------------------------

function clamp01(n: number) {
  if (Number.isFinite(n) === false) return 0;
  if (n < 0) return 0;
  if (n > 1) return 1;
  return n;
}

/**
 * Compute a composite review readiness score (0..1).
 * Weights can be overridden. Default balances extraction, moderation, geocode and provenance trust.
 */
export function computeReviewReadiness(
  bundle: EventProvenanceBundle,
  weights?: {
    extraction?: number;
    moderation?: number;
    geocode?: number;
    provenanceTrust?: number;
  },
): number {
  const w = {
    extraction: 0.45,
    moderation: 0.35,
    geocode: 0.1,
    provenanceTrust: 0.1,
    ...weights,
  };

  const confidences = bundle.confidences || {};
  const extraction = clamp01(confidences.extraction ?? 0);
  const moderation = clamp01(confidences.moderation ?? 0);
  const geocode = clamp01(confidences.geocode ?? 0);
  const provenanceTrust = clamp01(bundle.provenance.trustScore ?? 0.5);

  const score =
    extraction * w.extraction +
    moderation * w.moderation +
    geocode * w.geocode +
    provenanceTrust * w.provenanceTrust;

  // normalize dividing by sum of weights to keep 0..1
  const sumW = w.extraction + w.moderation + w.geocode + w.provenanceTrust;
  return clamp01(score / sumW);
}

/**
 * Determine whether an event can be auto-published.
 * Default policy: readiness >= 0.85, review state not rejected, and no manualReviewRequired.
 */
export function canAutoPublish(
  bundle: EventProvenanceBundle,
  threshold = 0.85,
): boolean {
  const readiness = computeReviewReadiness(bundle);
  const review = bundle.review;

  if (review?.manualReviewRequired) return false;
  if (review?.state === ReviewState.Rejected) return false;
  if (readiness >= threshold) return true;
  return false;
}

/**
 * Determine whether event needs manual review.
 * True when manualReviewRequired flag is set, or readiness below threshold, or critical notes exist.
 */
export function requiresManualReview(
  bundle: EventProvenanceBundle,
  threshold = 0.75,
): boolean {
  const readiness = computeReviewReadiness(bundle);
  const review = bundle.review;

  if (review?.manualReviewRequired) return true;
  if (review?.state === ReviewState.NeedsClarification) return true;
  if (readiness < threshold) return true;
  if (review?.notes?.some((n) => n.severity === "critical")) return true;
  return false;
}

export default {
  SourceType,
  computeReviewReadiness,
  canAutoPublish,
  requiresManualReview,
};
/**
 * WeUP Phase 0 — Event Source Provenance, Confidence, and Review Metadata
 *
 * Every event must be traceable to its origin. This file defines:
 * - Source provenance models per ingestion kind
 * - Multi-dimensional confidence vector
 * - Review metadata structures
 * - Utility functions for review readiness, auto-publish eligibility, and blockers
 */

// ---------------------------------------------------------------------------
// Source provenance per kind
// SourceKind is the canonical definition — imported from types.ts.
// ---------------------------------------------------------------------------

import type { SourceKind } from "./types";
export type { SourceKind };

export interface BaseProvenance {
  kind: SourceKind;
  ingestedAt: string; // ISO 8601 UTC
  /** Semantic version of the extraction/normalization pipeline used. */
  extractionVersion: string;
}

export interface ManualSubmissionProvenance extends BaseProvenance {
  kind: "manual_submission";
  submitterId: string;
  submitterDisplayName?: string;
}

export interface FlyerUploadProvenance extends BaseProvenance {
  kind: "flyer_upload";
  submitterId: string;
  assetId: string;
  originalFilename: string;
  ocrJobId?: string;
  llmJobId?: string;
}

export interface PastedUrlProvenance extends BaseProvenance {
  kind: "pasted_url";
  submitterId: string;
  url: string;
  /** Snapshot of the page content captured at ingestion time, if stored. */
  snapshotRef?: string;
}

export interface ScrapedVenuePageProvenance extends BaseProvenance {
  kind: "scraped_venue_page";
  venueId?: string;
  sourceUrl: string;
  crawlJobId: string;
}

export interface ExternalFeedProvenance extends BaseProvenance {
  kind: "external_feed";
  feedId: string;
  feedName: string;
  externalEventId: string;
}

export type EventProvenance =
  | ManualSubmissionProvenance
  | FlyerUploadProvenance
  | PastedUrlProvenance
  | ScrapedVenuePageProvenance
  | ExternalFeedProvenance;

// ---------------------------------------------------------------------------
// Evidence reference
// ---------------------------------------------------------------------------

export interface EvidenceRef {
  /** Stable reference to the raw artifact (storage key, URL, job id, etc.) */
  ref: string;
  /** Human-readable description of the evidence type. */
  description: string;
  capturedAt: string; // ISO 8601 UTC
}

// ---------------------------------------------------------------------------
// Confidence vector
// ---------------------------------------------------------------------------

/**
 * Multi-dimensional confidence vector. Each dimension is 0–1.
 * Use `computeAggregateConfidence()` for a single scalar.
 */
export interface ConfidenceVector {
  [key: string]: number;
  /** How accurately the text extraction retrieved the event fields. */
  extractionConfidence: number;
  /** How accurately the address was geocoded. */
  geocodeConfidence: number;
  /** How confidently the temporal fields (start/end) were parsed. */
  temporalConfidence: number;
  /** How well the venue name resolved against known venues. */
  venueMatchConfidence: number;
  /** Confidence that this is not a duplicate of an existing event. */
  dedupeConfidence: number;
}

/**
 * Weighted average of the confidence vector dimensions.
 * Weights are intentionally tuned for Phase 0 nightlife/event discovery.
 */
export function computeAggregateConfidence(v: ConfidenceVector): number {
  const weights: Record<keyof ConfidenceVector, number> = {
    extractionConfidence: 0.3,
    geocodeConfidence: 0.25,
    temporalConfidence: 0.25,
    venueMatchConfidence: 0.1,
    dedupeConfidence: 0.1,
  };
  return (
    v.extractionConfidence * weights.extractionConfidence +
    v.geocodeConfidence * weights.geocodeConfidence +
    v.temporalConfidence * weights.temporalConfidence +
    v.venueMatchConfidence * weights.venueMatchConfidence +
    v.dedupeConfidence * weights.dedupeConfidence
  );
}

// ---------------------------------------------------------------------------
// Review status
// ---------------------------------------------------------------------------

export type ReviewStatus =
  | "NOT_REQUIRED"
  | "PENDING"
  | "APPROVED"
  | "REJECTED"
  | "CHANGES_REQUESTED";

// ---------------------------------------------------------------------------
// Review metadata (full model — extends the slim version in EventAggregate)
// ---------------------------------------------------------------------------

export interface ReviewMetadataFull {
  status: ReviewStatus;
  requiredReason?: string; // why manual review was triggered
  reviewerId?: string;
  reviewedAt?: string; // ISO 8601 UTC
  notes?: string;
  rejectionReason?: string;
  changeRequestInstructions?: string;
  evidenceRefs: EvidenceRef[];
  publishDecision?: "auto_approved" | "manually_approved" | "rejected";
}

// ---------------------------------------------------------------------------
// Ingestion metadata
// ---------------------------------------------------------------------------

export interface IngestionMetadata {
  provenance: EventProvenance;
  confidenceVector: ConfidenceVector;
  review: ReviewMetadataFull;
  evidenceRefs: EvidenceRef[];
}

// ---------------------------------------------------------------------------
// Utility functions
// ---------------------------------------------------------------------------

/** Minimum confidence below which manual review is always required. */
const MANUAL_REVIEW_THRESHOLD = 0.85;
/** Minimum per-dimension confidence that still produces a passing aggregate. */
const MIN_DIMENSION_CONFIDENCE = 0.5;

/**
 * Returns reasons why this event requires manual review.
 * An empty array means auto-publish is safe.
 */
export function getReviewBlockers(
  confidence: ConfidenceVector,
  reviewMetadata: ReviewMetadataFull,
): string[] {
  const blockers: string[] = [];

  const aggregate = computeAggregateConfidence(confidence);
  if (aggregate < MANUAL_REVIEW_THRESHOLD) {
    blockers.push(
      `Aggregate confidence ${aggregate.toFixed(2)} below threshold ${MANUAL_REVIEW_THRESHOLD}`,
    );
  }

  const dims = confidence as Record<string, number>;
  for (const [dim, val] of Object.entries(dims)) {
    if (val < MIN_DIMENSION_CONFIDENCE) {
      blockers.push(
        `${dim} = ${val.toFixed(2)} below minimum ${MIN_DIMENSION_CONFIDENCE}`,
      );
    }
  }

  if (reviewMetadata.status === "REJECTED") {
    blockers.push(
      `Review status is REJECTED: ${reviewMetadata.rejectionReason ?? "no reason given"}`,
    );
  }

  if (reviewMetadata.status === "CHANGES_REQUESTED") {
    blockers.push(
      `Changes requested: ${reviewMetadata.changeRequestInstructions ?? "no instructions given"}`,
    );
  }

  return blockers;
}

/**
 * Returns true if the event can be auto-published without human review.
 */
export function canAutoPublish(
  confidence: ConfidenceVector,
  reviewMetadata: ReviewMetadataFull,
): boolean {
  return getReviewBlockers(confidence, reviewMetadata).length === 0;
}

/**
 * Returns true if manual review is required before publication.
 */
export function requiresManualReview(
  confidence: ConfidenceVector,
  reviewMetadata: ReviewMetadataFull,
): boolean {
  return !canAutoPublish(confidence, reviewMetadata);
}

// ---------------------------------------------------------------------------
// Example canonical event objects (documentation / test fixtures)
// ---------------------------------------------------------------------------

export const EXAMPLE_MANUAL_SUBMISSION_INGESTION: IngestionMetadata = {
  provenance: {
    kind: "manual_submission",
    submitterId: "user_abc123",
    ingestedAt: "2025-09-15T21:00:00Z",
    extractionVersion: "1.0.0",
  },
  confidenceVector: {
    extractionConfidence: 0.95,
    geocodeConfidence: 0.9,
    temporalConfidence: 0.95,
    venueMatchConfidence: 0.8,
    dedupeConfidence: 0.98,
  },
  review: {
    status: "NOT_REQUIRED",
    evidenceRefs: [],
    publishDecision: "auto_approved",
  },
  evidenceRefs: [],
};

export const EXAMPLE_FLYER_OCR_INGESTION: IngestionMetadata = {
  provenance: {
    kind: "flyer_upload",
    submitterId: "user_xyz789",
    assetId: "asset_flyer_001",
    originalFilename: "saturday_night_flyer.jpg",
    ocrJobId: "ocr_job_2025_001",
    llmJobId: "llm_job_2025_001",
    ingestedAt: "2025-09-14T18:30:00Z",
    extractionVersion: "1.2.0",
  },
  confidenceVector: {
    extractionConfidence: 0.72,
    geocodeConfidence: 0.88,
    temporalConfidence: 0.65, // time was ambiguous on the flyer
    venueMatchConfidence: 0.6,
    dedupeConfidence: 0.9,
  },
  review: {
    status: "PENDING",
    requiredReason:
      "Temporal confidence below threshold; date required manual confirmation.",
    evidenceRefs: [
      {
        ref: "asset_flyer_001",
        description: "Original flyer image",
        capturedAt: "2025-09-14T18:30:00Z",
      },
      {
        ref: "ocr_job_2025_001",
        description: "OCR text extraction result",
        capturedAt: "2025-09-14T18:31:00Z",
      },
    ],
  },
  evidenceRefs: [
    {
      ref: "asset_flyer_001",
      description: "Original flyer image",
      capturedAt: "2025-09-14T18:30:00Z",
    },
  ],
};

export const EXAMPLE_EXTERNAL_FEED_INGESTION: IngestionMetadata = {
  provenance: {
    kind: "external_feed",
    feedId: "feed_eventbrite_atx",
    feedName: "Eventbrite Austin",
    externalEventId: "eb_12345678",
    ingestedAt: "2025-09-13T12:00:00Z",
    extractionVersion: "1.0.0",
  },
  confidenceVector: {
    extractionConfidence: 0.97,
    geocodeConfidence: 0.95,
    temporalConfidence: 0.99,
    venueMatchConfidence: 0.85,
    dedupeConfidence: 0.92,
  },
  review: {
    status: "NOT_REQUIRED",
    evidenceRefs: [],
    publishDecision: "auto_approved",
  },
  evidenceRefs: [],
};
