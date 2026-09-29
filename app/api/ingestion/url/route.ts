import { NextResponse } from 'next/server';
import { ingestionCoordinator } from '@/lib/ingestion/coordinator';
import { IngestionRequestEnvelope } from '@/types/ingestion';

export const dynamic = 'force-dynamic';

export async function POST(request: Request) {
  try {
    const payload = await request.json();
    
    const envelope: IngestionRequestEnvelope = {
      sourceKind: 'URL',
      payload
    };

    const job = await ingestionCoordinator.submit(envelope);
    
    return NextResponse.json(job);
  } catch (error: any) {
    return NextResponse.json(
      { error: error.message || 'Failed to submit URL ingestion' },
      { status: 500 }
    );
  }
}
