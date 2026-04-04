# Bundle 8 — P22-P25 Repair & Integration Prompts

**Document Created:** 2026-04-04
**Status:** Repair prompts for Phase 0 Post-Implementation
**Owner:** Development Team
**Sequence:** Execute after P25 endpoints are wired and both builds pass

---

## Overview

This bundle contains structured repair prompts targeting critical issues identified in `phase0-issues-and-mitigations.md` that emerged during rapid P19-P25 scaffolding. Focus is on:

1. **Integration Testing** — Verify contracts work end-to-end
2. **Missing Service Wiring** — Complete Program.cs registrations
3. **Frontend Service Stubs** — Create TypeScript service clients
4. **Test Framework Setup** — Establish P24 testing infrastructure
5. **Smoke Tests** — Validate all core flows

---

## Prompt 1: Complete P22 Observability Integration

**Target Issue:** M1 (No observability/logging)
**Depends On:** P22 scaffolds already committed
**Effort:** ~20 minutes
**Success Criteria:** Correlation IDs flow through requests, logs are structured

### Task

```
Integrate P22 observability infrastructure end-to-end.

1. Verify ObservabilitySetup is wired in Program.cs:
   - Confirm AddWeUPObservability() is called during service registration
   - Confirm UseWeUPObservability() is called in middleware pipeline
   - Check that CorrelationIdProvider is registered as Scoped

2. Test correlation ID propagation:
   - Make a POST request to /api/analytics/events
   - Check response headers for X-Correlation-ID
   - Verify correlation ID appears in console logs
   - Test that correlation ID persists across multiple requests in same scope

3. Wire structured logging to a key service:
   - Inject ILogger into ViewportQueryService
   - Add log statements at entry/exit points (GetEventsInViewportAsync)
   - Log includes: correlationId, bounds, district, event count
   - Verify logs appear in console output

4. Create observability guidelines document:
   - Explain how to inject ILogger<T>
   - Show example of structured logging with context
   - List key services that should emit logs
   - Document how to use correlation IDs in logs

Success Criteria:
- Each API request has unique X-Correlation-ID in response headers
- Logs include structured fields (timestamp, level, scope, correlation ID)
- SpatialEndpoints logs show viewport queries with bounds
- Documentation is clear enough for team to add logging to new services
```

---

## Prompt 2: Create Frontend Service Stubs for P21-P25

**Target Issue:** H3 (Timeline/calendar cosmetic) + media intake integration
**Depends On:** Endpoints committed in P21-P25
**Effort:** ~25 minutes
**Success Criteria:** Frontend can call all new backend services

### Task

```
Create TypeScript service clients for temporal, analytics, and media services.

1. Create services/temporalService.ts:
   - Interface: TemporalPresetRequest { preset: string; marketTimezone?: string }
   - Interface: TemporalQueryResponse { preset, presetLabel, timeWindowStart, timeWindowEnd, timezone, events, count }
   - Function: getEventsAtTime(request): Promise<TemporalQueryResponse>
   - Function: getTemporalPresets(): Promise<{ presets: Array<{ value, name, label }> }>
   - Add getAuthHeader() for auth support (reuse pattern from eventService.ts)

2. Create services/analyticsService.ts:
   - Interface: RecordAnalyticsRequest { eventType: string; userId?: string; properties?: Record<string, any> }
   - Interface: EventTypeDto { value: number; name: string }
   - Function: recordEvent(request): Promise<void>
   - Function: getEventTypes(): Promise<{ eventTypes: EventTypeDto[] }>

3. Create services/mediaService.ts:
   - Interface: FlyerAssetDto { assetId, originalFilename, fileSizeBytes, contentType, submitterId, uploadedAt, s3Url, localPath }
   - Function: uploadFlyer(file: File, submitterId: string): Promise<{ assetId: string }>
   - Function: getFlyerMetadata(assetId: string): Promise<FlyerAssetDto>
   - Function: listUserFlyers(submitterId: string): Promise<{ flyers: FlyerAssetDto[] }>
   - Function: deleteFlyer(assetId: string): Promise<void>

4. Test service functions in browser console:
   - Call getTemporalPresets() and verify response format
   - Call recordEvent({ eventType: 'MapViewed' }) and verify 202 Accepted
   - Verify no runtime errors in console

Success Criteria:
- All three services are importable from services/
- TypeScript compile passes
- Each service exports functions with proper typing
- Services use standard getAuthHeader() pattern
- No hardcoded URLs (use relative /api/ paths)
```

---

## Prompt 3: Wire P22-P25 Services into Program.cs

**Target Issue:** Integration gaps
**Depends On:** All service scaffolds committed
**Effort:** ~10 minutes
**Success Criteria:** Backend builds, all services registered

### Task

```
Ensure all P22-P25 services are registered in Program.cs.

1. Verify registrations in builder.Services section:
   - IAnalyticsService → ConsoleAnalyticsService ✓ (should be done)
   - IFlyerUploadService → LocalFlyerUploadService ✓ (should be done)
   - CorrelationIdProvider (Scoped) ✓ (should be done)

2. Verify middleware in app pipeline:
   - app.UseWeUPObservability() before request logging ✓ (should be done)

3. Verify endpoints are mapped:
   - app.MapTemporalEndpoints() ✓ (should be done)
   - app.MapAnalyticsEndpoints() ✓ (should be done)
   - app.MapMediaEndpoints() ✓ (should be done)

4. Run full backend build:
   - dotnet build from backend/
   - Verify 0 errors, 0 warnings
   - Verify all projects compile (Domain, Infrastructure, Contracts, Api)

5. Smoke test one endpoint per service:
   - POST /api/temporal/events-at-time with valid preset
   - POST /api/analytics/events with valid event type
   - GET /api/temporal/presets
   - GET /api/analytics/event-types
   - Verify 200/201/202 responses

Success Criteria:
- Backend compiles without errors
- All services are instantiable via DI
- Each of 5 new endpoints responds with correct HTTP status
- No "service not found" errors on injection
```

---

## Prompt 4: Implement Frontend Temporal UI Seam

**Target Issue:** H3 (Timeline/calendar cosmetic)
**Depends On:** services/temporalService.ts created + endpoints verified
**Effort:** ~30 minutes
**Success Criteria:** TimelineControl calls backend temporal service

### Task

```
Connect TimelineControl to temporal service and display time windows.

1. Update features/world/WorldCoordinator.tsx:
   - Import temporalService from services/temporalService
   - In currentTime state handler, call temporalService.getEventsAtTime()
   - Display returned TimeWindowStart/TimeWindowEnd in a debug panel
   - Log preset label and timezone to console

2. Create a simple TemporalDebugPanel component:
   - Display current preset name
   - Display time window boundaries (StartUtc, EndUtc)
   - Display market timezone
   - Show event count from response
   - Display any errors from service call

3. Wire panel into WorldCoordinator below the TimelineControl:
   - Only render in development (check process.env.NODE_ENV)
   - Pass currentTime, market timezone to panel
   - Update when TimelineControl changes

4. Test in browser:
   - Click TimelineControl presets (NOW, 6PM, 9PM, etc.)
   - Verify TemporalDebugPanel updates with new window bounds
   - Check browser console for API calls to /api/temporal/events-at-time
   - Verify response shape matches TemporalQueryResponse

Success Criteria:
- TimelineControl click calls backend service
- TemporalDebugPanel displays time window bounds
- No console errors
- API calls visible in Network tab
- Window bounds change correctly for each preset
```

---

## Prompt 5: Set Up P24 Test Infrastructure (Jest + xUnit)

**Target Issue:** C2 (No test infrastructure)
**Depends On:** Builds complete
**Effort:** ~40 minutes
**Success Criteria:** Jest and xUnit are configured, CI can run tests

### Task

```
Establish foundational testing infrastructure for Phase 0.

Frontend (Jest):

1. Install Jest and TypeScript support:
   npm install --save-dev jest ts-jest @types/jest

2. Create jest.config.js:
   - moduleFileExtensions: ['ts', 'tsx', 'js', 'jsx']
   - preset: 'ts-jest'
   - testEnvironment: 'jsdom'
   - roots: ['<rootDir>/']
   - testMatch: ['**/__tests__/**/*.test.ts', '**/?(*.)+(spec|test).ts']
   - moduleNameMapper: { '^@/(.*)$': '<rootDir>/$1' }

3. Create first test template at __tests__/services/temporalService.test.ts:
   - Import temporalService
   - Test that getTemporalPresets() is callable (mock fetch)
   - Test that getEventsAtTime() accepts valid presets
   - No assertions needed yet (just smoke tests)

4. Add test script to package.json:
   - "test": "jest"
   - "test:watch": "jest --watch"

Backend (xUnit):

5. Create WeUP.Tests project:
   dotnet new xunit -n WeUP.Tests
   Add ProjectReference to WeUP.Domain, WeUP.Infrastructure, WeUP.Api

6. Create first test template at WeUP.Tests/Temporal/TemporalPresetMapperTests.cs:
   - Test that GetTimeWindow(TemporalPreset.NOW) returns non-null TimeWindow
   - Test that TimeWindow is valid (Validate() doesn't throw)
   - Test that GetPresetLabel works for each preset

7. Add test script to root:
   Create scripts/test.sh (or test.ps1 for Windows)
   - Runs: npm test && dotnet test backend/

8. Wire tests into GitHub Actions:
   - Create .github/workflows/test.yml
   - On push/PR: npm test && dotnet test
   - Report results

Success Criteria:
- jest --version works
- npm test runs with 0 tests (passes)
- dotnet test from backend/ runs with 2-3 tests
- CI pipeline triggers on push
- Team can write tests following this template
```

---

## Prompt 6: Create Smoke Test Suite

**Target Issue:** M3 (No seed data/smoke tests)
**Depends On:** P24 test infrastructure set up
**Effort:** ~35 minutes
**Success Criteria:** Automated smoke tests validate all core endpoints

### Task

```
Build smoke test suite for release validation.

Endpoints to Test (One per Service):

1. Create backend test: WeUP.Tests/Integration/TemporalEndpointsTests.cs
   - Test POST /api/temporal/events-at-time with valid preset
   - Verify response status 200
   - Verify response contains TimeWindowStart, TimeWindowEnd, Timezone
   - Test GET /api/temporal/presets
   - Verify response contains at least 8 presets (NOW, 6PM, 9PM, etc.)

2. Create backend test: WeUP.Tests/Integration/AnalyticsEndpointsTests.cs
   - Test POST /api/analytics/events with valid event type
   - Verify response status 202 Accepted
   - Test GET /api/analytics/event-types
   - Verify response contains at least 8 event types

3. Create backend test: WeUP.Tests/Integration/MediaEndpointsTests.cs
   - Test POST /api/media/flyers with a small test image file
   - Verify response status 201 Created
   - Verify response contains assetId
   - Test GET /api/media/flyers/{assetId}
   - Verify metadata matches upload request

4. Create frontend test: __tests__/integration/core-flows.test.ts
   - Test that WorldCoordinator renders without errors
   - Test that TimelineControl is present
   - Test that clicking preset calls temporal service
   - Test that analytics service can record an event

Test Infrastructure:

5. Create TestFixture base class for backend tests:
   - Sets up WebApplicationFactory
   - Provides HttpClient for requests
   - Cleans up after each test

6. Create TestDataBuilder for frontend:
   - Generates mock events, users, markets
   - Used by integration tests to bootstrap state

7. Document smoke test guidelines:
   - When to add new smoke tests
   - Pattern for testing an endpoint
   - How to mock external dependencies

Success Criteria:
- Backend: dotnet test runs 6+ tests, all pass
- Frontend: npm test runs 4+ tests, all pass
- Each test verifies one specific behavior
- Tests are independently runnable
- CI pipeline runs tests on every push
- New engineer can understand test patterns from examples
```

---

## Prompt 7: Implement Seed Data Generator

**Target Issue:** M3 (No seed data/smoke tests)
**Depends On:** Test infrastructure in place
**Effort:** ~25 minutes
**Success Criteria:** Script generates 50+ realistic events in <5 seconds

### Task

```
Create seed data generator for local testing and QA.

1. Create backend seed script: scripts/seed-data.cs
   - Accepts argument: --count (default 50)
   - Generates realistic events:
     * Random titles (e.g., "Summer Beats @ Club Paradise")
     * Random times (next 2 weeks, various presets)
     * Random districts (SF: downtown, mission, marina)
     * Random venues (placeholder names)
     * Realistic confidence scores
   - Generates users (5-10 test accounts)
   - Populates markets (SF as launch market)
   - Runs in ~3-5 seconds for 50 events

2. Create frontend seed script: scripts/seed-data.ts
   - Callable from: npm run seed-data
   - Accepts argument: --count (default 50)
   - Calls backend seed endpoint to populate database
   - Logs completion status
   - Can be run against dev/staging environments

3. Wire seed script into backend startup:
   - Create optional startup mode: --seed-data
   - If enabled, automatically generates test data on app start
   - Useful for Docker Compose + local development

4. Create Docker Compose file: docker-compose.yml
   - Backend service (WeUP.Api)
   - Frontend service (Next.js dev server)
   - Optional: PostgreSQL for future EF Core migration
   - Volumes for hot reload
   - Environment variables pre-configured

5. Document seed data process:
   - How to run seed-data script
   - What data is generated
   - How to reset database between runs
   - How to use in CI/CD

Success Criteria:
- npm run seed-data generates 50 events in <5 seconds
- dotnet run --seed-data starts app with test data
- docker-compose up starts full local stack
- Generated events are visible in RadarMap
- Seed data is realistic enough for manual QA
- Script is repeatable (idempotent)
```

---

## Prompt 8: Verify End-to-End Contract Flow

**Target Issue:** Overall integration validation
**Depends On:** All services wired, endpoints tested
**Effort:** ~20 minutes
**Success Criteria:** Complete user journey works through contracts

### Task

```
Validate that contracts work end-to-end across P19-P25.

Test Flows (Manual + Automated):

1. Spatial Query Flow (P19):
   - RadarMap renders
   - Pan/zoom updates viewport
   - POST /api/spatial/map-feed returns events in bounds
   - Events render on map
   - Verify district filter UI is present

2. Market Assignment Flow (P20):
   - POST /api/markets/determine returns SF market
   - GET /api/markets/launch returns Phase 0 launch market
   - Verify market status is Active
   - Verify market has accepting submissions = true

3. Temporal Query Flow (P21):
   - TimelineControl preset click triggers POST /api/temporal/events-at-time
   - Response includes correct TimeWindow bounds
   - TemporalDebugPanel displays bounds
   - Preset label matches (NOW → "NOW", 6PM → "6PM", etc.)

4. Analytics Event Flow (P23):
   - User clicks TimelineControl preset
   - POST /api/analytics/events records TemporalPresetSelected event
   - No errors in console
   - Event is logged to console (ConsoleAnalyticsService output)

5. Media Upload Flow (P25):
   - User uploads a flyer image
   - POST /api/media/flyers returns 201 Created with assetId
   - GET /api/media/flyers/{assetId} returns metadata
   - Verify file size and content type match
   - List user flyers returns uploaded asset

Automated Test:

6. Create integration test: __tests__/integration/end-to-end.test.ts
   - Seed test data (50 events, 3 markets)
   - Simulate user discovering events (viewport query)
   - Simulate time filtering (temporal query)
   - Simulate event save (analytics event)
   - Simulate flyer upload (media endpoint)
   - Verify all responses successful

Documentation:

7. Create docs/CONTRACT_VALIDATION.md:
   - Lists all contracts (request/response shapes)
   - Links to endpoint definitions
   - Outlines expected behavior for each
   - Documents failure modes and recovery

Success Criteria:
- All 5 flows execute without errors
- Contracts match on both frontend and backend
- End-to-end test passes
- No type mismatches between request/response
- Documentation is accurate
```

---

## Execution Order

Execute prompts in this order:

1. ✅ **Prompt 1** — Observability integration (~20 min)
   - No blockers, can start immediately
   - Enables better debugging for subsequent prompts

2. ✅ **Prompt 2** — Frontend service stubs (~25 min)
   - Depends on: P21-P25 endpoints (already done)
   - Enables: UI integration testing

3. ✅ **Prompt 3** — Program.cs verification (~10 min)
   - Depends on: Scaffolds committed
   - Fast validation checkpoint

4. ✅ **Prompt 4** — Temporal UI integration (~30 min)
   - Depends on: Prompts 2-3
   - Demonstrates contract validation in action

5. ✅ **Prompt 5** — Test infrastructure setup (~40 min)
   - Depends on: Nothing, parallel-safe
   - Enables: All subsequent test work

6. ✅ **Prompt 6** — Smoke test suite (~35 min)
   - Depends on: Prompt 5
   - Provides: Release validation gates

7. ✅ **Prompt 7** — Seed data generator (~25 min)
   - Depends on: Prompt 5 (optional)
   - Improves: Local development ergonomics

8. ✅ **Prompt 8** — End-to-end validation (~20 min)
   - Depends on: All others
   - Final integration checkpoint

**Total Estimated Time:** ~3 hours implementation + testing

---

## Success Criteria (Bundle 8 Complete)

- ✅ All P22-P25 scaffolds are fully integrated
- ✅ Both frontend and backend builds pass
- ✅ Correlation IDs flow through every request
- ✅ Frontend can call all new backend services
- ✅ All 5 core user flows work end-to-end
- ✅ Test infrastructure is ready for team use
- ✅ 10+ smoke tests validate core paths
- ✅ Seed data generator works reliably
- ✅ Documentation is complete and accurate
- ✅ No breaking changes to existing P19-P20 contracts

---

## Notes

- Execute prompts sequentially to catch issues early
- After each prompt, both builds should pass
- Commit after completing every 2 prompts (natural checkpoint)
- If a prompt fails, diagnose before proceeding to next
- Update this document with completion status as you progress
