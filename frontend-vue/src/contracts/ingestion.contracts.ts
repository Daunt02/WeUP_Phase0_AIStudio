/**
 * WEUP-SYNTH:
 * source=types/ingestion.ts (authoritative)
 * destination=frontend-vue/src/contracts/ingestion.contracts.ts
 * mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
 * notes=G2 arbitration outcome: the authoritative ingestion model is the one
 *   served by the Next API substrate (app/api/ingestion/**), whose handlers
 *   return lib/ingestion IngestionJobRecord payloads verbatim. An earlier
 *   draft of this file modeled a mixed-case DTO set that no served route
 *   produces; it is realigned here to the authoritative types/ingestion.ts
 *   shapes. ProvenanceStageDto (the Vue-side 8-stage projection from mission
 *   section 22) is unchanged — it is UI projection, not a competing model.
 */

/** Authoritative: types/ingestion.ts IngestionSourceKind. */
export type IngestionSourceKind =
  | "MANUAL"
  | "URL"
  | "VENUE_PAGE"
  | "EXTERNAL_FEED"
  | "FLYER_OCR";

/**
 * Authoritative: types/ingestion.ts IngestionJobStatus.
 * RECEIVED -> VALIDATING -> NORMALIZING -> CANDIDATE_CREATED -> REQUIRES_REVIEW,
 * with terminal FAILED / RETRYABLE_FAILURE.
 */
export type IngestionJobStatus =
  | "RECEIVED"
  | "VALIDATING"
  | "NORMALIZING"
  | "CANDIDATE_CREATED"
  | "REQUIRES_REVIEW"
  | "FAILED"
  | "RETRYABLE_FAILURE";

/** Authoritative: types/ingestion.ts CanonicalIngestionIssue. */
export interface CanonicalIngestionIssue {
  readonly code: string;
  readonly message: string;
  readonly severity: "WARNING" | "ERROR";
  readonly field?: string;
}

/** Authoritative: types/ingestion.ts CanonicalSourceEvidence. */
export interface CanonicalSourceEvidence {
  readonly sourceUrl?: string;
  readonly assetId?: string;
  readonly rawPayload?: unknown;
  readonly capturedAt: string;
  readonly extractedBy: string;
  readonly confidenceScore: number;
  readonly confidenceVector?: ConfidenceVector;
  readonly ocrText?: string;
  readonly processingMetadata?: Readonly<Record<string, unknown>>;
}

/** Authoritative: types/ingestion.ts ConfidenceVector. */
export interface ConfidenceVector {
  readonly extraction: number;
  readonly temporal: number;
  readonly venueMatch: number;
  readonly geocode: number;
  readonly overall: number;
}

/** Authoritative: types/ingestion.ts FlyerOcrResult. */
export interface FlyerOcrResult {
  readonly rawText: string;
  readonly blocks?: unknown[];
  readonly confidence: number;
  readonly ocrEngine: string;
  readonly ocrVersion: string;
}

/** Authoritative: types/ingestion.ts FlyerNormalizationResult. */
export interface FlyerNormalizationResult {
  readonly candidate: CanonicalEventCandidate;
  readonly confidenceVector: ConfidenceVector;
  readonly issues: CanonicalIngestionIssue[];
  readonly modelName: string;
  readonly modelVersion: string;
}

/**
 * Authoritative: types/ingestion.ts CanonicalEventCandidate.
 * A candidate is NEVER a canonical event until the backend persists it; the
 * Vue surface may preview it through the canonical event grammar but must
 * never promote it into canonical event state.
 */
export interface CanonicalEventCandidate {
  readonly title?: string;
  readonly description?: string;
  readonly venueName?: string;
  readonly address?: string;
  readonly startTime?: string;
  readonly endTime?: string;
  readonly category?: string;
  readonly priceTier?: string;
  readonly imageUrl?: string;
  readonly latitude?: number;
  readonly longitude?: number;
  readonly tags?: string[];
}

/** Authoritative: types/ingestion.ts IngestionRequestEnvelope. */
export interface IngestionRequestEnvelope {
  readonly sourceKind: IngestionSourceKind;
  readonly payload: Record<string, unknown>;
  readonly metadata?: Record<string, unknown>;
}

/** Authoritative: types/ingestion.ts IngestionResult. */
export interface IngestionResult {
  readonly jobId: string;
  readonly status: IngestionJobStatus;
  readonly candidate?: CanonicalEventCandidate;
  readonly evidence: CanonicalSourceEvidence;
  readonly issues: CanonicalIngestionIssue[];
  readonly executedAt: string;
}

/** Authoritative: types/ingestion.ts AdapterCapabilityDescriptor. */
export interface AdapterCapabilityDescriptor {
  readonly adapterId: string;
  readonly supportedKinds: IngestionSourceKind[];
  readonly version: string;
}

/**
 * Authoritative: types/ingestion.ts IngestionJobRecord — the exact payload
 * returned by GET /api/ingestion/jobs/{id} and by every POST
 * /api/ingestion/* route (lib/ingestion/coordinator submit/getJobStatus).
 */
export interface IngestionJobRecord {
  readonly id: string;
  readonly sourceKind: IngestionSourceKind;
  readonly status: IngestionJobStatus;
  readonly requestPayload: Record<string, unknown>;
  readonly result?: IngestionResult;
  readonly createdAt: string;
  readonly updatedAt: string;
  readonly errorContext?: string;
}

/**
 * Payload of GET /api/ingestion/flyers/{id}/evidence — the evidence slice of
 * a flyer job's result plus its issue ledger.
 */
export interface FlyerEvidenceDto {
  readonly jobId: string;
  readonly sourceKind: IngestionSourceKind;
  readonly evidence: CanonicalSourceEvidence;
  readonly issues: CanonicalIngestionIssue[];
}

/** Mission section 22: the 8 required provenance stages. */
export type ProvenanceStageKind =
  | "SOURCE"
  | "INGESTION_JOB"
  | "EXTRACTION"
  | "NORMALIZATION"
  | "CONFIDENCE"
  | "ISSUES"
  | "CANONICAL_EVENT"
  | "UI_PROJECTION";

/**
 * Stage completion state. "pending" is reserved for backend stages that are
 * stubbed or not yet linked — it must never be rendered as success.
 */
export type ProvenanceStageStatus =
  | "complete"
  | "pending"
  | "unavailable"
  | "failed";

export interface ProvenanceStageDto {
  readonly stage: ProvenanceStageKind;
  readonly title: string;
  readonly status: ProvenanceStageStatus;
  /** One-line human-readable status for the stage. */
  readonly summary: string;
  /** Evidence detail rows, shown when the stage is expanded. */
  readonly details: ReadonlyArray<{
    readonly label: string;
    readonly value: string;
    readonly mono: boolean;
  }>;
}

// ─── G11: ingestion state-machine vocabulary ────────────────────────────────
// Derived from the authoritative lib/ingestion/coordinator lifecycle:
// RECEIVED -> VALIDATING -> NORMALIZING -> (adapter result status), with
// terminal FAILED / RETRYABLE_FAILURE. These helpers are pure and unit-tested.

/** Ordered observable lifecycle stages (mission section 12). The last two are
 * terminal review states — CANDIDATE_CREATED / REQUIRES_REVIEW stop polling. */
export const INGESTION_STATUS_FLOW: readonly IngestionJobStatus[] = [
  "RECEIVED",
  "VALIDATING",
  "NORMALIZING",
  "CANDIDATE_CREATED",
  "REQUIRES_REVIEW",
] as const;

/** Terminal statuses: polling stops, the job never advances further. */
export const TERMINAL_INGESTION_STATUSES: ReadonlySet<IngestionJobStatus> =
  new Set(["CANDIDATE_CREATED", "REQUIRES_REVIEW", "FAILED", "RETRYABLE_FAILURE"]);

export function isTerminalIngestionStatus(status: IngestionJobStatus): boolean {
  return TERMINAL_INGESTION_STATUSES.has(status);
}

export function isFailedIngestionStatus(status: IngestionJobStatus): boolean {
  return status === "FAILED" || status === "RETRYABLE_FAILURE";
}

/** Human-readable labels for the observable lifecycle (never a fake stage). */
export function ingestionStatusLabel(status: IngestionJobStatus): string {
  switch (status) {
    case "RECEIVED":
      return "Received";
    case "VALIDATING":
      return "Validating payload";
    case "NORMALIZING":
      return "Extracting + normalizing";
    case "CANDIDATE_CREATED":
      return "Candidate created";
    case "REQUIRES_REVIEW":
      return "Requires review";
    case "FAILED":
      return "Failed";
    case "RETRYABLE_FAILURE":
      return "Retryable failure";
  }
}

/**
 * Per-source submission metadata for the CREATE flow. `submittable` reflects
 * surveyed backend reality (lib/ingestion/resolver.ts + route handlers):
 * EXTERNAL_FEED has no registered resolver adapter and no POST route, so the
 * UI must expose the path honestly and block submission with the reason.
 */
export interface IngestionSourceMeta {
  readonly kind: IngestionSourceKind;
  readonly label: string;
  readonly hint: string;
  readonly submittable: boolean;
  readonly blockedReason: string | null;
}

export const INGESTION_SOURCE_METAS: readonly IngestionSourceMeta[] = [
  {
    kind: "MANUAL",
    label: "Manual entry",
    hint: "Type the event details yourself — highest-confidence path",
    submittable: true,
    blockedReason: null,
  },
  {
    kind: "URL",
    label: "Paste link",
    hint: "Submit a URL; the backend extraction stage is currently stubbed",
    submittable: true,
    blockedReason: null,
  },
  {
    kind: "VENUE_PAGE",
    label: "Venue page",
    hint: "Submit a venue page URL; the backend crawler is currently stubbed",
    submittable: true,
    blockedReason: null,
  },
  {
    kind: "FLYER_OCR",
    label: "Flyer upload",
    hint: "Submit a flyer asset; the backend OCR/normalization are explicit stubs",
    submittable: true,
    blockedReason: null,
  },
  {
    kind: "EXTERNAL_FEED",
    label: "External feed",
    hint: "Feed adapter ingestion",
    submittable: false,
    blockedReason:
      "No feed adapter is registered in the backend resolver and no POST /api/ingestion/external-feed route exists — submission is blocked until the backend lands one.",
  },
] as const;

export function ingestionSourceMeta(
  kind: IngestionSourceKind,
): IngestionSourceMeta {
  const meta = INGESTION_SOURCE_METAS.find((entry) => entry.kind === kind);
  if (!meta) {
    throw new Error(`Unknown ingestion source kind: ${kind}`);
  }
  return meta;
}
