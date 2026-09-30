/**
 * WEUP-SYNTH:
 * source=components/AddEventModal.tsx
 * destination=frontend-vue/src/composables/useIngestionWizard.ts
 * mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
 * notes=Wizard state machine for the CREATE flow per the G3 chain. All
 *   simulation paths from the source (simulated upload progress,
 *   simulateExtraction, 'error'/'duplicate' link triggers, hardcoded
 *   candidates, local publish list) are EXCLUDED by design: every transition
 *   is driven by a real /api/ingestion/* response. Skeleton/progress states
 *   bind to the actual job.status values observed while polling — never
 *   arbitrary animation. Vue owns input, validation presentation, submission,
 *   progress, polling, error state, candidate review, and evidence display;
 *   the backend owns ingestion execution, OCR, normalization, resolver,
 *   evidence, and persistence.
 */
import { computed, ref } from "vue";
import type {
  CanonicalEventCandidate,
  CanonicalIngestionIssue,
  CanonicalSourceEvidence,
  ConfidenceVector,
  IngestionJobRecord,
  IngestionJobStatus,
  IngestionSourceKind,
} from "../contracts/ingestion.contracts";
import {
  ingestionSourceMeta,
  isFailedIngestionStatus,
  isTerminalIngestionStatus,
} from "../contracts/ingestion.contracts";
import {
  getIngestionJob,
  IngestionApiRequestError,
  postFlyerIngest,
  postManualIngest,
  postUrlIngest,
  postVenuePageIngest,
  type FlyerIngestPayload,
  type ManualIngestPayload,
  type UrlIngestPayload,
  type VenuePageIngestPayload,
} from "../services/ingestionService";

export type WizardStage =
  | "SOURCE"
  | "INPUT"
  | "SUBMITTING"
  | "PROCESSING"
  | "REVIEW"
  | "ERROR";

export interface WizardErrorState {
  readonly title: string;
  readonly message: string;
  /** True when a retry (resubmission) can plausibly succeed. */
  readonly retryable: boolean;
}

export interface ManualFormState {
  title: string;
  venueName: string;
  description: string;
  address: string;
  startDate: string;
  startTime: string;
  endDate: string;
  endTime: string;
  category: string;
  priceTier: string;
  latitude: string;
  longitude: string;
  tags: string;
}

/** Payload handed to the parent when the user accepts a reviewed candidate. */
export interface AcceptedIngestionCandidate {
  readonly jobId: string;
  readonly sourceKind: IngestionSourceKind;
  readonly candidate: CanonicalEventCandidate;
  /** Full job record — lets the parent project provenance without re-fetching. */
  readonly job: IngestionJobRecord;
}

function emptyManualForm(): ManualFormState {
  return {
    title: "",
    venueName: "",
    description: "",
    address: "",
    startDate: "",
    startTime: "",
    endDate: "",
    endTime: "",
    category: "nightlife",
    priceTier: "$$",
    latitude: "",
    longitude: "",
    tags: "",
  };
}

const POLL_INTERVAL_MS = 1500;
const MAX_POLL_ATTEMPTS = 40;
const MAX_CONSECUTIVE_POLL_ERRORS = 3;

export interface UseIngestionWizardOptions {
  /** Prefill coordinates for manual entry (canonical map center). */
  mapCenter?: { lat: number; lng: number } | null;
}

export function useIngestionWizard(options?: UseIngestionWizardOptions) {
  const stage = ref<WizardStage>("SOURCE");
  const sourceKind = ref<IngestionSourceKind | null>(null);
  const manualForm = ref<ManualFormState>(emptyManualForm());
  const urlInput = ref("");
  const venueIdInput = ref("");
  const venuePageUrlInput = ref("");
  const flyerAssetId = ref("");
  const flyerFile = ref<File | null>(null);
  const flyerPreviewUrl = ref<string | null>(null);
  const validationErrors = ref<Record<string, string>>({});
  const job = ref<IngestionJobRecord | null>(null);
  const errorState = ref<WizardErrorState | null>(null);
  const acceptedCandidate = ref<AcceptedIngestionCandidate | null>(null);
  const pollAttempts = ref(0);
  /**
   * Statuses actually observed on the current job (POST response + every
   * poll). Rendered honestly in the lifecycle display — stages never
   * observed are never shown as done.
   */
  const observedStatuses = ref<IngestionJobStatus[]>([]);

  function recordObservedStatus(status: IngestionJobStatus): void {
    if (!observedStatuses.value.includes(status)) {
      observedStatuses.value.push(status);
    }
  }

  function clearObservedStatuses(): void {
    observedStatuses.value = [];
  }

  let pollTimer: ReturnType<typeof setInterval> | null = null;
  let consecutivePollErrors = 0;
  /** Last successfully built payload, kept for retryable resubmission. */
  let lastPayload: {
    kind: IngestionSourceKind;
    payload:
      | ManualIngestPayload
      | UrlIngestPayload
      | VenuePageIngestPayload
      | FlyerIngestPayload;
  } | null = null;

  const isBusy = computed(
    () => stage.value === "SUBMITTING" || stage.value === "PROCESSING",
  );

  const isSourceSubmittable = computed(() =>
    sourceKind.value === null
      ? false
      : ingestionSourceMeta(sourceKind.value).submittable,
  );

  const blockedReason = computed(() =>
    sourceKind.value === null
      ? null
      : ingestionSourceMeta(sourceKind.value).blockedReason,
  );

  const candidate = computed<CanonicalEventCandidate | null>(
    () => job.value?.result?.candidate ?? null,
  );

  const issues = computed<CanonicalIngestionIssue[]>(
    () => job.value?.result?.issues ?? [],
  );

  const evidence = computed<CanonicalSourceEvidence | null>(
    () => job.value?.result?.evidence ?? null,
  );

  const confidenceVector = computed<ConfidenceVector | null>(
    () => job.value?.result?.evidence?.confidenceVector ?? null,
  );

  const confidenceScore = computed<number | null>(
    () => job.value?.result?.evidence?.confidenceScore ?? null,
  );

  const currentJobStatus = computed<IngestionJobStatus | null>(
    () => job.value?.status ?? null,
  );

  function clearPollTimer(): void {
    if (pollTimer !== null) {
      clearInterval(pollTimer);
      pollTimer = null;
    }
  }

  function setError(title: string, message: string, retryable: boolean): void {
    clearPollTimer();
    errorState.value = { title, message, retryable };
    stage.value = "ERROR";
  }

  function validateCurrent(): boolean {
    const errors: Record<string, string> = {};
    const kind = sourceKind.value;

    if (!kind) {
      errors.source = "Choose an ingestion source first.";
    } else if (kind === "MANUAL") {
      if (!manualForm.value.title.trim()) {
        errors.title = "Title is required.";
      }
      if (!manualForm.value.venueName.trim()) {
        errors.venueName = "Venue name is required.";
      }
      for (const field of ["latitude", "longitude"] as const) {
        const raw = manualForm.value[field].trim();
        if (raw && Number.isNaN(Number(raw))) {
          errors[field] = "Must be a number.";
        }
      }
    } else if (kind === "URL") {
      const url = urlInput.value.trim();
      if (!url) {
        errors.url = "A URL is required.";
      } else if (!/^https?:\/\//i.test(url)) {
        errors.url = "The URL must start with http:// or https://.";
      }
    } else if (kind === "VENUE_PAGE") {
      if (!venueIdInput.value.trim()) {
        errors.venueId = "Venue id is required.";
      }
      const pageUrl = venuePageUrlInput.value.trim();
      if (!pageUrl) {
        errors.pageUrl = "A page URL is required.";
      } else if (!/^https?:\/\//i.test(pageUrl)) {
        errors.pageUrl = "The page URL must start with http:// or https://.";
      }
    } else if (kind === "FLYER_OCR") {
      if (!flyerAssetId.value.trim() && !flyerFile.value) {
        errors.flyer =
          "Select a flyer image or provide an asset id — the backend requires an asset identifier.";
      }
    } else if (kind === "EXTERNAL_FEED") {
      errors.source =
        ingestionSourceMeta("EXTERNAL_FEED").blockedReason ??
        "External feed ingestion is not available.";
    }

    validationErrors.value = errors;
    return Object.keys(errors).length === 0;
  }

  function buildManualPayload(): ManualIngestPayload {
    const form = manualForm.value;
    const combine = (date: string, time: string): string | undefined => {
      if (!date) {
        return undefined;
      }
      return `${date}T${time || "00:00"}:00`;
    };
    // Object composition rather than mutation: the payload interfaces are
    // readonly, so optional fields are composed via spreads.
    const description = form.description.trim();
    const address = form.address.trim();
    const start = combine(form.startDate.trim(), form.startTime.trim());
    const end = combine(form.endDate.trim(), form.endTime.trim());
    const category = form.category.trim();
    const priceTier = form.priceTier.trim();
    const latRaw = form.latitude.trim();
    const lngRaw = form.longitude.trim();
    const lat = Number(latRaw);
    const lng = Number(lngRaw);
    const tags = form.tags
      .split(",")
      .map((tag) => tag.trim())
      .filter((tag) => tag.length > 0);
    return {
      title: form.title.trim(),
      venueName: form.venueName.trim(),
      ...(description ? { description } : {}),
      ...(address ? { address } : {}),
      ...(start ? { startTime: start } : {}),
      ...(end ? { endTime: end } : {}),
      ...(category ? { category } : {}),
      ...(priceTier ? { priceTier } : {}),
      ...(latRaw && !Number.isNaN(lat) ? { latitude: lat } : {}),
      ...(lngRaw && !Number.isNaN(lng) ? { longitude: lng } : {}),
      ...(tags.length > 0 ? { tags } : {}),
    };
  }

  function settleTerminalJob(record: IngestionJobRecord): void {
    // Terminal: the job never advances further — stop polling immediately so
    // no stranded interval keeps hitting the backend.
    clearPollTimer();
    job.value = record;
    if (isFailedIngestionStatus(record.status)) {
      const detail =
        record.errorContext?.trim() ||
        "The ingestion job ended in a failed state.";
      setError(
        record.status === "RETRYABLE_FAILURE"
          ? "Ingestion failed — retryable"
          : "Ingestion failed",
        detail,
        record.status === "RETRYABLE_FAILURE",
      );
      return;
    }
    // CANDIDATE_CREATED / REQUIRES_REVIEW: the lifecycle is observable and
    // complete from the backend's side; the user now reviews the candidate.
    stage.value = "REVIEW";
  }

  async function pollJob(jobId: string): Promise<void> {
    pollAttempts.value += 1;
    try {
      const record = await getIngestionJob(jobId);
      consecutivePollErrors = 0;
      recordObservedStatus(record.status);
      if (isTerminalIngestionStatus(record.status)) {
        settleTerminalJob(record);
        return;
      }
      job.value = record;
      if (pollAttempts.value >= MAX_POLL_ATTEMPTS) {
        setError(
          "Ingestion timed out",
          `The job ${jobId} did not reach a terminal state after ${MAX_POLL_ATTEMPTS} status checks. The job may still be processing server-side.`,
          true,
        );
      }
    } catch (cause) {
      consecutivePollErrors += 1;
      if (consecutivePollErrors >= MAX_CONSECUTIVE_POLL_ERRORS) {
        const message =
          cause instanceof IngestionApiRequestError
            ? `Status polling failed (${cause.status}). ${cause.message}`
            : cause instanceof Error
              ? cause.message
              : "Status polling failed.";
        setError("Status polling failed", message, true);
      }
      // Transient poll errors below the threshold keep the interval alive.
    }
  }

  function startPolling(jobId: string): void {
    clearPollTimer();
    consecutivePollErrors = 0;
    pollAttempts.value = 0;
    stage.value = "PROCESSING";
    pollTimer = setInterval(() => {
      void pollJob(jobId);
    }, POLL_INTERVAL_MS);
  }

  async function postForKind(
    kind: IngestionSourceKind,
    payload:
      | ManualIngestPayload
      | UrlIngestPayload
      | VenuePageIngestPayload
      | FlyerIngestPayload,
  ): Promise<IngestionJobRecord> {
    switch (kind) {
      case "MANUAL":
        return postManualIngest(payload as ManualIngestPayload);
      case "URL":
        return postUrlIngest(payload as UrlIngestPayload);
      case "VENUE_PAGE":
        return postVenuePageIngest(payload as VenuePageIngestPayload);
      case "FLYER_OCR":
        return postFlyerIngest(payload as FlyerIngestPayload);
      case "EXTERNAL_FEED":
        throw new Error(
          ingestionSourceMeta("EXTERNAL_FEED").blockedReason ??
            "External feed ingestion is not available.",
        );
    }
  }

  async function submitCurrent(): Promise<void> {
    const kind = sourceKind.value;
    if (!kind) {
      return;
    }

    validationErrors.value = {};
    errorState.value = null;

    if (!validateCurrent()) {
      return;
    }

    let payload:
      | ManualIngestPayload
      | UrlIngestPayload
      | VenuePageIngestPayload
      | FlyerIngestPayload;
    if (kind === "MANUAL") {
      payload = buildManualPayload();
    } else if (kind === "URL") {
      payload = { url: urlInput.value.trim() };
    } else if (kind === "VENUE_PAGE") {
      payload = {
        venueId: venueIdInput.value.trim(),
        pageUrl: venuePageUrlInput.value.trim(),
      };
    } else if (kind === "FLYER_OCR") {
      payload = {
        assetId:
          flyerAssetId.value.trim() || flyerFile.value?.name || undefined,
      };
    } else {
      // EXTERNAL_FEED is rejected by validateCurrent; unreachable.
      return;
    }

    lastPayload = { kind, payload };
    stage.value = "SUBMITTING";

    try {
      const record = await postForKind(kind, payload);
      job.value = record;
      recordObservedStatus(record.status);
      if (isTerminalIngestionStatus(record.status)) {
        settleTerminalJob(record);
      } else {
        startPolling(record.id);
      }
    } catch (cause) {
      const message =
        cause instanceof IngestionApiRequestError
          ? `Submission failed (${cause.status}). ${cause.message}`
          : cause instanceof Error
            ? cause.message
            : "Submission failed.";
      // Transport/validation failures are retryable: nothing was persisted.
      setError("Submission failed", message, true);
    }
  }

  function selectSource(kind: IngestionSourceKind): void {
    clearPollTimer();
    sourceKind.value = kind;
    job.value = null;
    clearObservedStatuses();
    errorState.value = null;
    acceptedCandidate.value = null;
    validationErrors.value = {};
    stage.value = "INPUT";
  }

  function backToSource(): void {
    clearPollTimer();
    stage.value = "SOURCE";
    job.value = null;
    clearObservedStatuses();
    errorState.value = null;
  }

  function backToInput(): void {
    clearPollTimer();
    stage.value = "INPUT";
    job.value = null;
    clearObservedStatuses();
    errorState.value = null;
  }

  function retry(): void {
    if (!lastPayload) {
      return;
    }
    errorState.value = null;
    void submitCurrent();
  }

  /**
   * Discard the current candidate. Local-only: the backend job record is
   * unchanged (there is no reject/withdraw endpoint); the wizard returns to
   * the input stage for a fresh submission.
   */
  function discardCandidate(): void {
    job.value = null;
    clearObservedStatuses();
    acceptedCandidate.value = null;
    errorState.value = null;
    stage.value = "INPUT";
  }

  /**
   * Accept the reviewed candidate. The candidate is handed to the parent for
   * projection through the canonical event surface; it is never written into
   * canonical event state (no backend publish endpoint exists).
   */
  function acceptCandidate(): AcceptedIngestionCandidate | null {
    const record = job.value;
    const reviewed = candidate.value;
    if (!record || !reviewed || stage.value !== "REVIEW") {
      return null;
    }
    const accepted: AcceptedIngestionCandidate = {
      jobId: record.id,
      sourceKind: record.sourceKind,
      candidate: reviewed,
      job: record,
    };
    acceptedCandidate.value = accepted;
    return accepted;
  }

  function reset(): void {
    clearPollTimer();
    stage.value = "SOURCE";
    sourceKind.value = null;
    manualForm.value = emptyManualForm();
    urlInput.value = "";
    venueIdInput.value = "";
    venuePageUrlInput.value = "";
    flyerAssetId.value = "";
    setFlyerFile(null);
    validationErrors.value = {};
    job.value = null;
    errorState.value = null;
    acceptedCandidate.value = null;
    lastPayload = null;
    pollAttempts.value = 0;
    clearObservedStatuses();
  }

  function setFlyerFile(file: File | null): void {
    if (flyerPreviewUrl.value) {
      URL.revokeObjectURL(flyerPreviewUrl.value);
      flyerPreviewUrl.value = null;
    }
    flyerFile.value = file;
    if (file) {
      try {
        flyerPreviewUrl.value = URL.createObjectURL(file);
      } catch {
        flyerPreviewUrl.value = null;
      }
      if (!flyerAssetId.value) {
        flyerAssetId.value = file.name;
      }
    }
  }

  function applyMapCenterToManualForm(): void {
    const center = options?.mapCenter;
    if (!center) {
      return;
    }
    manualForm.value.latitude = String(center.lat);
    manualForm.value.longitude = String(center.lng);
  }

  function dispose(): void {
    clearPollTimer();
  }

  return {
    stage,
    sourceKind,
    manualForm,
    urlInput,
    venueIdInput,
    venuePageUrlInput,
    flyerAssetId,
    flyerFile,
    flyerPreviewUrl,
    validationErrors,
    job,
    errorState,
    acceptedCandidate,
    pollAttempts,
    observedStatuses,
    isBusy,
    isSourceSubmittable,
    blockedReason,
    candidate,
    issues,
    evidence,
    confidenceVector,
    confidenceScore,
    currentJobStatus,
    selectSource,
    backToSource,
    backToInput,
    submitCurrent,
    retry,
    discardCandidate,
    acceptCandidate,
    applyMapCenterToManualForm,
    setFlyerFile,
    reset,
    dispose,
  };
}

export type UseIngestionWizardReturn = ReturnType<typeof useIngestionWizard>;
