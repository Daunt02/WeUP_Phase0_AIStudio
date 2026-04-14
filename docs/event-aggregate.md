# WeUP Phase 0 — Canonical Event Aggregate

## Purpose

Replace the prototype `NightlifeItem` (which mixed canonical data, presentation fields, and mock-generation artifacts) with a production-grade domain model that can survive ingestion, moderation, deduplication, and eventual backend persistence.

## Separation of concerns

| Layer                 | Type                                                                         | Location                       |
| --------------------- | ---------------------------------------------------------------------------- | ------------------------------ |
| Domain aggregate      | `EventAggregate`                                                             | `domains/event/types.ts`       |
| Lifecycle transitions | `transitionEventStatus`, `canTransitionEventStatus`                          | `domains/event/transitions.ts` |
| UI projections        | `EventMapCardProjection`, `EventCalendarProjection`, `EventDetailProjection` | `domains/event/projections.ts` |

**Components must only import from `projections.ts`. They must never import `EventAggregate` directly.**

## Lifecycle state machine

```
DRAFT ──► INGESTED ──► NEEDS_REVIEW ──► APPROVED ──► PUBLISHED
                               │                          │
                               ▼                          ▼
                            REJECTED                   ARCHIVED
                               │
                               ▼
                            DRAFT  (re-submission reset)
```

### Field requirements by status

| Status       | Required fields                            |
| ------------ | ------------------------------------------ |
| DRAFT        | id, status, audit                          |
| INGESTED     | + canonicalTitle, sourceRefs               |
| NEEDS_REVIEW | + confidence                               |
| APPROVED     | + venue, address, geo, timeRange, timezone |
| PUBLISHED    | same as APPROVED                           |
| REJECTED     | id, status, audit                          |
| ARCHIVED     | id, status, audit                          |

## Auto-approval

Events with `confidence >= 0.85` and all required PUBLISHED fields satisfied are eligible for auto-approval. All others require manual review. See `isPublishableEvent()` in `transitions.ts`.

## Migration from NightlifeItem

`NightlifeItem` is preserved in `types/index.ts` only as a temporary compatibility shape. Active application flow must not consume it directly outside the legacy adapter boundary in `features/world/legacyBoundary.ts`.

See `docs/event-representation-boundary.md` for the strict approved transformation paths across frontend runtime projections, canonical domain aggregate, backend DTOs, and EF entities.
