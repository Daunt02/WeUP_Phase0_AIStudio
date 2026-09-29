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

    if (!job.result || !job.result.evidence) {
      return NextResponse.json({ error: 'Evidence not available for this job' }, { status: 404 });
    }

    // Return the evidence part of the result
    return NextResponse.json({
      jobId: id,
      sourceKind: job.sourceKind,
      evidence: job.result.evidence,
      issues: job.result.issues
    });
  } catch (error: any) {
    return NextResponse.json(
      { error: error.message || 'Failed to fetch job evidence' },
      { status: 500 }
    );
  }
}
