<!--
  WEUP-SYNTH:
  source=components/SocialSignalPanel.tsx
  destination=frontend-vue/src/components/SocialSignalPanel.vue
  mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
  notes=Prototype-isolated translation (§14, G3 chain): the source social graph
    (MOCK_TIERS, unlockedTiers, trust meter, unlock buttons) has NO backend
    counterpart — no social-graph API was observed in the surveyed routes, so
    live state is BLOCKED_WITH_REASON. This panel preserves the UI contract
    only, backed by useSocialPrototype: an explicitly non-persistent,
    session-only, non-networked store labeled PROTOTYPE. Nothing is persisted
    to localStorage or any backend, and nothing is ever presented as real.
-->
<template>
  <Transition name="social-panel">
    <div
      v-if="isOpen"
      class="social-panel"
      data-plane="z1"
      role="dialog"
      aria-label="Social signal panel (prototype)"
    >
      <div class="prototype-banner" aria-label="Prototype notice">
        <q-icon name="science" size="14px" />
        <span>Prototype — not real social data. Resets on reload.</span>
      </div>

      <div class="panel-header">
        <div class="header-copy">
          <div class="panel-title">Interest Saved</div>
          <div class="panel-subtitle">Social signal active</div>
        </div>
        <button
          type="button"
          class="close-btn"
          aria-label="Close social signal panel"
          @click="$emit('close')"
        >
          <q-icon name="close" />
        </button>
      </div>

      <div v-if="eventSummary" class="event-summary">
        <div class="event-thumb">
          <img
            v-if="eventSummary.flyerUrl"
            :src="eventSummary.flyerUrl"
            :alt="eventSummary.title"
            class="event-thumb-img"
            referrerpolicy="no-referrer"
          />
          <div v-else class="event-thumb-fallback">
            <q-icon name="image_not_supported" size="28px" />
          </div>
        </div>
        <div class="event-copy">
          <div class="event-title">{{ eventSummary.title }}</div>
          <div class="event-meta">{{ eventSummary.venueName }}</div>
          <div class="event-meta">{{ formatEventDate(eventSummary.startUtc) }}</div>
        </div>
      </div>

      <div class="tiers-section">
        <div class="tiers-row">
          <span class="tiers-label">Nearby social layer</span>
          <span class="tiers-count">
            {{ social.unlockedTierLevels.length }} tiers unlocked
          </span>
        </div>

        <div class="tier-list">
          <div
            v-for="tier in social.tiers.value"
            :key="tier.id"
            class="tier-card"
            :class="{
              'is-unlocked': social.isTierUnlocked(tier.tierLevel),
              'is-hidden': !social.isTierVisible(tier),
            }"
          >
            <template v-if="social.isTierVisible(tier)">
              <div class="tier-head">
                <div
                  class="tier-icon"
                  :class="{
                    'is-unlocked': social.isTierUnlocked(tier.tierLevel),
                  }"
                >
                  <q-icon
                    :name="
                      social.isTierUnlocked(tier.tierLevel)
                        ? 'verified_user'
                        : 'lock'
                    "
                    size="18px"
                  />
                </div>
                <div class="tier-copy">
                  <div
                    class="tier-invite-type"
                    :class="{
                      'is-unlocked': social.isTierUnlocked(tier.tierLevel),
                    }"
                  >
                    Tier {{ tier.tierLevel }}: {{ tier.inviteType }}
                  </div>
                  <div class="tier-title">{{ tier.title }}</div>
                </div>
              </div>

              <div v-if="social.isTierUnlocked(tier.tierLevel)" class="tier-body">
                <q-btn
                  flat
                  dense
                  no-caps
                  label="Explore invite (demo)"
                  icon-right="arrow_forward"
                  class="tier-explore"
                  @click="onExploreTier(tier.tierLevel)"
                />
              </div>
              <div v-else class="tier-body">
                <div class="tier-lock">
                  <q-icon name="lock" size="10px" />
                  <span>
                    Trust requirement: {{ tier.trustRequirement }}% — prototype
                  </span>
                </div>
                <q-btn
                  outline
                  dense
                  no-caps
                  label="Unlock (demo)"
                  class="tier-unlock"
                  @click="social.toggleTierUnlock(tier.tierLevel)"
                />
              </div>
            </template>
            <template v-else>
              <div class="tier-hidden-row">
                <div class="tier-icon is-hidden">
                  <q-icon name="visibility_off" size="18px" />
                </div>
                <div class="tier-copy">
                  <div class="tier-invite-type">
                    Tier {{ tier.tierLevel }} invites
                  </div>
                  <div class="tier-title tier-title-dim">Hidden layer</div>
                </div>
              </div>
            </template>
          </div>
        </div>
      </div>

      <div class="panel-footer">
        <q-btn
          no-caps
          class="footer-keep"
          :label="
            interestedLabel
          "
          icon="bolt"
          @click="toggleInterested"
        />
        <q-btn
          no-caps
          outline
          class="footer-details"
          label="View event details"
          @click="$emit('close')"
        />
      </div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useSocialPrototype } from "../composables/useSocialPrototype";

export interface SocialPanelEventSummary {
  readonly id: string;
  readonly title: string;
  readonly venueName: string;
  readonly district: string | null;
  readonly startUtc: string;
  readonly flyerUrl: string | null;
}

const props = defineProps<{
  isOpen: boolean;
  eventSummary: SocialPanelEventSummary | null;
}>();

const emit = defineEmits<{
  (event: "close"): void;
}>();

const social = useSocialPrototype();

const interestedLabel = computed(() => {
  const interested =
    props.eventSummary !== null &&
    social.isEventMarkedInterested(props.eventSummary.id);
  return interested ? "Signal active (demo)" : "Keep signal active (demo)";
});

function toggleInterested(): void {
  if (!props.eventSummary) {
    return;
  }

  // Prototype session flag only: explicitly NOT merged into canonical saved
  // state, never persisted, never networked.
  const next = !social.isEventMarkedInterested(props.eventSummary.id);
  social.setEventInterested(props.eventSummary.id, next);
}

function onExploreTier(_tierLevel: number): void {
  // Prototype contract: unlock affordance is demo-only by design.
  // The visible tiers render their unlocked states; no backend call exists.
  void _tierLevel;
}

function formatEventDate(value: string): string {
  const parsed = Date.parse(value);
  if (Number.isNaN(parsed)) {
    return value;
  }

  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(parsed);
}
</script>

<style scoped>
/* G4 tokens: dark prototype surface. Slide-in 300-500ms ease; reduced-motion
   collapses the transition entirely (motion rules: terminating only). */
/* WEUP-2.5D (D10): intelligence surface — Z1 structure with a signal edge
   (mission §X: activity surfaces carry signal illumination). Elevation from
   the global [data-plane="z1"] rule; the edge marks live activity. */
.social-panel {
  position: fixed;
  right: 12px;
  top: 88px;
  bottom: 128px;
  z-index: 6200;
  width: min(400px, calc(100vw - 24px));
  display: flex;
  flex-direction: column;
  background: rgba(5, 5, 5, 0.94);
  backdrop-filter: blur(40px);
  -webkit-backdrop-filter: blur(40px);
  border: 1px solid var(--weup-glow-signal-soft);
  border-radius: 24px;
  overflow: hidden;
  box-shadow: var(--weup-elevation-2), var(--weup-glow-signal);
}

.social-panel-enter-active,
.social-panel-leave-active {
  transition:
    transform 0.4s ease,
    opacity 0.4s ease;
}

.social-panel-enter-from,
.social-panel-leave-to {
  transform: translateX(64px);
  opacity: 0;
}

.prototype-banner {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  background: rgba(251, 191, 36, 0.08);
  border-bottom: 1px solid rgba(251, 191, 36, 0.25);
  color: #fbbf24;
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.14em;
}

.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 16px 8px;
}

.header-copy {
  min-width: 0;
}

.panel-title {
  color: #fff;
  font-size: 20px;
  font-weight: 800;
  text-transform: uppercase;
  font-style: italic;
  letter-spacing: -0.02em;
}

.panel-subtitle {
  color: rgba(0, 255, 156, 0.6);
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 9px;
  text-transform: uppercase;
  letter-spacing: 0.3em;
  margin-top: 2px;
}

.close-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.05);
  color: rgba(255, 255, 255, 0.6);
  font-size: 18px;
  cursor: pointer;
  transition:
    background-color 0.3s ease,
    color 0.3s ease;
}

.close-btn:hover {
  background: rgba(255, 255, 255, 0.1);
  color: #fff;
}

.event-summary {
  display: flex;
  gap: 12px;
  margin: 8px 16px 0;
  padding: 12px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 16px;
  background: rgba(255, 255, 255, 0.02);
}

.event-thumb {
  width: 88px;
  height: 56px;
  border-radius: 12px;
  overflow: hidden;
  flex-shrink: 0;
  border: 1px solid rgba(255, 255, 255, 0.1);
  background: rgba(255, 255, 255, 0.04);
}

.event-thumb-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  opacity: 0.7;
}

.event-thumb-fallback {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: rgba(255, 255, 255, 0.2);
}

.event-copy {
  min-width: 0;
}

.event-title {
  color: #fff;
  font-size: 14px;
  font-weight: 800;
  text-transform: uppercase;
  font-style: italic;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.event-meta {
  color: rgba(255, 255, 255, 0.4);
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.12em;
  margin-top: 2px;
}

.tiers-section {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.tiers-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.tiers-label {
  color: rgba(255, 255, 255, 0.35);
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.3em;
}

.tiers-count {
  color: #00ff9c;
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 9px;
  text-transform: uppercase;
  letter-spacing: 0.12em;
  padding: 4px 8px;
  border: 1px solid rgba(0, 255, 156, 0.25);
  border-radius: 999px;
  background: rgba(0, 255, 156, 0.08);
}

.tier-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.tier-card {
  padding: 16px;
  border-radius: 24px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  background: rgba(255, 255, 255, 0.05);
  transition:
    border-color 0.4s ease,
    background-color 0.4s ease;
}

.tier-card.is-unlocked {
  border-color: rgba(0, 255, 156, 0.2);
  background: rgba(0, 255, 156, 0.05);
  box-shadow: 0 0 30px rgba(0, 255, 156, 0.05);
}

.tier-card.is-hidden {
  opacity: 0.55;
  border-style: dashed;
  border-color: rgba(255, 255, 255, 0.08);
}

.tier-head,
.tier-hidden-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.tier-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.1);
  color: rgba(255, 255, 255, 0.4);
  flex-shrink: 0;
}

.tier-icon.is-unlocked {
  background: rgba(0, 255, 156, 0.2);
  color: #00ff9c;
}

.tier-icon.is-hidden {
  background: rgba(255, 255, 255, 0.05);
  color: rgba(255, 255, 255, 0.2);
}

.tier-copy {
  min-width: 0;
}

.tier-invite-type {
  color: rgba(255, 255, 255, 0.4);
  font-size: 10px;
  font-weight: 800;
  text-transform: uppercase;
  font-style: italic;
}

.tier-invite-type.is-unlocked {
  color: #00ff9c;
}

.tier-title {
  color: #fff;
  font-size: 13px;
  font-weight: 800;
  text-transform: uppercase;
  font-style: italic;
  margin-top: 2px;
}

.tier-title-dim {
  color: rgba(255, 255, 255, 0.4);
}

.tier-body {
  margin-top: 12px;
}

.tier-explore {
  width: 100%;
  color: #000;
  background: #fff;
  font-weight: 800;
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.2em;
}

.tier-lock {
  display: flex;
  align-items: center;
  gap: 6px;
  color: rgba(255, 255, 255, 0.25);
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 9px;
  text-transform: uppercase;
  letter-spacing: 0.12em;
}

.tier-unlock {
  margin-top: 10px;
  width: 100%;
  color: rgba(255, 255, 255, 0.6);
  border-color: rgba(255, 255, 255, 0.2);
  font-size: 10px;
  letter-spacing: 0.2em;
}

.panel-footer {
  padding: 12px 16px 16px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.footer-keep {
  width: 100%;
  background: #00ff9c;
  color: #000;
  font-weight: 800;
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.2em;
  border-radius: 16px;
  min-height: 48px;
}

.footer-details {
  width: 100%;
  color: rgba(255, 255, 255, 0.6);
  border-color: rgba(255, 255, 255, 0.1);
  font-size: 10px;
  letter-spacing: 0.2em;
  border-radius: 16px;
  min-height: 48px;
}

@media (prefers-reduced-motion: reduce) {
  .social-panel-enter-active,
  .social-panel-leave-active {
    transition: none;
  }

  .social-panel-enter-from,
  .social-panel-leave-to {
    transform: none;
    opacity: 1;
  }
}
</style>
