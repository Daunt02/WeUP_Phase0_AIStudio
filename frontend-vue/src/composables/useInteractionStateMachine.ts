/**
 * WEUP-2.5D (D17) — Interaction state-machine integration.
 * mission=WEUP-PHASE0-2.5D-FULL-EXPRESSION-001
 *
 * Central coordination for SignalState / ClusterState / CorridorState /
 * InspectorState / CommandState / NavigationState and the chain
 *   STATE → DEPTH STATE → VISUAL STATE → INTERACTION SURFACE   (mission §XIV)
 *
 * COORDINATION, not replacement: this module owns NO business logic. It reads
 * the existing composables (useDiscoveryState, useTemporalNavigation,
 * useWeupNavMode, useMapEvents, useEventDetailModal, useAssistantSession,
 * useIngestionWizard) and derives the canonical interaction state that drives
 * the `data-state` / `data-plane` attributes the D04–D13 components already
 * render. Same state in → same visual out (deterministic; transition table in
 * receipts/WEUP-PHASE0-2.5D-FULL-EXPRESSION-001/17-state-machine-verification.md).
 *
 * Depth values are never produced here — only state names. All visual values
 * come from frontend-vue/src/styles/weup-2.5d-tokens.css.
 */
import { computed, onBeforeUnmount, ref, type Ref } from "vue";
import type { WeupNavMode } from "../navigation/weupNavModes";

/* ─── Canonical vocabulary (mission §XIV) ─────────────────────────────────── */

export type CompositionKind = "mobile" | "tablet" | "desktop" | "wide";
export type SignalState =
  | "idle"
  | "focused"
  | "selected"
  | "expanded"
  | "actionable"
  | "archived";
export type ClusterState = "discovered" | "visible" | "focused" | "selected";
export type CorridorState = "visible" | "active" | "emphasized";
export type InspectorState = "closed" | "visible" | "expanded";
export type CommandState = "idle" | "visible" | "actionable" | "degraded";
export type DepthPlane = "z0" | "z1" | "z2" | "z3" | "z4";

/**
 * Canonical breakpoints — the single JS source of truth. Literals mirror
 * --weup-breakpoint-*-min/max in weup-2.5d-tokens.css (CSS vars cannot drive
 * @media, so both layers pin the same numbers; they must stay in sync).
 */
export const COMPOSITION_BREAKPOINTS = {
  mobileMax: 767,
  tabletMin: 768,
  tabletMax: 1023,
  desktopMin: 1024,
  desktopMax: 1439,
  wideMin: 1440,
} as const;

/** Pure: viewport width → composition kind. Deterministic by construction. */
export function compositionForWidth(width: number): CompositionKind {
  if (width <= COMPOSITION_BREAKPOINTS.mobileMax) return "mobile";
  if (width <= COMPOSITION_BREAKPOINTS.tabletMax) return "tablet";
  if (width <= COMPOSITION_BREAKPOINTS.desktopMax) return "desktop";
  return "wide";
}

/**
 * Viewport width tracker. matchMedia is the primary source (canonical
 * boundaries); falls back to window.innerWidth; 1024 outside a browser.
 * Guarded for jsdom/test environments where matchMedia is absent.
 */
export function useViewportWidth(): Ref<number> {
  const width = ref(
    typeof window !== "undefined" ? window.innerWidth : 1024,
  );
  if (typeof window === "undefined" || typeof window.matchMedia !== "function") {
    return width;
  }
  const queries = [
    `(max-width: ${COMPOSITION_BREAKPOINTS.mobileMax}px)`,
    `(min-width: ${COMPOSITION_BREAKPOINTS.tabletMin}px) and (max-width: ${COMPOSITION_BREAKPOINTS.tabletMax}px)`,
    `(min-width: ${COMPOSITION_BREAKPOINTS.desktopMin}px)`,
  ].map((q) => window.matchMedia(q));
  const update = (): void => {
    width.value = window.innerWidth;
  };
  for (const mql of queries) {
    if (typeof mql.addEventListener === "function") {
      mql.addEventListener("change", update);
    }
  }
  onBeforeUnmount(() => {
    for (const mql of queries) {
      if (typeof mql.removeEventListener === "function") {
        mql.removeEventListener("change", update);
      }
    }
  });
  return width;
}

/* ─── Pure state derivations (unit-tested; no Vue component needed) ───────── */

export interface ClusterIdentity {
  focusedClusterId: string | null;
  selectedEventId: string | null;
}

/**
 * Cluster state for one cluster id. Single FOCUSED by construction: focus is
 * one nullable id, never a set. Selection clears focus (deterministic).
 */
export function clusterStateFor(
  clusterId: string,
  identity: ClusterIdentity,
): ClusterState {
  if (
    identity.selectedEventId !== null &&
    identity.selectedEventId === clusterId
  ) {
    return "selected";
  }
  if (
    identity.selectedEventId === null &&
    identity.focusedClusterId === clusterId
  ) {
    return "focused";
  }
  return "visible";
}

export interface CorridorEdgeDistricts {
  a: string;
  b: string;
}

export interface CorridorIdentity {
  /** District applied as a filter (existing activeDistrict prop). */
  activeDistrict?: string | null;
  /** District of the SELECTED cluster (D17: corridor emphasis on select). */
  emphasizedDistrict?: string | null;
}

/**
 * Corridor edge state. Priority: emphasized (selection) > active (filter) >
 * visible. Selection is more specific than the filter, so it wins when both
 * touch the same edge.
 */
export function corridorStateFor(
  edge: CorridorEdgeDistricts,
  identity: CorridorIdentity,
): CorridorState {
  const touches = (district: string | null | undefined): boolean =>
    !!district && (edge.a === district || edge.b === district);
  if (touches(identity.emphasizedDistrict)) return "emphasized";
  if (touches(identity.activeDistrict)) return "active";
  return "visible";
}

export function inspectorStateFor(input: {
  open: boolean;
  expanded: boolean;
}): InspectorState {
  if (!input.open) return "closed";
  return input.expanded ? "expanded" : "visible";
}

export function commandStateFor(input: {
  assistantOpen: boolean;
  wizardOpen: boolean;
  wizardDegraded: boolean;
  thinking: boolean;
}): CommandState {
  if (input.wizardDegraded) return "degraded";
  if (input.wizardOpen || (input.assistantOpen && input.thinking))
    return "actionable";
  if (input.assistantOpen) return "visible";
  return "idle";
}

export function signalStateFor(input: {
  selected: boolean;
  expanded: boolean;
  actionable: boolean;
  archived: boolean;
}): SignalState {
  if (input.archived) return "archived";
  if (input.actionable) return "actionable";
  if (input.expanded) return "expanded";
  if (input.selected) return "selected";
  return "idle";
}

/** State → depth plane (mission §IV; static per component-state pair). */
export function depthPlaneFor(kind: "inspector" | "command"): DepthPlane {
  return kind === "command" ? "z4" : "z3";
}

/* ─── Coordination composable ─────────────────────────────────────────────── */

export interface InteractionInputs {
  viewportWidth: Ref<number>;
  /** Explicit focus override (machine-owned); null = derive from day key. */
  temporalDayKey: Ref<string | null>;
  /** Canonical selection (useDiscoveryState.selectedEventId). */
  selectedEventId: Ref<string | null>;
  /** District of the selected event (for corridor emphasis). */
  selectedDistrict: Ref<string | null>;
  /** District filter (useDiscoveryState.activeFilters.district). */
  activeDistrict: Ref<string | null | undefined>;
  inspectorOpen: Ref<boolean>;
  inspectorExpanded: Ref<boolean>;
  assistantOpen: Ref<boolean>;
  wizardOpen: Ref<boolean>;
  wizardDegraded: Ref<boolean>;
  assistantThinking: Ref<boolean>;
  activeNavMode: Ref<WeupNavMode>;
  /** Saved/profile sheet open → side inspector column (tablet/desktop). */
  sideInspectorOpen: Ref<boolean>;
}

export interface InteractionStateMachine {
  composition: Ref<CompositionKind>;
  /** Canonical focused cluster id. Single by construction; null while selected. */
  focusedClusterId: Ref<string | null>;
  requestFocus: (clusterId: string) => void;
  clearFocus: () => void;
  clusterStateForId: (clusterId: string) => ClusterState;
  corridorStateForEdge: (edge: CorridorEdgeDistricts) => CorridorState;
  /** District whose corridors are emphasized (selected cluster's district). */
  emphasizedCorridorDistrict: Ref<string | null>;
  inspector: Ref<InspectorState>;
  command: Ref<CommandState>;
  navigation: Ref<WeupNavMode>;
  /** Z2 dimming hook: true while a Z3 inspector or actionable Z4 is open. */
  dimBackground: Ref<boolean>;
  sideInspectorOpen: Ref<boolean>;
}

export function useInteractionStateMachine(
  inputs: InteractionInputs,
): InteractionStateMachine {
  /** Explicit focus requests; null = fall back to the temporal day key. */
  const focusOverride = ref<string | null>(null);

  const focusedClusterId = computed<string | null>(() => {
    // Selection clears focus — deterministic, no extra event wiring.
    if (inputs.selectedEventId.value !== null) return null;
    return focusOverride.value ?? inputs.temporalDayKey.value;
  });

  function requestFocus(clusterId: string): void {
    if (inputs.selectedEventId.value !== null) return; // selection wins
    focusOverride.value = clusterId;
  }

  function clearFocus(): void {
    focusOverride.value = null;
  }

  const composition = computed<CompositionKind>(() =>
    compositionForWidth(inputs.viewportWidth.value),
  );

  const emphasizedCorridorDistrict = computed<string | null>(
    () => inputs.selectedDistrict.value,
  );

  const inspector = computed<InspectorState>(() =>
    inspectorStateFor({
      open: inputs.inspectorOpen.value,
      expanded: inputs.inspectorExpanded.value,
    }),
  );

  const command = computed<CommandState>(() =>
    commandStateFor({
      assistantOpen: inputs.assistantOpen.value,
      wizardOpen: inputs.wizardOpen.value,
      wizardDegraded: inputs.wizardDegraded.value,
      thinking: inputs.assistantThinking.value,
    }),
  );

  const navigation = computed<WeupNavMode>(() => inputs.activeNavMode.value);

  const dimBackground = computed<boolean>(
    () => inspector.value !== "closed" || command.value === "actionable",
  );

  return {
    composition,
    focusedClusterId,
    requestFocus,
    clearFocus,
    clusterStateForId: (clusterId: string) =>
      clusterStateFor(clusterId, {
        focusedClusterId: focusedClusterId.value,
        selectedEventId: inputs.selectedEventId.value,
      }),
    corridorStateForEdge: (edge: CorridorEdgeDistricts) =>
      corridorStateFor(edge, {
        activeDistrict: inputs.activeDistrict.value ?? null,
        emphasizedDistrict: emphasizedCorridorDistrict.value,
      }),
    emphasizedCorridorDistrict,
    inspector,
    command,
    navigation,
    dimBackground,
    sideInspectorOpen: inputs.sideInspectorOpen,
  };
}
