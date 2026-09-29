import { 
  IEventSourceAdapter 
} from '../interfaces';
import { 
  IngestionRequestEnvelope, 
  IngestionResult, 
  AdapterCapabilityDescriptor,
  CanonicalEventCandidate
} from '@/types/ingestion';

export class ManualAdapter implements IEventSourceAdapter {
  getCapabilities(): AdapterCapabilityDescriptor {
    return {
      adapterId: 'manual-submission-adapter',
      supportedKinds: ['MANUAL'],
      version: '1.0.0'
    };
  }

  async validate(envelope: IngestionRequestEnvelope): Promise<boolean> {
    const { payload } = envelope;
    return !!(payload && payload.title && payload.venueName);
  }

  async ingest(envelope: IngestionRequestEnvelope): Promise<IngestionResult> {
    const { payload } = envelope;
    
    const candidate: CanonicalEventCandidate = {
      title: payload.title,
      description: payload.description,
      venueName: payload.venueName,
      address: payload.address,
      startTime: payload.startTime,
      endTime: payload.endTime,
      category: payload.category,
      priceTier: payload.priceTier,
      imageUrl: payload.imageUrl,
      latitude: payload.latitude,
      longitude: payload.longitude,
      tags: payload.tags
    };

    return {
      jobId: '', // Will be set by coordinator
      status: 'CANDIDATE_CREATED',
      candidate,
      evidence: {
        rawPayload: payload,
        capturedAt: new Date().toISOString(),
        extractedBy: this.getCapabilities().adapterId,
        confidenceScore: 1.0 // Manual is high confidence by definition
      },
      issues: [],
      executedAt: new Date().toISOString()
    };
  }
}
