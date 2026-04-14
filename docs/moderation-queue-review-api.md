# Moderation Queue and Review Dashboard API (P13)

## Purpose

This API exposes a deterministic, auditable moderation workflow for:

- reviewable canonical events
- ingestion candidates
- dedupe/resolution conflicts
- rollback follow-up items

The moderation queue state is separate from publish/lifecycle state.

## Queue Lifecycle

`ModerationItemStatus` tracks queue workflow:

- `Open`: waiting for moderator action
- `InReview`: moderator requested changes / active handling
- `Resolved`: decision reached
- `Closed`: archived after resolution

`ModerationReviewStatus` tracks decision semantics:

- `NEEDS_REVIEW`
- `APPROVED`
- `REJECTED`
- `CHANGES_REQUESTED`

Decision-to-lifecycle mapping (canonical event lifecycle seam):

- `Approve` -> `APPROVED`
- `Reject` -> `REJECTED`
- `RequestChanges` -> `NEEDS_REVIEW`

This mapping is applied through `IEventLifecycleRepository` and does not mutate queue history records.

## Filters

Queue filtering contract: `ModerationQueueFilter`

Supported fields:

- `Status`
- `ReviewStatus`
- `Kind`
- `SourceKind`
- `MinConfidence` / `MaxConfidence`
- `ConfidenceBucket`
- `MinDuplicateSeverity`
- `AssignedReviewerId`
- `IngestionJobId`
- `AfterUtc` / `BeforeUtc`
- `PageSize` / `Cursor`

## Evidence Inspection Model

Evidence endpoint returns `ModerationEvidenceBundle` with:

- source refs (`SourceKind`, `SourceRef`, `SourceRefs`)
- evidence refs (`EvidenceRefs`)
- confidence vector (`ConfidenceVector`)
- blocker reasons (`BlockerReasons`)
- OCR and extraction payloads when available (`OcrText`, `RawExtractionText`)
- resolution explanation for dedupe/merge contexts (`ResolutionExplanation`)

Evidence assembly is handled by `IModerationEvidenceService` and reads flyer evidence records where available.

## Review Actions

Review submission contract: `ReviewDecisionRequest`

- `ActorId`
- `Decision`: `Approve`, `Reject`, `RequestChanges`
- `Comment`
- `Reasons`
- `CorrelationId`

Decision result contract: `ReviewDecisionResponse`

- accepted/rejected outcome
- previous/new review status
- lifecycle before/after
- appended immutable `ReviewAuditRecord`

## Audit and History Rules

Each review mutation appends immutable records:

- queue-local immutable history entry (`ReviewHistoryEntry` with `RecordId`)
- append-only audit trail entry (`AuditTrailEntry`)

No destructive updates are performed on prior review records.

History endpoint (`GET /api/moderation/reviews/{id}/history`) returns `ReviewAuditRecord[]` derived from immutable history.

## API Surface

- `GET /api/moderation/queue`
- `GET /api/moderation/queue/{id}`
- `GET /api/moderation/queue/{id}/evidence`
- `POST /api/moderation/reviews/{id}/approve`
- `POST /api/moderation/reviews/{id}/reject`
- `POST /api/moderation/reviews/{id}/request-changes`
- `GET /api/moderation/reviews/{id}/history`
- `POST /api/moderation/events/{eventId}/rollback`

Backward-compatible aliases are retained for legacy `queue/{id}/{action}` routes.

## Authorization Seam

All moderation endpoints run through `ModeratorAuthorizationFilter`.

The current implementation uses `AllowAllModerationAuthorizationService` (Phase 0 seam), and can be replaced with role/policy-backed checks without changing endpoint signatures.
