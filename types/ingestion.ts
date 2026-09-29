import { NightlifeItem } from './index';

/**
 * The kind of source being ingested.
 */
export type IngestionSourceKind = 
  | 'MANUAL' 
  | 'URL' 
  | 'VENUE_PAGE' 
  | 'EXTERNAL_FEED' 
  | 'FLYER_OCR';

/**
 * The lifecycle status of an ingestion job.
 */
export type IngestionJobStatus = 
  | 'RECEIVED'
  | 'VALIDATING'
  | 'NORMALIZING'
  | 'CANDIDATE_CREATED'
  | 'REQUIRES_REVIEW'
  | 'FAILED'
  | 'RETRYABLE_FAILURE';

/**
 * A warning or error encountered during ingestion.
 */
export interface CanonicalIngestionIssue {
  code: string;
  message: string;
  severity: 'WARNING' | 'ERROR';
  field?: string;
}

/**
 * Evidence or provenance metadata for the ingestion.
 */
export interface CanonicalSourceEvidence {
  sourceUrl?: string;
  assetId?: string; // Reference to uploaded flyer/image
  rawPayload?: any;
  capturedAt: string;
  extractedBy: string; // Adapter ID or Name
  confidenceScore: number;
  confidenceVector?: ConfidenceVector; // Detailed confidence breakdown
  ocrText?: string; // Raw OCR output if applicable
  processingMetadata?: Record<string, any>; // OCR/LLM run IDs, versions, etc.
}

/**
 * Detailed confidence breakdown.
 */
export interface ConfidenceVector {
  extraction: number; // OCR quality
  temporal: number;   // Date/Time certainty
  venueMatch: number; // Venue identification certainty
  geocode: number;    // Location certainty
  overall: number;
}

/**
 * Result of an OCR extraction stage.
 */
export interface FlyerOcrResult {
  rawText: string;
  blocks?: any[];
  confidence: number;
  ocrEngine: string;
  ocrVersion: string;
}

/**
 * Result of an LLM normalization stage.
 */
export interface FlyerNormalizationResult {
  candidate: CanonicalEventCandidate;
  confidenceVector: ConfidenceVector;
  issues: CanonicalIngestionIssue[];
  modelName: string;
  modelVersion: string;
}

/**
 * A pre-domain event candidate. 
 * This is NOT yet a persisted NightlifeItem.
 */
export interface CanonicalEventCandidate {
  title?: string;
  description?: string;
  venueName?: string;
  address?: string;
  startTime?: string;
  endTime?: string;
  category?: string;
  priceTier?: string;
  imageUrl?: string;
  latitude?: number;
  longitude?: number;
  tags?: string[];
}

/**
 * The envelope for an ingestion request.
 */
export interface IngestionRequestEnvelope {
  sourceKind: IngestionSourceKind;
  payload: any;
  metadata?: Record<string, any>;
}

/**
 * The result of an ingestion execution.
 */
export interface IngestionResult {
  jobId: string;
  status: IngestionJobStatus;
  candidate?: CanonicalEventCandidate;
  evidence: CanonicalSourceEvidence;
  issues: CanonicalIngestionIssue[];
  executedAt: string;
}

/**
 * Descriptor for what an adapter can handle.
 */
export interface AdapterCapabilityDescriptor {
  adapterId: string;
  supportedKinds: IngestionSourceKind[];
  version: string;
}

/**
 * The persisted record of an ingestion job.
 */
export interface IngestionJobRecord {
  id: string;
  sourceKind: IngestionSourceKind;
  status: IngestionJobStatus;
  requestPayload: any;
  result?: IngestionResult;
  createdAt: string;
  updatedAt: string;
  errorContext?: string;
}
