WeUP Phase0 — Runtime Spine
=================================

Overview
--------
This change extracts the application's runtime orchestration into a single coordinator layer to form a clear "world-surface" runtime spine while preserving the original UX and visual behavior.

Key Layers
----------
- App Shell / World Surface: `app/page.tsx` (now thin) + `features/world/WorldCoordinator.tsx` (runtime owner).
- Feature State Orchestration: `features/*` (coordinator-level orchestration, local state and mode management).
- Data Access Services: `services/eventService.ts` (keeps fetch/cache seam; ready for backend integration).
- Domain Models: `types/index.ts` (strict types for domain entities and UI state).
- UI Components: `components/*` (presentational; no orchestration responsibility).
- Shared Hooks / Utilities: `hooks/*` and `lib/*` (no changes yet; place for future shared logic).

Runtime Responsibilities (moved)
--------------------------------
- Active mode, modal stack, selected event, map interaction, temporal state, side-panel state: moved from `app/page.tsx` into `features/world/WorldCoordinator.tsx`.
- Async event feed loading and debounce: now handled by `features/world/WorldCoordinator.tsx` via `services/eventService.ts`.
- Saved/profile/social/add-event flows: orchestration (UI state transitions) consolidated in the coordinator; components remain presentational.

Why this structure
-------------------
- Keeps `app/page.tsx` thin and composition-oriented for Next.js.
- Preserves map-first, single-viewport UX (no routing/scrolling changes).
- Avoids external state libraries; uses React local state and a single coordinator for orchestration.
- Makes service seams explicit for replacing `eventService` with a real API.

Next steps
----------
- Move any remaining imperative orchestration from components into coordinator or hooks.
- Add `hooks/useWorld.ts` for derived selectors and small action helpers if needed.
- Add CI TypeScript checks and run the app to validate runtime behavior.
