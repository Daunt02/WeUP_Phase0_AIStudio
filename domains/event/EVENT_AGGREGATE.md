# WeUP Phase 0 — Canonical Event Aggregate

This document explains the canonical Phase 0 event aggregate, the lifecycle state machine, required fields by status, and helper semantics for frontend and backend parity.

## 1. Separation of concerns

- Source payload fields (`EventSourcePayload`)
  - Raw/untouched fields from ingestion or client submissions (OCR output, scraped text, pasted URLs, original media URLs).
  - Kept permissive and stored for audit / evidence.

- Normalized/canonical fields (`EventNormalized` / `EventAggregate`)
  - Cleaned, typed, validated fields that are the single source of truth.
  - Used for moderation, ranking, and publication.

- Derived/display fields (`EventDisplayFields` / projections)
  - Computed and locale-aware strings, teasers, image URLs for UI.
  - Must not be treated as authoritative data for ingestion or backend matching.

- Provenance & ingestion metadata (`IngestionMetadata`, in `provenance.ts`)
  - Rich evidence, confidence vector, and review metadata describing how the normalized fields were produced.

## 2. Event identity & key recommended fields

- Identity
  - `id: EventId` (opaque stable id)
  - `sourceRefs: SourceRef[]` (traceable source references)

- Canonical content
  - `canonicalTitle: string`
  - `canonicalDescription?: string | null`
  - `category: EventCategory`

- Venue reference
  - `venue: VenueSnapshot` (may be `venueId` when resolved)
  - `address: AddressSnapshot` (normalized address)
  - `geo: GeoPoint` (lat/lng as numbers)

- Temporal
  - `timeRange: { startUtc: string; endUtc: string | null }` (ISO 8601 UTC)
  - `timezone: string` (IANA tz like `America/Chicago`)

- Media/taxonomy
  - `mediaRefs: MediaRef[]`
  - `tags: string[]`

- Confidence & moderation
  - `confidence: number` (0..1 scalar, computed from `ConfidenceVector`)
  - `review: ReviewMetadata` (slim; full model in `provenance.ts`)

- Audit
  - `audit: AuditMetadata` (createdAt, updatedAt, createdBy)

## 3. Lifecycle state machine

Statuses (exported as `EventStatus`):

- `DRAFT` — local user draft, minimal validation
- `INGESTED` — event has been ingested by pipeline and stored as a candidate
- `NEEDS_REVIEW` — manual review required (temporal ambiguity, low confidence, policy flags)
- `APPROVED` — review passed (but not yet published)
- `PUBLISHED` — visible to end-users
- `REJECTED` — judged invalid or spam
- `ARCHIVED` — terminal state for historical events

Legal transitions (exported `LEGAL_TRANSITIONS`):

- DRAFT -> INGESTED, REJECTED
- INGESTED -> NEEDS_REVIEW, APPROVED, REJECTED
- NEEDS_REVIEW -> APPROVED, REJECTED
- APPROVED -> PUBLISHED, NEEDS_REVIEW, REJECTED
- PUBLISHED -> ARCHIVED, NEEDS_REVIEW
- REJECTED -> DRAFT (allow resubmission)
- ARCHIVED -> (none)

Use `canTransitionEventStatus(from,to)` to check legality.

## 4. Transition protections & field requirements

- `REQUIRED_FIELDS_BY_STATUS` maps which canonical fields must be present (non-null / non-empty) for each status.
- Use `getMissingFieldsForStatus(event, status)` to enumerate blockers.
- Two helper functions in `transitions.ts`:
  - `transitionEventStatus(event, to)` — throws on illegal move (useful for imperative flows)
  - `tryTransitionEventStatus(event, to)` — returns `{ ok, event?, reason? }` and performs both legal-transition check and required-field validation (preferred in async pipelines where you want to handle failures gracefully).

## 5. Confidence, provenance, and review

- Confidence is multi-dimensional (`ConfidenceVector` in `provenance.ts`) and combined by `computeAggregateConfidence()`.
- `IngestionMetadata` carries `provenance`, `confidenceVector`, `review` and `evidenceRefs`.
- Use `getReviewBlockers()` and `canAutoPublish()` to decide whether to escalate to `NEEDS_REVIEW` or allow auto-`APPROVED`/`PUBLISHED`.

## 6. Frontend / Backend parity guidance

- Frontend should treat `EventAggregate` as read-only canonical data and never attempt to mutate display-only fields back into the aggregate.
- Clients should send `EventSourcePayload` (or a `draft` shaped similarly) when creating events; backend ingestion pipelines transform `EventSourcePayload` → `EventNormalized` → `EventAggregate` while recording `IngestionMetadata`.
- Backend must enforce transitions server-side using the provided guard helpers. Never rely on client-provided `status` changes without server validation.

## 7. Why this improves ingestion & moderation

- Clear separation of raw vs canonical vs derived avoids accidental data loss and mixing presentation with truth.
- Explicit lifecycle and required-field guards prevent accidental publication of incomplete events.
- Provenance + confidence vectors enable deterministic auto-approval rules and explainable review decisions.
- Shared TypeScript types ease frontend/backend parity and reduce translation bugs during ingestion and moderation flows.

## 8. Next steps / integrations

- Wire ingestion pipelines to populate `IngestionMetadata` and compute `confidence` using `computeAggregateConfidence()`.
- Add backend-side persistence mapping for `EventAggregate` and indexes for `geo` and `timeRange`.
- Implement review UI that consumes `ReviewMetadataFull` from `provenance.ts` and calls server endpoints that use `tryTransitionEventStatus`.

## Provenance & Review Metadata (formal)

This project formalizes provenance and review metadata in `domains/event/provenance.ts`.

- Every event should include or reference an `EventProvenanceBundle` with three first-class parts:
  - `provenance` — who/what produced the source and pointers to immutable evidence
  - `confidences` — numeric confidence vector (0..1) per meaningful extraction category
  - `review` — structured review metadata (state, reviewer, notes, flags, evidence references)

Design principles:

- Traceability: `provenance.id` must be retained for any derived or merged event so the original source and evidence are auditable.
- Explicit confidences: use numeric 0..1 semantics; do not rely on ad-hoc text like "low"/"medium".
- Review-as-data: review information is structured and stored (not freeform); reviewers add `notes[]` with timestamps and severity.

Storage / DB mapping suggestions:

- Keep `provenance` (lightweight) embedded or referenced from the main event; store large `rawEvidence` assets in a separate `evidence` collection and reference by id.
- Store `review` records in a `reviews` table/collection to preserve history (allow multiple review rounds). Each review entry should reference `provenanceId` and `evidenceRefs`.

Automation helpers (see code):

- `computeReviewReadiness(bundle)` — returns 0..1 readiness.
- `canAutoPublish(bundle)` — default safe threshold 0.85.
- `requiresManualReview(bundle)` — checks flags, critical notes, and readiness threshold.

Canonical example payloads (see the code examples in this repo for JSON samples). These show how different sources map into `provenance` and the ways confidences + review metadata evolve during ingestion and moderation.
