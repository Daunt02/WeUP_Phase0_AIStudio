import { FlyerOcrResult } from '@/types/ingestion';
import { IFlyerOcrService } from '../interfaces';

/**
 * Stub implementation of the Flyer OCR Service.
 * TODO: Replace with Google Cloud Vision or AWS Textract in Phase 1.
 */
export class StubFlyerOcrService implements IFlyerOcrService {
  async extractText(assetId: string): Promise<FlyerOcrResult> {
    console.log(`[StubFlyerOcrService] Extracting text from asset: ${assetId}`);
    
    // Simulate processing delay
    await new Promise(resolve => setTimeout(resolve, 800));

    return {
      rawText: `
        WEUP PRESENTS: HOUSTON NIGHTS
        DATE: APRIL 20, 2026
        TIME: 9PM - 2AM
        LOCATION: THE WAREHOUSE, 123 MAIN ST
        FEATURING: DJ SCREW & FRIENDS
        $20 ENTRY | 21+
      `,
      confidence: 0.92,
      ocrEngine: 'StubOCR',
      ocrVersion: '1.0.0'
    };
  }
}

export const flyerOcrService = new StubFlyerOcrService();
