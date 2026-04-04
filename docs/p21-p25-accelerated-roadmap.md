# P21-P25 Accelerated Implementation Roadmap

**Scope:** 5 prompts in rapid succession  
**Target:** ~3 hours of implementation + 1 hour testing/diagnostic  
**Strategy:** Create minimal viable implementations with stubs, focus on contracts/services, defer UI integration

## P21: Temporal Query Logic (Current)

**Quick Win Components:**
- TemporalPreset enum (NOW, 6PM, 9PM, MIDNIGHT, 3AM, FRI, SAT, SUN)
- TimeWindow record (start, end, timezone)
- TemporalQueryService (stub with preset mapping)
- Endpoints: POST /api/temporal/events-at-time, GET /api/temporal/presets
- Frontend service (temporalService.ts)
- **NOT included:** TimelineControl/CulturalCalendar refactor (deferred to UI phase)

**Effort:** ~45 min  
**Blockers:** None (P19 + P20 foundation ready)

---

## P22: Observability / OpenTelemetry (Lightweight)

**Quick Win Components:**
- Structured logging setup (Serilog wire-in)
- Correlation ID middleware
- Trace instrumentation for key services (IEventRepository, IViewportQueryService, etc.)
- Health check enrichment
- **NOT included:** Full tracing export, dashboards, samples

**Effort:** ~30 min  
**Blockers:** None

---

## P23: Product Analytics (Minimal)

**Quick Win Components:**
- Analytics event types (enum: MapView, EventSave, EventSubmit, etc.)
- Analytics service (stub: logs to console)
- Feature flag infrastructure (IF-THEN stub)
- Telemetry guard rails (no PII)
- **NOT included:** Real sink (datadog, mixpanel, etc.), instrumentation in all components

**Effort:** ~30 min  
**Blockers:** None

---

## P25: Flyer Media Intake (Minimum Viable)

**Quick Win Components:**
- Media storage contracts (UploadFlyer request/response)
- Flyer service interface (IFlyerUploadService)
- Stub implementation (stores to local /uploads dir, fake UUID)
- POST /api/media/flyers endpoint
- Frontend service (flyerUploadService.ts)
- **NOT included:** OCR, video processing, validation, moderation

**Effort:** ~30 min  
**Note:** P24 (testing) skipped per user request; will be addressed in final diagnostic

---

## Execution Order

1. ✅ P19 (Complete)
2. ✅ P20 (Complete)
3. **→ P21** (~45 min) — Temporal semantics
4. → P22 (~30 min) — Logging + correlation IDs
5. → P23 (~30 min) — Analytics scaffolding
6. → P25 (~30 min) — Media upload scaffolding
7. → **Full Diagnostic** (~30 min) — Coverage report, gaps, risks

**Total ETA:** ~2.5 hours implementation + testing

---

## Why This Approach Works

- **Contracts first:** All services use contracts; future implementation is drop-in replacement
- **No UI refactoring:** UX integration deferred to post-P25 phase
- **Stubs sufficient:** Temporal presets, analytics events, media uploads all work with in-memory implementations
- **Test-ready infrastructure:** P22 logging + P23 events ready for P24 test framework

---

## Post-Diagnostic Action Items

1. **Identify gaps:** Which services need contracts or were skipped?
2. **Integration blockers:** What's preventing end-to-end test of P19-P23?
3. **P24 scope:** Create test harness for the services we built
4. **UI phase:** Refactor TimelineControl, RadarMap, etc. to consume contracts

