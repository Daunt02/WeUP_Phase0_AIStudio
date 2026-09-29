import { 
  IngestionRequestEnvelope, 
  IngestionJobRecord 
} from '@/types/ingestion';
import { IIngestionCoordinator } from './interfaces';
import { ingestionJobRepository } from './repository';
import { adapterResolver } from './resolver';

export class IngestionCoordinator implements IIngestionCoordinator {
  async submit(envelope: IngestionRequestEnvelope): Promise<IngestionJobRecord> {
    // 1. Create Job Record (RECEIVED)
    const job = await ingestionJobRepository.create({
      sourceKind: envelope.sourceKind,
      status: 'RECEIVED',
      requestPayload: envelope.payload
    });

    // 2. Resolve Adapter
    const adapter = adapterResolver.resolve(envelope.sourceKind);

    // 3. Validate (VALIDATING)
    await ingestionJobRepository.update(job.id, { status: 'VALIDATING' });
    const isValid = await adapter.validate(envelope);
    
    if (!isValid) {
      return await ingestionJobRepository.update(job.id, { 
        status: 'FAILED',
        errorContext: 'Validation failed for inbound payload.'
      });
    }

    // 4. Ingest/Normalize (NORMALIZING)
    await ingestionJobRepository.update(job.id, { status: 'NORMALIZING' });
    
    try {
      const result = await adapter.ingest(envelope);
      result.jobId = job.id;

      // 5. Update with Result
      return await ingestionJobRepository.update(job.id, {
        status: result.status,
        result: result
      });
    } catch (error: any) {
      return await ingestionJobRepository.update(job.id, {
        status: 'FAILED',
        errorContext: error.message || 'Unknown error during ingestion execution.'
      });
    }
  }

  async getJobStatus(jobId: string): Promise<IngestionJobRecord | null> {
    return await ingestionJobRepository.getById(jobId);
  }
}

export const ingestionCoordinator = new IngestionCoordinator();
