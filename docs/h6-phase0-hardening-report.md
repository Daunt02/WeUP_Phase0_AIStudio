# WeUP Phase 0 Hardening Report (H6-P30)

Date: 2026-04-15
Scope: H6-P26 through H6-P30

## Summary

This hardening pass converted observability seams into active telemetry, established release-mode fail-fast validation, and expanded end-to-end harness coverage across frontend and backend critical paths.

## What Was Hardened

### H6-P26: Real OpenTelemetry, Correlation IDs, Structured Logging

- Enabled active OpenTelemetry traces and metrics in API startup.
- Added JSON structured logging with scopes and UTC timestamps.
- Standardized correlation id propagation via `X-Correlation-ID` through:
  - inbound middleware,
  - outbound HTTP delegating handler,
  - exception telemetry paths,
  - Next.js analytics proxy route.
- Added runtime telemetry instrumentation for:
  - ingestion acceptance and job lookup,
  - moderation queue reads and decisions,
  - submission create/submit paths,
  - unhandled server exceptions.

### H6-P27: Product Analytics and Error Telemetry Boundaries

- Added client telemetry bootstrap layer so product instrumentation is centralized.
- Added automatic global capture of:
  - browser uncaught errors,
  - unhandled promise rejections,
  - network/fetch failures,
  - HTTP 5xx responses.
- Preserved component purity by keeping instrumentation in shared telemetry boundary files.

### H6-P28: Real End-to-End Harness Across FE + BE

- Expanded Playwright critical-path harness with explicit world-surface assertion.
- Added anonymous-vs-authenticated event detail behavior checks.
- Existing harness continues to validate map feed, event detail, saves, submission lifecycle, and moderation approval paths against running frontend + backend stack.

### H6-P29: Release Mode Gatekeeping and Fail-Fast Validation

- Added backend `ReleaseStartupValidator` with release-mode checks for:
  - database runtime enforcement,
  - auth service presence,
  - telemetry endpoint requirement when enabled,
  - contract manifest drift detection at startup.
- Added repo release environment gate script (`scripts/release-validate.mjs`).
- Integrated release validation into `release:check` pipeline before typecheck/test stages.

## What Was Removed or Replaced

- Replaced inline ad-hoc correlation middleware seam with concrete `CorrelationIdMiddleware` registration.
- Replaced basic request logging seam with structured exception telemetry pipeline.
- Replaced per-component/manual instrumentation pressure with root-level telemetry bootstrap.

## Remaining Gaps and Blockers to True Operational Phase 0

| Area                | Current State                           | Gap                                                                                    | Blocker Severity |
| ------------------- | --------------------------------------- | -------------------------------------------------------------------------------------- | ---------------- |
| Auth hardening      | Token service required in release mode  | Full JWT bearer middleware still not enabled in HTTP pipeline                          | High             |
| Telemetry backend   | OTel active and export-capable          | No enforced production backend/export destination by default                           | Medium           |
| Alerting/SLOs       | Trace+log generation active             | No alert rules, dashboards, or SLO burn alerts wired in repo                           | Medium           |
| Data durability     | Postgres mode gateable                  | Release workflow does not run migration smoke against a real target DB in CI           | Medium           |
| Contract governance | Startup and tests validate manifest     | No auto-generation of manifest from contracts (manual manifest updates still required) | Low              |
| Security posture    | Release checks block missing essentials | Secret scanning / policy-as-code gates not yet enforced in CI                          | Medium           |

## Explicit Remaining Blockers Before Release

1. Enable and validate real bearer auth middleware flow for protected endpoints in production runtime.
2. Require non-local telemetry sink and validate exporter reachability in deployment pipeline.
3. Add production deployment preflight that runs migrations and rollback verification against target environment.
4. Add CI policy gates for secret leakage, dependency vulnerabilities, and branch protection requirements.

## Verification Targets

- `npm run release:validate-env`
- `npm run test:e2e -- e2e/critical-path.spec.ts`
- `npm run test:backend`
