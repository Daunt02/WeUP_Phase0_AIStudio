import { 
  IEventSourceAdapter 
} from '../interfaces';
import { 
  IngestionRequestEnvelope, 
  IngestionResult, 
  AdapterCapabilityDescriptor
} from '@/types/ingestion';
import { flyerIngestionPipeline } from '../services/FlyerIngestionPipeline';

/**
 * Adapter for Flyer OCR ingestion.
 * Uses the FlyerIngestionPipeline to process uploaded flyer assets.
 */
export class FlyerOcrAdapter implements IEventSourceAdapter {
  getCapabilities(): AdapterCapabilityDescriptor {
    return {
      adapterId: 'flyer-ocr-adapter',
      supportedKinds: ['FLYER_OCR'],
      version: '1.0.0'
    };
  }

  async validate(envelope: IngestionRequestEnvelope): Promise<boolean> {
    const { payload } = envelope;
    // Expecting an assetId or a temporary file reference
    return !!(payload && (payload.assetId || payload.tempPath));
  }

  async ingest(envelope: IngestionRequestEnvelope): Promise<IngestionResult> {
    const { payload } = envelope;
    const assetId = payload.assetId || payload.tempPath;
    
    return await flyerIngestionPipeline.process(assetId);
  }
}
