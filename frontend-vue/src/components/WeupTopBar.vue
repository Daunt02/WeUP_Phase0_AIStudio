<!--
  WEUP-SYNTH:
  source=components/TopBar.tsx
  destination=frontend-vue/src/components/WeupTopBar.vue
  mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
  notes=Canonical application header per G3: brand mark, LIVE status pill derived
    from the REAL feed state (useMapEvents isLoading/error, map token presence)
    — never hardcoded; district search filters through the canonical district
    taxonomy via useDiscoveryState.applyFilters (no fake backend); save/profile
    affordances dispatch SAVED/PROFILE modes. Only telemetry dots may animate
    continuously, and they honor prefers-reduced-motion (G4 motion rules).
-->
<template>
  <div class="weup-topbar">
    <div class="brand-block">
      <h1 class="brand-mark">WEUP</h1>
      <span class="brand-sub">Signal_Network</span>
    </div>

    <!-- Location / context: current canonical district filter, when set. -->
    <div
      v-if="district"
      class="district-chip"
      role="status"
      :aria-label="`Filtered to district ${district}`"
    >
      <q-icon name="place" class="district-icon" />
      <span class="district-name">{{ district }}</span>
      <button
        type="button"
        class="district-clear"
        aria-label="Clear district filter"
        @click="emit('clear-district-filter')"
      >
        <q-icon name="close" />
      </button>
    </div>

    <div class="topbar-controls">
      <!-- Search: filters by canonical district taxonomy only (G1/G3).
           No live search backend exists, so free-text search is not offered;
           the input resolves the query against known districts and applies
           useDiscoveryState.applyFilters({ district }) via the parent. -->
      <div class="search-wrap">
        <q-icon name="search" class="search-icon" />
        <input
          v-model="searchQuery"
          type="text"
          class="search-input"
          placeholder="Filter by district"
          aria-label="Filter events by district"
          autocomplete="off"
          @input="onSearchInput"
        />
        <button
          v-if="searchQuery"
          type="button"
          class="search-clear"
          aria-label="Clear district search"
          @click="clearSearch"
        >
          <q-icon name="close" />
        </button>
        <span
          v-if="showNoMatchHint"
          class="search-hint"
          role="status"
        >
          no district match in current feed
        </span>
      </div>

      <!-- LIVE status pill: derived from real feed state, never hardcoded. -->
      <div
        class="status-pill"
        :class="pillClass"
        role="status"
        :aria-label="pillAriaLabel"
      >
        <span class="status-dot" aria-hidden="true" />
        <span class="status-text">{{ pillText }}</span>
      </div>

      <!-- Save / profile affordances dispatch the canonical nav modes. -->
      <button
        type="button"
        class="icon-affordance"
        aria-label="Open saved events"
        title="Saved"
        @click="emit('mode-change', 'SAVED')"
      >
        <q-icon name="bookmark" />
      </button>
      <button
        type="button"
        class="icon-affordance"
        aria-label="Open profile"
        title="Profile"
        @click="emit('mode-change', 'PROFILE')"
      >
        <q-icon name="vpn_key" />
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import type { WeupNavMode } from "../navigation/weupNavModes";

export interface TopBarFeedStatus {
  readonly isLoading: boolean;
  readonly error: string | null;
  /** True when the map token is missing (source: isMapOffline). */
  readonly offline: boolean;
}

const props = withDefaults(
  defineProps<{
    feedStatus: TopBarFeedStatus;
    /** Current canonical district filter (location context). */
    district?: string | undefined;
    /** Canonical district taxonomy derived from the visible feed. */
    knownDistricts?: readonly string[];
  }>(),
  {
    district: undefined,
    knownDistricts: () => [],
  },
);

const emit = defineEmits<{
  (event: "mode-change", mode: WeupNavMode): void;
  /** Query resolved to a canonical district — parent applies via applyFilters. */
  (event: "apply-district-filter", district: string): void;
  (event: "clear-district-filter"): void;
}>();

const searchQuery = ref("");
const showNoMatchHint = ref(false);

type PillState = "live" | "loading" | "offline";

const pillState = computed<PillState>(() => {
  if (props.feedStatus.offline || props.feedStatus.error !== null) {
    return "offline";
  }
  if (props.feedStatus.isLoading) {
    return "loading";
  }
  return "live";
});

const pillClass = computed(() => `pill-${pillState.value}`);

const pillText = computed(() => {
  switch (pillState.value) {
    case "offline":
      return "OFFLINE";
    case "loading":
      return "LOADING";
    case "live":
      return "LIVE";
  }
});

const pillAriaLabel = computed(() => {
  switch (pillState.value) {
    case "offline":
      return props.feedStatus.error
        ? `Event feed offline: ${props.feedStatus.error}`
        : "Event feed offline: map token missing";
    case "loading":
      return "Event feed loading";
    case "live":
      return "Event feed live";
  }
});

function resolveDistrict(query: string): string | null {
  const normalized = query.trim().toLowerCase();
  if (normalized.length === 0) {
    return null;
  }

  // Exact match first, then prefix match against the canonical taxonomy.
  const exact = props.knownDistricts.find(
    (district) => district.toLowerCase() === normalized,
  );
  if (exact) {
    return exact;
  }

  return (
    props.knownDistricts.find((district) =>
      district.toLowerCase().startsWith(normalized),
    ) ?? null
  );
}

function onSearchInput(): void {
  const query = searchQuery.value;

  if (query.trim().length === 0) {
    showNoMatchHint.value = false;
    emit("clear-district-filter");
    return;
  }

  const matched = resolveDistrict(query);
  if (matched) {
    showNoMatchHint.value = false;
    emit("apply-district-filter", matched);
  } else {
    // Honest non-match: no filter is applied and no fake results are shown.
    showNoMatchHint.value = true;
  }
}

function clearSearch(): void {
  searchQuery.value = "";
  showNoMatchHint.value = false;
  emit("clear-district-filter");
}

// Keep the hint in sync if the feed taxonomy changes under a typed query.
watch(
  () => props.knownDistricts,
  () => {
    if (searchQuery.value.trim().length > 0) {
      showNoMatchHint.value = resolveDistrict(searchQuery.value) === null;
    }
  },
);
</script>

<style scoped>
/* Source grammar: translucent top strip, pointer-events-none wrapper with
   interactive children re-enabled. */
.weup-topbar {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  width: 100%;
  min-height: 56px;
  padding: 0.5rem 1.25rem;
  pointer-events: none;
}

.weup-topbar > * {
  pointer-events: auto;
}

.brand-block {
  display: flex;
  flex-direction: column;
  line-height: 1;
  flex-shrink: 0;
}

.brand-mark {
  margin: 0;
  font-size: 1.125rem;
  font-weight: 900;
  letter-spacing: -0.05em;
  text-transform: uppercase;
  font-style: italic;
  color: #fff;
}

.brand-sub {
  font-family: ui-monospace, monospace;
  font-size: 6px;
  letter-spacing: 0.3em;
  text-transform: uppercase;
  color: rgba(255, 255, 255, 0.4);
}

.district-chip {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  background: rgba(0, 255, 156, 0.1);
  border: 1px solid rgba(0, 255, 156, 0.2);
  border-radius: 9999px;
  padding: 0.25rem 0.5rem 0.25rem 0.625rem;
  max-width: 12rem;
}

.district-icon {
  font-size: 14px;
  color: #00ff9c;
  flex-shrink: 0;
}

.district-name {
  font-family: ui-monospace, monospace;
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: #00ff9c;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.district-clear {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 20px;
  height: 20px;
  border: 0;
  border-radius: 9999px;
  background: transparent;
  color: rgba(0, 255, 156, 0.7);
  cursor: pointer;
  flex-shrink: 0;
}

.district-clear:hover {
  color: #00ff9c;
  background: rgba(0, 255, 156, 0.15);
}

.topbar-controls {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-left: auto;
}

.search-wrap {
  position: relative;
  display: flex;
  align-items: center;
  min-width: 0;
}

.search-icon {
  position: absolute;
  left: 0.75rem;
  font-size: 16px;
  color: rgba(255, 255, 255, 0.4);
  pointer-events: none;
}

.search-input {
  width: 11rem;
  height: 2.25rem;
  padding: 0 2rem 0 2.25rem;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  background: rgba(0, 0, 0, 0.7);
  backdrop-filter: blur(16px);
  -webkit-backdrop-filter: blur(16px);
  color: #fff;
  font-family: ui-monospace, monospace;
  font-size: 10px;
  letter-spacing: 0.15em;
  text-transform: uppercase;
  transition: border-color 0.3s ease;
}

.search-input::placeholder {
  color: rgba(255, 255, 255, 0.25);
}

.search-input:focus {
  outline: none;
  border-color: #00ff9c;
}

.search-clear {
  position: absolute;
  right: 0.375rem;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border: 0;
  border-radius: 9999px;
  background: transparent;
  color: rgba(255, 255, 255, 0.4);
  cursor: pointer;
}

.search-clear:hover {
  color: #fff;
  background: rgba(255, 255, 255, 0.1);
}

.search-hint {
  position: absolute;
  top: calc(100% + 4px);
  left: 0;
  font-family: ui-monospace, monospace;
  font-size: 9px;
  letter-spacing: 0.1em;
  color: rgba(255, 255, 255, 0.45);
  white-space: nowrap;
}

/* Status pill: source grammar (bg-black/40 backdrop-blur rounded-full).
   Only the telemetry dot may animate continuously (G4). */
.status-pill {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  background: rgba(0, 0, 0, 0.4);
  backdrop-filter: blur(12px);
  -webkit-backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.05);
  border-radius: 9999px;
  padding: 0.25rem 0.75rem;
  transition: border-color 0.5s ease;
  flex-shrink: 0;
}

.status-pill.pill-offline {
  border-color: rgba(239, 68, 68, 0.4);
}

.status-dot {
  width: 4px;
  height: 4px;
  border-radius: 9999px;
  background: #00ff9c;
  animation: telemetry-pulse 2s ease-in-out infinite;
}

.status-pill.pill-offline .status-dot {
  background: #ef4444;
  animation: none;
}

.status-pill.pill-loading .status-dot {
  background: #fbbf24;
}

.status-text {
  font-family: ui-monospace, monospace;
  font-size: 7px;
  font-weight: 700;
  letter-spacing: 0.2em;
  text-transform: uppercase;
  color: rgba(255, 255, 255, 0.4);
}

.status-pill.pill-offline .status-text {
  color: #ef4444;
}

.status-pill.pill-live .status-text {
  color: rgba(255, 255, 255, 0.55);
}

.status-pill.pill-loading .status-text {
  color: #fbbf24;
}

@keyframes telemetry-pulse {
  0%,
  100% {
    opacity: 1;
  }
  50% {
    opacity: 0.35;
  }
}

.icon-affordance {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(16px);
  -webkit-backdrop-filter: blur(16px);
  color: rgba(255, 255, 255, 0.6);
  font-size: 18px;
  cursor: pointer;
  transition:
    color 0.3s ease,
    border-color 0.3s ease;
}

.icon-affordance:hover {
  color: #00ff9c;
  border-color: rgba(0, 255, 156, 0.4);
}

@media (max-width: 560px) {
  .search-input {
    width: 7.5rem;
  }

  .icon-affordance {
    display: none;
  }
}

@media (prefers-reduced-motion: reduce) {
  .status-dot {
    animation: none;
  }

  .search-input,
  .icon-affordance {
    transition: none;
  }
}
</style>
