# Analytics & Telemetry — Phase 0

Overview

- Two separate concerns: product analytics (user behavior) and operational telemetry (errors, performance).
- Phase 0 aims for provider-agnostic seams so we can swap vendors later.

Event taxonomy (typed)

- `map_feed_viewed`
- `event_detail_opened`
- `signal_saved`
- `signal_unsaved`
- `calendar_opened`
- `temporal_preset_selected`
- `submission_draft_created`
- `submission_sent_for_review`
- `moderation_item_resolved`

Design rules

- Centralized typed API: use `lib/analytics.ts` and `hooks/useAnalytics.ts` in frontend.
- Minimize payloads: drop PII (email, phone, name, address, ssn, dob).
- Keep product analytics distinct from structured operational logs and errors.

Frontend flows

- UI code should call `useAnalytics().track(...)` with typed events.
- Events post to `/api/analytics`, which forwards to backend if `NEXT_PUBLIC_BACKEND_ANALYTICS_ENDPOINT` is configured.

Backend flows

- Backend has `IOperationalTelemetry` implemented in `Observability/TelemetryService.cs` to record events and exceptions via `ILogger` and OpenTelemetry Activities.
- Feature flags are read from config section `FeatureFlags` and exposed via `IFeatureFlagService`.

Privacy

- Only hashed/anonymized identifiers may be sent (`userIdHash`).
- No free-form user properties. Strip common PII keys in Phase 0.

Verification

- Use `components/VerificationExamples.tsx` for local checks: save tracking, frontend exception reporting, and flag snapshots.
