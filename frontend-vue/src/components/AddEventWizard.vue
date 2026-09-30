<!--
  WEUP-SYNTH:
  source=components/AddEventModal.tsx
  destination=frontend-vue/src/components/AddEventWizard.vue
  mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
  notes=Native Vue CREATE flow. The source's multistep grammar (choice ->
    input -> processing -> location confirm -> details -> publish) is
    translated onto the real ingestion API: every stage transition is driven
    by an actual /api/ingestion/* response via useIngestionWizard. All
    simulation paths from the source are excluded: no simulated upload
    progress, no simulated link extraction, no 'error'/'duplicate' link
    trigger strings, no hardcoded candidates, no window.alert, no local
    publish list. Candidates are never written into canonical event state;
    accepting a candidate emits it to the parent for projection through the
    canonical EventDetailModal surface. G4 design tokens; terminating motion
    only, reduced-motion safe.
-->
<template>
  <q-dialog
    :model-value="modelValue"
    transition-show="slide-up"
    transition-hide="slide-down"
    @update:model-value="onDialogModelValue"
  >
    <q-card class="add-event-wizard">
      <q-card-section class="wizard-header">
        <div>
          <div class="wizard-eyebrow">Create</div>
          <div class="wizard-title">{{ stageTitle }}</div>
          <div v-if="wizard.job.value" class="wizard-jobline">
            JOB {{ wizard.job.value.id }} · {{ wizard.job.value.sourceKind }}
          </div>
        </div>
        <q-btn
          flat
          round
          dense
          icon="close"
          aria-label="Close create flow"
          class="wizard-close"
          @click="closeWizard"
        />
      </q-card-section>

      <q-separator class="wizard-separator" />

      <q-card-section class="wizard-body">
        <!-- STEP: source selection -->
        <div v-if="wizard.stage.value === 'SOURCE'" class="source-grid">
          <button
            v-for="meta in sourceMetas"
            :key="meta.kind"
            type="button"
            class="source-card"
            :class="{ 'source-card-blocked': !meta.submittable }"
            @click="wizard.selectSource(meta.kind)"
          >
            <q-icon :name="sourceIcon(meta.kind)" size="22px" />
            <span class="source-label">{{ meta.label }}</span>
            <span class="source-hint">{{ meta.hint }}</span>
            <span v-if="!meta.submittable" class="source-blocked-chip">
              Unavailable
            </span>
          </button>
        </div>

        <!-- STEP: per-kind input -->
        <div v-else-if="wizard.stage.value === 'INPUT'" class="input-stack">
          <q-banner
            v-if="!wizard.isSourceSubmittable.value"
            rounded
            class="blocked-banner"
          >
            <template #avatar>
              <q-icon name="block" color="warning" />
            </template>
            <div class="banner-title">This source is not available</div>
            <div class="banner-text">{{ wizard.blockedReason.value }}</div>
            <template #action>
              <q-btn
                flat
                dense
                label="Choose another source"
                class="banner-action"
                @click="wizard.backToSource()"
              />
            </template>
          </q-banner>

          <!-- MANUAL -->
          <div v-else-if="wizard.sourceKind.value === 'MANUAL'" class="form-grid">
            <label class="field">
              <span class="field-label">Event title *</span>
              <input
                v-model="wizard.manualForm.value.title"
                class="field-input"
                type="text"
                placeholder="UNTITLED_EVENT"
                autocomplete="off"
              />
              <span v-if="wizard.validationErrors.value.title" class="field-error">
                {{ wizard.validationErrors.value.title }}
              </span>
            </label>
            <label class="field">
              <span class="field-label">Venue name *</span>
              <input
                v-model="wizard.manualForm.value.venueName"
                class="field-input"
                type="text"
                placeholder="UNKNOWN_VENUE"
                autocomplete="off"
              />
              <span v-if="wizard.validationErrors.value.venueName" class="field-error">
                {{ wizard.validationErrors.value.venueName }}
              </span>
            </label>
            <label class="field field-full">
              <span class="field-label">Description</span>
              <textarea
                v-model="wizard.manualForm.value.description"
                class="field-input field-textarea"
                rows="2"
                placeholder="What is happening?"
              />
            </label>
            <label class="field field-full">
              <span class="field-label">Address</span>
              <input
                v-model="wizard.manualForm.value.address"
                class="field-input"
                type="text"
                placeholder="Street, Houston TX"
                autocomplete="off"
              />
            </label>
            <div class="field-row">
              <label class="field">
                <span class="field-label">Start date</span>
                <input
                  v-model="wizard.manualForm.value.startDate"
                  class="field-input"
                  type="date"
                />
              </label>
              <label class="field">
                <span class="field-label">Start time</span>
                <input
                  v-model="wizard.manualForm.value.startTime"
                  class="field-input"
                  type="time"
                />
              </label>
            </div>
            <div class="field-row">
              <label class="field">
                <span class="field-label">Category</span>
                <select
                  v-model="wizard.manualForm.value.category"
                  class="field-input"
                >
                  <option value="nightlife">NIGHTLIFE</option>
                  <option value="concert">CONCERT</option>
                  <option value="lounge">LOUNGE</option>
                  <option value="restaurant">RESTAURANT</option>
                  <option value="private">PRIVATE</option>
                  <option value="tech">TECH</option>
                </select>
              </label>
              <label class="field">
                <span class="field-label">Price tier</span>
                <select
                  v-model="wizard.manualForm.value.priceTier"
                  class="field-input"
                >
                  <option value="$">$</option>
                  <option value="$$">$$</option>
                  <option value="$$$">$$$</option>
                </select>
              </label>
            </div>
            <div class="field-row">
              <label class="field">
                <span class="field-label">Latitude</span>
                <input
                  v-model="wizard.manualForm.value.latitude"
                  class="field-input"
                  type="text"
                  inputmode="decimal"
                  placeholder="29.7604"
                  autocomplete="off"
                />
                <span v-if="wizard.validationErrors.value.latitude" class="field-error">
                  {{ wizard.validationErrors.value.latitude }}
                </span>
              </label>
              <label class="field">
                <span class="field-label">Longitude</span>
                <input
                  v-model="wizard.manualForm.value.longitude"
                  class="field-input"
                  type="text"
                  inputmode="decimal"
                  placeholder="-95.3698"
                  autocomplete="off"
                />
                <span v-if="wizard.validationErrors.value.longitude" class="field-error">
                  {{ wizard.validationErrors.value.longitude }}
                </span>
              </label>
            </div>
            <button
              type="button"
              class="ghost-button"
              :disabled="!props.mapCenter"
              @click="wizard.applyMapCenterToManualForm()"
            >
              <q-icon name="my_location" size="16px" />
              Use current map center
            </button>
            <label class="field field-full">
              <span class="field-label">Tags (comma separated)</span>
              <input
                v-model="wizard.manualForm.value.tags"
                class="field-input"
                type="text"
                placeholder="techno, warehouse, 21+"
                autocomplete="off"
              />
            </label>
          </div>

          <!-- URL -->
          <div v-else-if="wizard.sourceKind.value === 'URL'" class="form-grid">
            <label class="field field-full">
              <span class="field-label">Source URL *</span>
              <input
                v-model="wizard.urlInput.value"
                class="field-input"
                type="url"
                placeholder="https://instagram.com/p/..."
                autocomplete="off"
                spellcheck="false"
              />
              <span v-if="wizard.validationErrors.value.url" class="field-error">
                {{ wizard.validationErrors.value.url }}
              </span>
            </label>
            <p class="form-note">
              Supports Instagram, Eventbrite, RA, and Dice links. The backend
              extraction stage is currently stubbed — the job record will carry
              a STUBBED_EXTRACTION issue and a placeholder candidate.
            </p>
          </div>

          <!-- VENUE_PAGE -->
          <div v-else-if="wizard.sourceKind.value === 'VENUE_PAGE'" class="form-grid">
            <label class="field field-full">
              <span class="field-label">Venue id *</span>
              <input
                v-model="wizard.venueIdInput.value"
                class="field-input"
                type="text"
                placeholder="warehouse-houston"
                autocomplete="off"
                spellcheck="false"
              />
              <span v-if="wizard.validationErrors.value.venueId" class="field-error">
                {{ wizard.validationErrors.value.venueId }}
              </span>
            </label>
            <label class="field field-full">
              <span class="field-label">Venue page URL *</span>
              <input
                v-model="wizard.venuePageUrlInput.value"
                class="field-input"
                type="url"
                placeholder="https://venue.example.com/events"
                autocomplete="off"
                spellcheck="false"
              />
              <span v-if="wizard.validationErrors.value.pageUrl" class="field-error">
                {{ wizard.validationErrors.value.pageUrl }}
              </span>
            </label>
            <p class="form-note">
              The backend venue crawler is currently stubbed — the job record
              will carry a STUBBED_VENUE_PAGE issue and a placeholder candidate.
            </p>
          </div>

          <!-- FLYER_OCR -->
          <div v-else-if="wizard.sourceKind.value === 'FLYER_OCR'" class="form-grid">
            <label class="field field-full">
              <span class="field-label">Flyer image</span>
              <input
                ref="fileInput"
                class="field-input field-file"
                type="file"
                accept="image/*"
                @change="onFileChange"
              />
            </label>
            <div v-if="wizard.flyerPreviewUrl.value" class="flyer-preview">
              <img
                :src="wizard.flyerPreviewUrl.value"
                alt="Selected flyer preview"
                class="flyer-preview-img"
              />
            </div>
            <label class="field field-full">
              <span class="field-label">Asset id</span>
              <input
                v-model="wizard.flyerAssetId.value"
                class="field-input"
                type="text"
                placeholder="flyer asset identifier"
                autocomplete="off"
                spellcheck="false"
              />
            </label>
            <span v-if="wizard.validationErrors.value.flyer" class="field-error">
              {{ wizard.validationErrors.value.flyer }}
            </span>
            <p class="form-note">
              This phase has no binary upload endpoint; the selected file is
              previewed locally and its identifier is submitted. The backend
              OCR and normalization services are explicit stubs — the job's
              evidence carries the stub engine metadata honestly.
            </p>
          </div>

          <div class="input-actions">
            <q-btn
              flat
              label="Sources"
              icon="arrow_back"
              class="wizard-btn-secondary"
              @click="wizard.backToSource()"
            />
            <q-btn
              v-if="wizard.isSourceSubmittable.value"
              unelevated
              :label="`Submit ${submitKindLabel}`"
              icon="arrow_forward"
              class="wizard-btn-primary"
              :loading="wizard.isBusy.value"
              @click="wizard.submitCurrent()"
            />
          </div>
        </div>

        <!-- STEP: submitting -->
        <div v-else-if="wizard.stage.value === 'SUBMITTING'" class="busy-stack">
          <q-spinner size="48px" color="primary" />
          <p class="busy-label">Submitting to {{ submitEndpoint }}…</p>
        </div>

        <!-- STEP: processing (real job lifecycle) -->
        <div v-else-if="wizard.stage.value === 'PROCESSING'" class="processing-stack">
          <FlyerSkeleton
            :current-status="wizard.currentJobStatus.value"
            :observed-statuses="wizard.observedStatuses.value"
            :job-id="wizard.job.value?.id"
          />
          <p class="busy-label">
            {{ processingLabel }} · status check #{{ wizard.pollAttempts.value }}
          </p>
          <q-btn
            flat
            dense
            label="Cancel"
            class="wizard-btn-secondary"
            @click="wizard.backToInput()"
          />
        </div>

        <!-- STEP: review -->
        <div v-else-if="wizard.stage.value === 'REVIEW' && wizard.job.value" class="review-stack">
          <q-banner
            v-for="stub in stubIssues"
            :key="stub.code"
            rounded
            class="stub-banner"
          >
            <template #avatar>
              <q-icon name="warning" color="warning" />
            </template>
            <div class="banner-title">Backend stage stubbed — placeholder candidate</div>
            <div class="banner-text">{{ stub.message }}</div>
          </q-banner>

          <div class="candidate-card">
            <div class="candidate-eyebrow">Candidate · awaiting review</div>
            <h3 class="candidate-title">
              {{ wizard.candidate.value?.title || "UNTITLED_EVENT" }}
            </h3>
            <p class="candidate-venue">
              {{ wizard.candidate.value?.venueName || "UNKNOWN_VENUE" }}
            </p>
            <div class="candidate-meta">
              <div v-if="wizard.candidate.value?.address" class="meta-row">
                <q-icon name="place" size="14px" />
                <span>{{ wizard.candidate.value.address }}</span>
              </div>
              <div v-if="wizard.candidate.value?.startTime" class="meta-row">
                <q-icon name="event" size="14px" />
                <span>{{ formatDateTime(wizard.candidate.value.startTime) }}</span>
              </div>
              <div v-if="wizard.candidate.value?.category" class="meta-row">
                <q-icon name="sell" size="14px" />
                <span>{{ wizard.candidate.value.category }}</span>
              </div>
              <div
                v-if="wizard.candidate.value?.latitude != null && wizard.candidate.value?.longitude != null"
                class="meta-row"
              >
                <q-icon name="my_location" size="14px" />
                <span>
                  {{ wizard.candidate.value.latitude.toFixed(4) }},
                  {{ wizard.candidate.value.longitude.toFixed(4) }}
                </span>
              </div>
            </div>
            <p v-if="wizard.candidate.value?.description" class="candidate-description">
              {{ wizard.candidate.value.description }}
            </p>
            <div
              v-if="wizard.candidate.value?.tags?.length"
              class="candidate-tags"
            >
              <span
                v-for="tag in wizard.candidate.value.tags"
                :key="tag"
                class="candidate-tag"
              >
                {{ tag }}
              </span>
            </div>
          </div>

          <div class="confidence-block">
            <div class="confidence-head">
              <span>Confidence</span>
              <span class="confidence-overall">
                {{ confidencePercent(wizard.confidenceScore.value) }}
              </span>
            </div>
            <div v-if="wizard.confidenceVector.value" class="confidence-rows">
              <div
                v-for="dimension in confidenceDimensions"
                :key="dimension.key"
                class="confidence-row"
              >
                <span class="confidence-dim">{{ dimension.label }}</span>
                <div class="confidence-bar">
                  <div
                    class="confidence-fill"
                    :style="{ width: `${Math.round((dimension.value ?? 0) * 100)}%` }"
                  />
                </div>
                <span class="confidence-val">
                  {{ confidencePercent(dimension.value) }}
                </span>
              </div>
            </div>
            <p v-else class="form-note">No per-dimension confidence vector on this job.</p>
          </div>

          <div class="issues-block">
            <div class="block-title">
              Issues ({{ wizard.issues.value.length }})
            </div>
            <ul v-if="wizard.issues.value.length" class="issue-list">
              <li
                v-for="issue in wizard.issues.value"
                :key="issue.code"
                class="issue-item"
                :class="issue.severity === 'ERROR' ? 'issue-error' : 'issue-warning'"
              >
                <span class="issue-code">{{ issue.code }}</span>
                <span class="issue-message">{{ issue.message }}</span>
              </li>
            </ul>
            <p v-else class="form-note">No issues recorded on this job.</p>
          </div>

          <q-expansion-item
            label="Evidence"
            header-class="evidence-header"
            class="evidence-item"
          >
            <div class="evidence-body">
              <div v-if="displayEvidence" class="evidence-rows">
                <div class="evidence-row">
                  <span>Extracted by</span>
                  <span class="mono">{{ displayEvidence.extractedBy }}</span>
                </div>
                <div class="evidence-row">
                  <span>Captured at</span>
                  <span class="mono">{{ displayEvidence.capturedAt }}</span>
                </div>
                <div v-if="displayEvidence.sourceUrl" class="evidence-row">
                  <span>Source URL</span>
                  <span class="mono break">{{ displayEvidence.sourceUrl }}</span>
                </div>
                <div v-if="displayEvidence.assetId" class="evidence-row">
                  <span>Asset id</span>
                  <span class="mono">{{ displayEvidence.assetId }}</span>
                </div>
                <div v-if="displayEvidence.ocrText" class="evidence-row">
                  <span>OCR text</span>
                  <pre class="mono ocr-text">{{ displayEvidence.ocrText }}</pre>
                </div>
                <div
                  v-if="displayEvidence.processingMetadata"
                  class="evidence-row"
                >
                  <span>Processing metadata</span>
                  <pre class="mono">{{ formatMetadata(displayEvidence.processingMetadata) }}</pre>
                </div>
              </div>
              <p v-else class="form-note">No evidence attached to this job yet.</p>
              <q-btn
                v-if="isFlyerJob"
                flat
                dense
                label="Reload evidence from server"
                icon="refresh"
                class="wizard-btn-secondary"
                :loading="evidenceLoading"
                @click="reloadEvidence"
              />
            </div>
          </q-expansion-item>

          <details class="raw-candidate">
            <summary>View raw candidate JSON</summary>
            <pre class="mono">{{ formatMetadata(wizard.candidate.value ?? {}) }}</pre>
          </details>

          <p class="form-note">
            Accepting opens the candidate in the canonical event surface for
            review. The candidate is not published — no backend publish path
            exists yet, and candidates never enter canonical event state.
          </p>

          <div class="input-actions">
            <q-btn
              flat
              label="Discard"
              class="wizard-btn-secondary"
              @click="onDiscard"
            />
            <q-btn
              unelevated
              label="Accept candidate"
              icon="check"
              class="wizard-btn-primary"
              :disable="!wizard.candidate.value"
              @click="onAccept"
            />
          </div>
        </div>

        <!-- STEP: error -->
        <div v-else-if="wizard.stage.value === 'ERROR' && wizard.errorState.value" class="error-stack">
          <div class="error-icon">
            <q-icon name="error_outline" size="40px" color="negative" />
          </div>
          <h3 class="error-title">{{ wizard.errorState.value.title }}</h3>
          <p class="error-message">{{ wizard.errorState.value.message }}</p>
          <div class="input-actions">
            <q-btn
              flat
              label="Edit input"
              class="wizard-btn-secondary"
              @click="wizard.backToInput()"
            />
            <q-btn
              v-if="wizard.errorState.value.retryable"
              unelevated
              label="Retry"
              icon="refresh"
              class="wizard-btn-primary"
              @click="wizard.retry()"
            />
          </div>
        </div>
      </q-card-section>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from "vue";
import { useQuasar } from "quasar";
import FlyerSkeleton from "./FlyerSkeleton.vue";
import {
  useIngestionWizard,
  type AcceptedIngestionCandidate,
} from "../composables/useIngestionWizard";
import {
  INGESTION_SOURCE_METAS,
  ingestionStatusLabel,
  type CanonicalSourceEvidence,
  type FlyerEvidenceDto,
  type IngestionSourceKind,
} from "../contracts/ingestion.contracts";
import {
  getFlyerEvidence,
  IngestionApiRequestError,
} from "../services/ingestionService";

const props = defineProps<{
  modelValue: boolean;
  mapCenter?: { lat: number; lng: number } | null;
}>();

const emit = defineEmits<{
  (event: "update:modelValue", value: boolean): void;
  (event: "close"): void;
  (event: "candidate-accepted", payload: AcceptedIngestionCandidate): void;
}>();

const $q = useQuasar();
const wizard = useIngestionWizard({ mapCenter: props.mapCenter ?? null });
const serverEvidence = ref<FlyerEvidenceDto | null>(null);
const evidenceLoading = ref(false);

const sourceMetas = INGESTION_SOURCE_METAS;

function sourceIcon(kind: IngestionSourceKind): string {
  switch (kind) {
    case "MANUAL":
      return "edit";
    case "URL":
      return "link";
    case "VENUE_PAGE":
      return "storefront";
    case "FLYER_OCR":
      return "image";
    case "EXTERNAL_FEED":
      return "rss_feed";
  }
}

const stageTitle = computed(() => {
  switch (wizard.stage.value) {
    case "SOURCE":
      return "Add Event";
    case "INPUT":
      return wizard.sourceKind.value
        ? `${sourceMetas.find((meta) => meta.kind === wizard.sourceKind.value)?.label} ingestion`
        : "Add Event";
    case "SUBMITTING":
      return "Submitting";
    case "PROCESSING":
      return "Processing";
    case "REVIEW":
      return "Review candidate";
    case "ERROR":
      return "Ingestion failed";
  }
});

const submitKindLabel = computed(() => {
  const kind = wizard.sourceKind.value;
  if (kind === "MANUAL") return "manual";
  if (kind === "URL") return "URL";
  if (kind === "VENUE_PAGE") return "venue page";
  if (kind === "FLYER_OCR") return "flyer";
  return "ingestion";
});

const submitEndpoint = computed(() => {
  switch (wizard.sourceKind.value) {
    case "MANUAL":
      return "/api/ingestion/manual";
    case "URL":
      return "/api/ingestion/url";
    case "VENUE_PAGE":
      return "/api/ingestion/venue-page";
    case "FLYER_OCR":
      return "/api/ingestion/flyers";
    default:
      return "/api/ingestion";
  }
});

const processingLabel = computed(() => {
  const status = wizard.currentJobStatus.value;
  return status ? ingestionStatusLabel(status) : "Starting";
});

const stubIssues = computed(() =>
  wizard.issues.value.filter((issue) => issue.code.startsWith("STUBBED")),
);

const isFlyerJob = computed(
  () => wizard.job.value?.sourceKind === "FLYER_OCR",
);

const displayEvidence = computed<CanonicalSourceEvidence | null>(
  () => serverEvidence.value?.evidence ?? wizard.evidence.value,
);

const confidenceDimensions = computed(() => {
  const vector = wizard.confidenceVector.value;
  if (!vector) {
    return [];
  }
  return [
    { key: "extraction", label: "Extraction", value: vector.extraction },
    { key: "temporal", label: "Temporal", value: vector.temporal },
    { key: "venueMatch", label: "Venue match", value: vector.venueMatch },
    { key: "geocode", label: "Geocode", value: vector.geocode },
  ] as const;
});

function confidencePercent(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) {
    return "—";
  }
  return `${Math.round(value * 100)}%`;
}

/** Deterministic Houston-time rendering for candidate timestamps (§17). */
function formatDateTime(iso: string | null | undefined): string {
  if (!iso) {
    return "—";
  }
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return iso;
  }
  return new Intl.DateTimeFormat("en-US", {
    timeZone: "America/Chicago",
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(date);
}

function formatMetadata(value: unknown): string {
  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return String(value);
  }
}

function onFileChange(event: Event): void {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0] ?? null;
  wizard.setFlyerFile(file);
}

async function reloadEvidence(): Promise<void> {
  const jobId = wizard.job.value?.id;
  if (!jobId) {
    return;
  }
  evidenceLoading.value = true;
  try {
    serverEvidence.value = await getFlyerEvidence(jobId);
  } catch (cause) {
    $q.notify({
      type: "negative",
      message:
        cause instanceof IngestionApiRequestError
          ? `Evidence request failed (${cause.status}).`
          : "Evidence request failed.",
    });
  } finally {
    evidenceLoading.value = false;
  }
}

function onAccept(): void {
  const accepted = wizard.acceptCandidate();
  if (!accepted) {
    return;
  }
  $q.notify({
    type: "positive",
    message: "Candidate accepted — opening the preview (not a published event).",
  });
  emit("candidate-accepted", accepted);
  closeWizard();
}

function onDiscard(): void {
  wizard.discardCandidate();
  serverEvidence.value = null;
  $q.notify({
    type: "info",
    message: "Candidate discarded — the backend job record is unchanged.",
  });
}

function closeWizard(): void {
  emit("update:modelValue", false);
}

function onDialogModelValue(value: boolean): void {
  emit("update:modelValue", value);
  if (!value) {
    wizard.dispose();
    serverEvidence.value = null;
    emit("close");
  }
}

// Opening the dialog always starts a fresh wizard run; the backend job
// records persist server-side and remain retrievable via their job ids.
watch(
  () => props.modelValue,
  (open) => {
    if (open) {
      serverEvidence.value = null;
      wizard.reset();
    }
  },
);

onBeforeUnmount(() => {
  wizard.dispose();
});
</script>

<style scoped>
/* G4 tokens: dark substrate #050505, accent #00FF9C, subtle borders,
   glass primitives. One responsive composition; motion terminates and
   respects reduced-motion (mission sections 18, 19, 20). */
.add-event-wizard {
  width: min(560px, calc(100vw - 24px));
  max-height: min(92vh, 900px);
  display: flex;
  flex-direction: column;
  background: rgba(8, 8, 8, 0.96);
  backdrop-filter: blur(24px);
  -webkit-backdrop-filter: blur(24px);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 24px;
  color: #fff;
  overflow: hidden;
}

.wizard-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  padding: 20px 20px 16px;
}

.wizard-eyebrow {
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.2em;
  text-transform: uppercase;
  color: #00ff9c;
}

.wizard-title {
  margin-top: 4px;
  font-size: 20px;
  font-weight: 800;
  letter-spacing: -0.01em;
}

.wizard-jobline {
  margin-top: 6px;
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 10px;
  letter-spacing: 0.14em;
  color: rgba(255, 255, 255, 0.4);
  word-break: break-all;
}

.wizard-close {
  color: rgba(255, 255, 255, 0.5);
}

.wizard-separator {
  background: rgba(255, 255, 255, 0.08);
}

.wizard-body {
  overflow-y: auto;
  padding: 20px;
}

.source-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.source-card {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 8px;
  padding: 18px 16px;
  background: rgba(255, 255, 255, 0.04);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 18px;
  color: #fff;
  cursor: pointer;
  text-align: left;
  transition:
    background-color 0.2s ease,
    border-color 0.2s ease,
    transform 0.15s ease;
}

.source-card:hover {
  background: rgba(255, 255, 255, 0.08);
  border-color: rgba(0, 255, 156, 0.4);
  transform: translateY(-1px);
}

.source-card .q-icon {
  color: #00ff9c;
}

.source-label {
  font-size: 13px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.source-hint {
  font-size: 11px;
  line-height: 1.5;
  color: rgba(255, 255, 255, 0.5);
}

.source-card-blocked {
  opacity: 0.65;
}

.source-card-blocked .q-icon {
  color: #fbbf24;
}

.source-blocked-chip {
  margin-top: 4px;
  padding: 3px 10px;
  border-radius: 999px;
  border: 1px solid rgba(251, 191, 36, 0.4);
  background: rgba(251, 191, 36, 0.1);
  color: #fbbf24;
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 9px;
  letter-spacing: 0.18em;
  text-transform: uppercase;
}

.input-stack {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.form-grid {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
}

.field-full {
  grid-column: 1 / -1;
}

.field-label {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 10px;
  letter-spacing: 0.18em;
  text-transform: uppercase;
  color: rgba(255, 255, 255, 0.45);
}

.field-input {
  width: 100%;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: 12px;
  padding: 12px 14px;
  color: #fff;
  font-size: 14px;
  outline: none;
  transition: border-color 0.2s ease;
  color-scheme: dark;
}

.field-input:focus {
  border-color: rgba(0, 255, 156, 0.6);
}

.field-input::placeholder {
  color: rgba(255, 255, 255, 0.25);
}

.field-textarea {
  resize: vertical;
  min-height: 56px;
  font-family: inherit;
}

.field-file {
  padding: 10px 12px;
}

.field-error {
  font-size: 12px;
  color: #f87171;
}

.field-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.form-note {
  font-size: 12px;
  line-height: 1.6;
  color: rgba(255, 255, 255, 0.5);
  font-style: italic;
  margin: 0;
}

.ghost-button {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  align-self: flex-start;
  padding: 10px 16px;
  border-radius: 999px;
  border: 1px solid rgba(255, 255, 255, 0.16);
  background: rgba(255, 255, 255, 0.05);
  color: rgba(255, 255, 255, 0.8);
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  cursor: pointer;
  transition:
    border-color 0.2s ease,
    color 0.2s ease;
}

.ghost-button:hover:not(:disabled) {
  border-color: rgba(0, 255, 156, 0.5);
  color: #00ff9c;
}

.ghost-button:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.flyer-preview {
  border-radius: 16px;
  overflow: hidden;
  border: 1px solid rgba(255, 255, 255, 0.1);
  max-height: 220px;
}

.flyer-preview-img {
  width: 100%;
  height: auto;
  max-height: 220px;
  object-fit: cover;
  display: block;
}

.input-actions {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  margin-top: 4px;
}

.wizard-btn-primary {
  background: #00ff9c;
  color: #000;
  font-weight: 800;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  border-radius: 14px;
  padding: 10px 20px;
}

.wizard-btn-secondary {
  color: rgba(255, 255, 255, 0.7);
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  border-radius: 14px;
}

.blocked-banner,
.stub-banner {
  background: rgba(251, 191, 36, 0.08);
  border: 1px solid rgba(251, 191, 36, 0.25);
  color: #fff;
}

.banner-title {
  font-size: 13px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.banner-text {
  font-size: 12px;
  line-height: 1.6;
  color: rgba(255, 255, 255, 0.65);
}

.banner-action {
  color: #00ff9c;
  font-weight: 700;
}

.busy-stack {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 18px;
  padding: 48px 0;
}

.busy-label {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 11px;
  letter-spacing: 0.2em;
  text-transform: uppercase;
  color: rgba(255, 255, 255, 0.5);
  text-align: center;
  margin: 0;
}

.processing-stack {
  display: flex;
  flex-direction: column;
  gap: 18px;
  align-items: center;
}

.processing-stack .busy-label {
  width: 100%;
}

.review-stack {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.candidate-card {
  background: rgba(255, 255, 255, 0.04);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 18px;
  padding: 18px;
}

.candidate-eyebrow {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 10px;
  letter-spacing: 0.2em;
  text-transform: uppercase;
  color: #00ff9c;
}

.candidate-title {
  margin: 8px 0 2px;
  font-size: 20px;
  font-weight: 800;
  letter-spacing: -0.01em;
}

.candidate-venue {
  margin: 0;
  font-size: 13px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: rgba(255, 255, 255, 0.65);
}

.candidate-meta {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-top: 14px;
}

.meta-row {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 13px;
  color: rgba(255, 255, 255, 0.8);
}

.meta-row .q-icon {
  color: #00ff9c;
  flex-shrink: 0;
}

.candidate-description {
  margin: 14px 0 0;
  font-size: 13px;
  line-height: 1.6;
  color: rgba(255, 255, 255, 0.7);
}

.candidate-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 12px;
}

.candidate-tag {
  padding: 4px 12px;
  border-radius: 999px;
  border: 1px solid rgba(0, 255, 156, 0.3);
  background: rgba(0, 255, 156, 0.08);
  color: #00ff9c;
  font-size: 11px;
  font-weight: 700;
}

.confidence-block {
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 16px;
  padding: 16px;
}

.confidence-head {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 11px;
  letter-spacing: 0.18em;
  text-transform: uppercase;
  color: rgba(255, 255, 255, 0.55);
}

.confidence-overall {
  color: #00ff9c;
  font-size: 16px;
  font-weight: 800;
}

.confidence-rows {
  display: flex;
  flex-direction: column;
  gap: 10px;
  margin-top: 12px;
}

.confidence-row {
  display: grid;
  grid-template-columns: 96px 1fr 48px;
  align-items: center;
  gap: 10px;
  font-size: 12px;
}

.confidence-dim {
  color: rgba(255, 255, 255, 0.6);
  text-transform: uppercase;
  letter-spacing: 0.06em;
  font-size: 10px;
}

.confidence-bar {
  height: 6px;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.08);
  overflow: hidden;
}

.confidence-fill {
  height: 100%;
  background: #00ff9c;
  border-radius: 999px;
  transition: width 0.4s ease;
}

.confidence-val {
  text-align: right;
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  color: rgba(255, 255, 255, 0.7);
}

.issues-block .block-title {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 11px;
  letter-spacing: 0.18em;
  text-transform: uppercase;
  color: rgba(255, 255, 255, 0.55);
  margin-bottom: 10px;
}

.issue-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.issue-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 12px 14px;
  border-radius: 12px;
  border: 1px solid;
  font-size: 12px;
}

.issue-warning {
  border-color: rgba(251, 191, 36, 0.35);
  background: rgba(251, 191, 36, 0.07);
}

.issue-error {
  border-color: rgba(248, 113, 113, 0.4);
  background: rgba(248, 113, 113, 0.08);
}

.issue-code {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 10px;
  letter-spacing: 0.14em;
}

.issue-warning .issue-code {
  color: #fbbf24;
}

.issue-error .issue-code {
  color: #f87171;
}

.issue-message {
  color: rgba(255, 255, 255, 0.75);
  line-height: 1.5;
}

.evidence-item {
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 16px;
  background: rgba(255, 255, 255, 0.03);
}

.evidence-body {
  padding: 4px 16px 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.evidence-rows {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.evidence-row {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 12px;
  color: rgba(255, 255, 255, 0.5);
}

.mono {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 11px;
  color: rgba(255, 255, 255, 0.8);
  word-break: break-word;
}

.mono.break {
  word-break: break-all;
}

.ocr-text {
  white-space: pre-wrap;
  background: rgba(0, 0, 0, 0.4);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 10px;
  padding: 10px;
  max-height: 160px;
  overflow-y: auto;
  margin: 0;
}

.raw-candidate {
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  padding: 10px 14px;
  font-size: 12px;
  color: rgba(255, 255, 255, 0.6);
}

.raw-candidate summary {
  cursor: pointer;
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 10px;
  letter-spacing: 0.16em;
  text-transform: uppercase;
}

.raw-candidate pre {
  margin: 10px 0 0;
  max-height: 200px;
  overflow-y: auto;
}

.error-stack {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
  text-align: center;
  padding: 24px 0;
}

.error-title {
  margin: 0;
  font-size: 18px;
  font-weight: 800;
}

.error-message {
  margin: 0;
  font-size: 13px;
  line-height: 1.6;
  color: rgba(255, 255, 255, 0.6);
  word-break: break-word;
}

.error-stack .input-actions {
  width: 100%;
  justify-content: center;
  margin-top: 8px;
}

@media (max-width: 480px) {
  .source-grid {
    grid-template-columns: 1fr;
  }

  .field-row {
    grid-template-columns: 1fr;
  }

  .confidence-row {
    grid-template-columns: 84px 1fr 44px;
  }
}

@media (prefers-reduced-motion: reduce) {
  .source-card,
  .field-input,
  .ghost-button,
  .confidence-fill {
    transition: none;
  }

  .source-card:hover {
    transform: none;
  }
}
</style>
