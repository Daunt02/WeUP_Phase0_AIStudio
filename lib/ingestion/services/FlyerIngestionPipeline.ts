import { 
  IngestionResult, 
  IngestionJobStatus 
} from '@/types/ingestion';
import { IFlyerIngestionPipeline } from '../interfaces';
import { flyerOcrService } from './FlyerOcrService';
import { flyerNormalizationService } from './FlyerNormalizationService';

/**
 * Orchestrates the flyer ingestion pipeline:
 * Asset -> OCR -> Normalization -> Candidate
 */
export class FlyerIngestionPipeline implements IFlyerIngestionPipeline {
  async process(assetId: string): Promise<IngestionResult> {
    const executedAt = new Date().toISOString();
    
    try {
      // 1. OCR Stage
      const ocrResult = await flyerOcrService.extractText(assetId);
      
      // 2. Normalization Stage
      const normResult = await flyerNormalizationService.normalize(ocrResult.rawText);
      
      // 3. Determine Status
      // If confidence is low or there are critical issues, require review.
      const status: IngestionJobStatus = (normResult.confidenceVector.overall < 0.8 || normResult.issues.length > 0)
        ? 'REQUIRES_REVIEW'
        : 'CANDIDATE_CREATED';

      return {
        jobId: '', // Set by coordinator
        status,
        candidate: normResult.candidate,
        evidence: {
          assetId,
          capturedAt: executedAt,
          extractedBy: 'flyer-ingestion-pipeline',
          confidenceScore: normResult.confidenceVector.overall,
          confidenceVector: normResult.confidenceVector,
          ocrText: ocrResult.rawText,
          processingMetadata: {
            ocrEngine: ocrResult.ocrEngine,
            ocrVersion: ocrResult.ocrVersion,
            modelName: normResult.modelName,
            modelVersion: normResult.modelVersion
          }
        },
        issues: normResult.issues,
        executedAt: new Date().toISOString()
      };
    } catch (error: any) {
      return {
        jobId: '',
        status: 'FAILED',
        evidence: {
          assetId,
          capturedAt: executedAt,
          extractedBy: 'flyer-ingestion-pipeline',
          confidenceScore: 0
        },
        issues: [{
          code: 'PIPELINE_ERROR',
          message: error.message || 'Unknown error during flyer pipeline execution',
          severity: 'ERROR'
        }],
        executedAt: new Date().toISOString()
      };
    }
  }
}

export const flyerIngestionPipeline = new FlyerIngestionPipeline();
