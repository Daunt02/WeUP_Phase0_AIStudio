# Feature Flags & Operational Kill Switches — Phase 0

Phase 0 model

- Static config-driven flags with environment-variable overrides.
- Typed accessors in frontend (`lib/featureFlags.ts`) and backend (`FeatureFlagsOptions` + `FeatureFlagService`).

Flags included

- `flyerOcr` — flyer OCR ingestion
- `linkIngestion` — link/flyer link ingestion
- `venuePageIngestion` — ingestion of venue pages
- `moderationDashboard` — moderation UI
- `profilePreferences` — profile/preferences surfaces
- `socialTrustFeatures` — future social/trust unlock features

Operational kill switches

- `ingestionDisabled` — disable ingestion pipeline end-to-end
- `submissionDisabled` — prevent new submissions
- `moderationFallbackDisabled` — disable moderation fallback behavior

How to configure

- Backend: add a `FeatureFlags` section to `appsettings.{Environment}.json` or set env vars with `FeatureFlags__FlyerOcr=true` style.
- Frontend: set `NEXT_PUBLIC_FEATURE_<name>` env vars (e.g. `NEXT_PUBLIC_FEATURE_ingestionDisabled=1`).

Usage rules

- Use typed `IFeatureFlagService` for server decisions.
- Use `isFeatureEnabled('...')` or `featureFlags` in UI; avoid scattering raw string checks.
- Critical kill switches must be evaluated server-side before performing high-risk actions.

Future extension

- Add remote flag provider (LaunchDarkly, Split, Azure App Configuration) by implementing `IFeatureFlagService` or swapping frontend env read to a remote fetch with caching.
