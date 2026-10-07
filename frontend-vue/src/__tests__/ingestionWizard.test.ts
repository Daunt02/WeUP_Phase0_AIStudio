/**
 * WEUP-SYNTH G11 tests: CREATE/ingestion flow.
 * Mission: WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
 *
 * Covers: the ingestion state-machine vocabulary, the service transport
 * contract (mock fetch at the transport boundary only — no backend logic
 * duplicated here), the wizard state machine transitions, the polling
 * lifecycle, validation presentation, and error states.
 */
import { defineComponent, h } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  INGESTION_SOURCE_METAS,
  INGESTION_STATUS_FLOW,
  ingestionStatusLabel,
  isFailedIngestionStatus,
  isTerminalIngestionStatus,
  type IngestionJobRecord,
  type IngestionJobStatus,
} from "../contracts/ingestion.contracts";
import {
  getFlyerEvidence,
  IngestionApiRequestError,
  postFlyerIngest,
  postManualIngest,
  postUrlIngest,
  postVenuePageIngest,
} from "../services/ingestionService";
import { useIngestionWizard } from "../composables/useIngestionWizard";
import AddEventWizard from "../components/AddEventWizard.vue";

vi.mock("quasar", async () => {
  const actual = await vi.importActual<typeof import("quasar")>("quasar");
  return {
    ...actual,
    useQuasar: () => ({ notify: vi.fn() }),
  };
});

function jsonResponse(body: unknown, status = 200): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.resolve(body),
    text: () => Promise.resolve(JSON.stringify(body)),
  } as Response;
}

function stubFetch(handler: (url: string, init?: RequestInit) => Response) {
  const fetchMock = vi.fn(
    (url: string, init?: RequestInit) =>
      Promise.resolve(handler(url, init)) as Promise<Response>,
  );
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function buildJobRecord(
  status: IngestionJobStatus,
  overrides: Partial<IngestionJobRecord> = {},
): IngestionJobRecord {
  return {
    id: "job-123",
    sourceKind: "MANUAL",
    status,
    requestPayload: { title: "Neon Ritual", venueName: "Factory X" },
    result:
      status === "RECEIVED" ||
      status === "VALIDATING" ||
      status === "NORMALIZING"
        ? undefined
        : {
            jobId: "job-123",
            status,
            candidate:
              status === "CANDIDATE_CREATED" || status === "REQUIRES_REVIEW"
                ? {
                    title: "Neon Ritual",
                    venueName: "Factory X",
                    startTime: "2026-10-02T21:00:00Z",
                  }
                : undefined,
            evidence: {
              rawPayload: { title: "Neon Ritual" },
              capturedAt: "2026-09-29T10:00:00Z",
              extractedBy: "manual-submission-adapter",
              confidenceScore: 1,
            },
            issues: [],
            executedAt: "2026-09-29T10:00:01Z",
          },
    createdAt: "2026-09-29T10:00:00Z",
    updatedAt: "2026-09-29T10:00:01Z",
    ...(status === "FAILED" ? { errorContext: "Validation failed." } : {}),
    ...overrides,
  };
}

beforeEach(() => {
  vi.useRealTimers();
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

// ─── State-machine vocabulary ─────────────────────────────────────────────

describe("ingestion state machine vocabulary", () => {
  it("declares the lifecycle in authoritative order", () => {
    expect([...INGESTION_STATUS_FLOW]).toEqual([
      "RECEIVED",
      "VALIDATING",
      "NORMALIZING",
      "CANDIDATE_CREATED",
      "REQUIRES_REVIEW",
    ]);
  });

  it("marks CANDIDATE_CREATED / REQUIRES_REVIEW / FAILED / RETRYABLE_FAILURE as terminal", () => {
    for (const status of [
      "CANDIDATE_CREATED",
      "REQUIRES_REVIEW",
      "FAILED",
      "RETRYABLE_FAILURE",
    ] as IngestionJobStatus[]) {
      expect(isTerminalIngestionStatus(status)).toBe(true);
    }
    for (const status of [
      "RECEIVED",
      "VALIDATING",
      "NORMALIZING",
    ] as IngestionJobStatus[]) {
      expect(isTerminalIngestionStatus(status)).toBe(false);
    }
  });

  it("classifies only FAILED / RETRYABLE_FAILURE as failed", () => {
    expect(isFailedIngestionStatus("FAILED")).toBe(true);
    expect(isFailedIngestionStatus("RETRYABLE_FAILURE")).toBe(true);
    expect(isFailedIngestionStatus("REQUIRES_REVIEW")).toBe(false);
    expect(isFailedIngestionStatus("CANDIDATE_CREATED")).toBe(false);
  });

  it("labels every status without inventing stages", () => {
    const labels = new Map<IngestionJobStatus, string>([
      ["RECEIVED", "Received"],
      ["VALIDATING", "Validating payload"],
      ["NORMALIZING", "Extracting + normalizing"],
      ["CANDIDATE_CREATED", "Candidate created"],
      ["REQUIRES_REVIEW", "Requires review"],
      ["FAILED", "Failed"],
      ["RETRYABLE_FAILURE", "Retryable failure"],
    ]);
    for (const [status, label] of labels) {
      expect(ingestionStatusLabel(status)).toBe(label);
    }
  });

  it("declares all five source kinds with EXTERNAL_FEED blocked honestly", () => {
    expect(INGESTION_SOURCE_METAS.map((meta) => meta.kind)).toEqual([
      "MANUAL",
      "URL",
      "VENUE_PAGE",
      "FLYER_OCR",
      "EXTERNAL_FEED",
    ]);
    const blocked = INGESTION_SOURCE_METAS.filter((meta) => !meta.submittable);
    expect(blocked).toHaveLength(1);
    expect(blocked[0].kind).toBe("EXTERNAL_FEED");
    expect(blocked[0].blockedReason).toContain("No feed adapter");
  });
});

// ─── Service transport contract ───────────────────────────────────────────

describe("ingestionService transport", () => {
  it("posts manual envelopes to /api/ingestion/manual with the exact route contract", async () => {
    const record = buildJobRecord("CANDIDATE_CREATED");
    const fetchMock = stubFetch(() => jsonResponse(record));

    const result = await postManualIngest({
      title: "Neon Ritual",
      venueName: "Factory X",
    });

    expect(result).toEqual(record);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("/api/ingestion/manual");
    expect(init?.method).toBe("POST");
    // The Next handlers read the whole JSON body as the adapter payload
    // and add sourceKind server-side — the client posts the raw payload.
    expect(JSON.parse(init?.body as string)).toEqual({
      title: "Neon Ritual",
      venueName: "Factory X",
    });
  });

  it("posts URL envelopes to /api/ingestion/url", async () => {
    const record = buildJobRecord("REQUIRES_REVIEW", { sourceKind: "URL" });
    const fetchMock = stubFetch(() => jsonResponse(record));

    await postUrlIngest({ url: "https://instagram.com/p/abc" });

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("/api/ingestion/url");
    expect(JSON.parse(init?.body as string)).toEqual({
      url: "https://instagram.com/p/abc",
    });
  });

  it("posts venue-page envelopes to /api/ingestion/venue-page", async () => {
    const record = buildJobRecord("REQUIRES_REVIEW", {
      sourceKind: "VENUE_PAGE",
    });
    const fetchMock = stubFetch(() => jsonResponse(record));

    await postVenuePageIngest({
      venueId: "warehouse",
      pageUrl: "https://venue.example.com",
    });

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("/api/ingestion/venue-page");
    expect(JSON.parse(init?.body as string)).toEqual({
      venueId: "warehouse",
      pageUrl: "https://venue.example.com",
    });
  });

  it("posts flyer envelopes to /api/ingestion/flyers", async () => {
    const record = buildJobRecord("REQUIRES_REVIEW", {
      sourceKind: "FLYER_OCR",
    });
    const fetchMock = stubFetch(() => jsonResponse(record));

    await postFlyerIngest({ assetId: "flyer.png" });

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("/api/ingestion/flyers");
    expect(JSON.parse(init?.body as string)).toEqual({
      assetId: "flyer.png",
    });
  });

  it("throws IngestionApiRequestError with the backend error detail on failure", async () => {
    stubFetch(() => jsonResponse({ error: "Missing flyer asset identifier" }, 400));

    const failure = await postFlyerIngest({}).catch((cause) => cause);
    expect(failure).toBeInstanceOf(IngestionApiRequestError);
    expect((failure as IngestionApiRequestError).status).toBe(400);
    expect((failure as Error).message).toContain(
      "Missing flyer asset identifier",
    );
  });

  it("fetches flyer evidence from /api/ingestion/flyers/{id}/evidence", async () => {
    const evidence = {
      jobId: "job-123",
      sourceKind: "FLYER_OCR",
      evidence: {
        assetId: "flyer.png",
        capturedAt: "2026-09-29T10:01:00Z",
        extractedBy: "flyer-ingestion-pipeline",
        confidenceScore: 0.87,
      },
      issues: [],
    };
    const fetchMock = stubFetch(() => jsonResponse(evidence));

    const result = await getFlyerEvidence("job-123");

    expect(result).toEqual(evidence);
    const [url] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("/api/ingestion/flyers/job-123/evidence");
  });
});

// ─── Wizard state machine ─────────────────────────────────────────────────

describe("useIngestionWizard", () => {
  function fillManualForm(
    wizard: ReturnType<typeof useIngestionWizard>,
    overrides: Partial<{ title: string; venueName: string }> = {},
  ) {
    wizard.manualForm.value.title = overrides.title ?? "Neon Ritual";
    wizard.manualForm.value.venueName = overrides.venueName ?? "Factory X";
  }

  it("starts at SOURCE with no job and no errors", () => {
    const wizard = useIngestionWizard();
    expect(wizard.stage.value).toBe("SOURCE");
    expect(wizard.job.value).toBeNull();
    expect(wizard.validationErrors.value).toEqual({});
    wizard.dispose();
  });

  it("presents validation errors without touching the network (manual)", async () => {
    const fetchMock = stubFetch(() => jsonResponse(buildJobRecord("CANDIDATE_CREATED")));
    const wizard = useIngestionWizard();
    wizard.selectSource("MANUAL");
    fillManualForm(wizard, { title: "", venueName: "" });

    await wizard.submitCurrent();

    expect(fetchMock).not.toHaveBeenCalled();
    expect(wizard.validationErrors.value.title).toContain("required");
    expect(wizard.validationErrors.value.venueName).toContain("required");
    expect(wizard.stage.value).toBe("INPUT");
    wizard.dispose();
  });

  it("rejects non-http URLs at validation (URL)", async () => {
    const fetchMock = stubFetch(() => jsonResponse(buildJobRecord("REQUIRES_REVIEW")));
    const wizard = useIngestionWizard();
    wizard.selectSource("URL");
    wizard.urlInput.value = "not-a-url";

    await wizard.submitCurrent();

    expect(fetchMock).not.toHaveBeenCalled();
    expect(wizard.validationErrors.value.url).toContain("http");
    wizard.dispose();
  });

  it("blocks EXTERNAL_FEED submission honestly with no network call", async () => {
    const fetchMock = stubFetch(() => jsonResponse(buildJobRecord("REQUIRES_REVIEW")));
    const wizard = useIngestionWizard();
    wizard.selectSource("EXTERNAL_FEED");

    expect(wizard.isSourceSubmittable.value).toBe(false);
    expect(wizard.blockedReason.value).toContain("No feed adapter");

    await wizard.submitCurrent();

    expect(fetchMock).not.toHaveBeenCalled();
    expect(wizard.validationErrors.value.source).toContain("No feed adapter");
    wizard.dispose();
  });

  it("requires an asset identifier for flyer submission (no binary upload exists)", async () => {
    const fetchMock = stubFetch(() => jsonResponse(buildJobRecord("REQUIRES_REVIEW")));
    const wizard = useIngestionWizard();
    wizard.selectSource("FLYER_OCR");

    await wizard.submitCurrent();

    expect(fetchMock).not.toHaveBeenCalled();
    expect(wizard.validationErrors.value.flyer).toContain("asset");
    wizard.dispose();
  });

  it("transitions SUBMITTING -> REVIEW when the POST already returns a terminal job", async () => {
    const fetchMock = stubFetch(() =>
      jsonResponse(buildJobRecord("CANDIDATE_CREATED")),
    );
    const wizard = useIngestionWizard();
    wizard.selectSource("MANUAL");
    fillManualForm(wizard);

    await wizard.submitCurrent();

    expect(wizard.stage.value).toBe("REVIEW");
    expect(wizard.job.value?.id).toBe("job-123");
    expect(wizard.candidate.value?.title).toBe("Neon Ritual");
    expect(wizard.confidenceScore.value).toBe(1);

    // Transport boundary: the wizard posts the raw source payload to the
    // source-specific route — never a wrapped envelope the handler would
    // misread as the payload.
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("/api/ingestion/manual");
    const body = JSON.parse(init?.body as string) as Record<string, unknown>;
    expect(body).not.toHaveProperty("sourceKind");
    expect(body).not.toHaveProperty("payload");
    expect(body.title).toBe("Neon Ritual");
    expect(body.venueName).toBe("Factory X");
    wizard.dispose();
  });

  it("polls the real job lifecycle until a terminal status (no fake timers)", async () => {
    vi.useFakeTimers();
    const calls: string[] = [];
    stubFetch((url) => {
      calls.push(url);
      if (url === "/api/ingestion/manual") {
        return jsonResponse(buildJobRecord("RECEIVED"));
      }
      const step = calls.filter((call) =>
        call.startsWith("/api/ingestion/jobs/"),
      ).length;
      const status: IngestionJobStatus =
        step === 1 ? "VALIDATING" : step === 2 ? "NORMALIZING" : "REQUIRES_REVIEW";
      return jsonResponse(buildJobRecord(status));
    });

    const wizard = useIngestionWizard();
    wizard.selectSource("MANUAL");
    fillManualForm(wizard);

    await wizard.submitCurrent();
    expect(wizard.stage.value).toBe("PROCESSING");
    expect(wizard.currentJobStatus.value).toBe("RECEIVED");

    await vi.advanceTimersByTimeAsync(1500);
    expect(wizard.currentJobStatus.value).toBe("VALIDATING");

    await vi.advanceTimersByTimeAsync(1500);
    expect(wizard.currentJobStatus.value).toBe("NORMALIZING");

    await vi.advanceTimersByTimeAsync(1500);
    expect(wizard.stage.value).toBe("REVIEW");
    expect(wizard.currentJobStatus.value).toBe("REQUIRES_REVIEW");

    // Terminal: polling stops — advancing time issues no further GETs.
    const getCalls = calls.filter((call) =>
      call.startsWith("/api/ingestion/jobs/"),
    ).length;
    await vi.advanceTimersByTimeAsync(15000);
    expect(
      calls.filter((call) => call.startsWith("/api/ingestion/jobs/")).length,
    ).toBe(getCalls);
    wizard.dispose();
  });

  it("records the actually observed statuses in a ledger (no inference)", async () => {
    vi.useFakeTimers();
    let fetchMock!: ReturnType<typeof vi.fn>;
    fetchMock = stubFetch((url) => {
      if (url === "/api/ingestion/manual") {
        return jsonResponse(buildJobRecord("RECEIVED"));
      }
      const step = fetchMock.mock.calls.filter((call) =>
        String((call as [string, RequestInit])[0]).startsWith(
          "/api/ingestion/jobs/",
        ),
      ).length;
      // Simulate a backend that skips VALIDATING in the observable stream.
      const status: IngestionJobStatus =
        step === 1 ? "NORMALIZING" : "REQUIRES_REVIEW";
      return jsonResponse(buildJobRecord(status));
    });

    const wizard = useIngestionWizard();
    wizard.selectSource("MANUAL");
    fillManualForm(wizard);
    await wizard.submitCurrent();
    await vi.advanceTimersByTimeAsync(1500);
    await vi.advanceTimersByTimeAsync(1500);

    expect(wizard.stage.value).toBe("REVIEW");
    // Only statuses actually seen on the wire appear — VALIDATING is not
    // invented even though it precedes NORMALIZING in the lifecycle order.
    expect(wizard.observedStatuses.value).toEqual([
      "RECEIVED",
      "NORMALIZING",
      "REQUIRES_REVIEW",
    ]);
    wizard.dispose();
  });

  it("dispose() stops polling (no stranded interval)", async () => {
    vi.useFakeTimers();
    const calls: string[] = [];
    stubFetch((url) => {
      calls.push(url);
      if (url === "/api/ingestion/manual") {
        return jsonResponse(buildJobRecord("RECEIVED"));
      }
      return jsonResponse(buildJobRecord("VALIDATING"));
    });

    const wizard = useIngestionWizard();
    wizard.selectSource("MANUAL");
    fillManualForm(wizard);
    await wizard.submitCurrent();
    expect(wizard.stage.value).toBe("PROCESSING");

    // The startPolling() immediate probe may already be in flight; dispose
    // must prevent any *further* scheduled polls.
    const getsBeforeDispose = calls.filter((call) =>
      call.startsWith("/api/ingestion/jobs/"),
    ).length;
    wizard.dispose();
    await vi.advanceTimersByTimeAsync(15000);
    expect(
      calls.filter((call) => call.startsWith("/api/ingestion/jobs/")).length,
    ).toBe(getsBeforeDispose);
  });

  it("surfaces a FAILED job with its errorContext (not retryable)", async () => {
    vi.useFakeTimers();
    stubFetch((url) =>
      jsonResponse(
        url === "/api/ingestion/manual"
          ? buildJobRecord("RECEIVED")
          : buildJobRecord("FAILED"),
      ),
    );

    const wizard = useIngestionWizard();
    wizard.selectSource("MANUAL");
    fillManualForm(wizard);
    await wizard.submitCurrent();
    await vi.advanceTimersByTimeAsync(1500);

    expect(wizard.stage.value).toBe("ERROR");
    expect(wizard.errorState.value?.title).toBe("Ingestion failed");
    expect(wizard.errorState.value?.message).toContain("Validation failed.");
    expect(wizard.errorState.value?.retryable).toBe(false);
    wizard.dispose();
  });

  it("marks RETRYABLE_FAILURE as retryable and retry() resubmits", async () => {
    vi.useFakeTimers();
    let posts = 0;
    stubFetch((url) => {
      if (url === "/api/ingestion/manual") {
        posts += 1;
        return jsonResponse(
          posts === 1
            ? buildJobRecord("RETRYABLE_FAILURE")
            : buildJobRecord("CANDIDATE_CREATED"),
        );
      }
      return jsonResponse(buildJobRecord("RETRYABLE_FAILURE"));
    });

    const wizard = useIngestionWizard();
    wizard.selectSource("MANUAL");
    fillManualForm(wizard);
    await wizard.submitCurrent();

    expect(wizard.stage.value).toBe("ERROR");
    expect(wizard.errorState.value?.retryable).toBe(true);

    wizard.retry();
    await flushPromises();
    expect(posts).toBe(2);
    expect(wizard.stage.value).toBe("REVIEW");
    wizard.dispose();
  });

  it("surfaces a transport failure on submit as a retryable error", async () => {
    stubFetch(() => jsonResponse({ error: "boom" }, 500));
    const wizard = useIngestionWizard();
    wizard.selectSource("MANUAL");
    fillManualForm(wizard);

    await wizard.submitCurrent();

    expect(wizard.stage.value).toBe("ERROR");
    expect(wizard.errorState.value?.title).toBe("Submission failed");
    expect(wizard.errorState.value?.message).toContain("500");
    expect(wizard.errorState.value?.retryable).toBe(true);
    wizard.dispose();
  });

  it("acceptCandidate returns the accepted payload only in REVIEW", async () => {
    stubFetch(() => jsonResponse(buildJobRecord("CANDIDATE_CREATED")));
    const wizard = useIngestionWizard();

    expect(wizard.acceptCandidate()).toBeNull();

    wizard.selectSource("MANUAL");
    fillManualForm(wizard);
    await wizard.submitCurrent();

    const accepted = wizard.acceptCandidate();
    expect(accepted?.jobId).toBe("job-123");
    expect(accepted?.sourceKind).toBe("MANUAL");
    expect(accepted?.candidate.title).toBe("Neon Ritual");
    expect(accepted?.job.id).toBe("job-123");
    wizard.dispose();
  });

  it("discardCandidate returns to INPUT without touching the job server-side", async () => {
    const fetchMock = stubFetch(() =>
      jsonResponse(buildJobRecord("CANDIDATE_CREATED")),
    );
    const wizard = useIngestionWizard();
    wizard.selectSource("MANUAL");
    fillManualForm(wizard);
    await wizard.submitCurrent();
    expect(wizard.stage.value).toBe("REVIEW");

    const callsBefore = fetchMock.mock.calls.length;
    wizard.discardCandidate();

    expect(wizard.stage.value).toBe("INPUT");
    expect(wizard.job.value).toBeNull();
    // Discard is local-only: no further network traffic.
    expect(fetchMock.mock.calls.length).toBe(callsBefore);
    wizard.dispose();
  });
});

// ─── FlyerSkeleton lifecycle honesty ──────────────────────────────────────

import FlyerSkeleton from "../components/FlyerSkeleton.vue";

function mountSkeleton(props: Record<string, unknown> = {}) {
  return mount(FlyerSkeleton, {
    props: { currentStatus: null, observedStatuses: [], ...props },
  });
}

describe("FlyerSkeleton", () => {
  it("marks only observed stages done — never infers skipped stages", () => {
    // The backend skipped VALIDATING in the observable stream (e.g. the
    // POST returned NORMALIZING directly): VALIDATING must stay pending.
    const wrapper = mountSkeleton({
      currentStatus: "NORMALIZING",
      observedStatuses: ["VALIDATING", "NORMALIZING"],
      jobId: "job-123",
    });
    const items = wrapper.findAll(".stage-item");
    const byLabel = Object.fromEntries(
      items.map((item) => [item.find(".stage-label").text(), item.classes()]),
    );

    expect(byLabel["Received"]).toContain("stage-pending");
    expect(byLabel["Validating payload"]).toContain("stage-done");
    expect(byLabel["Extracting + normalizing"]).toContain("stage-active");
    expect(byLabel["Candidate created"]).toContain("stage-pending");
    expect(byLabel["Requires review"]).toContain("stage-pending");
    expect(wrapper.find('[data-testid="skeleton-job-id"]').text()).toContain(
      "job-123",
    );
  });

  it("renders all stages pending before any status is observed", () => {
    const wrapper = mountSkeleton();
    expect(wrapper.findAll(".stage-item")).toHaveLength(5);
    for (const item of wrapper.findAll(".stage-item")) {
      expect(item.classes()).toContain("stage-pending");
    }
  });
});


// ─── AddEventWizard component ─────────────────────────────────────────────

const GenericStub = (name: string) =>
  defineComponent({
    name: `${name}-stub`,
    setup(_, { slots, attrs }) {
      return () => h("div", { ...attrs, [`data-stub`]: name }, slots.default?.());
    },
  });

function mountWizard(props: Record<string, unknown> = {}) {
  return mount(AddEventWizard, {
    props: { modelValue: true, ...props },
    global: {
      stubs: {
        "q-dialog": GenericStub("q-dialog"),
        "q-card": GenericStub("q-card"),
        "q-card-section": GenericStub("q-card-section"),
        "q-separator": GenericStub("q-separator"),
        "q-btn": GenericStub("q-btn"),
        "q-icon": GenericStub("q-icon"),
        "q-spinner": GenericStub("q-spinner"),
        "q-banner": GenericStub("q-banner"),
        "q-expansion-item": GenericStub("q-expansion-item"),
      },
    },
  });
}

describe("AddEventWizard", () => {
  it("renders all five source choices with the feed path honestly marked unavailable", () => {
    const wrapper = mountWizard();
    const cards = wrapper.findAll(".source-card");
    expect(cards).toHaveLength(5);
    expect(cards[4].classes()).toContain("source-card-blocked");
    expect(cards[4].find(".source-blocked-chip").exists()).toBe(true);
    expect(wrapper.text()).toContain("External feed");
  });

  it("shows the manual form on selection and presents validation errors without fetching", async () => {
    const fetchMock = stubFetch(() =>
      jsonResponse(buildJobRecord("CANDIDATE_CREATED")),
    );
    const wrapper = mountWizard();

    await wrapper.findAll(".source-card")[0].trigger("click");
    expect(wrapper.find('input[placeholder="UNTITLED_EVENT"]').exists()).toBe(true);

    await wrapper.find(".wizard-btn-primary").trigger("click");
    await flushPromises();

    expect(fetchMock).not.toHaveBeenCalled();
    expect(wrapper.findAll(".field-error").length).toBeGreaterThan(0);
    expect(wrapper.text()).toContain("Title is required.");
  });

  it("renders the review surface from a real terminal job (no fabricated candidate)", async () => {
    stubFetch(() => jsonResponse(buildJobRecord("REQUIRES_REVIEW", { sourceKind: "URL" })));
    const wrapper = mountWizard();

    await wrapper.findAll(".source-card")[1].trigger("click");
    const urlInput = wrapper.find('input[type="url"]');
    await urlInput.setValue("https://instagram.com/p/abc");
    await wrapper.find(".wizard-btn-primary").trigger("click");
    await flushPromises();

    expect(wrapper.find(".candidate-title").text()).toBe("Neon Ritual");
    expect(wrapper.find(".candidate-venue").text()).toBe("Factory X");
    expect(wrapper.text()).toContain("JOB job-123");
  });

  it("renders the error surface from a failed job with its backend context", async () => {
    stubFetch(() => jsonResponse(buildJobRecord("FAILED")));
    const wrapper = mountWizard();

    await wrapper.findAll(".source-card")[0].trigger("click");
    await wrapper.find('input[placeholder="UNTITLED_EVENT"]').setValue("Neon Ritual");
    await wrapper.find('input[placeholder="UNKNOWN_VENUE"]').setValue("Factory X");
    await wrapper.find(".wizard-btn-primary").trigger("click");
    await flushPromises();

    expect(wrapper.find(".error-title").text()).toBe("Ingestion failed");
    expect(wrapper.find(".error-message").text()).toContain("Validation failed.");
  });
});
