import { 
  IngestionRequestEnvelope, 
  IngestionResult, 
  IngestionSourceKind, 
  AdapterCapabilityDescriptor,
  IngestionJobRecord,
  FlyerOcrResult,
  FlyerNormalizationResult
} from '@/types/ingestion';

/**
 * Interface for a specific source adapter.
 */
export interface IEventSourceAdapter {
  getCapabilities(): AdapterCapabilityDescriptor;
  validate(envelope: IngestionRequestEnvelope): Promise<boolean>;
  ingest(envelope: IngestionRequestEnvelope): Promise<IngestionResult>;
}

/**
 * Interface for OCR extraction from flyer assets.
 */
export interface IFlyerOcrService {
  extractText(assetId: string): Promise<FlyerOcrResult>;
}

/**
 * Interface for normalizing OCR text into structured event candidates.
 */
export interface IFlyerNormalizationService {
  normalize(ocrText: string): Promise<FlyerNormalizationResult>;
}

/**
 * Interface for the flyer ingestion pipeline orchestrator.
 */
export interface IFlyerIngestionPipeline {
  process(assetId: string): Promise<IngestionResult>;
}

/**
 * Interface for resolving the correct adapter for a request.
 */
export interface IEventSourceAdapterResolver {
  resolve(kind: IngestionSourceKind): IEventSourceAdapter;
}

/**
 * Interface for the ingestion coordinator that manages the lifecycle.
 */
export interface IIngestionCoordinator {
  submit(envelope: IngestionRequestEnvelope): Promise<IngestionJobRecord>;
  getJobStatus(jobId: string): Promise<IngestionJobRecord | null>;
}

/**
 * Interface for persisting ingestion job records.
 */
export interface IIngestionJobRepository {
  create(record: Partial<IngestionJobRecord>): Promise<IngestionJobRecord>;
  update(id: string, updates: Partial<IngestionJobRecord>): Promise<IngestionJobRecord>;
  getById(id: string): Promise<IngestionJobRecord | null>;
}
