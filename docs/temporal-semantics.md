**Temporal Semantics (P22)**

- Purpose: ensure frontend and backend resolve identical temporal windows for discovery.

- Canonical presets:
  - `Today`: local market 00:00 -> 00:00 next day
  - `Tonight`: local market 18:00 -> 03:00 next day
  - `Weekend`: Friday 18:00 -> Monday 00:00 local market
  - `Next7Days`: rolling now -> now + 7 days

- Canonical models:
  - Frontend: `TemporalPreset` and `TimeWindow` in `domains/query/contracts.ts`
  - Backend: `TemporalPreset` and `TimeWindow` in `WeUP.Domain.Temporal`
  - Transport: `TemporalQuery` / `TimeWindowDto` in `WeUP.Contracts.Temporal`

- Timezone rules:
  - Houston Phase 0 default timezone: `America/Chicago`
  - Preset windows are computed in market-local time, then emitted as UTC boundaries
  - Unknown timezone ids fall back to UTC and return `UTC` as timezone value

- Validation and determinism:
  - `startUtc` is inclusive, `endUtc` is exclusive
  - `startUtc` must be strictly before `endUtc`
  - Custom windows must pass normalization before query execution

- Drift detection:
  - TS unit tests validate preset duration/shape
  - C# unit tests validate preset duration/shape and timezone fallback
  - Contract manifest tests catch DTO shape drift

- Authoritative reference:
  - See `docs/city-semantics-houston-phase0.md` for full cross-layer semantics.
