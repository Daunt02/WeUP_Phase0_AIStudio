# M8-P39 Temporal Contract Synchronization v1.0

## Canonical Contract

All temporal requests across map, calendar, and saved retrieval use the same shape:

- `preset`
- `fromUtc`
- `toUtc`
- `marketTimezone`
- `referenceInstantUtc`

`fromUtc` and `toUtc` are only required for custom-range flows (`customRange` or legacy `custom`).

## Ownership Boundaries

- Backend owns authoritative window resolution.
- Frontend sends intent only; it does not expand presets into UTC windows.
- Frontend validation only protects request integrity (missing fields, malformed datetimes, inverted ranges).
- Backend remains source of truth for final temporal semantics.

## Anti-Drift Rules

- Build temporal payloads from one composable: `useTemporalQueryState`.
- Convert local datetime inputs to UTC exactly once in that composable.
- Do not implement preset branching in UI components.
- Keep transport aliases (`timezone`, `customStartUtc`, `customEndUtc`) only for backward compatibility.
- Prefer canonical names (`marketTimezone`, `fromUtc`, `toUtc`) for all new code.

## Selection Reset Behavior

Selection reset is explicit and deterministic:

- Preserve selection when the selected event remains in the refreshed visible set.
- Clear selection when refreshed results no longer include the selected event id.
- This applies equally to map marker selection, calendar card selection, and saved-event detail focus.

## Example State Flow (Map + Calendar + Saved)

1. User picks `preset=tonight`, `marketTimezone=America/Chicago`.
2. `useTemporalQueryState` emits one canonical `TemporalQueryDto`.
3. Map feed adapter forwards canonical fields (plus compatibility aliases) to `/api/events/map-feed/v1`.
4. Calendar overlay query is projected from the same canonical temporal DTO, not recomputed.
5. Saved-event retrieval can attach the same temporal DTO as query params for consistent scope filtering.
6. After response reconciliation, selected event is retained only if still in visible ids; otherwise it is cleared.

## Contract Test Compatibility Guidance

- Keep enum wire values stable (`now`, `tonight`, `tomorrow`, `thisWeekend`, `next24Hours`, `next48Hours`, `customRange`, `custom`).
- When adding temporal fields, keep old aliases available until all clients migrate.
- Validate canonical fields in one place and reuse adapters to avoid divergent map/calendar/saved behavior.
