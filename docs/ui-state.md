# UI State Topology — World Surface (P03)

Summary
- Goal: Normalize UI state, remove render-time mutations, and provide a typed reducer-based coordinator.

Core problems found
- State updates performed during render in `RadarMap.tsx` and `AddEventModal.tsx` (fixed).
- Orchestration state scattered across `app/page.tsx` and many components (modal open/close, selected event, ghost draft, map center/bounds, temporal selection).
- Mocked persistence and ad-hoc in-component mutation patterns make testing and future backend seams brittle.

Normalized topology
- Coordinator hook: `useWorldSurfaceState()` (hooks/useWorldSurfaceState.tsx)
- Types: `types/ui.ts` (discriminated action union + `WorldSurfaceState`).
- Ownership rules:
  - `world-surface` coordinator: owns selection, modal stack, ghost draft, view mode, temporal selection, map center/bounds.
  - `services` (stateless): data fetching / persistence (e.g., `eventService`) — no UI mutation.
  - `presentational components`: receive props & dispatch actions via hook helpers; no direct cross-component mutation.

Implementation
- Added `types/ui.ts` — typed state + actions.
- Added `hooks/useWorldSurfaceState.tsx` — reducer + helper dispatch wrappers.
- Fixed render-time mutation patterns in `components/RadarMap.tsx` and `components/AddEventModal.tsx` by moving conditional state updates into `useEffect`.

Concise state transition table
- `SELECT_EVENT` : selectedEventId = id
- `INTEREST_EVENT`: interestedEventId = id (camera fly-to)
- `OPEN_MODAL` : push modal kind onto modal stack
- `CLOSE_MODAL`: pop modal stack or set NONE
- `SET_MAP_CENTER`: update map center (no side effects inside reducer)
- `SET_MAP_BOUNDS`: update map bounds
- `SET_TEMPORAL_MODE` / `SET_SELECTED_DATE`: update temporal controls (UI triggers sweep animation)
- `TOGGLE_SAVE_EVENT`: add/remove id from `savedEventIds`
- `BEGIN_DRAFT`: set `ghostDraft`
- `UPDATE_DRAFT`: patch `ghostDraft`
- `PUBLISH_DRAFT`: clear `ghostDraft` (and production path should call service to persist)

Notes & next steps
- Integrate `useWorldSurfaceState()` into `app/page.tsx` and a world coordinator component (suggested `features/world/WorldCoordinator.tsx`) to thin the page-level orchestration.
- Replace remaining render-time updates (search for `if (` patterns that call setState outside effects) and refactor into effects or dispatch actions.
- Add unit tests for reducer transitions and small integration tests for `WorldCoordinator` behavior.
