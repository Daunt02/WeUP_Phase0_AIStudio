import { IngestionJobRecord } from '@/types/ingestion';
import { IIngestionJobRepository } from './interfaces';

/**
 * In-memory implementation of the IngestionJobRepository.
 * TODO: Promote to EF/Postgres persistence in Phase 1.
 */
export class InMemoryIngestionJobRepository implements IIngestionJobRepository {
  private jobs: Map<string, IngestionJobRecord> = new Map();

  async create(record: Partial<IngestionJobRecord>): Promise<IngestionJobRecord> {
    const id = record.id || Math.random().toString(36).substring(7);
    const now = new Date().toISOString();
    
    const newRecord: IngestionJobRecord = {
      id,
      sourceKind: record.sourceKind!,
      status: record.status || 'RECEIVED',
      requestPayload: record.requestPayload,
      createdAt: now,
      updatedAt: now,
      ...record
    };

    this.jobs.set(id, newRecord);
    return newRecord;
  }

  async update(id: string, updates: Partial<IngestionJobRecord>): Promise<IngestionJobRecord> {
    const existing = this.jobs.get(id);
    if (!existing) {
      throw new Error(`Job ${id} not found`);
    }

    const updated: IngestionJobRecord = {
      ...existing,
      ...updates,
      updatedAt: new Date().toISOString()
    };

    this.jobs.set(id, updated);
    return updated;
  }

  async getById(id: string): Promise<IngestionJobRecord | null> {
    return this.jobs.get(id) || null;
  }
}

// Singleton for the prototype
export const ingestionJobRepository = new InMemoryIngestionJobRepository();
