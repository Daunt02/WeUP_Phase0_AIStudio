/**
 * WEUP-SYNTH:
 * sources=[app/api/ingestion/manual/route.ts, app/api/ingestion/url/route.ts,
 *   app/api/ingestion/venue-page/route.ts, app/api/ingestion/flyers/route.ts,
 *   app/api/ingestion/jobs/[id]/route.ts,
 *   app/api/ingestion/flyers/[id]/evidence/route.ts]
 * destination=frontend-vue/src/services/ingestionService.ts
 * mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
 * notes=One function per ingestion route per the G3 chain. The Next route
 *   handlers read the entire request JSON body as the adapter payload and
 *   add sourceKind server-side (the sourceKind is encoded in the route
 *   itself), so each function POSTs the raw source payload directly — never
 *   a { sourceKind, payload } wrapper, which the handlers would otherwise
 *   treat as the payload and fail validation on. Responses are the
 *   coordinator's IngestionJobRecord payloads verbatim (types/ingestion.ts) —
 *   no re-shaping, no simulated polling, no fabricated job payloads. Vue
 *   owns input/validation presentation/submission/progress/polling/error
 *   state/candidate review/evidence display; the backend owns ingestion
 *   execution, OCR, normalization, resolver, evidence, and persistence.
 */
import type {
  FlyerEvidenceDto,
  IngestionJobRecord,
} from "../contracts/ingestion.contracts";

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

async function readErrorDetail(response: Response): Promise<string> {
  try {
    const text = await response.text();
    if (!text) {
      return "";
    }
    try {
      const parsed = JSON.parse(text) as { error?: unknown };
      return typeof parsed.error === "string" ? parsed.error : text;
    } catch {
      return text;
    }
  } catch {
    return "";
  }
}

/**
 * POSTs the raw source payload to an ingestion route. Each Next handler
 * reads the whole request JSON as the adapter payload and adds sourceKind
 * server-side, so no envelope wrapper is sent.
 */
async function postIngestion<TResponse>(
  path: string,
  payload: unknown,
  init?: RequestInit,
): Promise<TResponse> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      ...(init?.headers ?? {}),
    },
    body: JSON.stringify(payload),
    ...init,
  });

  if (!response.ok) {
    const detail = await readErrorDetail(response);
    throw new IngestionApiRequestError(
      `Ingestion submission failed (${response.status}). ${detail}`.trim(),
      response.status,
    );
  }

  return (await response.json()) as TResponse;
}

/** Payload accepted by the manual-submission adapter (lib/ingestion/adapters/ManualAdapter). */
export interface ManualIngestPayload {
  readonly title: string;
  readonly venueName: string;
  readonly description?: string;
  readonly address?: string;
  readonly startTime?: string;
  readonly endTime?: string;
  readonly category?: string;
  readonly priceTier?: string;
  readonly imageUrl?: string;
  readonly latitude?: number;
  readonly longitude?: number;
  readonly tags?: string[];
}

/** Payload accepted by the URL adapter: payload.url must start with http(s). */
export interface UrlIngestPayload {
  readonly url: string;
}

/** Payload accepted by the venue-page adapter: venueId + pageUrl required. */
export interface VenuePageIngestPayload {
  readonly venueId: string;
  readonly pageUrl: string;
}

/**
 * Payload accepted by the flyer route. The route returns 400 unless assetId
 * or tempPath is present (there is no binary upload endpoint in this phase;
 * the backend stub processes the asset identifier).
 */
export interface FlyerIngestPayload {
  readonly assetId?: string;
  readonly tempPath?: string;
}

/**
 * POST /api/ingestion/manual — manual event submission. The adapter builds a
 * real candidate from the payload (status CANDIDATE_CREATED, confidence 1.0).
 */
export async function postManualIngest(
  payload: ManualIngestPayload,
  init?: RequestInit,
): Promise<IngestionJobRecord> {
  return postIngestion<IngestionJobRecord>(
    "/api/ingestion/manual",
    payload,
    init,
  );
}

/**
 * POST /api/ingestion/url — URL ingestion. Backend extraction is currently
 * stubbed (STUBBED_EXTRACTION issue on the job); the job record itself is
 * real and the stub is reported via its issue ledger.
 */
export async function postUrlIngest(
  payload: UrlIngestPayload,
  init?: RequestInit,
): Promise<IngestionJobRecord> {
  return postIngestion<IngestionJobRecord>(
    "/api/ingestion/url",
    payload,
    init,
  );
}

/**
 * POST /api/ingestion/venue-page — venue page ingestion. Backend crawling is
 * currently stubbed (STUBBED_VENUE_PAGE issue on the job).
 */
export async function postVenuePageIngest(
  payload: VenuePageIngestPayload,
  init?: RequestInit,
): Promise<IngestionJobRecord> {
  return postIngestion<IngestionJobRecord>(
    "/api/ingestion/venue-page",
    payload,
    init,
  );
}

/**
 * POST /api/ingestion/flyers — flyer ingestion. Backend OCR and
 * normalization are explicit stubs (StubFlyerOcrService /
 * StubFlyerNormalizationService); the job's evidence carries the stub's
 * engine metadata honestly.
 */
export async function postFlyerIngest(
  payload: FlyerIngestPayload,
  init?: RequestInit,
): Promise<IngestionJobRecord> {
  return postIngestion<IngestionJobRecord>(
    "/api/ingestion/flyers",
    payload,
    init,
  );
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
    const detail = await readErrorDetail(response);
    throw new IngestionApiRequestError(
      `Ingestion job request failed (${response.status}). ${detail}`.trim(),
      response.status,
    );
  }

  return (await response.json()) as IngestionJobRecord;
}

/**
 * GET /api/ingestion/flyers/{id}/evidence — evidence slice of a flyer job.
 * 404 when the job is unknown or carries no result evidence yet.
 */
export async function getFlyerEvidence(
  jobId: string,
  init?: RequestInit,
): Promise<FlyerEvidenceDto> {
  const response = await fetch(
    `${API_BASE_URL}/api/ingestion/flyers/${encodeURIComponent(jobId)}/evidence`,
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
    const detail = await readErrorDetail(response);
    throw new IngestionApiRequestError(
      `Flyer evidence request failed (${response.status}). ${detail}`.trim(),
      response.status,
    );
  }

  return (await response.json()) as FlyerEvidenceDto;
}
