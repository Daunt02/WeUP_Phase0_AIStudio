// Normalized UI state types for the world surface coordinator
// Split into persisted (persistence-ready) and transient (ephemeral) slices.
// Ownership rules (convention):
// - Page / Coordinator (`useWorldSurfaceState`) owns orchestration and cross-cutting UI state.
// - Presentation components (maps, panels, modals) own purely local ephemeral UI (hover, focused input) only.
// - Persisted-ready slices (lastKnownMapCenter) are exposed for storage sync.

export type ViewMode = "RADAR" | "CALENDAR" | "PROFILE" | "SAVED";

export type ModalKind =
  | "ADD_EVENT"
  | "EVENT_DETAIL"
  | "GEO_CONTROLS"
  | "PROFILE"
  | "NONE";

export type ModalState =
  | { kind: "NONE" }
  | { kind: "STACK"; stack: ModalKind[] };

export type TemporalMode = "TODAY" | "NEXT_7_DAYS" | "CUSTOM_DATE";

// Draft object used while creating a new event on the UI surface. Ephemeral by default.
export interface GhostDraft {
  id?: string;
  latitude?: number;
  longitude?: number;
  title?: string;
  venue_name?: string;
  address?: string;
  category?: string;
  start_time?: string;
  end_time?: string;
  description?: string;
  image_url?: string;
  price_tier?: string;
  tags?: string[];
}

// Persisted slice: what we consider safe to persist to localStorage.
export interface PersistedUIState {
  lastKnownMapCenter: { lat: number; lng: number } | null;
  // Backward-compat only for legacy local save migration.
  savedEventIds?: string[];
}

// Transient UI: ephemeral during a session and not persisted automatically.
export interface TransientUIState {
  viewMode: ViewMode;
  selectedEventId: string | null;
  interestedEventId: string | null; // temporary 'camera interest' signal
  modal: ModalState;
  mapCenter: { lat: number; lng: number };
  mapBounds: {
    minLat: number;
    maxLat: number;
    minLng: number;
    maxLng: number;
  } | null;
  temporalMode: TemporalMode;
  selectedDate: string; // human readable day string used by UI
  ghostDraft: GhostDraft | null;
}

// Full coordinator state keeps save IDs as an always-present synchronized field.
export type WorldSurfaceState = TransientUIState &
  Omit<PersistedUIState, "savedEventIds"> & {
    savedEventIds: string[];
  };

// Actions are discriminated union of typed events used to transition state.
export type WorldAction =
  | { type: "SELECT_EVENT"; id: string | null }
  | { type: "INTEREST_EVENT"; id: string | null }
  | { type: "OPEN_MODAL"; modal: ModalKind }
  | { type: "CLOSE_MODAL" }
  | { type: "SET_VIEW_MODE"; mode: ViewMode }
  | { type: "SET_MAP_CENTER"; center: { lat: number; lng: number } }
  | {
      type: "SET_MAP_BOUNDS";
      bounds: {
        minLat: number;
        maxLat: number;
        minLng: number;
        maxLng: number;
      };
    }
  | { type: "SET_TEMPORAL_MODE"; mode: TemporalMode }
  | { type: "SET_SELECTED_DATE"; date: string }
  | { type: "SET_SAVED_EVENT_IDS"; ids: string[] }
  | { type: "TOGGLE_SAVE_EVENT"; id: string }
  | { type: "BEGIN_DRAFT"; draft: GhostDraft }
  | { type: "UPDATE_DRAFT"; patch: Partial<GhostDraft> }
  | { type: "PUBLISH_DRAFT"; id: string }
  | { type: "ENTER_SIDE_PANEL" }
  | { type: "EXIT_SIDE_PANEL" }
  | { type: "RESTORE_PERSISTED"; persisted: PersistedUIState };
