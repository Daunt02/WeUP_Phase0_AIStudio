<!--
  WEUP-SYNTH:
  source=components/BottomNav.tsx
  destination=frontend-vue/src/components/WeupBottomNav.vue
  mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
  notes=Purely presentational per G3: mode state is projected from the shell's
    useWeupNavMode (which dispatches into useDiscoveryState); no local store.
    Saved badge reads useSavedEventsCollection.surfaceSnapshot.savedCountBadgeValue
    (passed as prop by App.vue). Motion: terminating transitions only; CREATE
    press-scale terminates; no continuous animation.
-->
<template>
  <nav
    class="weup-bottom-nav"
    data-plane="z1"
    aria-label="Primary navigation"
  >
    <div class="nav-cluster">
      <button
        v-for="tab in tabs"
        :key="tab.mode"
        type="button"
        class="nav-tab"
        :class="{ 'is-active': tab.mode === activeMode }"
        :aria-label="tab.ariaLabel"
        :aria-current="tab.mode === activeMode ? 'page' : undefined"
        @click="emit('mode-change', tab.mode)"
      >
        <span
          v-if="tab.mode === 'SAVED' && savedCount > 0"
          class="saved-badge"
          aria-hidden="true"
        >
          {{ savedCount > 99 ? "99+" : savedCount }}
        </span>
        <q-icon :name="tab.icon" class="nav-icon" />
        <span class="nav-label">{{ tab.label }}</span>
      </button>

      <!-- Primary CREATE action: accent affordance, translated from the
           source's elevated Plus button (bg-[#00FF9C] when active). -->
      <button
        type="button"
        class="nav-create"
        :class="{ 'is-active': activeMode === 'CREATE' }"
        aria-label="Create event"
        :aria-current="activeMode === 'CREATE' ? 'page' : undefined"
        @click="emit('mode-change', 'CREATE')"
      >
        <q-icon name="add" class="create-icon" />
        <span class="create-caption" aria-hidden="true">CREATE</span>
      </button>
    </div>
  </nav>
</template>

<script setup lang="ts">
import type { WeupNavMode } from "../navigation/weupNavModes";

interface NavTab {
  mode: Exclude<WeupNavMode, "CREATE">;
  label: string;
  ariaLabel: string;
  icon: string;
}

// Tab order mirrors the source BottomNav: EXPLORE / ACTIVITY / [CREATE] / SAVED / KEYS.
const tabs: NavTab[] = [
  { mode: "DISCOVER", label: "EXPLORE", ariaLabel: "Discover events", icon: "explore" },
  { mode: "ACTIVITY", label: "ACTIVITY", ariaLabel: "Activity and calendar", icon: "local_activity" },
  { mode: "SAVED", label: "SAVED", ariaLabel: "Saved events", icon: "bookmark" },
  { mode: "PROFILE", label: "KEYS", ariaLabel: "Profile", icon: "vpn_key" },
];

withDefaults(
  defineProps<{
    activeMode: WeupNavMode;
    /** Canonical saved count badge value (useSavedEventsCollection.surfaceSnapshot). */
    savedCount?: number;
  }>(),
  { savedCount: 0 },
);

const emit = defineEmits<{
  (event: "mode-change", mode: WeupNavMode): void;
}>();
</script>

<style scoped>
/* G4 tokens: navigation glass — bg-black/80 backdrop-blur-3xl, rounded-3xl,
   border white/10, elevated shadow. Source z-[200] preserved. */
.weup-bottom-nav {
  position: fixed;
  left: 0;
  right: 0;
  bottom: 0;
  z-index: 200;
  display: flex;
  justify-content: center;
  align-items: flex-end;
  padding-bottom: max(1.5rem, env(safe-area-inset-bottom));
  pointer-events: none;
}

.nav-cluster {
  display: flex;
  align-items: center;
  gap: 0.25rem;
  background: rgba(0, 0, 0, 0.8);
  backdrop-filter: blur(24px);
  -webkit-backdrop-filter: blur(24px);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 24px;
  padding: 0.375rem;
  box-shadow: 0 24px 48px rgba(0, 0, 0, 0.55);
  pointer-events: auto;
}

.nav-tab {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.375rem;
  min-width: 56px;
  min-height: 56px;
  padding: 0.75rem 0.9rem;
  border: 0;
  border-radius: 16px;
  background: transparent;
  color: rgba(255, 255, 255, 0.4);
  cursor: pointer;
  /* Terminating transition only (G4 motion rules); no continuous animation. */
  transition:
    color 0.3s ease,
    background-color 0.3s ease,
    transform 0.15s ease;
  user-select: none;
}

.nav-tab:hover {
  color: rgba(255, 255, 255, 0.7);
}

.nav-tab:active {
  transform: scale(0.95);
}

.nav-tab.is-active {
  color: #00ff9c;
  background: rgba(0, 255, 156, 0.06);
}

.nav-tab:focus-visible,
.nav-create:focus-visible {
  outline: 2px solid #00ff9c;
  outline-offset: 2px;
}

.nav-icon {
  font-size: 22px;
  transition: transform 0.3s ease;
}

.nav-tab.is-active .nav-icon {
  transform: scale(1.1);
}

.nav-label {
  font-size: 9px;
  font-weight: 800;
  letter-spacing: 0.15em;
  line-height: 1;
  opacity: 0.6;
  transition: opacity 0.3s ease;
}

.nav-tab.is-active .nav-label {
  opacity: 1;
}

.nav-tab:hover .nav-label {
  opacity: 1;
}

.saved-badge {
  position: absolute;
  top: 4px;
  right: 8px;
  min-width: 18px;
  height: 18px;
  padding: 0 5px;
  border-radius: 9999px;
  background: #00ff9c;
  color: #000;
  font-size: 10px;
  font-weight: 800;
  line-height: 18px;
  text-align: center;
}

/* CREATE primary action: source grammar — elevated accent button with glow
   when active; plus rotates 45deg in CREATE mode. */
.nav-create {
  position: relative;
  width: 56px;
  height: 56px;
  margin: 0 0.5rem;
  border: 0;
  border-radius: 16px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #fff;
  color: #000;
  cursor: pointer;
  box-shadow: 0 12px 24px rgba(0, 0, 0, 0.45);
  transition:
    background-color 0.3s ease,
    color 0.3s ease,
    box-shadow 0.3s ease,
    transform 0.15s ease;
}

.nav-create:hover {
  transform: scale(1.05);
}

.nav-create:active {
  transform: scale(0.95);
}

.nav-create.is-active {
  background: #00ff9c;
  color: #000;
  box-shadow: 0 0 30px rgba(0, 255, 156, 0.5);
}

.create-icon {
  font-size: 30px;
  transition: transform 0.4s ease;
}

.nav-create.is-active .create-icon {
  transform: rotate(45deg);
}

.create-caption {
  position: absolute;
  bottom: -6px;
  left: 50%;
  transform: translateX(-50%);
  background: #000;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 4px;
  padding: 1px 6px;
  font-size: 6px;
  font-weight: 900;
  letter-spacing: 0.2em;
  color: #fff;
  white-space: nowrap;
}

@media (max-width: 380px) {
  .nav-tab {
    min-width: 48px;
    padding: 0.75rem 0.6rem;
  }

  .nav-create {
    margin: 0 0.25rem;
  }
}

@media (prefers-reduced-motion: reduce) {
  .nav-tab,
  .nav-icon,
  .nav-label,
  .nav-create,
  .create-icon {
    transition: none;
  }

  .nav-tab:active,
  .nav-create:hover,
  .nav-create:active {
    transform: none;
  }

  .nav-create.is-active .create-icon {
    transform: none;
  }
}

/* WEUP-2.5D (D11): desktop persistent side rail (mission §XI). Same
   component, same tabs, same emits, same aria — only the composition
   changes. NOT a shrunk desktop nav on mobile: below the desktop
   breakpoint the bottom-bar composition is untouched.
   Breakpoint = --weup-breakpoint-desktop-min (1024px); CSS media queries
   cannot consume var(), so the value is pinned here with the token named. */
@media (min-width: 1024px) {
  .weup-bottom-nav {
    top: 0;
    bottom: 0;
    left: 0;
    right: auto;
    align-items: center;
    padding-bottom: 0;
    padding-left: max(1rem, env(safe-area-inset-left));
  }

  .weup-bottom-nav .nav-cluster {
    flex-direction: column;
    gap: var(--weup-spacing-1);
  }
}
</style>
