import { NextResponse } from 'next/server';
import { ingestionCoordinator } from '@/lib/ingestion/coordinator';

export const dynamic = 'force-dynamic';

export async function GET(
  request: Request,
  { params }: { params: Promise<{ id: string }> }
) {
  try {
    const { id } = await params;
    const job = await ingestionCoordinator.getJobStatus(id);
    
    if (!job) {
      return NextResponse.json({ error: 'Job not found' }, { status: 404 });
    }
    
    return NextResponse.json(job);
  } catch (error: any) {
    return NextResponse.json(
      { error: error.message || 'Failed to fetch job status' },
      { status: 500 }
    );
  }
}
