# WeUP Phase 0 — Runtime Spine (P01)

## Target Architecture

The app is a **map-first single-viewport world surface**. There is no page
navigation. All interactions project as layered overlays on top of a
persistent map. The runtime is structured as:

```
app/page.tsx                  ← thin composition root (Next.js entry)
  └─ features/world/
       └─ WorldCoordinator    ← runtime spine; owns all coordinator state
            ├─ hooks/useWorldSurfaceState  ← reducer: view mode, modal stack,
            │                                map state, temporal state,
            │                                saved events, ghost draft
            ├─ hooks/useEventFeed          ← bounds-driven event fetching seam
            ├─ hooks/useTemporalQuery      ← temporal preset → time window seam
            └─ hooks/useEventSubmission    ← draft → review submission seam
```

Components in `components/` are **purely presentational** — they accept typed
props and emit typed callbacks. They have no knowledge of fetch, state
management, or service calls.

---

## Layered Structure

| Layer               | Path                                                                                | Responsibility                                                                           |
| ------------------- | ----------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------- |
| App shell           | `app/`                                                                              | Next.js entry, layout, global CSS                                                        |
| Runtime coordinator | `features/world/WorldCoordinator.tsx`                                               | Owns all runtime state; composes the surface                                             |
| Coordinator state   | `hooks/useWorldSurfaceState.tsx`                                                    | Reducer backing the coordinator                                                          |
| Data seams          | `hooks/useEventFeed.ts`, `hooks/useTemporalQuery.ts`, `hooks/useEventSubmission.ts` | Async data access, isolated and replaceable                                              |
| Service facades     | `services/`                                                                         | HTTP client wrappers; mock in Phase 0, real API in P09+                                  |
| Domain models       | `domains/event/`, `domains/query/`                                                  | Canonical types, projections, lifecycle transitions                                      |
| UI types            | `types/ui.ts`                                                                       | World-surface state types (`ViewMode`, `ModalState`, `WorldSurfaceState`, `WorldAction`) |
| Entity types        | `types/index.ts`                                                                    | Domain entities (`NightlifeItem`, etc.) + re-exports from `types/ui.ts`                  |
| UI components       | `components/`                                                                       | Presentational; depend on types only                                                     |
| Utilities           | `hooks/use-mobile.ts`, `lib/`, `utils/`                                             | Shared, stateless helpers                                                                |

---

## Coordinator State Model

`WorldSurfaceState` (in `types/ui.ts`) is the single source of truth for:

- **`viewMode`** — active UI surface: `RADAR | CALENDAR | PROFILE | SAVED`
- **`modal`** — stacked modal system: `NONE | STACK(ModalKind[])`
- **`selectedEventId`** — which event the detail modal is showing
- **`interestedEventId`** — ephemeral camera interest signal from map
- **`mapCenter`** / **`mapBounds`** — current map viewport
- **`temporalMode`** / **`selectedDate`** — temporal filter state
- **`savedEventIds`** — local save list (seam for backend sync later)
- **`ghostDraft`** — in-progress event creation draft

The reducer and helper dispatchers live in `hooks/useWorldSurfaceState.tsx`.
This is NOT Redux — it is a plain `useReducer` with a typed action union.

---

## Runtime Responsibilities — Before / After

| Responsibility                 | Before                                                          | After                                                     |
| ------------------------------ | --------------------------------------------------------------- | --------------------------------------------------------- |
| Active view mode               | Parallel `activeMode` useState + `state.viewMode` (two systems) | Single `state.viewMode` from reducer                      |
| Modal stack                    | `state.modal` in reducer                                        | Unchanged                                                 |
| Map state (center, bounds)     | `state.mapCenter/mapBounds` in reducer                          | Unchanged                                                 |
| Event feed loading             | Inline `useEffect` in `WorldCoordinator`                        | `hooks/useEventFeed.ts`                                   |
| Temporal query                 | Inline `useEffect` in `WorldCoordinator`                        | `hooks/useTemporalQuery.ts`                               |
| Draft publish / submission     | Inline `handlePublish` in `WorldCoordinator`                    | `hooks/useEventSubmission.ts`                             |
| Selected event + detail modal  | `selectEvent` + `openModal` in coordinator                      | Unchanged                                                 |
| Saved events                   | `toggleSave` in reducer                                         | Unchanged                                                 |
| Ghost draft                    | `beginDraft` / `updateDraft` / `publishDraft`                   | Unchanged                                                 |
| ViewMode type                  | Defined twice (`types/index.ts` wide + `types/ui.ts` narrow)    | Owned by `types/ui.ts`; re-exported from `types/index.ts` |
| EventSignalModal display state | Imported `ModalState` from `@/types` (conflicting name)         | Renamed to `EventSignalState` in `types/index.ts`         |

---

## Service Seams (Phase 0 → Real Backend)

Each hook owns exactly one service boundary. To wire a real backend:

| Hook                 | Service                                             | Endpoint (future)                   |
| -------------------- | --------------------------------------------------- | ----------------------------------- |
| `useEventFeed`       | `eventService.fetchEventsInBounds`                  | `GET /api/events?bounds=...`        |
| `useTemporalQuery`   | `temporalService.getEventsAtTime`                   | `POST /api/temporal/events-at-time` |
| `useEventSubmission` | `submissionService.createDraft` + `submitForReview` | `POST /api/events/submissions`      |

---

## Design Constraints

- No Redux. State is `useReducer` in the coordinator hook.
- No routing. The world surface is a single viewport; overlays use `isVisible` props.
- No server components on the world surface (Mapbox requires client rendering).
- `app/page.tsx` must remain thin — just `<WorldCoordinator />`.
- Components must not import from services or hooks directly.
