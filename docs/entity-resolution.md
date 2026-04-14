# P12 Entity Resolution and Merge Rules

## Purpose

This module provides deterministic, explainable duplicate detection and merge planning for event ingestion candidates. It supports:

- candidate-to-existing event matching
- candidate-to-candidate matching
- merge preview plans
- safety-gated merge commit
- manual-review routing for risky conflicts

No merge path removes provenance, evidence references, or auditability.

## Scoring Model

Composite duplicate score is a weighted sum of deterministic signals:

- title similarity: 0.30
- venue similarity: 0.20
- time overlap: 0.20
- address similarity: 0.10
- geo proximity: 0.10
- source reference duplication: 0.10

Implementation details:

- text similarity uses normalized Levenshtein + token Jaccard
- time overlap is bucketed by start-time delta
- geo proximity uses haversine distance buckets
- source duplication checks exact sourceRef / externalSourceId and evidence-ref intersection

## Thresholds

Thresholds are enforced in code (DeterministicEventDuplicateDetector + EntityResolutionService):

- likely duplicate: score >= 0.82
- possible duplicate requiring review: score >= 0.62 and < 0.82
- no match: score < 0.62

Auto-merge is allowed only when:

- score >= 0.82
- target is an existing canonical event (not candidate-to-candidate)
- no material conflict blockers are present

## Manual-Review Blockers

Any of the following blocks auto-merge:

- materially different venue (venue similarity < 0.35)
- materially different date/time (start-time delta > 240 minutes)
- incompatible address (address similarity < 0.30)
- low-confidence source mismatch (incoming extraction confidence < 0.45 and weak source linkage)
- best match is candidate-to-candidate without a canonical event target

Blocked merges are recorded as `MANUAL_REVIEW_REQUIRED`.

## Merge Precedence Rules

Merge plan generation is deterministic and explainable:

- canonical title/venue/address/time/category/description prefer existing canonical event values when present
- missing canonical fields can be filled from incoming candidate
- source references are unioned (never dropped)
- evidence references are unioned (never dropped)
- existing review history references are preserved in the merge plan
- merged confidence uses a deterministic policy from incoming confidence and duplicate score
- field conflicts are recorded explicitly as `FieldConflict` records

No silent overwrite is allowed for materially conflicting data. Conflicts are preserved in the merge plan and audit trail.

## Endpoint Seam

The backend exposes:

- `POST /api/resolution/evaluate`
  - inputs: `EvaluateResolutionRequest`
  - output: `EntityResolutionResult`
  - stores explainable score signals, decision, conflicts, and merge preview

- `POST /api/resolution/merge`
  - inputs: `MergeResolutionRequest`
  - output: `MergeResolutionResponse`
  - commits merge only when safe or explicitly forced by reviewer policy

- `GET /api/resolution/{id}`
  - returns saved `EntityResolutionResult`

## Audit and Provenance Guarantees

For every successful merge commit:

- source refs from both records are preserved/unioned
- evidence refs from both records are preserved/unioned
- merge rationale and conflict details are recorded
- ingestion audit row (`RESOLUTION_MERGE`) captures merge context

For blocked merges:

- resolution status is persisted as manual review required
- blockers are included in decision reasons and conflict records

## Safe vs Unsafe Examples

Safe auto-merge example:

- score: 0.91
- title/venue/time strongly aligned
- no material conflicts
- candidate merged into existing canonical event

Unsafe merge example:

- score: 0.76
- venue similarity 0.29 and time delta 6h+
- blocked for manual review with explicit conflict records
