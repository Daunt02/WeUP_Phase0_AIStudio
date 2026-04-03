# WeUP Phase 0 — Complete Work Remaining

**Current Status:** P18 Complete (Event Submission Workflow)  
**Next:** Bundle 7 (P19-P21) — Spatial and Temporal Semantics  
**Document Updated:** 2026-04-02

---

## Executive Summary

The project is at **P18 of P24 (Bundle 6 complete, Bundle 7-8 remaining)** for Phase 0 release. After P19-P21, the remaining work is primarily **observability, telemetry, testing infrastructure, and release hardening**.

### Packs Completed
- ✅ Pack 1 (P01-P09): Runtime spine, domain, backend scaffold
- ✅ Bundle 2 (P04-P06): Event domain and contracts
- ✅ Bundle 3 (P07-P09): .NET persistence scaffold
- ✅ Bundle 4 (P10-P12): Ingestion pipelines (manual, links, venue pages)
- ✅ Bundle 5 (P13-P15): Moderation, confidence scoring, review actions
- ✅ Bundle 6 (P16-P18): Auth, user persistence, event submission workflow

### Current Backlog (P19-P48)

---

## Bundle 7 — Spatial and Temporal Semantics (P19-P21)

### Purpose
Make the map and time controls mean something real by binding them to actual backend query contracts.

### P19 — Implement Bounding Box, Cluster, and District Query Semantics
**Effort:** ~4-6 hours  
**Type:** Backend domain + frontend integration  
**Blockers:** None  
**Dependencies:** Inherent (none)

**What gets done:**
- [ ] Spatial query contracts (BoundingBox, MapFeedRequest/Response)
- [ ] Backend viewport query service
- [ ] District/neighborhood query model
- [ ] Spatial validation rules
- [ ] RadarMap refactored to use contracts
- [ ] Tests for bounding-box semantics

**Key Decisions:**
- Clustering location: backend pre-aggregation vs frontend on canonical results
- District approximation: polygon vs bounding-box seam
- Index strategy for spatial reads (PostGIS or LINQ)

**Outputs:**
- Domain models: `backend/WeUP.Domain/Spatial/`
- Services: `backend/WeUP.Infrastructure/Spatial/`
- Frontend: `lib/spatial/`, `services/spatialService.ts`
- Tests: verification examples for spatial queries

**Success Metrics:**
- RadarMap accepts MapFeedRequest contracts
- Bounding-box validation rejects invalid ranges
- District filtering works end-to-end
- Empty viewport doesn't break UX

---

### P20 — Define City Partitioning, Neighborhood Taxonomy, and Market Freeze Rules
**Effort:** ~5-7 hours  
**Type:** Backend domain + policy/rules  
**Blockers:** Needs P19 complete (spatial queries are foundation)  
**Dependencies:** P19

**What gets done:**
- [ ] Market domain models (Market, District, Neighborhood)
- [ ] Market policy service (freeze rules, assignment)
- [ ] Market taxonomy service (alias resolution)
- [ ] Market read endpoints (active markets, taxonomy)
- [ ] Launch market configuration
- [ ] Tests for market assignment and freeze enforcement

**Key Decisions:**
- Taxonomy layers: District only vs District + Neighborhood?
- Freeze mechanism: soft warning vs hard block?
- Phase 0 launch market: which city/region?
- Multi-city expansion seams (how to prep for future)?

**Outputs:**
- Domain models: `backend/WeUP.Domain/Markets/`
- Services: `backend/WeUP.Infrastructure/Markets/`
- Endpoints: `backend/WeUP.Api/Endpoints/MarketEndpoints.cs`
- Config: market metadata and freeze rules
- Frontend types: `types/market.ts`, `lib/markets/`

**Success Metrics:**
- Events can be assigned to markets by geography
- Market freeze rules are enforced
- Taxonomy is queryable (aliases resolved)
- System supports single-market launch without breaking multi-city seams

---

### P21 — Convert Timeline and Calendar UX into Real Temporal Query Logic
**Effort:** ~5-7 hours  
**Type:** Backend domain + frontend UX refactor  
**Blockers:** Needs P20 complete (market-local timezone)  
**Dependencies:** P19, P20

**What gets done:**
- [ ] Temporal query contracts (TimeWindow, DateWindow, TemporalPreset)
- [ ] Preset label → window mapping logic
- [ ] Backend temporal query service
- [ ] Event temporal rules (upcoming, ongoing, cross-midnight)
- [ ] TimelineControl and CulturalCalendar refactor
- [ ] Timezone-aware temporal validation
- [ ] Tests for edge cases (midnight spanning, timezone boundaries)

**Key Decisions:**
- Preset semantics: relative to "now" or absolute?
- Timezone authority: market-local or browser-local?
- Timeline + Calendar conflict resolution
- Cross-midnight event display logic

**Outputs:**
- Domain models: `backend/WeUP.Domain/Temporal/`
- Services: `backend/WeUP.Infrastructure/Temporal/`
- Frontend lib: `lib/temporal/`, `types/temporal.ts`
- Refactored components: `components/TimelineControl.tsx`, `components/CulturalCalendar.tsx`
- Service: `services/temporalService.ts`

**Success Metrics:**
- Timeline labels map to deterministic time windows
- Calendar selection drives real queries
- Ongoing events display correctly across midnight
- Timezone edge cases handled correctly

---

## Bundle 8 — Observability and Release Hardening (P22-P24)

### Purpose
Make Phase 0 testable, observable, and releasable with proper instrumentation, telemetry, and quality gates.

### P22 — Add OpenTelemetry, Structured Logging, and Correlation IDs
**Effort:** ~6-8 hours  
**Type:** Infrastructure/observability  
**Blockers:** None  
**Dependencies:** None (can work in parallel with P19-P21)

**What gets done:**
- [ ] OpenTelemetry SDK wiring in .NET backend
- [ ] Structured logging setup (Serilog or built-in)
- [ ] Correlation ID propagation (request tracing)
- [ ] Trace instrumentation for key services
- [ ] Log context enrichment
- [ ] Frontend observability hook

**Outputs:**
- Observability config in Program.cs
- Log context helpers
- Trace decorators for services
- Documentation: observability guidelines

---

### P23 — Add Product Analytics, Error Telemetry, and Feature Flags
**Effort:** ~5-7 hours  
**Type:** Product instrumentation  
**Blockers:** P22 (structured logging foundation)  
**Dependencies:** P22

**What gets done:**
- [ ] Error telemetry capture (frontend + backend)
- [ ] Feature flag infrastructure
- [ ] Key product events (view map, save event, submit, etc.)
- [ ] Dashboard/sink setup (local development compatible)
- [ ] Analytics guard rails (no PII)

**Outputs:**
- Analytics client library
- Event type definitions
- Feature flag service
- Telemetry sink configuration

---

### P24 — Add Seed Data, E2E Tests, Smoke Tests, and Release Gates
**Effort:** ~8-10 hours  
**Type:** Testing + QA infrastructure  
**Blockers:** P22-P23 (depends on working observability)  
**Dependencies:** P22, P23

**What gets done:**
- [ ] Seed data generator (realistic events, users, markets)
- [ ] Smoke test suite (health checks, basic flows)
- [ ] E2E tests (user journeys: discover → save → submit)
- [ ] Release checklist/gates
- [ ] Docker setup for local testing
- [ ] CI/CD hooks (GitHub Actions if applicable)

**Outputs:**
- `scripts/seed-data.ts`
- `backend/Tests/` and `tests/` (frontend)
- Release gate documentation
- Docker Compose setup

---

## Pack 2 — Phase 0.15 Media Signal Pack (P25-P32)

### Purpose
Add real media ingestion: flyer upload, video processing, OCR confidence, media moderation, and venue asset libraries.

### Scope Overview
- Flyer media intake and upload service
- Video flyer pipeline (frame extraction, poster generation)
- Media validation, file policies, abuse controls
- Venue asset library and reusable media catalog
- OCR confidence review dashboard
- Media-derived event enrichment
- Source attribution and copyright
- Media QA dashboard for operators

### P25-P32 Effort Estimate
**Total:** ~40-50 hours  
**Sequence:** P25 → P26 → P27 → P28 → P29 → P30 → P31 → P32

**Key Decisions Ahead:**
- Media storage (local file, S3, Azure Blob)?
- OCR service (AWS Textract, Google Vision, local)?
- Video processing (FFmpeg, cloud video API)?
- Media moderation (human review, automated policy)?

---

## Pack 3 — Phase 0.20 Trust Graph Pack (P33-P40)

### Purpose
Move from public discovery to gated cultural access through trust and proximity rules.

### Scope Overview
- Trust identity graph and edges
- Invite tier engine (public, adjacent, private layers)
- Proximity scoring and venue-adjacent access rules
- Invite issuance, acceptance, expiry, revocation
- Reputation signals (attendance, host trust, referrals)
- Anti-spam and abuse detection
- Private reveal UX and API guardrails
- Trust moderation and appeals

### P33-P40 Effort Estimate
**Total:** ~50-60 hours  
**Sequence:** P33 → P34 → P35 → P36 → P37 → P38 → P39 → P40

**Key Decisions Ahead:**
- Trust graph DB model (adjacency, edge weights)?
- Tier unlock thresholds (how many invites = adjacent access)?
- Proximity rules (geographic vs social distance)?
- Reputation aggregation strategy?

---

## Pack 4 — Phase 0.25 Operator Revenue Pack (P41-P48)

### Purpose
Turn WeUP into an operating system for venues, sponsors, and admins with billing and analytics.

### Scope Overview
- Venue portal (event submission, editing, analytics)
- Sponsor portal (geo-activation campaigns)
- Campaign attribution (views, saves, opens, attendance)
- Sponsored placement and inventory guardrails
- Venue and sponsor verification, role-based access
- Billing, invoicing, and settlement
- Operator admin console (city-level oversight)
- KPI dashboard (venue growth, sponsor yield, market health)

### P41-P48 Effort Estimate
**Total:** ~60-80 hours  
**Sequence:** P41 → P42 → P43 → P44 → P45 → P46 → P47 → P48

**Key Decisions Ahead:**
- Billing system (Stripe integration, custom)?
- Venue verification (email domain, manual review)?
- Sponsor account types (self-serve vs sales-assisted)?
- Analytics granularity (hourly, daily, real-time)?

---

## Pack 5 — Phase 0.30+ Intelligence Pack (P49-P56)

### Purpose
Add ranking, recommendations, live signal fusion, and experimentation infrastructure.

### Scope Overview
- Event ranking engine (freshness, density, trust, intent)
- Live signal fusion (saves, opens, shares, venue activity)
- Personalized discovery (cold-start compatible)
- GeoAudio ingestion and matching
- Cultural heatmap and district momentum
- Sponsor recommendation and placement optimization
- City expansion control plane
- Experimentation framework and model versioning

### P49-P56 Effort Estimate
**Total:** ~70-100 hours  
**Sequence:** P49 → P50 → P51 → P52 → P53 → P54 → P55 → P56

**Key Decisions Ahead:**
- Ranking weights (how to balance freshness vs quality)?
- Cold-start strategy (popular events, venue-based, geographic)?
- GeoAudio as core dependency or experimental edge?
- Experimentation framework (feature flags, A/B bucketing)?

---

## Critical Path & Dependencies

```
P19 (Spatial)
  ↓
P20 (Market) ← depends on spatial foundation
  ↓
P21 (Temporal) ← depends on market config
  ↓
P22 (Observability) ← can start in parallel
  ↓
P23 (Analytics) ← needs logging
  ↓
P24 (Testing) ← needs observability
  ↓
Phase 0 Release (P01-P24 Complete)
  ↓
Pack 2 (P25-P32) ← Media Signal (parallel stream)
Pack 3 (P33-P40) ← Trust Graph (post-P24)
Pack 4 (P41-P48) ← Revenue Ops (post-Pack 3)
Pack 5 (P49-P56) ← Intelligence (post-Pack 4)
```

### Estimated Timeline
- **Bundle 7 (P19-P21):** 2-3 weeks (15-20 hours)
- **Bundle 8 (P22-P24):** 2-3 weeks (20-25 hours)
- **Phase 0 Complete:** 4-6 weeks total
- **Pack 2 (P25-P32):** 6-8 weeks (40-50 hours)
- **Pack 3 (P33-P40):** 8-10 weeks (50-60 hours)
- **Pack 4 (P41-P48):** 10-12 weeks (60-80 hours)
- **Pack 5 (P49-P56):** 12-16 weeks (70-100 hours)

---

## Known Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Temporal semantics leak into UI as labels | Medium | P21 enforces backend contracts; tests verify mapping |
| Market model assumptions wrong for launch | High | P20 architecture review; freeze rules configurable |
| Timezone edge cases (midnight, DST) | Medium | Comprehensive test suite; market-local as source of truth |
| Observability overhead slows development | Low | Structured logging best practices; optional sampling |
| Missing seed data breaks QA | Medium | P24 generates realistic datasets; CI uses seeds |
| Media storage cost/scale | Medium | Phase 0 uses local storage; upgrade path documented |
| Trust graph complexity | High | P33-P40 is largest pack; staged implementation recommended |
| Billing integration delays revenue | Medium | Start integration early in P41; use test payment gateway |

---

## Remaining Test Infrastructure Gaps

| Test Type | Backend | Frontend | Status |
|-----------|---------|----------|--------|
| Unit tests | None | None | Missing (P24) |
| Integration tests | None | None | Missing (P24) |
| E2E tests | None | None | Missing (P24) |
| Load tests | None | N/A | Not planned |
| Security tests | None | None | Not planned |

**Action:** P24 introduces Jest (frontend) and xUnit (backend) with CI hooks.

---

## Breaking Changes Ahead

1. **P19:** MapFeedRequest contract change (breaking old fetch calls)
2. **P20:** Market assignment logic (events must have market_id)
3. **P21:** TemporalFeedRequest contract change (timeline/calendar refactor)
4. **P22:** Logging structure change (old log format incompatible)
5. **P23:** Event telemetry schema (backward compatibility strategy?)
6. **P24:** Seed data schema changes (migration scripts needed)

**Mitigation:** Changelog and migration guides for each bundle.

---

## Deployment Checkpoints

- **After P21:** Map and time UX are contract-driven ✓
- **After P24:** Phase 0 releasable with observability ✓
- **After P32:** Flyer OCR and video media ready ✓
- **After P40:** Trust system active; multi-tier access ready ✓
- **After P48:** Venue/sponsor revenue ops live ✓
- **After P56:** Intelligence layer and ranking live ✓

---

## External Dependencies & Assumptions

| Dependency | Status | Notes |
|------------|--------|-------|
| PostgreSQL + EF Core | Active (P08) | Using stub implementations; migrate in P24 |
| Mapbox API | Active | Free tier for dev; paid for production |
| OpenTelemetry | To-do (P22) | Open standard; no vendor lock |
| Flyer OCR | To-do (P25) | AWS Textract, Google Vision, or local |
| Video processing | To-do (P26) | FFmpeg or cloud API |
| Billing (Stripe) | To-do (P46) | Integration in P46 |
| GeoAudio | To-do (P52) | Experimental; architecture TBD |

---

## Success Criteria for Full Build

- ✅ Phase 0: User can discover, save, submit, and review events on stable map
- ✅ Phase 0.15: Real flyer/video media ingestion with OCR
- ✅ Phase 0.20: Trust-gated access with tiers and proximity rules
- ✅ Phase 0.25: Venues and sponsors can operate and monetize
- ✅ Phase 0.30+: Cultural intelligence layer with ranking and recommendations

---

## Notes

This document tracks the **complete prompt bundle path** and is updated after each prompt completion. Refer back to **`bundle-07-progress.md`** during P19-P21 for granular tracking.

