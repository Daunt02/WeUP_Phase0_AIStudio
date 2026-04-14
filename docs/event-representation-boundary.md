# Event Representation Boundary (H1-P01)

## Purpose

Eliminate representation drift by assigning one clear role to each event shape and enforcing explicit mapping seams.

## Canonical Roles

| Layer                         | Type                                                                                               | Role                                                                                                     | Allowed To Depend On                     |
| ----------------------------- | -------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------- | ---------------------------------------- |
| Frontend legacy compatibility | `NightlifeItem`                                                                                    | Prototype-only compatibility shape used by legacy modal/view components during migration. Not canonical. | UI components only via boundary adapters |
| Frontend runtime projection   | `RuntimeEventProjection`                                                                           | Active world-surface working model for map/calendar/detail orchestration.                                | UI hooks/components                      |
| Frontend domain canonical     | `EventAggregate`                                                                                   | Canonical event truth used by projection mappers and transition logic.                                   | Domain mappers and transitions           |
| Backend API contracts         | `EventMapCardDto`, `EventCalendarDto`, `EventDetailDto`, `DraftSubmissionRequest`, `SubmissionDto` | Wire/API DTOs for transport in/out of backend endpoints.                                                 | Endpoints/application services           |
| Backend EF persistence        | `EventEntity` (+ `EventSourceEntity`, `EventMediaEntity`, `EventReviewEntity`)                     | Storage model optimized for DB concerns. Not returned directly to clients.                               | EF repositories only                     |

## Approved Transformations

Only these transformations are approved in active code paths:

1. `EventAggregate -> EventMapCardProjection | EventCalendarProjection | EventDetailProjection`
2. `RuntimeEventProjection -> DraftSubmissionRequest`
3. `EventEntity -> EventMapCardDto | EventCalendarDto | EventDetailDto`
4. `DraftSubmissionRequest -> EventEntity (submission draft write)`
5. `NightlifeItem <-> RuntimeEventProjection` only inside `features/world/legacyBoundary.ts`
6. `Partial<NightlifeItem> <-> GhostDraftPatch` only inside `features/world/legacyBoundary.ts`

Any new conversion must be added to this file and implemented in one dedicated mapper module.

## Prohibited Direct Usage

1. Do not consume `NightlifeItem` in hooks/services that drive active feed, query, or submission orchestration.
2. Do not expose `EventEntity` from repository boundaries.
3. Do not bind UI components directly to backend DTOs.
4. Do not mutate `EventAggregate` with display-only fields.

## Enforced Boundary Modules

1. `features/world/legacyBoundary.ts`: legacy prototype compatibility only
2. `domains/event/projections.ts`: canonical aggregate to UI projections
3. `features/world/runtimeTypes.ts`: runtime projection and submission request mapping
4. `backend/WeUP.Infrastructure/Persistence/EfEventRepository.cs`: EF entity to contracts mapping

## Consolidation Notes

1. Active world feed now consumes map-feed projections only; legacy merge path was removed.
2. Submission flow now maps runtime projections into contract-shaped draft requests.
3. Legacy `NightlifeItem` remains temporary and isolated behind a single adapter boundary.
