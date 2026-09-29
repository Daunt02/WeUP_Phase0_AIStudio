import { NextResponse } from 'next/server';
import { ingestionCoordinator } from '@/lib/ingestion/coordinator';
import { IngestionRequestEnvelope } from '@/types/ingestion';

export const dynamic = 'force-dynamic';

/**
 * Endpoint for submitting flyer ingestion requests.
 * In a real app, this might handle multipart/form-data for file uploads,
 * but for this prototype, we accept an assetId or tempPath.
 */
export async function POST(request: Request) {
  try {
    const payload = await request.json();
    
    // Ensure we have an identifier for the flyer asset
    if (!payload.assetId && !payload.tempPath) {
      return NextResponse.json(
        { error: 'Missing flyer asset identifier (assetId or tempPath)' },
        { status: 400 }
      );
    }

    const envelope: IngestionRequestEnvelope = {
      sourceKind: 'FLYER_OCR',
      payload
    };

    const job = await ingestionCoordinator.submit(envelope);
    
    return NextResponse.json(job);
  } catch (error: any) {
    return NextResponse.json(
      { error: error.message || 'Failed to submit flyer ingestion' },
      { status: 500 }
    );
  }
}
