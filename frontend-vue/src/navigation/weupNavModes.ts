/**
 * WEUP-SYNTH:
 * source=components/BottomNav.tsx (ViewMode: types/index.ts)
 * destination=frontend-vue/src/navigation/weupNavModes.ts
 * mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
 * notes=Source ViewMode ("DISCOVER" | "ACTIVITY" | "SAVED" | "PROFILE" | "CREATE")
 *   translated natively. This module is a TYPE + MAPPING CONTRACT only: it owns
 *   no runtime state. The authoritative UI state remains useDiscoveryState and
 *   the existing composables; WeupNavMode is the shell's navigation-mode
 *   projection (see composables/useWeupNavMode.ts), which dispatches every
 *   transition into the canonical composables. No second routing/state model.
 */

/**
 * Canonical navigation modes, translated 1:1 from the AI Studio source
 * ViewMode (types/index.ts: 'DISCOVER' | 'ACTIVITY' | 'SAVED' | 'PROFILE' | 'CREATE').
 */
export type WeupNavMode = "DISCOVER" | "ACTIVITY" | "SAVED" | "PROFILE" | "CREATE";

export const WEUP_NAV_MODES: readonly WeupNavMode[] = [
  "DISCOVER",
  "ACTIVITY",
  "SAVED",
  "PROFILE",
  "CREATE",
] as const;

export function isWeupNavMode(value: unknown): value is WeupNavMode {
  return (
    typeof value === "string" &&
    (WEUP_NAV_MODES as readonly string[]).includes(value)
  );
}

export type NavSurfaceDisposition = "ADAPTED" | "PROJECTION_DEFINED";

export interface NavSurfaceMapping {
  /** Navigation mode. */
  readonly mode: WeupNavMode;
  /** Source behavior being translated (from AI Studio HomeClient/OverlayLayer). */
  readonly sourceBehavior: string;
  /** Vue surface the mode resolves to. */
  readonly vueSurface: string;
  /** Disposition per mission §30 vocabulary (adapted to the G6 slice). */
  readonly disposition: NavSurfaceDisposition;
  /** Slice owning the surface's full implementation. */
  readonly owner: "G6" | "G7" | "G9" | "G11";
  /** Interim behavior while the owning slice is pending, if any. */
  readonly notes: string;
}

/**
 * Mode -> surface mapping contract for the canonical bottom navigation.
 * Every mode resolves to a declared surface; no dead navigation entries.
 * CREATE and PROFILE surfaces are owned by G11 and G9 respectively; their
 * mappings are declared here so G6 does not pre-execute downstream slices.
 */
export const NAV_SURFACE_MAP: Readonly<Record<WeupNavMode, NavSurfaceMapping>> =
  {
    DISCOVER: {
      mode: "DISCOVER",
      sourceBehavior:
        "HomeClient depth-0 world surface: map with search/filter affordances; modalState cleared on entry.",
      vueSurface:
        "World surface (MapSurface full-bleed) + calendar overlay at partial layer; selection cleared; saved sheet closed.",
      disposition: "ADAPTED",
      owner: "G6",
      notes:
        "Implemented in G6: dispatch closes the saved sheet, clears selection, sets overlay to partial.",
    },
    ACTIVITY: {
      mode: "ACTIVITY",
      sourceBehavior:
        "OverlayLayer: activeMode === 'ACTIVITY' opens the CulturalCalendar temporal overlay (Depth 1).",
      vueSurface:
        "CalendarOverlayShell at expanded layer — the Vue counterpart of CulturalCalendar (temporal projection of the canonical map set).",
      disposition: "ADAPTED",
      owner: "G6",
      notes:
        "Projection defined from source behavior. Calendar overlay shell already renders the canonical projection; full calendar/map temporal synchronization is owned by G7.",
    },
    SAVED: {
      mode: "SAVED",
      sourceBehavior:
        "OverlayLayer: activeMode === 'SAVED' opens the SavedEvents overlay panel.",
      vueSurface:
        "SavedEventsPanel mounted as a shell overlay sheet, wired to useSavedEventsCollection (items, counts, refresh, select-event).",
      disposition: "ADAPTED",
      owner: "G6",
      notes:
        "Implemented in G6. Saved-state enrichment (folders, profile coherence) is owned by G9.",
    },
    PROFILE: {
      mode: "PROFILE",
      sourceBehavior:
        "OverlayLayer: activeMode === 'PROFILE' opens ProfilePanel (wallet/tier/reputation surface).",
      vueSurface:
        "ProfilePanel mounted as a shell overlay sheet (z-150), wired to the canonical saved-state authority: saved count from useSavedEventsCollection.surfaceSnapshot, city/context from anonymous discovery context + user-context preferences, prototype-local folders, prototype-labeled social tiers. Fabrication path (profileService/Gemini) excluded; server profile explicitly unavailable (BLOCKED_WITH_REASON: no profile API observed).",
      disposition: "ADAPTED",
      owner: "G9",
      notes:
        "Implemented in G9 (WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001): App.vue profile overlay sheet opens on PROFILE mode; fabrication paths excluded per G2 arbitration; folders prototype-local per G3 chain.",
    },
    CREATE: {
      mode: "CREATE",
      sourceBehavior:
        "HomeClient handleModeChange: mode === 'CREATE' sets modalState = 'ADD_EVENT', opening AddEventModal (ingestion wizard).",
      vueSurface:
        "AddEventWizard (useIngestionWizard) opens on CREATE mode: all five source choices, per-source validation presentation, real /api/ingestion/* submission (raw source payloads), job polling with the observed lifecycle, candidate review with evidence/issues/confidence, and acceptance into the canonical EventDetailModal preview. EXTERNAL_FEED exposes the path but blocks submission with BLOCKED_WITH_REASON. The assistant surface never touches ingestion.",
      disposition: "ADAPTED",
      owner: "G11",
      notes:
        "Implemented in G11 (WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001): CREATE dispatch opens the wizard in App.vue; closing without acceptance resets to DISCOVER. Candidate acceptance projects through the canonical EventDetailModal contract labeled CANDIDATE — never promoted into canonical event state (no backend publish endpoint exists).",
    },
  };
