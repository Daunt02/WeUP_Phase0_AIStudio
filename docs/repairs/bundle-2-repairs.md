# Bundle 2 — End-of-Bundle Repair Prompts

## Issues found after review of P04–P06

### R2-01: Duplicate SourceKind export
**Problem:** `SourceKind` is declared and exported in both `domains/event/types.ts` and `domains/event/provenance.ts`. This creates a risk of drift between the two definitions.
**Fix:** Remove `SourceKind` from `provenance.ts` and re-import it from `types.ts`.

### R2-02: ReviewMetadata / ReviewMetadataFull inconsistency
**Problem:** `EventAggregate.review` in `types.ts` uses a slim `ReviewMetadata` interface, while `provenance.ts` defines the richer `ReviewMetadataFull`. The aggregate should carry the full review model.
**Fix:** Remove the slim `ReviewMetadata` from `types.ts` and update `EventAggregate.review` to use `ReviewMetadataFull` from `provenance.ts` (or a common shared import).

### R2-03: Unsafe SourceKind cast in eventService.ts adapter
**Problem:** `nightlifeItemToAggregate()` casts `item.source` (`'manual' | 'scraped' | 'api'`) to `SourceKind` (`'manual_submission' | 'flyer_upload' | ...`). The value sets don't overlap, making the cast a lie that will produce runtime mismatches.
**Fix:** Add a proper `legacySourceToKind()` mapper function that converts old values to the canonical `SourceKind`.

### R2-04: GhostDraft index signature may cause strict TS issues
**Problem:** `GhostDraft` in `types/ui.ts` has `[k: string]: any` mixed with typed fields. Under strict TypeScript this may produce "index signature parameter type 'string' must be a supertype of all property values" errors.
**Fix:** Remove the index signature and add explicit optional fields, or use `Record<string, unknown>` for extension data as a separate field.
