# M4-P20: Merge Lineage and Event Evolution v1.0

## Goal

Canonical events must preserve traceability when they evolve through deduplication and merge operations. This document defines the structured lineage and evolution semantics used by Phase 0.

## Lineage Model

### Canonical Identity Continuity

- `EventAggregate.CanonicalEventId` is immutable.
- Safe merges append lineage and provenance state to the existing canonical identity.
- Merge operations do not replace canonical identity and do not rewrite historical entries.

### Structured Merge Lineage

`EventMergeLineage` tracks merge ancestry and applied merge metadata:

- `ParentCanonicalEventId`: optional parent chain for canonical-to-canonical consolidation.
- `MergedCanonicalEventIds`: canonical IDs merged into this canonical event (if applicable).
- `AppliedMergePlanIds`: append-only merge operation IDs.
- `MergedSourceRefs`: append-only source references retained across merges.
- `MergeRecords`: append-only `EventMergeRecord[]` with:
  - merge id
  - merged timestamp
  - merge reason
  - merge actor
  - merge origin (system/pipeline)
  - merge confidence
  - candidate IDs
  - structured source references (`EventMergeSourceReference[]`)
  - merge rationale
  - manual-review requirement flag

### Source Traceability Rules

- Source references from canonical and incoming candidate are unioned, never dropped.
- `Provenance.SourceRefs` and `EventMergeLineage.MergedSourceRefs` remain inspectable post-merge.
- Per-merge source context is retained in `EventMergeRecord.SourceReferences`.

## Evolution Model

### Structured Evolution Types

`EventStateChangeEntry` now includes `EvolutionType` (`EventEvolutionType`) to classify evolution events:

- `TitleUpdate`
- `VenueCorrection`
- `TimeCorrection`
- `Reschedule`
- `Cancellation`
- `ModerationOverride`
- `Publish`
- `Unpublish`
- `MergeApplied`
- `DemergeReview`
- `Other`

### Evolution Classification

Evolution type is derived from `EventVersionReason` + changed fields + before/after state:

- merge operations -> `MergeApplied`
- reschedule -> `Reschedule`
- cancel -> `Cancellation`
- moderation transitions -> `ModerationOverride`
- publish status transitions -> `Publish`/`Unpublish`
- content edits with title/venue/time changes -> typed correction/update variants

## Query and Review Surfaces

### Moderation/Admin Queryability

Resolution/provenance services expose:

- field lineage (`FieldLineage[]`)
- merge history (`MergeHistoryEntry[]`)
- evolution history projection (`EventEvolutionHistoryEntry[]`)

These are append-only projections suitable for moderation review, incident analysis, and audit workflows.

## Interaction with Dedup, Moderation, and Audit

### Dedup and Merge Planning

- Dedup + merge planning proposes merge-safe field decisions.
- Merge execution appends lineage records and provenance entries.
- Unsafe merges continue to route to manual review.

### Moderation

- Merge updates remain versioned and can set `RequiresModerationReview`.
- Moderation tools can inspect evolution and merge history without accessing raw ingestion payloads.

### Future Audit/Reporting Extensions

- `MergeRecords` and `EventEvolutionHistoryEntry` support trend reporting:
  - merge volume by actor/system origin
  - confidence drift across repeated merges
  - correction frequency (title/venue/time)
  - moderation override rates following merges

## Non-Goals (MVP)

- No generic graph engine.
- No destructive lineage rewrites.
- No canonical identity reassignment in safe merge flow.
