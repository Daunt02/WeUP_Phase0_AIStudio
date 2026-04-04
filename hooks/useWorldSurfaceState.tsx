"use client";
import { useReducer, useCallback } from 'react';
import { WorldSurfaceState, WorldAction } from '@/types/ui';

const initialState: WorldSurfaceState = {
  viewMode: 'RADAR',
  selectedEventId: null,
  interestedEventId: null,
  modal: { kind: 'NONE' },
  mapCenter: { lat: 29.7604, lng: -95.3698 },
  mapBounds: null,
  temporalMode: 'TODAY',
  selectedDate: 'DAY 1',
  savedEventIds: [],
  ghostDraft: null,
};

function reducer(state: WorldSurfaceState, action: WorldAction): WorldSurfaceState {
  switch (action.type) {
    case 'SELECT_EVENT':
      return { ...state, selectedEventId: action.id };
    case 'INTEREST_EVENT':
      return { ...state, interestedEventId: action.id };
    case 'OPEN_MODAL': {
      if (state.modal.kind === 'STACK') {
        return { ...state, modal: { kind: 'STACK', stack: [...state.modal.stack, action.modal] } };
      }
      return { ...state, modal: { kind: 'STACK', stack: [action.modal] } };
    }
    case 'CLOSE_MODAL': {
      if (state.modal.kind !== 'STACK' || state.modal.stack.length <= 1) return { ...state, modal: { kind: 'NONE' } };
      const stack = [...state.modal.stack];
      stack.pop();
      return { ...state, modal: { kind: 'STACK', stack } };
    }
    case 'SET_VIEW_MODE':
      return { ...state, viewMode: action.mode };
    case 'SET_MAP_CENTER':
      return { ...state, mapCenter: action.center };
    case 'SET_MAP_BOUNDS':
      return { ...state, mapBounds: action.bounds };
    case 'SET_TEMPORAL_MODE':
      return { ...state, temporalMode: action.mode };
    case 'SET_SELECTED_DATE':
      return { ...state, selectedDate: action.date };
    case 'TOGGLE_SAVE_EVENT': {
      const exists = state.savedEventIds.includes(action.id);
      return { ...state, savedEventIds: exists ? state.savedEventIds.filter(i => i !== action.id) : [...state.savedEventIds, action.id] };
    }
    case 'BEGIN_DRAFT':
      return { ...state, ghostDraft: action.draft };
    case 'UPDATE_DRAFT':
      return { ...state, ghostDraft: { ...(state.ghostDraft || {}), ...action.patch } };
    case 'PUBLISH_DRAFT':
      return { ...state, ghostDraft: null };
    case 'ENTER_SIDE_PANEL':
      return { ...state, viewMode: 'PROFILE' };
    case 'EXIT_SIDE_PANEL':
      return { ...state, viewMode: 'RADAR' };
    default:
      return state;
  }
}

export function useWorldSurfaceState() {
  const [state, dispatch] = useReducer(reducer, initialState);

  // Helper dispatch wrappers
  const selectEvent = useCallback((id: string | null) => dispatch({ type: 'SELECT_EVENT', id }), []);
  const interestEvent = useCallback((id: string | null) => dispatch({ type: 'INTEREST_EVENT', id }), []);
  const openModal = useCallback((modal: any) => dispatch({ type: 'OPEN_MODAL', modal }), []);
  const closeModal = useCallback(() => dispatch({ type: 'CLOSE_MODAL' }), []);
  const setMapCenter = useCallback((center: { lat: number; lng: number }) => dispatch({ type: 'SET_MAP_CENTER', center }), []);
  const setMapBounds = useCallback((bounds: any) => dispatch({ type: 'SET_MAP_BOUNDS', bounds }), []);
  const toggleSave = useCallback((id: string) => dispatch({ type: 'TOGGLE_SAVE_EVENT', id }), []);
  const beginDraft = useCallback((draft: any) => dispatch({ type: 'BEGIN_DRAFT', draft }), []);
  const updateDraft = useCallback((patch: any) => dispatch({ type: 'UPDATE_DRAFT', patch }), []);
  const publishDraft = useCallback((id: string) => dispatch({ type: 'PUBLISH_DRAFT', id }), []);

  return {
    state,
    dispatch,
    selectEvent,
    interestEvent,
    openModal,
    closeModal,
    setMapCenter,
    setMapBounds,
    toggleSave,
    beginDraft,
    updateDraft,
    publishDraft
  };
}
