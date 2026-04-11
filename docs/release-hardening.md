# WeUP Phase 0 Release Hardening

## Strategy

WeUP Phase 0 now uses one deterministic seed dataset, one backend reset mechanism, and one release gate stack.

- Seed data lives in `seed/phase0-dataset.json`.
- Local, test, and CI environments load that dataset through the backend `Phase0SeedService`.
- Browser E2E and backend smoke checks validate the same seeded identities, events, moderation items, and ingestion jobs.
- Release validation explicitly blocks on user-risk paths instead of synthetic coverage counts.

## Seed Architecture

The seed dataset covers:

- markets and city taxonomy
- venues
- events
- users
- saved signals
- moderation queue items
- ingestion jobs

Design rules enforced by the seed layer:

- no random demo data in release validation
- fixed timestamps for all seeded entities
- stable identifiers for seeded events, users, moderation items, and jobs
- deterministic reset via `POST /internal/seed/reset`

Backend seed flow:

1. `Phase0SeedLoader` resolves `seed/phase0-dataset.json`
2. `Phase0SeedService` resets all in-memory repositories from that file
3. Development, testing, E2E, and smoke environments enable seed-on-startup
4. Local reset scripts and Playwright call the reset endpoint before validation

## Test Architecture

### Backend tests

`dotnet test backend/WeUP.sln --configuration Release`

Backend release tests cover:

- health endpoint
- seeded map feed response
- login plus `/auth/me`
- save and saved-list flow
- moderation queue and approve action

### Frontend tests

`npm run test:frontend`

Frontend unit tests remain Jest-based for component and service seams.

### End-to-end tests

`npm run test:e2e`

Playwright critical-path coverage includes:

- seeded map feed load through the real backend
- event detail page render
- login/session hydration on the detail page
- save and unsave signal through backend API
- create submission draft
- submit for review
- moderation resolve action on a seeded queue item

### Smoke tests

`npm run test:smoke`

Smoke checks validate:

- `/health`
- `/api/events/map`
- `/auth/me`
- save endpoint
- moderation queue endpoint

For deployed environments:

`npm run test:smoke:deployed -- --baseUrl=https://your-backend.example.com`

## Release Gates

Primary release blockers:

- TypeScript typecheck fails
- ESLint fails
- backend tests fail
- frontend tests fail
- Playwright critical path fails
- smoke checks fail
- deterministic seed reset fails

Warnings:

- non-critical analytics or observability regressions that do not break launch flow
- documentation drift that does not invalidate runtime behavior
- secondary market taxonomy inconsistencies outside the active launch market

Rule of thumb:

- if a failure breaks discovery, identity, save state, submission, moderation, or deploy health, it is a blocker
- if a failure affects non-launch polish but not the launch path, it is a warning

## Developer Workflow

Fresh local bootstrap:

1. `npm ci`
2. `dotnet restore backend/WeUP.sln`
3. `npm run dev:backend`
4. `npm run dev:frontend`

Reset the deterministic seed:

1. `npm run seed-data`

Run validation locally:

1. `npm run typecheck`
2. `npm run lint`
3. `npm run test:frontend`
4. `npm run test:backend`
5. `npm run test:e2e`
6. `npm run test:smoke`

Run the full gate set:

1. `npm run release:check`

## Failure Interpretation

If seed reset fails:

- the environment is not trustworthy for release validation
- stop and fix the seed path, reset endpoint, or boot config first

If smoke fails but unit tests pass:

- the app likely builds but is not releasable
- treat this as deployment or integration breakage

If E2E fails but smoke passes:

- the backend may be healthy while the user path is broken
- treat this as a release blocker for Phase 0

## CI Surface

`.github/workflows/ci.yml` now runs:

- `npm ci`
- .NET 8 setup
- Playwright browser install
- `npm run release:check`

`.github/workflows/release-smoke.yml` provides a manual deployed-environment smoke gate.
