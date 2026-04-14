# Publish Eligibility Gate (P14)

## Purpose

Phase 0 now enforces a deterministic publish gate in backend moderation logic. Confidence and provenance signals are no longer passive metadata. Every approval attempt is checked against a single `IPublishEligibilityService` decision path.

Outcomes are explicit:

- `AutoPublishable`
- `ManualReviewRequired`
- `Blocked`

## Contracts

Primary publish gate contracts are in:

- `backend/WeUP.Domain/Moderation/IPublishEligibility.cs`

Key contracts:

- `PublishEligibilityResult`
- `PublishBlocker`
- `ConfidenceGateDecision`
- `FieldCompletenessResult`
- `AutoPublishPolicy`
- `PublishEligibilityContext`

`PublishEligibilityResult` always includes:

- eligibility (`Eligible`)
- blockers (`PublishBlocker[]`)
- recommended action (`PublishRecommendedAction`)
- confidence summary (`ConfidenceGateDecision`)
- field completeness summary (`FieldCompletenessResult`)
- active policy snapshot (`AutoPublishPolicy`)

## Centralized Thresholds

Policy is centralized in:

- `backend/WeUP.Application/Moderation/PublishEligibilityService.cs`
- `PublishEligibilityPolicies.Phase0.Policy`

Phase 0 thresholds:

- Auto-publish threshold: `0.85`
- Manual-review threshold: `0.55`
- Hard block threshold: `0.40`
- Minimum dimension threshold: `0.50`

Dimension minimums:

- extraction: `0.50`
- geocode: `0.50`
- temporal: `0.50`
- venueMatch: `0.45`
- dupeRisk: `0.45`
- sourceTrust: `0.50`

No threshold values are duplicated across endpoints.

## Blocker Taxonomy

Machine-readable blocker codes (`PublishBlockerCode`) include:

- missing required fields: title, venue, address, geo, startTime, timezone, provenance, confidence
- confidence blockers: low extraction, temporal, geocode, venue-match, aggregate confidence
- integrity blockers: unresolved venue, geo not validated, source integrity failed, incomplete evidence chain
- moderation blockers: manual review pending, rejected review state
- dedupe blocker: unresolved duplicate conflict
- lifecycle blockers: rejected lifecycle, archived lifecycle

Human-readable messages are emitted for each blocker and returned to moderation workflows.

## Required-Field Policy

Publication requires:

- title
- venue
- address
- geo validation
- start time
- timezone
- provenance integrity
- confidence signals

Missing/invalid required fields are hard blockers.

## Confidence Gate Policy

Confidence decision is deterministic and explainable:

- `AutoPublishable`: aggregate >= auto threshold and no hard blockers
- `ManualReviewRequired`: aggregate between manual and auto thresholds, or pending review blockers
- `Blocked`: hard blockers present or aggregate below block threshold

Confidence summary always includes per-dimension scores and reasoning.

## Moderation Integration

Approval flow enforcement is in:

- `backend/WeUP.Application/Moderation/ModerationReviewServices.cs`

Before any `Approve` action is applied:

1. Build `PublishEligibilityContext` from moderation item, provenance, confidence, dedupe, lifecycle.
2. Evaluate via `IPublishEligibilityService`.
3. If not eligible, return `422`-style failure through review response and do not mutate lifecycle.

This prevents review actions from silently forcing invalid publication.

## Manual Review Policy

Manual review is required when:

- confidence lands in review band
- moderation state is still pending
- non-hard review blockers exist

Manual approval can proceed only when hard blockers are cleared.

## Relation To Review Decisions

`ReviewDecisionKind.Approve` is now constrained by the publish gate.

- If gate passes: approve proceeds and lifecycle transition applies.
- If gate fails: decision is rejected with explicit blocker reasons.

`Reject` and `RequestChanges` remain valid moderation actions and are not publish actions.
