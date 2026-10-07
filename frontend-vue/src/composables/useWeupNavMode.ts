/**
 * WEUP-SYNTH:
 * source=components/BottomNav.tsx (activeMode prop + onModeChange callback)
 * destination=frontend-vue/src/composables/useWeupNavMode.ts
 * mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
 * notes=Shell-level navigation mode. This is NOT a second routing/state model:
 *   it holds only the nav-mode label for the chrome; every mode request is
 *   dispatched by the caller (App.vue) into the canonical composables
 *   (useDiscoveryState overlay/selection, saved-panel mount state, G11 create
 *   flow). The mode value therefore always projects the authoritative UI state.
 */

import { readonly, ref, type DeepReadonly, type Ref } from "vue";
import { isWeupNavMode, type WeupNavMode } from "../navigation/weupNavModes";

const _activeMode = ref<WeupNavMode>("DISCOVER");

export interface UseWeupNavModeReturn {
  /** Readonly active navigation mode for the chrome highlight. */
  activeMode: DeepReadonly<Ref<WeupNavMode>>;
  /**
   * Request a mode change. Unknown values are rejected (mode unchanged) —
   * a routing mistake must not corrupt shell state.
   * Returns true when the mode was accepted.
   */
  requestMode: (mode: unknown) => boolean;
  /** Return the shell to its default navigation mode. */
  resetMode: () => void;
}

export function useWeupNavMode(): UseWeupNavModeReturn {
  function requestMode(mode: unknown): boolean {
    if (!isWeupNavMode(mode)) {
      return false;
    }

    // Idempotent: re-requesting the active mode is a no-op, never a re-dispatch.
    if (_activeMode.value === mode) {
      return true;
    }

    _activeMode.value = mode;
    return true;
  }

  function resetMode(): void {
    _activeMode.value = "DISCOVER";
  }

  return {
    activeMode: readonly(_activeMode),
    requestMode,
    resetMode,
  };
}

/** Test-only singleton reset. */
export function resetWeupNavModeForTests(): void {
  _activeMode.value = "DISCOVER";
}
