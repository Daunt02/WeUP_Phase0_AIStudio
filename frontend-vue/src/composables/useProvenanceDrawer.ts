/**
 * WEUP-SYNTH:
 * source=components/ReceiptDrawer.tsx
 * destination=frontend-vue/src/composables/useProvenanceDrawer.ts
 * mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
 * notes=Provenance projection for the 8-stage chain (mission section 22).
 *   projectProvenanceStages is pure and unit-tested; stubbed backend stages
 *   (URL/venue extraction, OCR, normalization) are labeled PENDING explicitly
 *   and never rendered as success. Fabricated purchase keys and pseudo-QR
 *   codes are excluded by design.
 */
import { computed, nextTick, ref, watch } from "vue";
import type { EventDetailDto } from "../contracts/event-detail.contracts";
import type {
  IngestionJobRecord,
  IngestionJobStatus,
  ProvenanceStageDto,
  ProvenanceStageStatus,
} from "../contracts/ingestion.contracts";
import {
  getIngestionJob,
  IngestionApiRequestError,
} from "../services/ingestionService";

function formatConfidence(value: number): string {
  return `${Math.round(value * 100)}%`;
}

function stageDetail(
  label: string,
  value: string,
  mono = false,
): { label: string; value: string; mono: boolean } {
  return { label, value, mono };
}

/**
 * Project the 8-stage provenance chain from canonical event data and an
 * optional ingestion job record. Pure: no network, no side effects.
 */
export function projectProvenanceStages(
  event: EventDetailDto,
  job: IngestionJobRecord | null,
): ProvenanceStageDto[] {
  const provenance = event.provenanceSummary;

  // G11 realignment: the served job payload carries the result (candidate /
  // evidence / issues) under `result`; the record itself holds id, sourceKind,
  // status, and the request payload. All derivations below read that shape.
  const result = job?.result ?? null;
  const evidence = result?.evidence ?? null;
  const issues = result?.issues ?? [];
  const vector = evidence?.confidenceVector ?? null;

  const jobStatus: ProvenanceStageStatus = job ? "complete" : "unavailable";
  const extractionComplete =
    !!evidence &&
    !!(evidence.ocrText || evidence.sourceUrl || evidence.assetId);
  const normalizationComplete =
    !!result &&
    (result.status === "CANDIDATE_CREATED" ||
      result.status === "REQUIRES_REVIEW" ||
      vector != null);

  const failedStatuses = new Set<IngestionJobStatus>([
    "FAILED",
    "RETRYABLE_FAILURE",
  ]);

  return [
    {
      stage: "SOURCE",
      title: "Source",
      status: "complete",
      summary: `${provenance.primarySourceKind} · ${provenance.sourceCount} source${provenance.sourceCount === 1 ? "" : "s"}`,
      details: [
        stageDetail("Primary source kind", provenance.primarySourceKind, true),
        stageDetail(
          "Source count",
          String(provenance.sourceCount),
          true,
        ),
        stageDetail(
          "First observed",
          provenance.firstObservedAtUtc,
          true,
        ),
        stageDetail(
          "Last observed",
          provenance.lastObservedAtUtc,
          true,
        ),
      ],
    },
    {
      stage: "INGESTION_JOB",
      title: "Ingestion job",
      status: job ? (failedStatuses.has(job.status) ? "failed" : jobStatus) : jobStatus,
      summary: job
        ? `${job.id} · ${job.status}`
        : "No ingestion job is linked to this event in the detail contract — stage unavailable, not pending",
      details: job
        ? [
            stageDetail("Job id", job.id, true),
            stageDetail("Status", job.status, true),
            stageDetail("Source kind", job.sourceKind, true),
            stageDetail("Created", job.createdAt, true),
            stageDetail("Updated", job.updatedAt, true),
            ...(job.errorContext
              ? [stageDetail("Error context", job.errorContext)]
              : []),
          ]
        : [
            stageDetail(
              "Reason",
              "The event detail contract carries no ingestion-job link for this event.",
            ),
          ],
    },
    {
      stage: "EXTRACTION",
      title: "Extraction",
      status: extractionComplete ? "complete" : "pending",
      summary: extractionComplete
        ? `Extracted by ${evidence!.extractedBy} · captured ${evidence!.capturedAt}`
        : "No extraction payload is attached to the job record yet — PENDING",
      details: evidence
        ? [
            stageDetail("Extracted by", evidence.extractedBy, true),
            stageDetail("Captured at", evidence.capturedAt, true),
            ...(evidence.sourceUrl
              ? [stageDetail("Source URL", evidence.sourceUrl, true)]
              : []),
            ...(evidence.assetId
              ? [stageDetail("Asset id", evidence.assetId, true)]
              : []),
            stageDetail(
              "Confidence score",
              formatConfidence(evidence.confidenceScore),
              true,
            ),
          ]
        : [
            stageDetail(
              "Reason",
              "Extraction evidence becomes visible when the job record carries a result.",
            ),
          ],
    },
    {
      stage: "NORMALIZATION",
      title: "Normalization",
      status: normalizationComplete ? "complete" : "pending",
      summary: normalizationComplete
        ? `Candidate ${result!.status === "REQUIRES_REVIEW" ? "requires review" : "created"} · vector present`
        : "No normalization outcome is attached to the job record yet — PENDING",
      details: vector
        ? (Object.entries(vector) as Array<[string, number]>).map(
            ([dimension, value]) =>
              stageDetail(dimension, formatConfidence(value), true),
          )
        : [
            stageDetail(
              "Reason",
              "No normalization vector is exposed for this event yet.",
            ),
          ],
    },
    {
      stage: "CONFIDENCE",
      title: "Confidence",
      status: "complete",
      summary: `Canonical event confidence ${formatConfidence(event.confidence)}`,
      details: [
        stageDetail(
          "Canonical confidence",
          formatConfidence(event.confidence),
          true,
        ),
        stageDetail(
          "Per-dimension vector",
          vector ? "provided by job" : "not measured — PENDING",
          true,
        ),
      ],
    },
    {
      stage: "ISSUES",
      title: "Issues",
      status: job ? "complete" : "unavailable",
      summary: job
        ? issues.length === 0
          ? "No issues recorded on the job"
          : `${issues.length} issue${issues.length === 1 ? "" : "s"} recorded`
        : "Issue ledger requires a linked ingestion job — unavailable",
      details: job
        ? issues.map((issue) =>
            stageDetail(
              `${issue.code} · ${issue.severity}`,
              issue.message + (issue.field ? ` (field: ${issue.field})` : ""),
              true,
            ),
          )
        : [
            stageDetail(
              "Reason",
              "The canonical event DTO carries no issue ledger of its own.",
            ),
          ],
    },
    {
      stage: "CANONICAL_EVENT",
      title: "Canonical event",
      status: "complete",
      summary: `event ${event.id} · v${event.version}`,
      details: [
        stageDetail("Event id", event.id, true),
        stageDetail("Version", String(event.version), true),
        stageDetail(
          "Last change",
          event.lastChangeType ?? "no recorded change",
          true,
        ),
        stageDetail("Timezone", event.timezone, true),
        stageDetail("Status", event.status, true),
      ],
    },
    {
      stage: "UI_PROJECTION",
      title: "UI projection",
      status: "complete",
      summary:
        "Projected from canonical EventDetailDto in the event surface; display-only formatting",
      details: [
        stageDetail(
          "Identity source",
          "EventDetailDto.id — never overridden by enrichment",
          true,
        ),
        stageDetail(
          "Coordinates source",
          `EventDetailDto.lat/lng (${event.lat}, ${event.lng})`,
          true,
        ),
        stageDetail(
          "Enrichment policy",
          "AI enrichment may enrich presentation only; identity, saved state, coordinates, and provenance are never overridden",
        ),
      ],
    },
  ];
}

/**
 * Drawer state for the provenance surface. Loads the ingestion job only when
 * an explicit job id is supplied; the current event detail contract exposes
 * no job link, so the INGESTION_JOB stage degrades honestly to "unavailable"
 * instead of issuing a lookup that cannot succeed.
 */
export function useProvenanceDrawer() {
  const isOpen = ref(false);
  const activeEvent = ref<EventDetailDto | null>(null);
  const jobRecord = ref<IngestionJobRecord | null>(null);
  const isLoadingJob = ref(false);
  const jobError = ref<string | null>(null);
  const requestId = ref(0);

  const stages = computed<ProvenanceStageDto[]>(() => {
    if (!activeEvent.value) {
      return [];
    }
    return projectProvenanceStages(activeEvent.value, jobRecord.value);
  });

  watch(
    () => activeEvent.value?.id,
    () => {
      jobRecord.value = null;
      jobError.value = null;
      isLoadingJob.value = false;
      requestId.value += 1;
    },
  );

  function openDrawer(
    event: EventDetailDto,
    options?: { jobId?: string },
  ): void {
    activeEvent.value = event;
    isOpen.value = true;
    jobRecord.value = null;
    jobError.value = null;
    isLoadingJob.value = !!options?.jobId;

    if (!options?.jobId) {
      return;
    }

    // Defer the lookup past the activeEvent watcher flush so this load is
    // not invalidated by the watcher's own request-id bump.
    const jobId = options.jobId;
    void nextTick().then(() => {
      if (isOpen.value && activeEvent.value?.id === event.id) {
        void loadJob(jobId);
      }
    });
  }

  async function loadJob(jobId: string): Promise<void> {
    requestId.value += 1;
    const currentRequest = requestId.value;
    isLoadingJob.value = true;
    jobError.value = null;

    try {
      const record = await getIngestionJob(jobId);
      if (currentRequest !== requestId.value) {
        return;
      }
      jobRecord.value = record;
    } catch (cause) {
      if (currentRequest !== requestId.value) {
        return;
      }
      if (
        cause instanceof IngestionApiRequestError &&
        cause.status === 404
      ) {
        jobRecord.value = null;
      } else {
        jobError.value =
          cause instanceof Error
            ? cause.message
            : "Failed to load the ingestion job record.";
      }
    } finally {
      if (currentRequest === requestId.value) {
        isLoadingJob.value = false;
      }
    }
  }

  function closeDrawer(): void {
    requestId.value += 1;
    isOpen.value = false;
    isLoadingJob.value = false;
  }

  return {
    isOpen,
    activeEvent,
    jobRecord,
    isLoadingJob,
    jobError,
    stages,
    openDrawer,
    closeDrawer,
    loadJob,
  };
}
