# WeUP Phase 0 — Issues & Mitigation Prompts

**Document Created:** 2026-04-03
**Status:** Catalog of blockers and work items for Phase 0 (P01-P24)
**Owner:** Development Team

---

## Overview

This document captures all known issues, gaps, and risks discovered during Phase 0 development with structured mitigation prompts. Each issue is categorized by severity and includes a specific prompt to guide future work.

**Issue Categories:**
- 🔴 **Critical** — Blocks deployment, breaks build, or corrupts data
- 🟠 **High** — Significant feature gap or architectural misalignment
- 🟡 **Medium** — Non-blocking issue; should be resolved before release
- 🔵 **Low** — Nice-to-have or cleanup; post-release acceptable

---

## 🔴 Critical Issues

### Issue #C1: TypeScript Compilation Error in Provenance

**Severity:** 🔴 Critical
**Location:** `domains/event/provenance.ts:186`
**Description:**
Type casting error prevents the build from completing. The `ConfidenceVector` type cannot be cast to `Record<string, number>` without an explicit `unknown` intermediate.

**Impact:**
- Build fails (`npm run build` exits with code 1)
- Cannot deploy frontend
- Blocks all work downstream

**Current Error:**
```
./domains/event/provenance.ts:186:16
Type error: Conversion of type 'ConfidenceVector' to type 'Record<string, number>'
may be a mistake because neither type sufficiently overlaps with the other.
If this was intentional, convert the expression to 'unknown' first.
Index signature for type 'string' is missing in type 'ConfidenceVector'.
```

**Mitigation Prompt:**
```
Fix the TypeScript type casting error in domains/event/provenance.ts:186.

Task:
1. Inspect the ConfidenceVector type definition in types/index.ts
2. Inspect the problematic code at line 186 of provenance.ts
3. Determine the correct type narrowing strategy (unknown cast, type guard, or refactoring)
4. Apply the fix without changing logic
5. Run npm run build to confirm compilation succeeds
6. Verify no runtime behavior changes

Success Criteria:
- Build completes without TypeScript errors
- No new errors introduced
- Existing tests (if any) still pass
```

---

### Issue #C2: No Test Infrastructure Exists

**Severity:** 🔴 Critical
**Impact:**
- 18 commits in, zero test coverage
- No regression detection
- Quality gates missing for Phase 0 release

**Current State:**
- No frontend tests (Jest not configured)
- No backend tests (xUnit not configured)
- No integration tests
- No E2E tests
- No smoke test suite

**Scope (P24):**
Implement full testing infrastructure including unit, integration, and E2E tests across both frontend and backend.

**Mitigation Prompt:**
```
Set up testing infrastructure foundation for the WeUP Phase 0 project.

Task:
1. Configure Jest for frontend with TypeScript support
2. Configure xUnit for backend (.NET)
3. Set up CI hooks (GitHub Actions) to run tests on push
4. Create initial test template files for both stacks
5. Add test script to package.json and .csproj
6. Document testing guidelines in contributing.md

Success Criteria:
- Jest is configured and can run (0 tests initially)
- xUnit is configured and can run (0 tests initially)
- CI pipeline triggers on commit
- Team can write first tests in next prompts
```

---

## 🟠 High Priority Issues

### Issue #H1: Spatial Queries Not Integrated into Frontend

**Severity:** 🟠 High
**Phase:** P19 (In Progress)
**Status:** Backend contracts exist; frontend integration incomplete

**Description:**
Backend spatial query contracts (BoundingBox, MapFeedRequest/Response) are defined but RadarMap component still uses loose client-side viewport logic. Map clustering is client-side only, not driven by backend queries.

**Current Implementation:**
- RadarMap uses `use-supercluster` on all loaded events (client clustering)
- Bounds tracked in local state, not tied to contracts
- No district/neighborhood model
- Map feed requests don't validate bounds

**Impact:**
- Frontend controls don't reflect real backend capabilities
- Can't enforce geographic boundaries
- Clustering inefficient at scale
- No audit trail of what queries drove what results

**Blockers:** None (can work in parallel)

**Mitigation Prompt:**
```
Integrate backend spatial query contracts into the RadarMap component.

Task:
1. Read backend/WeUP.Domain/Spatial/BoundingBox.cs to understand contract
2. Read services/eventService.ts to see current fetch implementation
3. Refactor RadarMap to construct MapFeedRequest with canonical bounds
4. Replace client-side clustering with backend result consumption
5. Add district filter UI seam (can be no-op for now)
6. Add tests verifying bounding-box validation
7. Test with various viewport sizes and zoom levels

Success Criteria:
- RadarMap sends MapFeedRequest to /api/events/map
- BoundingBox validation errors are caught and displayed
- Map doesn't break on empty results
- Clustering visually indistinguishable from before (but data real)
- District filter UI elements present (even if non-functional)
```

---

### Issue #H2: No Market/City Partitioning Model

**Severity:** 🟠 High
**Phase:** P20 (Not Started)
**Status:** Blocked by P19

**Description:**
There is no concept of markets, cities, or districts in the system. All events are treated as one undifferentiated pool. This blocks multi-city expansion and freeze enforcement.

**Current State:**
- No Market domain model
- No District or Neighborhood taxonomy
- No market assignment logic
- No market freeze rules
- Can't distinguish between cities

**Impact:**
- Cannot scale to multiple cities
- No way to enforce city-specific policies
- Events can't be validated against geographic boundaries
- Marketing/ops have no control over when markets launch

**Dependencies:** Requires P19 (spatial queries first)

**Mitigation Prompt:**
```
Design and implement the Market, District, and Neighborhood domain models.

Task:
1. Define Market entity with identity, status, boundary metadata
2. Define District entity with canonical names, aliases, parent relationships
3. Define Neighborhood entity (taxonomy layer under District)
4. Create IMarketAssignmentService to assign events to geography
5. Create IMarketPolicyService to enforce freeze rules
6. Create IMarketTaxonomyService to resolve aliases
7. Add Market persistence layer (EF entities + migrations)
8. Create GET /api/markets/active and GET /api/districts/taxonomy endpoints

Success Criteria:
- Market model can represent launch city/district structure
- Events can be assigned to markets by coordinate
- Freeze rules prevent submissions when market inactive
- Taxonomy endpoints return correct hierarchy
- Can query distinct market list
```

---

### Issue #H3: Timeline and Calendar UX Are Cosmetic, Not Contract-Driven

**Severity:** 🟠 High
**Phase:** P21 (Not Started)
**Status:** Blocked by P20

**Description:**
TimelineControl (NOW, 6PM, 9PM, MIDNIGHT, 3AM) and CulturalCalendar labels are UI-only. Clicking does not drive backend queries. No actual temporal windows, no timezone awareness, no cross-midnight event logic.

**Current Implementation:**
- TimelineControl drag → label state only
- Calendar click → visual selection only
- No TemporalPreset or TimeWindow contracts
- No temporal query service backend

**Impact:**
- Time-based filtering is fake
- Users see all events regardless of time selection
- Can't distinguish between nightlife, daytime, weekend events
- No timezone handling for multi-city expansion
- Cross-midnight events display unpredictably

**Dependencies:** Requires P20 (market config for timezone)

**Mitigation Prompt:**
```
Convert timeline and calendar UX into real temporal query semantics.

Task:
1. Define TimeWindow, DateWindow, TemporalPreset domain models
2. Create label → TimeWindow mapping (NOW, 6PM, 9PM, MIDNIGHT, 3AM)
3. Create ITemporalQueryService for time-window-based event queries
4. Add temporal validation rules (upcoming, ongoing, cross-midnight, multi-day)
5. Implement TemporalFeedRequest contract
6. Refactor TimelineControl to send real temporal queries
7. Refactor CulturalCalendar to send real date queries
8. Add tests for edge cases (midnight spanning, timezone boundaries, DST)

Success Criteria:
- Timeline labels map to deterministic time windows
- Calendar selection drives backend queries
- Ongoing events display correctly across midnight
- Timezone handling respects market-local time
- Empty time window doesn't break UX
```

---

## 🟡 Medium Priority Issues

### Issue #M1: No Observability, Logging, or Correlation IDs

**Severity:** 🟡 Medium
**Phase:** P22 (Not Started)
**Status:** Independent of other work

**Description:**
No OpenTelemetry instrumentation, structured logging, or request correlation. Makes debugging production issues impossible and prevents performance analysis.

**Current State:**
- No centralized logging
- No distributed tracing
- No correlation IDs for request tracking
- No error telemetry capture

**Impact:**
- Production issues undebuggable
- No visibility into request flow
- Performance problems invisible
- No audit trail for user actions

**Mitigation Prompt:**
```
Add OpenTelemetry, structured logging, and correlation IDs to the backend.

Task:
1. Wire OpenTelemetry SDK in Program.cs
2. Configure structured logging (Serilog or built-in ILogger)
3. Add middleware to generate and propagate correlation IDs
4. Instrument key services with tracing decorators
5. Add log context enrichment (userId, requestId, marketId)
6. Create observability guidelines doc

Success Criteria:
- All requests have correlation ID in logs
- Key services emit traces (EventService, SpatialService, etc.)
- Logs are structured JSON
- Documentation explains how to add logging to new services
```

---

### Issue #M2: No Feature Flags or Error Telemetry

**Severity:** 🟡 Medium
**Phase:** P23 (Not Started)
**Status:** Blocked by P22

**Description:**
No way to toggle features in production or track errors. Makes A/B testing, gradual rollouts, and error monitoring impossible.

**Current State:**
- All features hardcoded on
- Errors not captured
- No analytics infrastructure
- No guard rails on data collection

**Impact:**
- Can't safely deploy new features
- Can't detect errors in production
- Can't measure feature adoption
- PII data at risk (no guards)

**Mitigation Prompt:**
```
Add product analytics, error telemetry, and feature flag infrastructure.

Task:
1. Create feature flag service with in-memory provider
2. Define event types for key user actions (view map, save event, submit, etc.)
3. Implement error telemetry capture (frontend + backend)
4. Add PII guard rails (no email, user ID, location in events)
5. Create analytics sink configuration (local dev support)
6. Document how to define new events

Success Criteria:
- Features can be toggled via configuration
- Errors are logged to telemetry
- PII is not collected in analytics
- Local development works without external services
```

---

### Issue #M3: No Seed Data Generator or Smoke Tests

**Severity:** 🟡 Medium
**Phase:** P24 (Not Started)
**Status:** Blocked by P22-P23

**Description:**
No test data generator or smoke test suite. Makes QA, CI/CD, and release validation manual and fragile.

**Current State:**
- Only way to test is manual event creation
- No realistic dataset for performance testing
- No release checklist or gates
- No CI/CD integration

**Impact:**
- QA is slow and error-prone
- Can't validate releases automatically
- Performance issues invisible until production
- No staging environment simulation

**Mitigation Prompt:**
```
Create seed data generator and smoke test suite for Phase 0 release.

Task:
1. Build seed-data.ts script to generate realistic events, users, markets
2. Create smoke test suite (health checks, basic flows)
3. Add E2E test examples (discover → save → submit flow)
4. Create release checklist/gates
5. Set up Docker Compose for local testing
6. Wire tests into GitHub Actions CI

Success Criteria:
- Seed script generates 50-100 realistic events in <5 seconds
- Smoke tests validate all core endpoints work
- E2E tests run a complete user journey
- Release validation can be automated
```

---

## 🔵 Low Priority Issues

### Issue #L1: No Production Auth System

**Severity:** 🔵 Low
**Phase:** Post-P24
**Status:** Dev auth works locally

**Description:**
Auth system uses dev tokens stored in localStorage. Not secure for production. Needs real auth backbone (OAuth, JWT, or similar).

**Current Implementation:**
- `localStorage` stores `weup_dev_token`
- `services/auth.ts` manually resolves user
- No real session management

**Impact:**
- Cannot deploy to production
- No actual user authentication
- Security risk if exposed

**Mitigation Prompt:**
```
Design and implement production-grade authentication system.

Task:
1. Choose auth provider (OAuth 2.0, JWT, or identity provider)
2. Design session/token flow
3. Implement auth middleware
4. Migrate dev auth to production auth
5. Add secure cookie handling
6. Add CSRF protection

Success Criteria:
- Auth tokens are cryptographically signed
- Sessions have expiry
- User cannot forge identity
- Production deployment ready
```

---

### Issue #L2: Media Ingestion Not Yet Started

**Severity:** 🔵 Low
**Phase:** P25-P32 (Pack 2)
**Status:** Architecture defined, not implemented

**Description:**
Flyer upload, video processing, OCR confidence, media moderation, and venue asset library are planned but not built. Required for Phase 0.15.

**Current State:**
- Event links and venue page scraping work
- No media upload endpoint
- No OCR service integration
- No video processing pipeline

**Impact:**
- Can't ingest visual media (critical for nightlife discovery)
- Can't do OCR date/time extraction
- Venue photos not supported
- Limited to text-based ingestion

**Mitigation Prompt:**
```
Plan and implement media ingestion pipeline (flyers, videos, OCR).

Task:
1. Choose media storage (local file, S3, Azure Blob)
2. Choose OCR service (AWS Textract, Google Vision, local)
3. Choose video processing (FFmpeg, cloud API)
4. Design flyer upload endpoint
5. Implement OCR confidence extraction
6. Implement video frame extraction and poster generation
7. Add media moderation queue

Success Criteria:
- Flyer PDFs can be uploaded
- OCR extracts event dates with confidence
- Video poster generated from first frame
- Media moderation accessible to operators
```

---

### Issue #L3: Trust Graph System Not Started

**Severity:** 🔵 Low
**Phase:** P33-P40 (Pack 3)
**Status:** Architecture defined, not implemented

**Description:**
Trust identity graph, invite tier engine, and proximity rules are planned for Phase 0.20. Required for gated cultural access.

**Impact:**
- System remains fully public discovery
- No trust-based access tiers
- No invite mechanics
- No reputation signals

**Mitigation Prompt:**
```
Design and implement trust graph and invite tier system.

Task:
1. Model trust edges (follow, invite accept, proximity)
2. Design invite tier logic (public, adjacent, private)
3. Implement IInviteTierService
4. Create reputation aggregation service
5. Build invite issuance and acceptance workflows
6. Add anti-spam rules
7. Create tier unlock UI

Success Criteria:
- Trust edges are queryable
- Tier rules correctly filter content
- Invites can be issued and accepted
- Reputation signals affect tier eligibility
```

---

## 🔵 Low Priority Issues (Continued)

### Issue #L4: Operator Revenue Portal Not Started

**Severity:** 🔵 Low
**Phase:** P41-P48 (Pack 4)
**Status:** Architecture defined, not implemented

**Description:**
Venue portal, sponsor portal, campaign attribution, and billing system are planned for Phase 0.25. Required for operator monetization.

**Impact:**
- Venues can't manage their events
- No analytics for organizers
- No sponsored placement
- No revenue collection

**Mitigation Prompt:**
```
Design and implement venue/sponsor operator portals with billing.

Task:
1. Choose billing system integration (Stripe, PayPal, custom)
2. Design venue portal (edit events, view analytics, submissions)
3. Design sponsor portal (campaigns, geo-targeting, budget)
4. Implement campaign attribution (views, saves, attendance)
5. Build billing and invoicing
6. Create role-based access control
7. Build KPI dashboard (venue growth, sponsor yield)

Success Criteria:
- Venues can view their event analytics
- Sponsors can create campaigns with budget
- Billing integration working (test mode)
- Attribution data collected for all key actions
```

---

### Issue #L5: Intelligence and Ranking Layer Not Started

**Severity:** 🔵 Low
**Phase:** P49-P56 (Pack 5)
**Status:** Architecture defined, not implemented

**Description:**
Event ranking engine, personalized discovery, live signal fusion, and experimentation framework are planned for Phase 0.30+. Required for intelligent discovery.

**Impact:**
- No recommendations
- No personalized feed
- No ranking quality improvements
- No A/B testing capability

**Mitigation Prompt:**
```
Design and implement event ranking and recommendation system.

Task:
1. Define ranking signal weights (freshness, density, trust, intent)
2. Implement live signal fusion (saves, opens, shares, venue activity)
3. Build cold-start strategy (popular, venue-based, geographic)
4. Create personalization service
5. Build experimentation framework (feature flags, A/B bucketing)
6. Implement model versioning
7. Create ranking quality dashboards

Success Criteria:
- Events are ranked by signal weights
- Cold-start users get reasonable recommendations
- A/B tests can be run safely
- Personalization improves engagement
```

---

## Issue Summary Table

| ID | Issue | Severity | Phase | Status | Blocker |
|---|---|---|---|---|---|
| C1 | TypeScript compilation error | 🔴 Critical | Immediate | Blocker | None |
| C2 | No test infrastructure | 🔴 Critical | P24 | Blocker | P22-P23 |
| H1 | Spatial queries not integrated | 🟠 High | P19 | In Progress | None |
| H2 | No market/city model | 🟠 High | P20 | Blocked | P19 |
| H3 | Timeline/calendar cosmetic | 🟠 High | P21 | Blocked | P20 |
| M1 | No observability/logging | 🟡 Medium | P22 | Blocked | None |
| M2 | No feature flags/telemetry | 🟡 Medium | P23 | Blocked | P22 |
| M3 | No seed data/smoke tests | 🟡 Medium | P24 | Blocked | P22-P23 |
| L1 | Dev-only auth system | 🔵 Low | Post-P24 | Deferred | None |
| L2 | Media ingestion not started | 🔵 Low | P25-P32 | Deferred | None |
| L3 | Trust graph not started | 🔵 Low | P33-P40 | Deferred | None |
| L4 | Revenue portal not started | 🔵 Low | P41-P48 | Deferred | None |
| L5 | Intelligence layer not started | 🔵 Low | P49-P56 | Deferred | None |

---

## Recommended Resolution Order

### Immediate (Before Next Session)
1. **C1** — Fix TypeScript error (30 min)
2. Verify build passes

### Bundle 7 (P19-P21, Weeks 1-3)
3. **H1** — Integrate spatial queries into RadarMap (P19)
4. **H2** — Implement market/city model (P20, depends on H1)
5. **H3** — Convert timeline/calendar to temporal queries (P21, depends on H2)

### Bundle 8 (P22-P24, Weeks 4-6)
6. **M1** — Add observability and structured logging (P22)
7. **M2** — Add feature flags and error telemetry (P23, depends on M1)
8. **C2** — Implement test infrastructure (P24, depends on M1-M2)
9. **M3** — Add seed data and smoke tests (P24, depends on C2)

### Post-Phase 0
- L-series issues deferred to Packs 2-5

---

## Notes

- This document is the source of truth for blockers and known issues
- Each mitigation prompt is ready to be copied into a new session as a task
- Prompts are intentionally specific to enable rapid execution
- Do not execute prompts until explicitly requested
- Update this document after each bundle completion

