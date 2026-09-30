<!--
  WEUP-SYNTH:
  source=components/ProfilePanel.tsx + services/profileService.ts
  destination=frontend-vue/src/components/ProfilePanel.vue
  mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
  notes=§8 profile synthesis. The source's fabrication path
    (profileService via a browser-exposed NEXT_PUBLIC_GEMINI_API_KEY) is
    explicitly EXCLUDED — no Gemini fabrication, no fabricated snapshots.
    Every field cites its real source:
    - saved count      -> useSavedEventsCollection.surfaceSnapshot (canonical)
    - session identity -> SaveSessionKind from the saved-state authority
    - city/context     -> anonymous discovery context / user-context
      preferences (display only, non-authoritative; operator city)
    - folders          -> prototype-local folders (G2 arbitration:
      backend has NO folder concept), labeled non-persistent
    - tier/social      -> prototype-labeled preview from useSocialPrototype
    - server profile   -> BLOCKED_WITH_REASON: no profile API was observed
      in the surveyed Next routes; server-backed fields render as
      explicitly unavailable, never invented.
-->
<template>
  <div class="profile-panel" data-plane="z1">
    <!-- Operator header: identity is session-derived, never fabricated. -->
    <!-- WEUP-2.5D (D10): identity surface stays deliberately flat (Z1
         structural elevation only) — it frames the operator, it is not a
         signal. -->
    <section class="profile-header">
      <div class="identity-block">
        <div class="avatar">
          <q-icon name="person" size="34px" />
          <div class="avatar-badge">
            <q-icon name="shield" size="12px" />
          </div>
        </div>
        <div class="identity-copy">
          <div class="identity-title">Operator</div>
          <div class="identity-chips">
            <span class="chip status-chip">
              <span class="status-dot" />
              <span>{{ sessionLabel }}</span>
            </span>
            <span class="chip source-chip">{{ sessionSourceLabel }}</span>
          </div>
        </div>
      </div>
      <div class="context-block">
        <div class="context-city">{{ cityLabel }}</div>
        <div v-if="districtLabel" class="context-district">
          {{ districtLabel }}
        </div>
      </div>
    </section>

    <!-- Stats: all values derive from canonical state. -->
    <section class="profile-section">
      <div class="section-label">Saved state</div>
      <div class="stats-grid">
        <div class="stat-card">
          <div class="stat-value">{{ savedCount }}</div>
          <div class="stat-label">Saved</div>
          <div class="stat-source">saved collection</div>
        </div>
        <div class="stat-card">
          <div class="stat-value">{{ resolvedCount }}</div>
          <div class="stat-label">Resolved</div>
          <div class="stat-source">canonical events</div>
        </div>
        <div class="stat-card">
          <div class="stat-value">{{ savedInCurrentView }}</div>
          <div class="stat-label">In current view</div>
          <div class="stat-source">map window</div>
        </div>
      </div>
    </section>

    <!-- Server profile: explicit availability, never invented. -->
    <section class="profile-section">
      <div class="section-label">Server profile</div>
      <div class="server-block">
        <q-icon name="cloud_off" size="18px" />
        <div class="server-copy">
          <div class="server-title">Not available</div>
          <div class="server-detail">
            No profile API was observed in the surveyed backend routes. Server
            fields (display name, wallet, reputation) are explicitly
            unavailable — they are not invented. This surface renders only
            device-local and saved-state-derived data.
          </div>
        </div>
      </div>
    </section>

    <!-- Context: district affinity from user-context preferences. -->
    <section class="profile-section">
      <div class="section-label">Sector affinity</div>
      <div class="district-row">
        <span v-if="preferredDistrictLabels.length === 0" class="district-empty">
          No district preference recorded yet.
        </span>
        <button
          v-for="label in preferredDistrictLabels"
          :key="label"
          type="button"
          class="district-chip"
          @click="$emit('apply-district', label)"
        >
          {{ label }}
        </button>
      </div>
    </section>

    <!-- Folders: prototype-local, explicitly labeled non-persistent. -->
    <section class="profile-section">
      <div class="section-label-row">
        <span class="section-label">Folders</span>
        <span class="prototype-tag">{{ prototypeFoldersLabel }}</span>
      </div>
      <div class="folder-list">
        <div v-if="folders.length === 0" class="folder-empty">
          No folders yet. Create one below — folders live only in this browser.
        </div>
        <div
          v-for="folder in folders"
          :key="folder.name"
          class="folder-row"
        >
          <div class="folder-copy">
            <div class="folder-name">{{ folder.name }}</div>
            <div class="folder-count">
              {{ folder.savedCount }} saved
              <span v-if="folder.savedCount === 1">event</span>
              <span v-else>events</span>
            </div>
          </div>
          <button
            type="button"
            class="folder-delete"
            aria-label="Delete folder"
            @click="$emit('delete-folder', folder.name)"
          >
            <q-icon name="delete" size="16px" />
          </button>
        </div>
      </div>
      <form class="folder-create" @submit.prevent="onCreateFolder">
        <q-input
          v-model="newFolderName"
          dense
          outlined
          dark
          placeholder="New folder name"
          maxlength="48"
          class="folder-input"
        />
        <q-btn
          type="submit"
          color="accent"
          text-color="black"
          label="Add"
          :disable="newFolderName.trim().length === 0"
        />
      </form>
    </section>

    <!-- Social preview: prototype-labeled, session-only. -->
    <section class="profile-section">
      <div class="section-label-row">
        <span class="section-label">Social tiers</span>
        <span class="prototype-tag">Prototype — session only</span>
      </div>
      <div class="social-preview">
        <div class="stat-value">{{ socialUnlockedCount }} / 3</div>
        <div class="stat-label">tiers unlocked (demo)</div>
        <div class="social-note">
          Social unlocks are prototype-only: they reset on reload and are not
          real invitations. Open an event's detail to explore the social panel.
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { ref } from "vue";
import type { SaveSessionKind } from "../contracts/event-detail.contracts";
import type { ProfileFolderView } from "../composables/useProfileFolders";

const props = defineProps<{
  sessionKind: SaveSessionKind;
  savedCount: number;
  resolvedCount: number;
  savedInCurrentView: number;
  cityLabel: string;
  districtLabel: string | null;
  preferredDistrictLabels: readonly string[];
  folders: readonly ProfileFolderView[];
  prototypeFoldersLabel: string;
  socialUnlockedCount: number;
}>();

const emit = defineEmits<{
  (event: "create-folder", name: string): void;
  (event: "delete-folder", name: string): void;
  (event: "apply-district", label: string): void;
}>();

const newFolderName = ref("");

const sessionLabel = props.sessionKind === "authenticated"
  ? "Status: Active duty"
  : "Status: Anonymous";

const sessionSourceLabel = props.sessionKind === "authenticated"
  ? "authenticated sync"
  : "anonymous local";

function onCreateFolder(): void {
  const name = newFolderName.value.trim();
  if (!name) {
    return;
  }
  emit("create-folder", name);
  newFolderName.value = "";
}
</script>

<style scoped>
/* G4 tokens: dark operator surface, accent #00FF9C, mono citations.
   All sections are data-backed; unavailable fields render explicitly. */
.profile-panel {
  display: flex;
  flex-direction: column;
  gap: 28px;
  padding: 24px 20px;
  color: #fff;
}

.profile-header {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.identity-block {
  display: flex;
  align-items: center;
  gap: 20px;
}

.avatar {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 80px;
  height: 80px;
  border-radius: 20px;
  background: rgba(0, 255, 156, 0.08);
  border: 1px solid rgba(0, 255, 156, 0.2);
  color: #00ff9c;
  flex-shrink: 0;
}

.avatar-badge {
  position: absolute;
  bottom: -4px;
  right: -4px;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border-radius: 999px;
  background: #00ff9c;
  color: #000;
  border: 4px solid #050505;
}

.identity-title {
  color: #fff;
  font-size: 22px;
  font-weight: 800;
  text-transform: uppercase;
  font-style: italic;
  letter-spacing: -0.02em;
}

.identity-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 8px;
}

.chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  border-radius: 999px;
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 9px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.14em;
}

.status-chip {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  color: rgba(255, 255, 255, 0.6);
}

.status-dot {
  width: 6px;
  height: 6px;
  border-radius: 999px;
  background: #00ff9c;
}

.source-chip {
  background: rgba(0, 255, 156, 0.08);
  border: 1px solid rgba(0, 255, 156, 0.2);
  color: #00ff9c;
}

.context-block {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.context-city {
  color: #fff;
  font-size: 14px;
  font-weight: 700;
}

.context-district {
  color: rgba(255, 255, 255, 0.45);
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.2em;
}

.profile-section {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.section-label {
  color: rgba(255, 255, 255, 0.3);
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.3em;
}

.section-label-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.prototype-tag {
  color: #fbbf24;
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 9px;
  text-transform: uppercase;
  letter-spacing: 0.12em;
  padding: 3px 8px;
  border: 1px solid rgba(251, 191, 36, 0.3);
  border-radius: 999px;
  background: rgba(251, 191, 36, 0.08);
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px;
}

.stat-card {
  padding: 16px;
  border-radius: 20px;
  border: 1px solid rgba(255, 255, 255, 0.05);
  background: rgba(255, 255, 255, 0.02);
  transition: background-color 0.4s ease;
}

.stat-card:hover {
  background: rgba(255, 255, 255, 0.05);
}

.stat-value {
  color: #fff;
  font-size: 26px;
  font-weight: 800;
  font-style: italic;
  line-height: 1;
}

.stat-label {
  margin-top: 6px;
  color: rgba(255, 255, 255, 0.55);
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 9px;
  text-transform: uppercase;
  letter-spacing: 0.12em;
}

.stat-source {
  margin-top: 4px;
  color: rgba(255, 255, 255, 0.25);
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 8px;
  text-transform: uppercase;
  letter-spacing: 0.12em;
}

.server-block {
  display: flex;
  gap: 12px;
  padding: 16px;
  border-radius: 16px;
  border: 1px dashed rgba(255, 255, 255, 0.12);
  background: rgba(255, 255, 255, 0.015);
  color: rgba(255, 255, 255, 0.5);
}

.server-title {
  color: rgba(255, 255, 255, 0.75);
  font-weight: 700;
  font-size: 13px;
}

.server-detail {
  margin-top: 4px;
  font-size: 12px;
  line-height: 1.5;
  color: rgba(255, 255, 255, 0.45);
}

.district-row {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.district-empty {
  color: rgba(255, 255, 255, 0.35);
  font-size: 12px;
}

.district-chip {
  padding: 8px 16px;
  border-radius: 999px;
  border: 1px solid rgba(0, 255, 156, 0.2);
  background: rgba(0, 255, 156, 0.05);
  color: #00ff9c;
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 10px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.14em;
  cursor: pointer;
  transition:
    background-color 0.3s ease,
    color 0.3s ease;
}

.district-chip:hover {
  background: #00ff9c;
  color: #000;
}

.folder-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.folder-empty {
  padding: 16px;
  border: 1px dashed rgba(255, 255, 255, 0.1);
  border-radius: 16px;
  color: rgba(255, 255, 255, 0.35);
  font-size: 12px;
  text-align: center;
}

.folder-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-radius: 16px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  background: rgba(255, 255, 255, 0.02);
}

.folder-name {
  color: #fff;
  font-weight: 700;
  font-size: 14px;
}

.folder-count {
  margin-top: 2px;
  color: rgba(255, 255, 255, 0.4);
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 9px;
  text-transform: uppercase;
  letter-spacing: 0.12em;
}

.folder-delete {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  border-radius: 12px;
  border: 0;
  background: rgba(255, 255, 255, 0.04);
  color: rgba(255, 255, 255, 0.35);
  cursor: pointer;
  transition:
    background-color 0.3s ease,
    color 0.3s ease;
}

.folder-delete:hover {
  background: rgba(248, 113, 113, 0.12);
  color: #f87171;
}

.folder-create {
  display: flex;
  gap: 8px;
  align-items: center;
}

.folder-input {
  flex: 1;
}

.social-preview {
  padding: 16px;
  border-radius: 16px;
  border: 1px solid rgba(255, 255, 255, 0.06);
  background: rgba(255, 255, 255, 0.015);
}

.social-note {
  margin-top: 8px;
  font-size: 12px;
  line-height: 1.5;
  color: rgba(255, 255, 255, 0.45);
}
</style>
