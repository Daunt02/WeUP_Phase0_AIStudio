# Canonical Time Window Semantics v1.0

**M8-P36 — Authoritative reference for all WeUP time-window concepts**

---

## Overview

WeUP uses a **backend-first** time-window model. The frontend never computes
window bounds independently; it sends a `(preset, timezone)` pair and receives
fully-resolved UTC bounds from the backend. This guarantees that both layers
always interpret discovery windows identically.

---

## Preset Definitions

All presets are resolved against an explicit **reference instant** (UTC) and an
explicit **market timezone** (IANA string). No implicit browser-local time or
server-clock assumptions are permitted in production code.

| Preset            | Wire Value    | C# Enum Value     | Duration        | Boundary type             |
| ----------------- | ------------- | ----------------- | --------------- | ------------------------- |
| Now               | `now`         | `Now = 0`         | 4 h             | UTC-relative rolling      |
| Tonight           | `tonight`     | `Tonight = 1`     | 9 h             | Market-local day boundary |
| Tomorrow          | `tomorrow`    | `Tomorrow = 2`    | 24 h            | Market-local day boundary |
| This Weekend      | `thisWeekend` | `ThisWeekend = 3` | 54 h            | Market-local day boundary |
| Next 24 Hours     | `next24Hours` | `Next24Hours = 5` | 24 h            | UTC-relative rolling      |
| Next 48 Hours     | `next48Hours` | `Next48Hours = 6` | 48 h            | UTC-relative rolling      |
| Custom Range      | `customRange` | `CustomRange = 7` | caller-supplied | Explicit UTC              |
| Custom _(legacy)_ | `custom`      | `Custom = 4`      | caller-supplied | Explicit UTC              |

> **Note:** `Custom` (= 4) is a legacy alias kept for backward compatibility.
> New code should use `CustomRange` (= 7). The backend treats them identically.

---

## Semantic Rules

### `Now`

```
StartUtc = referenceInstant
EndUtc   = referenceInstant + 4 h
```

- Purely UTC-relative; no local day boundary applied.
- Suitable for "happening right now" feed queries.

### `Tonight`

```
if localRef < 03:00 (local):
    StartUtc = prior day 18:00 (local) → UTC
    EndUtc   = today    03:00 (local) → UTC
else:
    StartUtc = today    18:00 (local) → UTC
    EndUtc   = tomorrow 03:00 (local) → UTC   (= StartUtc + 9 h)
```

- Cross-midnight nightlife window: 9 hours total.
- Before 03:00 local, the _prior_ evening's window is returned, not the upcoming
  one, to avoid a gap between midnight and morning.
- Local day boundaries are computed in the market timezone.

### `Tomorrow`

```
StartUtc = (localRef.Date + 1 day) at 00:00 local → UTC
EndUtc   = StartUtc + 24 h
```

- Always a full calendar day in the market timezone.
- Day boundary is determined by the market timezone, not UTC midnight.

### `ThisWeekend`

```
Anchor = most recent Friday 18:00 (local)
Window = [Anchor, Anchor + 54 h)
       = Friday 18:00 → Monday 00:00 (local)

if referenceInstant < Anchor:
    use current week's Anchor
elif referenceInstant < Anchor + 54 h:
    use current week's Anchor   (already inside the window)
else:
    use next week's Anchor
```

- 54 hours = Friday 18:00 → Monday 00:00 exactly.
- Does not overlap with `Tomorrow` on Sunday (Tomorrow would return Monday,
  ThisWeekend ends at Monday 00:00).

### `Next24Hours`

```
StartUtc = referenceInstant
EndUtc   = referenceInstant + 24 h
```

- Purely UTC-relative rolling window.
- Useful for "events starting in the next day" regardless of local day boundary.

### `Next48Hours`

```
StartUtc = referenceInstant
EndUtc   = referenceInstant + 48 h
```

- Purely UTC-relative rolling window.
- Useful for weekend planning or multi-day discovery.

### `CustomRange` / `Custom`

```
StartUtc = caller-supplied (must be explicit UTC)
EndUtc   = caller-supplied (must be explicit UTC)
```

Validation rules (enforced server-side):

- `customStartUtc` and `customEndUtc` are **required**.
- `customStartUtc` must be **strictly before** `customEndUtc`.
- Span must **not exceed 30 days**.
- Timezone is stored for display purposes but does not alter the UTC bounds.

---

## Resolution Against Timezones

- All **market-local day boundary** presets (Tonight, Tomorrow, ThisWeekend) are
  expanded in the market's local timezone.
- All **UTC-relative rolling** presets (Now, Next24Hours, Next48Hours) are
  computed directly from the reference instant and are unaffected by the market
  timezone (timezone is still recorded in the result for labelling purposes).
- The resolved UTC bounds are **always returned** in the response so the frontend
  never needs to perform timezone arithmetic.

### Timezone resolution order (C# backend)

1. Direct `TimeZoneInfo.FindSystemTimeZoneById` lookup (works on Linux/macOS with
   IANA ids).
2. Manual IANA → Windows id fallback map for Windows hosts (covers the Phase-0
   launch markets).

Supported explicit mappings:

| IANA                  | Windows                     |
| --------------------- | --------------------------- |
| `America/Chicago`     | `Central Standard Time`     |
| `America/New_York`    | `Eastern Standard Time`     |
| `America/Los_Angeles` | `Pacific Standard Time`     |
| `America/Denver`      | `Mountain Standard Time`    |
| `America/Phoenix`     | `US Mountain Standard Time` |
| `Europe/London`       | `GMT Standard Time`         |
| `Europe/Paris`        | `Romance Standard Time`     |
| `Asia/Tokyo`          | `Tokyo Standard Time`       |

Phase-0 default market timezone: **`America/Chicago`** (Houston, TX).

---

## Resolved Example Outputs

All examples use reference instant `2026-04-26T14:30:00Z` and timezone
`America/Chicago` (CDT = UTC-5 at that date). Local time of reference = 09:30.

### `Now`

| Field    | Value                  |
| -------- | ---------------------- |
| StartUtc | `2026-04-26T14:30:00Z` |
| EndUtc   | `2026-04-26T18:30:00Z` |
| Duration | 4 h                    |

### `Tonight`

Local 09:30 → after 03:00 → use tonight's window.

| Field    | Value                                |
| -------- | ------------------------------------ |
| StartUtc | `2026-04-26T23:00:00Z` (18:00 CDT)   |
| EndUtc   | `2026-04-27T08:00:00Z` (03:00 CDT+1) |
| Duration | 9 h                                  |

### `Tomorrow`

Local date is 2026-04-26. Tomorrow = 2026-04-27.

| Field    | Value                                     |
| -------- | ----------------------------------------- |
| StartUtc | `2026-04-27T05:00:00Z` (00:00 CDT Apr 27) |
| EndUtc   | `2026-04-28T05:00:00Z` (00:00 CDT Apr 28) |
| Duration | 24 h                                      |

### `ThisWeekend`

2026-04-26 is a Sunday, inside the current weekend window (Fri Apr 24 18:00 →
Mon Apr 27 00:00 CDT).

| Field    | Value                                  |
| -------- | -------------------------------------- |
| StartUtc | `2026-04-24T23:00:00Z` (Fri 18:00 CDT) |
| EndUtc   | `2026-04-27T05:00:00Z` (Mon 00:00 CDT) |
| Duration | 54 h                                   |

### `Next24Hours`

| Field    | Value                  |
| -------- | ---------------------- |
| StartUtc | `2026-04-26T14:30:00Z` |
| EndUtc   | `2026-04-27T14:30:00Z` |
| Duration | 24 h                   |

### `Next48Hours`

| Field    | Value                  |
| -------- | ---------------------- |
| StartUtc | `2026-04-26T14:30:00Z` |
| EndUtc   | `2026-04-28T14:30:00Z` |
| Duration | 48 h                   |

### `CustomRange`

Caller supplies bounds; backend validates and echoes them.

| Field      | Value           |
| ---------- | --------------- |
| StartUtc   | caller-supplied |
| EndUtc     | caller-supplied |
| Constraint | span ≤ 30 days  |

---

## Backend–Frontend Contract

### Request (frontend → backend)

```typescript
interface TimeWindowFilterDto {
  preset: TimeWindowPreset; // wire string, e.g. "next24Hours"
  timezone: string; // IANA, e.g. "America/Chicago"
  customStartUtc?: string; // ISO 8601 UTC; required for CustomRange
  customEndUtc?: string; // ISO 8601 UTC; required for CustomRange
}
```

### Response (backend → frontend)

```typescript
interface ResolvedTimeWindowDto {
  startUtc: string; // ISO 8601 UTC
  endUtc: string; // ISO 8601 UTC
  timezone: string; // IANA string used during resolution
  sourcePreset: TimeWindowPreset; // preset that produced these bounds
  presetLabel: string; // human-readable, e.g. "Next 24 Hours"
  resolutionInstantUtc: string; // "now" used; for tracing/audit
}
```

The frontend must use `startUtc`/`endUtc` from the response for all event
filtering and display. It must never re-derive bounds from the preset value.

---

## Validation Rules Summary

| Condition                     | Error                                                                                 |
| ----------------------------- | ------------------------------------------------------------------------------------- |
| `timezone` is blank           | `"timezone is required and must be an IANA or Windows timezone identifier."`          |
| Timezone not in supported set | `"Unsupported timezone '{tz}'. Use a supported IANA or Windows timezone identifier."` |
| CustomRange: missing bounds   | `"customStartUtc and customEndUtc are required when preset=CustomRange."`             |
| CustomRange: start ≥ end      | `"customStartUtc must be earlier than customEndUtc."`                                 |
| CustomRange: span > 30 days   | `"CustomRange span must not exceed 30 days."`                                         |
| Unknown preset value          | `"Unsupported preset '{preset}'."`                                                    |

---

## Key Invariants

1. `StartUtc < EndUtc` — always guaranteed; consumers may rely on this.
2. UTC bounds have `Kind == Utc` — no ambiguous local-time values on the wire.
3. `Timezone` is never empty in a resolved window.
4. `ResolutionInstantUtc` is always stored — enables deterministic test replay.
5. Frontend never expands presets — one source of truth, in `TimeWindowPresetMapper`.
6. No duplicated rule sets — `domains/query/contracts.ts` `resolveTemporalPreset`
   should delegate to the backend API rather than re-implementing semantics.

---

## Files

| File                                                                                                                                             | Purpose                                                         |
| ------------------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------- |
| [backend/WeUP.Domain/Temporal/TimeWindowPreset.cs](../WeUP_Phase0_AIStudio/backend/WeUP.Domain/Temporal/TimeWindowPreset.cs)                     | C# enum, `ResolvedTimeWindow` record, `TimeWindowPresetMapper`  |
| [frontend-vue/src/contracts/time-window.contracts.ts](../WeUP_Phase0_AIStudio/frontend-vue/src/contracts/time-window.contracts.ts)               | TypeScript enum, `TimeWindowFilterDto`, `ResolvedTimeWindowDto` |
| [backend/WeUP.Tests/Temporal/TimeWindowPresetMapperTests.cs](../WeUP_Phase0_AIStudio/backend/WeUP.Tests/Temporal/TimeWindowPresetMapperTests.cs) | Unit tests for all presets                                      |
| This file                                                                                                                                        | Authoritative semantic documentation                            |
