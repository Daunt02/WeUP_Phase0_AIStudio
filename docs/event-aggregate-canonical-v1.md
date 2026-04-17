# EventAggregate Canonical Contract v1.0

## Purpose

`EventAggregate` is the canonical backend source of truth for event identity and state in Phase 0.
All ingestion, dedupe/merge, moderation, map feed, calendar feed, and event detail flows must resolve through this model.

## Scope

This contract is MVP-safe and Phase 0 centered.
It intentionally excludes non-MVP dimensions (sponsor graph, trust graph, blockchain, OCR-vendor-specific fields) unless represented as optional provenance or external references.

## Authoritative Fields

The canonical aggregate includes the following authoritative fields:

- `CanonicalEventId`: Stable identity for the event aggregate.
- `SourceEventIds`: Source IDs merged into this canonical event.
- `ExternalReferences`: Source-specific references and optional metadata.
- `Title`: Canonical title.
- `Description`: Canonical long description.
- `Tags`: Canonical tags.
- `Category`: Canonical category.
- `VenueName`: Canonical venue label.
- `Address`: Normalized address object (`AddressLine1`, `City`, `State`, `PostalCode`, `Country`, `RawAddress`, market/district/neighborhood codes).
- `Latitude`, `Longitude`: Canonical coordinates.
- `TimeZone`: Canonical timezone identifier.
- `StartUtc`, `EndUtc`: Canonical UTC temporal bounds.
- `LocalStartDisplay`, `LocalEndDisplay`: UI convenience fields only.
- `EventStatus`: Lifecycle state machine status.
- `PublishStatus`: Publication eligibility/publication state.
- `ModerationStatus`: Moderation review status.
- `RiskLevel`: Canonical risk tier (`EventRiskLevel`).
- `ConfidenceScore`: Canonical confidence in [0,1].
- `Provenance`: Primary source, evidence refs, source refs, first/last observed timestamps.
- `CreatedAtUtc`, `UpdatedAtUtc`: Audit timestamps.
- `Version`: Aggregate version (monotonic, starts at 1).
- `MergeLineage`: Parent/merged event refs and merge lineage metadata.

## Lifecycle Semantics

Canonical lifecycle status enum:

- `Draft`
- `Candidate`
- `Reviewed`
- `Approved`
- `Rejected`
- `Published`
- `Cancelled`
- `Archived`

Allowed transitions:

- `Draft -> Candidate, Archived`
- `Candidate -> Reviewed, Rejected, Cancelled`
- `Reviewed -> Approved, Rejected, Candidate`
- `Approved -> Published, Cancelled, Archived`
- `Rejected -> Candidate, Archived`
- `Published -> Cancelled, Archived`
- `Cancelled -> Published, Archived`
- `Archived -> (none)`

Transition validation is enforced by `EventAggregate.EnsureCanTransitionTo`.

## Invariant Rules

`EventAggregate.Validate()` enforces:

- Required identity and content fields are present.
- Coordinates are valid ranges.
- `EndUtc >= StartUtc` when end is present.
- `ConfidenceScore` is in [0,1].
- `Version >= 1`.
- Status consistency constraints:
  - `EventStatus=Published` requires `PublishStatus=Published`.
  - `PublishStatus=Published` requires `EventStatus=Published`.
  - `EventStatus=Rejected` requires `ModerationStatus=Rejected`.
  - Rejected moderation state cannot be published.

## Domain Truth vs UI Convenience

`StartUtc`, `EndUtc`, `TimeZone` are canonical temporal truth.
`LocalStartDisplay` and `LocalEndDisplay` are non-authoritative presentation fields.

## Phase 0 Refactor Notes

The following backend paths now project from canonical aggregate state:

- EF event repository feed/detail projections.
- Event lifecycle transition checks in EF/stub repositories.
- Entity resolution comparison/snapshot paths.

This reduces competing truth sources where equivalent fields were recomputed independently.

## Legacy Status Mapping

`EventLifecycleStatusMapper` provides compatibility mapping between legacy storage strings and canonical lifecycle enum values.

- Example: `NEEDS_REVIEW -> Candidate`
- Example: `Approved -> APPROVED` (storage)

## Migration Guidance

1. New persistence columns are not required for this phase.
2. Existing fields map into the canonical aggregate at repository boundaries.
3. Future migrations should persist explicit publish/moderation/risk/version/merge-lineage fields directly.
4. Downstream contracts should continue consuming DTOs, but DTO projection must start from `EventAggregate`.
