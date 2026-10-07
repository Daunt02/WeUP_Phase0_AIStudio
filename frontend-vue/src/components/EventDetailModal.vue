<!--
WEUP-SYNTH:
sources=[
  components/EventClient.tsx,
  components/DynamicEvent.tsx,
  components/EventSignalModal.tsx,
  components/MediaFlyerCard.tsx,
  components/ReceiptDrawer.tsx
]
destination=frontend-vue/src/components/EventDetailModal.vue
mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
notes=Canonical event-detail surface, extended with the AI Studio event
grammar: MediaFlyerCard hero grammar (aspect/caption/scrim, AWAITING SIGNAL
overlay for NEEDS_REVIEW), EventSignalModal signal states as deterministic
chips cited to canonical fields (AI insight prose BLOCKED_WITH_REASON; fake
checkout/door codes excluded), DynamicEvent signal-lost grammar, real
directions deeplink, session-state chip, temporal context from the temporal
singleton, and the ReceiptDrawer replaced by the first-class
ProvenanceDrawer (mission section 22 chain; stubbed stages PENDING).
-->
<template>
  <q-dialog
    :model-value="modelValue"
    :maximized="isCompact && props.composition !== 'tablet'"
    transition-show="slide-up"
    transition-hide="slide-down"
    @update:model-value="onDialogModelValue"
  >
    <q-card
      class="event-detail-modal"
      data-plane="z3"
      :data-state="isLoading ? 'visible' : 'expanded'"
      :data-composition="props.composition ?? undefined"
    >
      <q-card-section class="modal-header">
        <div>
          <div class="eyebrow">Event Detail</div>
          <div class="modal-title">
            {{ event?.title ?? "Loading event" }}
          </div>
        </div>

        <q-btn
          flat
          round
          dense
          icon="close"
          aria-label="Close event detail"
          @click="closeModal"
        />
      </q-card-section>

      <q-separator />

      <q-card-section class="modal-body">
        <div v-if="isLoading" class="modal-stack">
          <q-skeleton class="hero-skeleton" />
          <q-skeleton type="text" width="60%" />
          <q-skeleton type="text" width="80%" />
          <q-skeleton type="rect" height="96px" />
        </div>

        <q-banner v-else-if="error" rounded class="error-banner">
          {{ error }}
        </q-banner>

        <div v-else-if="event" class="modal-stack">
          <div class="hero-layout">
            <div class="flyer-media">
              <q-img
                v-if="heroImageUrl"
                :src="heroImageUrl"
                :alt="`${event.title} flyer`"
                class="flyer-image"
                fit="cover"
              />
              <div v-else class="hero-placeholder">Flyer unavailable</div>

              <div v-if="heroImageUrl" class="flyer-scrim" />
              <div v-if="heroImageUrl && flyerCaption" class="flyer-caption">
                {{ flyerCaption }}
              </div>

              <div v-if="isAwaitingSignal" class="awaiting-signal-overlay">
                <div class="awaiting-signal-copy">
                  <div class="awaiting-signal-pill">Awaiting signal</div>
                  <div class="awaiting-signal-sub">
                    Media is under review
                  </div>
                </div>
              </div>
            </div>

            <div class="hero-copy">
              <div class="meta-row">
                <q-badge color="primary" outline>
                  {{ event.status }}
                </q-badge>
                <q-badge color="secondary" outline>
                  {{ primaryCategory }}
                </q-badge>
                <q-badge
                  v-if="sessionKind"
                  color="accent"
                  outline
                  :title="`Save session: ${sessionKind}`"
                >
                  {{ sessionKind }}
                </q-badge>
              </div>

              <div class="summary-line">
                <q-icon name="schedule" size="18px" />
                <span>{{ formattedDateTime }}</span>
              </div>

              <div class="summary-line">
                <q-icon name="location_on" size="18px" />
                <div>
                  <div class="venue-name">{{ event.venueName }}</div>
                  <div class="address-copy">{{ event.address }}</div>
                </div>
              </div>

              <div v-if="displayTags.length > 0" class="chip-wrap">
                <q-chip
                  v-for="tag in displayTags"
                  :key="tag"
                  dense
                  outline
                  color="dark"
                >
                  {{ tag }}
                </q-chip>
              </div>

              <p v-if="event.description" class="description-copy">
                {{ event.description }}
              </p>
            </div>
          </div>

          <div v-if="mediaThumbs.length > 0" class="media-strip">
            <div
              v-for="media in mediaThumbs"
              :key="media.url"
              class="media-thumb"
            >
              <q-img :src="media.url" :alt="media.kind" fit="cover" class="media-thumb-img" />
              <span class="media-thumb-kind mono">{{ media.kind }}</span>
            </div>
          </div>

          <EventSignalChips
            :event="event"
            :session-kind="sessionKind"
            :temporal="temporalSignalContext"
            :visible-event-count="visibleEventCount"
          />

          <q-banner v-if="temporalSignalContext" rounded class="temporal-banner">
            <div class="provenance-title">Temporal context</div>
            <div>
              Viewing in {{ temporalSignalContext.mode
              }}{{
                temporalSignalContext.dayLabel
                  ? ` · ${temporalSignalContext.dayLabel}`
                  : ""
              }}
              — {{ visibleEventCount }} event{{
                visibleEventCount === 1 ? "" : "s"
              }}
              in the current window.
            </div>
            <div v-if="temporalSignalContext.overlapNote" class="temporal-note">
              00:00–03:00 Houston overlap: the TODAY pill and the backend
              Tonight window can name different spans in this window.
            </div>
          </q-banner>

          <q-banner rounded class="provenance-banner">
            <div class="provenance-title">Source summary</div>
            <div>{{ event.provenanceSummary.summaryLabel }}</div>
            <div class="provenance-meta">
              Primary source: {{ event.provenanceSummary.primarySourceKind }} •
              {{ event.provenanceSummary.sourceCount }} source{{
                event.provenanceSummary.sourceCount === 1 ? "" : "s"
              }}
            </div>
            <div class="provenance-actions">
              <q-btn
                outline
                dense
                color="primary"
                icon="account_tree"
                label="View provenance"
                @click="openProvenanceDrawer"
              />
            </div>
          </q-banner>
        </div>

        <div v-else class="signal-lost">
          <q-icon name="signal_wifi_off" size="48px" class="signal-lost-icon" />
          <div class="signal-lost-title">Signal Lost</div>
          <p class="signal-lost-copy">
            The requested event signal could not be located in the current
            sector. It may have been decommissioned or moved to a restricted
            frequency.
          </p>
          <q-btn
            color="primary"
            label="Return to radar"
            icon="arrow_back"
            @click="closeModal"
          />
        </div>
      </q-card-section>

      <q-separator />

      <q-card-actions class="modal-actions" align="between">
        <div v-if="event" class="footer-caption">
          Updated from canonical normalized data only.
        </div>

        <div class="action-row">
          <q-btn
            outline
            color="primary"
            icon="share"
            label="Share"
            @click="$emit('share')"
          />
          <q-btn
            v-if="directionsUrl"
            outline
            color="primary"
            icon="directions"
            label="Directions"
            @click="openDirections"
          />
          <q-btn
            :color="event?.savedByCurrentUser ? 'accent' : 'primary'"
            :outline="!event?.savedByCurrentUser"
            :loading="isSavePending"
            :disable="!event"
            :icon="event?.savedByCurrentUser ? 'bookmark' : 'bookmark_add'"
            :label="event?.savedByCurrentUser ? 'Saved' : 'Save'"
            @click="$emit('toggle-save')"
          />
          <!-- G9 (SocialSignalPanel): prototype social layer, bound to the
               selected event's canonical summary. Visible only once saved,
               mirroring the source's "Interest Saved" entry affordance. -->
          <q-btn
            v-if="event?.savedByCurrentUser"
            outline
            color="accent"
            icon="groups"
            label="Social"
            @click="socialPanelOpen = true"
          />
        </div>
      </q-card-actions>
    </q-card>
  </q-dialog>

  <ProvenanceDrawer
    :is-open="provenanceDrawer.isOpen.value"
    :active-event-id="provenanceDrawer.activeEvent.value?.id ?? null"
    :stages="provenanceDrawer.stages.value"
    :is-loading-job="provenanceDrawer.isLoadingJob.value"
    :job-error="provenanceDrawer.jobError.value"
    @update:is-open="onProvenanceDrawerModelValue"
  />

  <SocialSignalPanel
    :is-open="socialPanelOpen"
    :event-summary="socialPanelSummary"
    @close="socialPanelOpen = false"
  />
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useQuasar } from "quasar";
import type {
  EventDetailDto,
  SaveSessionKind,
} from "../contracts/event-detail.contracts";
import {
  formatHoustonDayLabel,
  houstonDayBoundsUtc,
  houstonHourOfDay,
} from "../utils/houstonTime";
import { useDiscoveryState } from "../composables/useDiscoveryState";
import { useTemporalNavigation } from "../composables/useTemporalNavigation";
import { useProvenanceDrawer } from "../composables/useProvenanceDrawer";
import type { CompositionKind } from "../composables/useInteractionStateMachine";
import EventSignalChips, {
  type TemporalSignalContext,
} from "./EventSignalChips.vue";
import ProvenanceDrawer from "./ProvenanceDrawer.vue";
import SocialSignalPanel, {
  type SocialPanelEventSummary,
} from "./SocialSignalPanel.vue";

const props = withDefaults(
  defineProps<{
    modelValue: boolean;
    event: EventDetailDto | null;
    isLoading: boolean;
    isSavePending: boolean;
    error: string | null;
    sessionKind?: SaveSessionKind | null;
    /**
     * G11 (Create): explicit ingestion job id to link when the modal renders
     * an accepted candidate preview. The provenance drawer then loads the
     * real job record — never a fabricated link.
     */
    provenanceJobId?: string | null;
    /**
     * WEUP-2.5D (D16): optional composition hint from the shell's interaction
     * state machine. mobile → maximized bottom flow (default); tablet/
     * desktop/wide → right-docked Z3 inspector panel. Null preserves the
     * legacy isCompact behavior.
     */
    composition?: CompositionKind | null;
  }>(),
  { sessionKind: null, provenanceJobId: null, composition: null },
);

const emit = defineEmits<{
  (event: "update:modelValue", value: boolean): void;
  (event: "toggle-save"): void;
  (event: "share"): void;
}>();

const $q = useQuasar();

// G8 consumes the canonical temporal singleton and the shared selection's
// visible event set. No duplicate preset/day state is created here.
const temporal = useTemporalNavigation();
const discovery = useDiscoveryState();
const provenanceDrawer = useProvenanceDrawer();

// G9 (SocialSignalPanel): prototype social layer bound to the canonical
// selected event. The panel reads its own session-only store; it never
// touches the canonical saved state or the network.
const socialPanelOpen = ref(false);

const socialPanelSummary = computed<SocialPanelEventSummary | null>(() => {
  const detail = props.event;
  if (!detail) {
    return null;
  }

  return {
    id: detail.id,
    title: detail.title,
    venueName: detail.venueName,
    district: null,
    startUtc: detail.startUtc,
    flyerUrl:
      detail.flyerImageUrl ??
      detail.mediaRefs.find((media) => media.kind === "poster")?.url ??
      detail.mediaRefs.find((media) => media.kind === "image")?.url ??
      null,
  };
});

const isCompact = computed(() => $q.screen.lt.md);

const heroImageUrl = computed(() => {
  if (!props.event) {
    return null;
  }

  return (
    props.event.flyerImageUrl ??
    props.event.mediaRefs.find((item) => item.kind === "poster")?.url ??
    props.event.mediaRefs.find((item) => item.kind === "image")?.url ??
    null
  );
});

const primaryFlyer = computed(() => {
  if (!props.event) {
    return null;
  }

  return (
    props.event.flyerImageUrl ??
    props.event.mediaRefs.find((item) => item.kind === "poster")?.url ??
    null
  );
});

const flyerCaption = computed(() => {
  if (!props.event) {
    return null;
  }

  const poster = props.event.mediaRefs.find((item) => item.kind === "poster");
  if (poster) {
    return "Event flyer";
  }

  const image = props.event.mediaRefs.find((item) => item.kind === "image");
  if (image) {
    return "Event image";
  }

  return props.event.flyerImageUrl ? "Event flyer" : null;
});

const isAwaitingSignal = computed(() => {
  return props.event?.status === "NEEDS_REVIEW";
});

const mediaThumbs = computed(() => {
  if (!props.event) {
    return [];
  }

  const primary = primaryFlyer.value;
  return props.event.mediaRefs
    .filter((media) => media.url !== primary)
    .slice(0, 4);
});

const primaryCategory = computed(() => {
  if (!props.event) {
    return "Uncategorized";
  }

  return props.event.categories[0] ?? props.event.category;
});

const displayTags = computed(() => {
  if (!props.event) {
    return [];
  }

  return props.event.tags.slice(0, 6);
});

const formattedDateTime = computed(() => {
  if (!props.event) {
    return "";
  }

  const start = new Date(props.event.startUtc);
  const end = props.event.endUtc ? new Date(props.event.endUtc) : null;
  const dateFormatter = new Intl.DateTimeFormat("en-US", {
    weekday: "short",
    month: "short",
    day: "numeric",
    timeZone: props.event.timezone,
  });
  const timeFormatter = new Intl.DateTimeFormat("en-US", {
    hour: "numeric",
    minute: "2-digit",
    timeZone: props.event.timezone,
  });

  const dateLabel = dateFormatter.format(start);
  const startLabel = timeFormatter.format(start);
  const endLabel = end ? timeFormatter.format(end) : null;

  return endLabel
    ? `${dateLabel} • ${startLabel} - ${endLabel}`
    : `${dateLabel} • ${startLabel}`;
});

const temporalSignalContext = computed<TemporalSignalContext | null>(() => {
  if (!props.event) {
    return null;
  }

  const dayKey = temporal.activeDayKey.value;
  const dayLabel = dayKey
    ? formatHoustonDayLabel(
        new Date(houstonDayBoundsUtc(dayKey).startUtc),
        props.event.timezone,
      )
    : null;

  return {
    mode: temporal.activeTemporalMode.value,
    dayLabel,
    // G7-flagged naming overlap: TODAY at 00:00–03:00 Houston selects the
    // calendar day that just started while the backend Tonight window still
    // resolves the prior evening's span.
    overlapNote: houstonHourOfDay(new Date()) < 3,
  };
});

const visibleEventCount = computed(() => {
  return discovery.visibleEventIds.value.size;
});

const directionsUrl = computed(() => {
  if (!props.event) {
    return null;
  }

  return `https://www.google.com/maps/dir/?api=1&destination=${props.event.lat},${props.event.lng}`;
});

function onDialogModelValue(value: boolean): void {
  if (!value) {
    provenanceDrawer.closeDrawer();
    // G9: dismiss the prototype social panel with the modal (see closeModal).
    socialPanelOpen.value = false;
  }
  emit("update:modelValue", value);
}

function closeModal(): void {
  provenanceDrawer.closeDrawer();
  // G9: the prototype social panel is bound to this event; closing the modal
  // dismisses it too (projection-only overlay, no state side effects).
  socialPanelOpen.value = false;
  emit("update:modelValue", false);
}

function openDirections(): void {
  if (!directionsUrl.value || typeof window === "undefined") {
    return;
  }

  window.open(directionsUrl.value, "_blank", "noopener,noreferrer");
}

function openProvenanceDrawer(): void {
  if (!props.event) {
    return;
  }

  provenanceDrawer.openDrawer(
    props.event,
    props.provenanceJobId ? { jobId: props.provenanceJobId } : undefined,
  );
}

function onProvenanceDrawerModelValue(value: boolean): void {
  if (!value) {
    provenanceDrawer.closeDrawer();
  }
}
</script>

<style scoped>
.event-detail-modal {
  width: min(720px, calc(100vw - 24px));
  max-width: 720px;
  max-height: min(90vh, 880px);
  border-radius: 24px;
  overflow: hidden;
}

/* WEUP-2.5D (D15/D14): tablet/desktop/wide compositions dock the Z3 inspector
   as a right-side panel instead of a centered dialog. margin-left: auto
   pushes the card to the right edge of the q-dialog flex inner — no Quasar
   internals are overridden. Mobile (maximized) keeps the slide-up bottom
   flow via the existing rule below. Every value tokenized. */
.event-detail-modal[data-composition="tablet"],
.event-detail-modal[data-composition="desktop"],
.event-detail-modal[data-composition="wide"] {
  margin-left: auto;
  width: min(420px, 92vw);
  max-width: 420px;
  height: 100vh;
  max-height: 100vh;
  border-radius: var(--weup-radius-surface) 0 0 var(--weup-radius-surface);
  box-shadow: var(--weup-elevation-4);
}

.modal-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  background: linear-gradient(135deg, #f7fbff 0%, #f5f1ea 100%);
}

.eyebrow {
  font-size: 0.75rem;
  font-weight: 700;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  color: #6b7280;
}

.modal-title {
  margin-top: 4px;
  font-size: 1.25rem;
  font-weight: 700;
  line-height: 1.2;
  color: #111827;
}

.modal-body {
  overflow-y: auto;
  padding: 20px;
}

.modal-stack {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.hero-layout {
  display: grid;
  grid-template-columns: 220px minmax(0, 1fr);
  gap: 18px;
  align-items: start;
}

/* MediaFlyerCard grammar: aspect-enforced flyer block with scrim, caption,
   and the AWAITING SIGNAL overlay for pending media policy. */
.flyer-media {
  position: relative;
  width: 100%;
  aspect-ratio: 4 / 5;
  min-height: 280px;
  border-radius: 18px;
  overflow: hidden;
  background: #0a0a0a;
  border: 1px solid rgba(255, 255, 255, 0.05);
}

.flyer-image {
  width: 100%;
  height: 100%;
}

.flyer-scrim {
  position: absolute;
  inset-inline: 0;
  bottom: 0;
  height: 40%;
  background: linear-gradient(to top, rgba(0, 0, 0, 0.6), transparent);
  pointer-events: none;
}

.flyer-caption {
  position: absolute;
  inset-inline: 0;
  bottom: 0;
  padding: 12px;
  font-size: 0.8rem;
  font-weight: 600;
  color: #ffffff;
  pointer-events: none;
}

.awaiting-signal-overlay {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(12px);
  -webkit-backdrop-filter: blur(12px);
  text-align: center;
  padding: 16px;
}

.awaiting-signal-copy {
  display: flex;
  flex-direction: column;
  gap: 8px;
  align-items: center;
}

.awaiting-signal-pill {
  font-size: 0.65rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.3em;
  color: #00ff9c;
  background: rgba(0, 255, 156, 0.1);
  border: 1px solid rgba(0, 255, 156, 0.2);
  padding: 6px 12px;
  border-radius: 12px;
}

.awaiting-signal-sub {
  font-size: 0.65rem;
  letter-spacing: 0.15em;
  text-transform: uppercase;
  color: rgba(255, 255, 255, 0.4);
}

.hero-skeleton {
  min-height: 280px;
  border-radius: 18px;
}

.hero-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
  background:
    radial-gradient(circle at top, rgba(37, 99, 235, 0.12), transparent 55%),
    #eef2f7;
  color: #6b7280;
  font-weight: 600;
}

.media-strip {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}

.media-thumb {
  position: relative;
  width: 96px;
  height: 96px;
  border-radius: 12px;
  overflow: hidden;
  border: 1px solid rgba(255, 255, 255, 0.08);
  background: #0a0a0a;
}

.media-thumb-img {
  width: 100%;
  height: 100%;
}

.media-thumb-kind {
  position: absolute;
  left: 6px;
  bottom: 6px;
  font-size: 0.6rem;
  color: #ffffff;
  background: rgba(0, 0, 0, 0.6);
  padding: 2px 6px;
  border-radius: 6px;
}

.mono {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
}

.hero-copy {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.meta-row,
.summary-line,
.action-row,
.chip-wrap {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}

.summary-line {
  align-items: flex-start;
  color: #1f2937;
}

.venue-name {
  font-weight: 700;
}

.address-copy {
  color: #4b5563;
}

.description-copy {
  margin: 0;
  color: #374151;
  line-height: 1.55;
  display: -webkit-box;
  -webkit-line-clamp: 4;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.provenance-banner {
  background: #f8fafc;
  color: #1f2937;
}

.temporal-banner {
  background: #f0fdf4;
  color: #1f2937;
  border: 1px solid rgba(0, 255, 156, 0.2);
}

.provenance-title {
  font-weight: 700;
  margin-bottom: 4px;
}

.provenance-meta,
.footer-caption {
  font-size: 0.85rem;
  color: #6b7280;
}

.provenance-actions {
  margin-top: 10px;
}

.temporal-note {
  margin-top: 8px;
  font-size: 0.8rem;
  color: #b45309;
}

.error-banner {
  background: #fff1f2;
  color: #9f1239;
}

.signal-lost {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  gap: 16px;
  padding: 48px 24px;
}

.signal-lost-icon {
  color: rgba(17, 24, 39, 0.25);
}

.signal-lost-title {
  font-size: 1.5rem;
  font-weight: 800;
  text-transform: uppercase;
  font-style: italic;
  letter-spacing: -0.02em;
  color: #111827;
}

.signal-lost-copy {
  margin: 0;
  max-width: 420px;
  font-size: 0.85rem;
  letter-spacing: 0.05em;
  color: #6b7280;
  line-height: 1.6;
}

.modal-actions {
  padding: 14px 20px 18px;
  gap: 12px;
}

@media (max-width: 768px) {
  .event-detail-modal {
    width: 100vw;
    max-width: none;
    max-height: 100vh;
    border-radius: 0;
  }

  .hero-layout {
    grid-template-columns: 1fr;
  }

  .flyer-media,
  .hero-skeleton {
    min-height: 220px;
  }

  .modal-actions {
    align-items: stretch;
  }

  .action-row {
    width: 100%;
  }

  .action-row :deep(.q-btn) {
    flex: 1;
  }
}

@media (prefers-reduced-motion: reduce) {
  .awaiting-signal-overlay {
    backdrop-filter: none;
    -webkit-backdrop-filter: none;
  }
}
</style>
