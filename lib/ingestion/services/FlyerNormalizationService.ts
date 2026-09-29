import { FlyerNormalizationResult, CanonicalIngestionIssue } from '@/types/ingestion';
import { IFlyerNormalizationService } from '../interfaces';

/**
 * Stub implementation of the Flyer Normalization Service.
 * TODO: Replace with Gemini Pro / LLM in Phase 1.
 */
export class StubFlyerNormalizationService implements IFlyerNormalizationService {
  async normalize(ocrText: string): Promise<FlyerNormalizationResult> {
    console.log(`[StubFlyerNormalizationService] Normalizing text: ${ocrText.substring(0, 50)}...`);
    
    // Simulate LLM processing delay
    await new Promise(resolve => setTimeout(resolve, 1200));

    const issues: CanonicalIngestionIssue[] = [];
    
    // Simulate some ambiguity
    if (!ocrText.includes('ADDRESS')) {
      issues.push({
        code: 'AMBIGUOUS_ADDRESS',
        message: 'Address was not explicitly labeled. Inferred from context.',
        severity: 'WARNING',
        field: 'address'
      });
    }

    return {
      candidate: {
        title: 'Houston Nights',
        description: 'WEUP Presents: Houston Nights featuring DJ Screw & Friends',
        venueName: 'The Warehouse',
        address: '123 Main St',
        startTime: '2026-04-20T21:00:00Z',
        endTime: '2026-04-21T02:00:00Z',
        category: 'Nightlife',
        priceTier: '$$',
        tags: ['DJ Screw', 'Houston', 'Nightlife']
      },
      confidenceVector: {
        extraction: 0.92,
        temporal: 0.85,
        venueMatch: 0.90,
        geocode: 0.80,
        overall: 0.87
      },
      issues,
      modelName: 'StubLLM',
      modelVersion: '1.0.0'
    };
  }
}

export const flyerNormalizationService = new StubFlyerNormalizationService();
