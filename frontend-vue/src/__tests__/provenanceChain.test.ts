/**
 * WEUP-SYNTH G8 tests: provenance chain projection and drawer state.
 * Mission: WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
 */
import { nextTick } from "vue";
import { flushPromises } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { EventDetailDto } from "../contracts/event-detail.contracts";
import type { IngestionJobRecord } from "../contracts/ingestion.contracts";
import {
  projectProvenanceStages,
  useProvenanceDrawer,
} from "../composables/useProvenanceDrawer";
import { IngestionApiRequestError } from "../services/ingestionService";

const { getIngestionJob } = vi.hoisted(() => ({
  getIngestionJob: vi.fn(),
}));

vi.mock("../services/ingestionService", async () => {
  const actual =
    await vi.importActual<typeof import("../services/ingestionService")>(
      "../services/ingestionService",
    );
  return {
    ...actual,
    getIngestionJob,
  };
});

function buildEvent(overrides: Partial<EventDetailDto> = {}): EventDetailDto {
  return {
    id: "evt-001",
    title: "Test Event",
    description: "A test event",
    venueName: "Test Venue",
    address: "123 Main St",
    lat: 29.76,
    lng: -95.37,
    category: "nightlife",
    categories: ["nightlife"],
    startUtc: "2026-09-30T02:00:00Z",
    endUtc: null,
    timezone: "America/Chicago",
    flyerImageUrl: null,
    mediaRefs: [],
    tags: ["test"],
    status: "PUBLISHED",
    confidence: 0.85,
    sourceKind: "manual",
    provenanceSummary: {
      primarySourceKind: "manual",
      sourceCount: 1,
      firstObservedAtUtc: "2026-09-29T10:00:00Z",
      lastObservedAtUtc: "2026-09-29T12:00:00Z",
      summaryLabel: "Manually submitted",
    },
    savedByCurrentUser: false,
    version: 3,
    lastChangeType: "title-updated",
    concurrencyToken: null,
    ...overrides,
  };
}

function buildJob(overrides: Partial<IngestionJobRecord> = {}): IngestionJobRecord {
  return {
    id: "job-123",
    sourceKind: "FLYER_OCR",
    status: "REQUIRES_REVIEW",
    requestPayload: { assetId: "flyer.png" },
    result: {
      jobId: "job-123",
      status: "REQUIRES_REVIEW",
      candidate: {
        title: "Houston Nights",
        venueName: "The Warehouse",
      },
      evidence: {
        assetId: "flyer.png",
        capturedAt: "2026-09-29T10:01:00Z",
        extractedBy: "flyer-ingestion-pipeline",
        confidenceScore: 0.87,
        confidenceVector: {
          extraction: 0.92,
          temporal: 0.85,
          venueMatch: 0.9,
          geocode: 0.8,
          overall: 0.87,
        },
        ocrText: "WEUP PRESENTS: HOUSTON NIGHTS",
        processingMetadata: {
          ocrEngine: "StubOCR",
          ocrVersion: "1.0.0",
          modelName: "StubLLM",
          modelVersion: "1.0.0",
        },
      },
      issues: [],
      executedAt: "2026-09-29T10:05:00Z",
    },
    createdAt: "2026-09-29T10:00:00Z",
    updatedAt: "2026-09-29T10:05:00Z",
    ...overrides,
  };
}

describe("projectProvenanceStages", () => {
  it("always projects exactly 8 stages in the mission section 22 order", () => {
    const stages = projectProvenanceStages(buildEvent(), null);
    expect(stages.map((stage) => stage.stage)).toEqual([
      "SOURCE",
      "INGESTION_JOB",
      "EXTRACTION",
      "NORMALIZATION",
      "CONFIDENCE",
      "ISSUES",
      "CANONICAL_EVENT",
      "UI_PROJECTION",
    ]);
  });

  it("degrades honestly when no ingestion job is linked (no fake stages)", () => {
    const stages = projectProvenanceStages(buildEvent(), null);
    const byStage = Object.fromEntries(stages.map((s) => [s.stage, s]));

    expect(byStage.SOURCE.status).toBe("complete");
    expect(byStage.INGESTION_JOB.status).toBe("unavailable");
    // Stubbed backend stages are PENDING explicitly, never success.
    expect(byStage.EXTRACTION.status).toBe("pending");
    expect(byStage.EXTRACTION.summary).toContain("PENDING");
    expect(byStage.NORMALIZATION.status).toBe("pending");
    expect(byStage.NORMALIZATION.summary).toContain("PENDING");
    expect(byStage.ISSUES.status).toBe("unavailable");
    expect(byStage.CONFIDENCE.status).toBe("complete");
    expect(byStage.CONFIDENCE.summary).toContain("85%");
    expect(byStage.CANONICAL_EVENT.status).toBe("complete");
    expect(byStage.UI_PROJECTION.status).toBe("complete");
  });

  it("completes extraction/normalization from real job evidence", () => {
    const stages = projectProvenanceStages(buildEvent(), buildJob());
    const byStage = Object.fromEntries(stages.map((s) => [s.stage, s]));

    expect(byStage.INGESTION_JOB.status).toBe("complete");
    expect(byStage.EXTRACTION.status).toBe("complete");
    expect(byStage.NORMALIZATION.status).toBe("complete");
    expect(byStage.ISSUES.status).toBe("complete");
    expect(byStage.ISSUES.summary).toContain("No issues recorded");
  });

  it("lists issues and marks failed jobs as failed, never as pending", () => {
    const job = buildJob({
      status: "FAILED",
      errorContext: "OCR engine crashed.",
      result: {
        jobId: "job-123",
        status: "FAILED",
        evidence: {
          assetId: "flyer.png",
          capturedAt: "2026-09-29T10:01:00Z",
          extractedBy: "flyer-ingestion-pipeline",
          confidenceScore: 0,
        },
        issues: [
          {
            code: "OCR_EMPTY",
            message: "No text extracted from flyer",
            severity: "ERROR",
            field: "assetId",
          },
        ],
        executedAt: "2026-09-29T10:05:00Z",
      },
    });
    const stages = projectProvenanceStages(buildEvent(), job);
    const byStage = Object.fromEntries(stages.map((s) => [s.stage, s]));

    expect(byStage.INGESTION_JOB.status).toBe("failed");
    expect(byStage.ISSUES.summary).toContain("1 issue");
    const issueDetails = byStage.ISSUES.details.map((d) => d.label).join(" ");
    expect(issueDetails).toContain("OCR_EMPTY");
  });
});

describe("useProvenanceDrawer", () => {
  beforeEach(() => {
    getIngestionJob.mockReset();
  });

  it("opens with projected stages without issuing a job lookup when no job id is supplied", async () => {
    const drawer = useProvenanceDrawer();
    drawer.openDrawer(buildEvent());
    await nextTick();

    expect(drawer.isOpen.value).toBe(true);
    expect(drawer.stages.value).toHaveLength(8);
    expect(getIngestionJob).not.toHaveBeenCalled();
  });

  it("loads the job record when an explicit job id is supplied", async () => {
    const drawer = useProvenanceDrawer();
    getIngestionJob.mockResolvedValue(buildJob());

    drawer.openDrawer(buildEvent(), { jobId: "job-123" });
    await flushPromises();

    expect(getIngestionJob).toHaveBeenCalledWith("job-123");
    expect(drawer.jobRecord.value?.id).toBe("job-123");
    expect(drawer.isLoadingJob.value).toBe(false);
  });

  it("treats a 404 job lookup as unavailable, not as an error", async () => {
    const drawer = useProvenanceDrawer();
    getIngestionJob.mockRejectedValue(
      new IngestionApiRequestError("not found", 404),
    );

    drawer.openDrawer(buildEvent(), { jobId: "job-missing" });
    await flushPromises();

    expect(drawer.jobRecord.value).toBeNull();
    expect(drawer.jobError.value).toBeNull();
    const byStage = Object.fromEntries(
      drawer.stages.value.map((s) => [s.stage, s]),
    );
    expect(byStage.INGESTION_JOB.status).toBe("unavailable");
  });

  it("surfaces non-404 job errors honestly", async () => {
    const drawer = useProvenanceDrawer();
    getIngestionJob.mockRejectedValue(new Error("boom"));

    drawer.openDrawer(buildEvent(), { jobId: "job-123" });
    await flushPromises();

    expect(drawer.jobError.value).toBe("boom");
    expect(drawer.isLoadingJob.value).toBe(false);
  });

  it("closes and resets", async () => {
    const drawer = useProvenanceDrawer();
    drawer.openDrawer(buildEvent());
    await nextTick();
    drawer.closeDrawer();
    await nextTick();

    expect(drawer.isOpen.value).toBe(false);
  });
});
