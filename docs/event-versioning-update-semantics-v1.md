# M4-P18 Event Versioning and Update Semantics v1.0

## Goal

Canonical events must evolve through explicit, auditable state transitions.
No material change should silently overwrite previously accepted canonical state.

## Canonical Version Model

Each `EventAggregate` stores:

- `Version` (monotonic integer, starts at 1)
- `ConcurrencyToken` (opaque token regenerated on each accepted update)
- `ChangeHistory` (bounded list of structured entries; newest retained up to 100 entries)

Each `EventStateChangeEntry` includes:

- from/to version
- actor and UTC timestamp
- reason (`EventVersionReason`)
- classified change type (`EventVersionChangeType`)
- changed fields
- moderation-review requirement flag
- before/after structured snapshots for rollback reasoning

## Version Increment Rules

Version increments by exactly +1 only when the resulting state changes.
No-op updates do not increment version.

Incrementing reasons:

- `MinorMetadataUpdate`: non-material metadata edits (for example tags/confidence updates)
- `ContentEdit`: title/description/category changes
- `TimeEdit`: start/end/timezone edits
- `VenueLocationEdit`: venue/address/coordinates edits
- `ModerationDecision`: lifecycle moderation transitions
- `PublishStatusChange`: publish gate transition changes
- `MergeApplied`: dedupe merge lineage and provenance update
- `Cancelled`: cancellation transition
- `Rescheduled`: schedule move preserving prior schedule in history

Change classification exposed to clients:

- `MinorMetadataUpdate`
- `MaterialEventChange`
- `StatusTransition`
- `MergeLineageUpdate`

## Update Semantics

### Mutable fields

Allowed via audited `ApplyUpdate` requests:

- title, description, tags, category
- venue/address/location coordinates
- timezone/start/end/local display values
- lifecycle/publish/moderation status
- risk/confidence
- provenance and merge lineage

### Immutable fields

Hard-guarded; update rejects if changed:

- canonical identity (`CanonicalEventId`)
- creation timestamp (`CreatedAtUtc`)

### Moderation-required fields

When an update sets `RequiresModerationReview = true`:

- moderation moves to `InReview`
- if event was published, lifecycle is stepped back to reviewed and publish status is unpublished
- change entry marks `RequiresModerationReview = true`

This is used for material edits and merge commits.

## Optimistic Concurrency

All audited updates require `ExpectedVersion`.
If `ExpectedVersion != current Version`, update throws a deterministic version mismatch error.

Persistence fields:

- `events.aggregate_version` (int)
- `events.concurrency_token` (concurrency token, EF concurrency column)
- `events.change_history_json` (jsonb)

## Merge and Dedupe Semantics

Merge application no longer uses raw record mutation.
Instead merge execution applies a single audited `MergeApplied` update that:

- updates canonical fields selected by the merge plan
- merges source/evidence provenance
- appends merge lineage (`MergedCanonicalEventIds`, `AppliedMergePlanIds`, merge actor/time)
- increments canonical version once
- flags moderation review requirement for post-merge verification

## Cancellation and Reschedule Lineage

Cancellation and reschedule are explicit reasons (`Cancelled`, `Rescheduled`).
Historical timeline is preserved in `ChangeHistory` snapshots:

- reschedule entry captures previous and new schedule
- cancellation entry captures status transition to cancelled/unpublished

This supports review and rollback reasoning without introducing full event-sourcing infrastructure.

## Frontend Interpretation

`EventDetailDto` and `EventModerationDto` expose:

- `Version`
- `ConcurrencyToken`
- `LastChangeType`
- moderation view also exposes `HasPendingReview`

This allows UI surfaces to clearly distinguish:

- minor metadata update
- material change
- status transition
- merge lineage update

## Storage and Migration Implications

Schema additions for `events`:

- `aggregate_version int not null default 1`
- `concurrency_token varchar(128) not null`
- `change_history_json jsonb null`

Operational notes:

- existing rows should be backfilled with `aggregate_version = 1`
- missing concurrency tokens should be generated as deterministic fallback (`{publicId}:v{version}`)
- history starts empty for existing rows and accumulates on first audited update
