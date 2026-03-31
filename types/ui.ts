// Normalized UI state types for the world surface coordinator
export type ViewMode = 'RADAR' | 'CALENDAR' | 'PROFILE' | 'SAVED';

export type ModalKind = 'ADD_EVENT' | 'EVENT_DETAIL' | 'GEO_CONTROLS' | 'PROFILE' | 'NONE';

export type ModalState =
  | { kind: 'NONE' }
  | { kind: 'STACK'; stack: ModalKind[] };

export type TemporalMode = 'TODAY' | 'NEXT_7_DAYS' | 'CUSTOM_DATE';

export interface GhostDraft {
  id?: string;
  latitude?: number;
  longitude?: number;
  title?: string;
  venue_name?: string;
  address?: string;
  category?: string;
  start_time?: string;
  [k: string]: any;
}

export interface WorldSurfaceState {
  viewMode: ViewMode;
  selectedEventId: string | null;
  interestedEventId: string | null; // temporary 'camera interest' signal
  modal: ModalState;
  mapCenter: { lat: number; lng: number };
  mapBounds: { minLat: number; maxLat: number; minLng: number; maxLng: number } | null;
  temporalMode: TemporalMode;
  selectedDate: string; // human readable day string used by UI
  savedEventIds: string[];
  ghostDraft: GhostDraft | null;
}

export type WorldAction =
  | { type: 'SELECT_EVENT'; id: string | null }
  | { type: 'INTEREST_EVENT'; id: string | null }
  | { type: 'OPEN_MODAL'; modal: ModalKind }
  | { type: 'CLOSE_MODAL' }
  | { type: 'SET_VIEW_MODE'; mode: ViewMode }
  | { type: 'SET_MAP_CENTER'; center: { lat: number; lng: number } }
  | { type: 'SET_MAP_BOUNDS'; bounds: { minLat: number; maxLat: number; minLng: number; maxLng: number } }
  | { type: 'SET_TEMPORAL_MODE'; mode: TemporalMode }
  | { type: 'SET_SELECTED_DATE'; date: string }
  | { type: 'TOGGLE_SAVE_EVENT'; id: string }
  | { type: 'BEGIN_DRAFT'; draft: GhostDraft }
  | { type: 'UPDATE_DRAFT'; patch: Partial<GhostDraft> }
  | { type: 'PUBLISH_DRAFT'; id: string }
  | { type: 'ENTER_SIDE_PANEL' }
  | { type: 'EXIT_SIDE_PANEL' };
