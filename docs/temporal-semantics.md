**Temporal Semantics (P21)**

- **Purpose:** canonicalize timeline and calendar UI labels into contract-driven temporal queries used by backend event feeds.

- **Core models:**
  - `TemporalQuery` (contract): supports `Preset`, `AbsoluteWindow`, `CalendarDate` modes.
  - `TimeWindow` (domain) / `TimeWindowDto` (contract): canonical UTC start/end with market timezone id.
  - `TemporalPreset`: NOW, 6PM, 9PM, MIDNIGHT, 3AM, FRI, SAT, SUN.

- **Preset mapping decisions:**
  - Presets are computed as market-local windows (not browser-local). This ensures consistency for market-facing products (nightlife, local events).
  - NOW: 1-hour window starting at reference time in market-local.
  - 6PM/9PM: the local day at 18:00–19:00 and 21:00–22:00 respectively.
  - MIDNIGHT: nightlife cross-midnight window 23:00–02:00 (3 hours) to capture late-night sets that span midnight.
  - 3AM: 03:00–04:00 local.
  - FRI/SAT/SUN: full local day from 00:00 to 24:00 on the next occurrence of that weekday.

- **Timezone handling:**
  - The backend resolves market timezone ids; it accepts common IANA ids and maps a small set to Windows ids for compatibility.
  - All canonical windows are returned as UTC start/end (and carry the market timezone id) so downstream services and frontend can operate deterministically.

- **Event visibility rules (high level):**
  - Upcoming: event.startUtc >= window.EndUtc -> treat as upcoming (not live in window)
  - Ongoing: event.startUtc < window.EndUtc && (event.endUtc == null || event.endUtc > window.StartUtc)
  - Cross-midnight: same rule applies; windows may span midnight and visibility respects UTC-converted boundaries.
  - Multi-day events: considered ongoing for any window that intersects their time range.
  - Archival: archival visibility is separate (not included in default feeds) and should be controlled by an `archival` flag on queries.

- **Frontend integration seam:**
  - New API endpoint: `POST /api/temporal/window` accepts `TemporalQuery` and returns `TimeWindowDto`.
  - UI components (TimelineControl, Calendar) should request canonical windows from this endpoint and then call the feed endpoints with the canonical UTC window instead of doing client-only filtering.
  - The frontend `eventService.fetchTemporalFeed()` provides a migration seam: it posts a `TemporalQuery`, obtains the canonical window, and forwards to the existing map/calendar feed logic.

- **Calendar + Timeline interaction:**
  - Combine semantics: calendar date narrow (explicit date selection) should be composable with timeline presets only when the UI indicates a combination mode. By default, a direct calendar pick overrides the preset. If both are supplied the server should follow explicit `TemporalQuery.Mode` semantics — prefer `AbsoluteWindow` or `CalendarDate` when provided.

- **Edge cases:**
  - Market timezone not recognized: fall back to UTC (explicitly returned in `TimeWindowDto` timezone field).
  - Daylight Saving transitions: windows are computed in market local with TimeZoneInfo offsets, so DST shifts are handled by OS timezone rules; tests should validate boundaries in DST transitions.
  - Empty-result windows: frontend should handle empty result sets gracefully and surface a date/time hint.

- **Next steps & acceptance tests:**
  - Unit tests added for midnight cross-boundary, NOW one-hour, and next-weekday full-day.
  - Integrate `ITemporalQueryService` implementation in application layer to enable DI-backed implementations and replace TemporalPresetMapper usage if needed.
