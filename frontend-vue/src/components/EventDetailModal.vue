<template>
  <q-dialog
    :model-value="modelValue"
    :maximized="isCompact"
    transition-show="slide-up"
    transition-hide="slide-down"
    @update:model-value="onDialogModelValue"
  >
    <q-card class="event-detail-modal">
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
            <q-img
              v-if="heroImageUrl"
              :src="heroImageUrl"
              :alt="`${event.title} flyer`"
              class="hero-image"
              fit="cover"
            />

            <div v-else class="hero-placeholder">Flyer unavailable</div>

            <div class="hero-copy">
              <div class="meta-row">
                <q-badge color="primary" outline>
                  {{ event.status }}
                </q-badge>
                <q-badge color="secondary" outline>
                  {{ primaryCategory }}
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

          <q-banner rounded class="provenance-banner">
            <div class="provenance-title">Source summary</div>
            <div>{{ event.provenanceSummary.summaryLabel }}</div>
            <div class="provenance-meta">
              Primary source: {{ event.provenanceSummary.primarySourceKind }} •
              {{ event.provenanceSummary.sourceCount }} source{{
                event.provenanceSummary.sourceCount === 1 ? "" : "s"
              }}
            </div>
          </q-banner>
        </div>

        <q-banner v-else rounded class="error-banner">
          No canonical event detail is available for the current selection.
        </q-banner>
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
            :color="event?.savedByCurrentUser ? 'accent' : 'primary'"
            :outline="!event?.savedByCurrentUser"
            :loading="isSavePending"
            :disable="!event"
            :icon="event?.savedByCurrentUser ? 'bookmark' : 'bookmark_add'"
            :label="event?.savedByCurrentUser ? 'Saved' : 'Save'"
            @click="$emit('toggle-save')"
          />
        </div>
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useQuasar } from "quasar";
import type { EventDetailDto } from "../contracts/event-detail.contracts";

const props = defineProps<{
  modelValue: boolean;
  event: EventDetailDto | null;
  isLoading: boolean;
  isSavePending: boolean;
  error: string | null;
}>();

const emit = defineEmits<{
  (event: "update:modelValue", value: boolean): void;
  (event: "toggle-save"): void;
  (event: "share"): void;
}>();

const $q = useQuasar();

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

function onDialogModelValue(value: boolean): void {
  emit("update:modelValue", value);
}

function closeModal(): void {
  emit("update:modelValue", false);
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

.hero-image,
.hero-placeholder,
.hero-skeleton {
  width: 100%;
  min-height: 280px;
  border-radius: 18px;
}

.hero-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  background:
    radial-gradient(circle at top, rgba(37, 99, 235, 0.12), transparent 55%),
    #eef2f7;
  color: #6b7280;
  font-weight: 600;
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

.provenance-title {
  font-weight: 700;
  margin-bottom: 4px;
}

.provenance-meta,
.footer-caption {
  font-size: 0.85rem;
  color: #6b7280;
}

.error-banner {
  background: #fff1f2;
  color: #9f1239;
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

  .hero-image,
  .hero-placeholder,
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
</style>
