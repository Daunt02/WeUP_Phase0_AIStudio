/**
 * WEUP-SYNTH:
 * source=components/ReceiptDrawer.tsx
 * destination=frontend-vue/src/contracts/ingestion.contracts.ts
 * mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
 * notes=New contract surface per G2 arbitration (ingestion/OCR/normalization
 *   DTOs had no Vue counterpart). Backend-aligned shapes are camelCase per the
 *   WeUP.Contracts.Ingestion serialization policy. ProvenanceStage* types are
 *   the Vue-side projection of mission section 22's 8-stage chain.
 */

/** Backend DTO: WeUP.Contracts.Ingestion.IngestionSourceKind */
export type IngestionSourceKind =
  | "ManualSubmission"
  | "PastedUrl"
  | "VenuePage"
  | "FlyerUpload"
  | "ExternalFeed";

/**
 * Backend DTO: WeUP.Contracts.Ingestion.IngestionJobStatus.
 * Mixed-case enum as shipped by the backend; the provenance projection maps
 * these to display labels rather than inventing a parallel status model.
 */
export type IngestionJobStatus =
  | "RECEIVED"
  | "VALIDATING"
  | "NORMALIZING"
  | "CANDIDATE_CREATED"
  | "REQUIRES_REVIEW"
  | "FAILED"
  | "RETRYABLE_FAILURE"
  | "Pending"
  | "Processing"
  | "OCR"
  | "Normalized"
  | "ReadyForDedup"
  | "Completed";

/** Backend DTO: WeUP.Contracts.Ingestion.IngestionIssueSeverity */
export type IngestionIssueSeverity = "Warning" | "Error";

/** Backend DTO: WeUP.Contracts.Ingestion.CanonicalIngestionIssue */
export interface CanonicalIngestionIssue {
  readonly code: string;
  readonly message: string;
  readonly severity: IngestionIssueSeverity;
  readonly isRetryable: boolean;
  readonly field: string | null;
  readonly metadata?: Readonly<Record<string, string | null>>;
}

/**
 * Backend DTO: WeUP.Contracts.Ingestion.CanonicalSourceEvidence.
 * G2 arbitration: adopted as the evidence contract for the provenance chain.
 */
export interface CanonicalSourceEvidence {
  readonly evidenceId: string;
  readonly evidenceKind: string;
  readonly reference: string;
  readonly mimeType: string | null;
  readonly payloadSnippet: string | null;
  readonly observedAtUtc: string;
  readonly confidence: number;
  readonly metadata?: Readonly<Record<string, string | null>>;
}

/**
 * Per-dimension confidence breakdown for the provenance CONFIDENCE stage.
 * G2 arbitration: ADOPT_AS_NEW_CONTRACT. No Vue equivalent existed; the
 * vector is populated only when a job record actually carries the evidence.
 */
export interface ConfidenceVector {
  readonly extraction: number | null;
  readonly normalization: number | null;
  readonly temporal: number | null;
  readonly venueMatch: number | null;
  readonly geocode: number | null;
  readonly overall: number | null;
}

/**
 * Backend DTO: WeUP.Contracts.Ingestion.IngestionRequestEnvelope (subset used
 * by the provenance surface).
 */
export interface IngestionRequestEnvelope {
  readonly requestId: string;
  readonly sourceKind: IngestionSourceKind;
  readonly submitterId: string;
  readonly submittedAtUtc: string;
  readonly sourceLabel: string | null;
}

/**
 * Backend DTO: WeUP.Contracts.Ingestion.CanonicalEventCandidate (subset).
 * A candidate is NEVER a canonical event until the backend approves it; the
 * provenance surface may cite it but never promotes it to event state.
 */
export interface CanonicalEventCandidate {
  readonly candidateId: string;
  readonly title: string;
  readonly venueName: string;
  readonly address: string | null;
  readonly startUtc: string | null;
  readonly endUtc: string | null;
  readonly latitude: number | null;
  readonly longitude: number | null;
  readonly priceTier: string | null;
}

/**
 * Backend DTO: WeUP.Contracts.Ingestion.AdapterCapabilityDescriptor (subset).
 */
export interface AdapterCapabilityDescriptor {
  readonly adapterKey: string;
  readonly displayName: string;
  readonly version: string;
  readonly supportedSourceKinds: IngestionSourceKind[];
  readonly isRetrySafe: boolean;
  readonly requiresNetworkAccess: boolean;
}

/**
 * Backend DTO: WeUP.Contracts.Ingestion.IngestionResult.
 * Response shape of GET /api/ingestion/jobs/{id}.
 */
export interface IngestionJobRecord {
  readonly jobId: string;
  readonly request: IngestionRequestEnvelope;
  readonly status: IngestionJobStatus;
  readonly candidate: CanonicalEventCandidate | null;
  readonly evidence: CanonicalSourceEvidence[];
  readonly issues: CanonicalIngestionIssue[];
  readonly adapter: AdapterCapabilityDescriptor | null;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string;
  /** Non-authoritative per-dimension vector when the job carries one. */
  readonly confidenceVector?: ConfidenceVector | null;
}

/**
 * Backend DTO: WeUP.Contracts.Ocr.FlyerOcrExtractionResult (subset).
 * G2 arbitration: ADOPT_WITH_CAVEAT — the flyer OCR service is an explicit
 * backend stub (hardcoded 'Houston Nights' output). The contract is adopted
 * for shape only; the provenance surface MUST label OCR output as stubbed
 * until a real extractor lands.
 */
export interface FlyerOcrResult {
  readonly extractionId: string;
  readonly jobId: string;
  readonly engine: string;
  readonly engineVersion: string;
  readonly confidence: number;
  readonly success: boolean;
  readonly rawText: string;
}

/**
 * Normalization outcome attached to a flyer job.
 * G2 arbitration: ADOPT_WITH_CAVEAT (same stub caveat as FlyerOcrResult).
 */
export interface FlyerNormalizationResult {
  readonly candidate: CanonicalEventCandidate | null;
  readonly confidenceVector: ConfidenceVector;
  readonly issues: CanonicalIngestionIssue[];
  readonly modelName: string;
  readonly modelVersion: string;
  readonly stubbed: boolean;
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
