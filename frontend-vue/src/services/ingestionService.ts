/**
 * WEUP-SYNTH:
 * source=components/ReceiptDrawer.tsx
 * destination=frontend-vue/src/services/ingestionService.ts
 * mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
 * notes=One function per ingestion route per the G3 chain. G8 ships the job
 *   lookup used by the provenance surface; G11 (AddEventWizard) owns the
 *   remaining routes. Real backend route: GET /api/ingestion/jobs/{id}
 *   (WeUP.Api Endpoints/IngestionEndpoints.cs). No simulated polling, no
 *   fabricated job payloads.
 */
import type { IngestionJobRecord } from "../contracts/ingestion.contracts";

const API_BASE_URL =
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? "";

export class IngestionApiRequestError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "IngestionApiRequestError";
    this.status = status;
  }
}

/**
 * Fetch a single ingestion job record (evidence + issues + lifecycle).
 * Throws IngestionApiRequestError(404) when the backend has no job with the
 * given id — callers must render that as an unavailable/pending stage, never
 * as success.
 */
export async function getIngestionJob(
  jobId: string,
  init?: RequestInit,
): Promise<IngestionJobRecord> {
  const response = await fetch(
    `${API_BASE_URL}/api/ingestion/jobs/${encodeURIComponent(jobId)}`,
    {
      method: "GET",
      headers: {
        Accept: "application/json",
        ...(init?.headers ?? {}),
      },
      ...init,
    },
  );

  if (!response.ok) {
    const detail = await response.text().catch(() => "");
    throw new IngestionApiRequestError(
      `Ingestion job request failed (${response.status}). ${detail}`.trim(),
      response.status,
    );
  }

  return (await response.json()) as IngestionJobRecord;
}
