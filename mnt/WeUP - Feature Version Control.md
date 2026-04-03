# WeUP Phase 0 — Feature Version Control & Roadmap

**Document Version:** 1.0  
**Last Updated:** 2026-04-02  
**Current Status:** P01–P18 Complete (Bundles 1–6) | P19–P24 Outstanding (Bundles 7–8)

---

## Table of Contents

1. [Version Map](#version-map)
2. [Implemented Features (P01–P18)](#implemented-features-p01--p18)
3. [Phase 0 Remaining Work (P19–P24)](#phase-0-remaining-work-p19--p24)
4. [Full Roadmap: Phase 0.15–0.30+](#full-roadmap-phase-015--030)
5. [Complete API Surface Reference](#complete-api-surface-reference)

---

## Version Map

Quick reference mapping versions to bundles, prompts, delivery estimates, and current status.

| Version | Bundle | Prompts | Description | Target Date | Status |
|---------|--------|---------|-------------|-------------|--------|
| v0.0 | Init | — | Mapbox Explorer project init | Jun 2025 | ✅ Done |
| v0.1 | B1 | P01–P03 | Frontend Runtime Spine (World Surface, Env Contract, UI State) | Jul 2026 (M1-E) | ✅ Done |
| v0.2 | B2 | P04–P06 | Canonical Event Domain (Aggregate, Provenance, Query Contracts) | Aug 2026 (M2-L) | ✅ Done |
| v0.3 | B3 | P07–P09 | .NET 8 API + EF Core Schema + First Endpoints | Oct 2026 (M4-L) | ✅ Done |
| v0.4 | B4 | P10–P12 | Ingestion Engine (Source Adapters, OCR Pipeline, Deduplication) | Nov 2026 (M5-L) | ✅ Done |
| v0.5 | B5 | P13–P15 | Moderation & Review (Queue, Confidence Scoring, Actions) | Dec 2026 (M6-L) | ✅ Done |
| v0.6 | B6 | P16–P18 | Auth, Sessions, Saves, Submissions (P16–P18) | Jan 2027 (M7-L) | ✅ Done |
| v0.7 | B7 | P19–P21 | Spatial & Temporal Semantics (Bounding-box, Districts, Temporal Query Logic) | — | ⏳ TODO |
| v0.8 | B8 | P22–P24 | Observability & Release Hardening (OpenTelemetry, Analytics, Tests, CI/CD) | — | ⏳ TODO |
| **v1.0** | — | — | **Phase 0 MVP Completion** (stable, releasable, E2E tested) | Feb 2027 | ⏳ TODO |
| v1.6–v1.15 | B9–B13 | P25–P38 | Phase 0.15: Media Signal Pack (Flyer/Video Upload, OCR, Moderation) | Jan–Feb 2027 | 📋 Roadmap |
| v1.16–v1.19 | B14–B18 | P39–P48 | Phase 0.20: Trust & Invite Graph (Trust Model, Tiers, Reputation, Anti-Abuse) | Mar–Apr 2027 | 📋 Roadmap |
| v1.20–v1.23 | B19–B23 | P41–P48 | Phase 0.25: Operator Revenue Pack (Venue/Sponsor Consoles, Billing) | May–Jun 2027 | 📋 Roadmap |
| **v1.24** | B24–B29 | P49–P56 | **Phase 0.30+: Intelligence Pack** (Ranking, Live Signals, Recommendations, GeoAudio, Multi-City) | Jun 2027 | 📋 Roadmap |

---

## Implemented Features (P01–P18)

### **Bundle 1 — Frontend Runtime Spine (v0.1)**

#### P01 — Establish WeUP World Surface Runtime
**What:** Single-viewport, persistent map-first Next.js frontend. Replaced prototype scattered state with coordinated architecture.
- Next.js 15 with React Server Components; Mapbox integration via `react-map-gl`
- World-Surface Coordinator (`features/world/WorldCoordinator.tsx`) as the central UI orchestrator
- Layered modal stack, sidebar panels, real-time map interaction
- **Key Files:**
  - `app/page.tsx` — thin page shell
  - `app/layout.tsx` — app boundary
  - `features/world/WorldCoordinator.tsx` — state coordinator
  - `components/RadarMap.tsx` — Mapbox wrapper with clustering
  - `components/BottomNav.tsx`, `components/TopBar.tsx` — chrome
- **Frontend API:** None (UI only)

#### P02 — Define Environment Contract and Secret Boundaries
**What:** Structured environment config with fail-fast validation. Splits public (client-safe) from server-only secrets.
- `lib/env/public.ts` — PUBLIC config (Mapbox token, API base)
- `lib/env/server.ts` — SERVER-ONLY config (DB password, keys)
- `lib/env/schema.ts` — Zod schema with validation
- `.env.example` — template for developers
- **Key Files:**
  - `lib/env/public.ts`
  - `lib/env/server.ts`
  - `lib/env/schema.ts`
  - `.env.example`
  - `docs/configuration.md` — setup guide

#### P03 — Refactor UI State Topology and Remove Render-Time Mutation
**What:** Centralized typed reducer + coordinator hook. Replaced scattered `useState` calls with deterministic state machine.
- `hooks/useWorldSurfaceState.tsx` — single source of truth for UI state
- Typed actions: `SELECT_EVENT`, `OPEN_MODAL`, `TOGGLE_SAVE`, `SET_TEMPORAL_FILTER`, etc.
- Immutable state shape prevents accidental mutations during render
- **Key Files:**
  - `hooks/useWorldSurfaceState.tsx` — main coordinator hook
  - `types/ui.ts` — action types, state shape
  - `docs/ui-state.md` — architectural guide
- **Frontend API:** None

**Bundle 1 Summary:** 3,500 lines of frontend refactoring. Stable runtime ready for backend integration.

---

### **Bundle 2 — Canonical Event Domain (v0.2)**

#### P04 — Define Canonical Event Aggregate and Status State Machine
**What:** Production-grade domain model replacing prototype `NightlifeItem`. Full event lifecycle with strict state transitions.
- **Event states:** `DRAFT → INGESTED → NEEDS_REVIEW → APPROVED → PUBLISHED / REJECTED / ARCHIVED`
- **Auto-approve threshold:** confidence ≥ 0.85
- **Fields:** id, slug, title, description, venueId, address, geoPoint(lat, lng), startTime, endTime, timezone, category, tags, status, confidence, mediaRefs, provenance, auditMeta
- Immutable domain model; illegal state transitions blocked at compile time
- **Key Files:**
  - `domains/event/types.ts` — Event aggregate, status enum, immutable shape
  - `domains/event/transitions.ts` — state-machine validation
  - `domains/event/projections.ts` — DTO projections for UI/API
  - `docs/event-aggregate.md` — domain specification

#### P05 — Add Source Provenance, Confidence, and Review Metadata
**What:** Every event traceable to its origin. Provenance + multi-dimensional confidence vector.
- **Source kinds:** MANUAL_SUBMISSION, LINK_EXTRACTION, VENUE_PAGE, FLYER_OCR (extensible)
- **Confidence vector:** extraction, geocode, temporal, venue-match, dupe-risk, source-trust, review-confidence (each 0.0–1.0)
- **Review metadata:** reviewer ID, timestamp, notes, action taken, rollback reference
- **Key Files:**
  - `domains/event/provenance.ts` — source kinds, provenance structs
  - `domains/event/types.ts` — confidence vector definition, review metadata

#### P06 — Define Temporal and Geospatial Query Contracts
**What:** Strongly typed query models for map feed (bounding-box) and calendar feed (time-range).
- `MapFeedQuery`: bbox (ne/sw corners), zoom level, filters (category, tier, confidence)
- `CalendarFeedQuery`: date window (start/end), timezone, filters
- **Key Files:**
  - `domains/query/contracts.ts` — query DTO, validation

**Bundle 2 Summary:** Domain model complete. Ready for backend persistence.

---

### **Bundle 3 — Backend Scaffold & First Endpoints (v0.3)**

#### P07 — Scaffold .NET 8 WeUP API with Clean Architecture
**What:** Four-layer Clean Architecture solution with strict dependency flow.
- **Layers:**
  - `WeUP.Api` — HTTP entry points (Minimal API, Swagger/OpenAPI)
  - `WeUP.Application` — use-case orchestration, business logic
  - `WeUP.Domain` — core domain model (interfaces, aggregates)
  - `WeUP.Infrastructure` — implementations (DB, external services, adapters)
- Dependency injection wired in `Program.cs`; swagger endpoint at `/swagger/v1/swagger.json`
- Health check endpoint: `GET /health`
- **Key Files:**
  - `backend/WeUP.sln` — solution file
  - `backend/WeUP.Api/Program.cs` — startup configuration
  - `backend/WeUP.Api/Endpoints/*` — all endpoint definitions
  - `docs/backend-architecture.md` — architecture guide

#### P08 — Create Postgres + EF Core Schema for Events, Sources, Users, Saves, Reviews
**What:** Durable PostgreSQL schema via EF Core. All Phase 0 data modeled and indexed.
- **Core tables:**
  - `Events` — canonical events (id, slug, title, address, coordinates, status, confidence, metadata)
  - `EventSources` — provenance (eventId, sourceKind, sourceId, extractionConfidence, review metadata)
  - `EventMedia` — flyer/media references (eventId, mediaAssetId, mediaType, confidence)
  - `Users` — profiles (userId, email, displayName, createdAt)
  - `SavedEvents` — user saves (userId, eventId, savedAt, position in itinerary)
  - `EventReviews` — moderation actions (eventId, reviewerId, action, reasoning, timestamp)
- Indexes on: (eventId, status), (bbox range), (startTime), (userId, savedAt)
- **Key Files:**
  - `backend/WeUP.Infrastructure/Persistence/WeUpDbContext.cs` — EF Core context
  - `backend/WeUP.Infrastructure/Persistence/Entities/*.cs` — entity models
  - `backend/WeUP.Infrastructure/Persistence/EfEventRepository.cs` — event persistence
  - `backend/WeUP.Infrastructure/Persistence/EfSaveRepository.cs` — save persistence

#### P09 — Implement Map Feed, Calendar Feed, Event Detail, and Save APIs
**What:** First four production endpoints. Real HTTP → in-memory data (stub repo).
- **Endpoints:**
  - `POST /api/events/map` (body: MapFeedQuery) → `EventMapProjection[]`
  - `POST /api/events/calendar` (body: CalendarFeedQuery) → `EventCalendarProjection[]`
  - `GET /api/events/{id}` → `EventDetailProjection`
  - `GET /api/users/me/saves` → `SavedEventProjection[]`
  - `POST /api/users/me/saves/{eventId}` → save event
  - `DELETE /api/users/me/saves/{eventId}` → unsave event
- **Key Files:**
  - `backend/WeUP.Api/Endpoints/EventEndpoints.cs`
  - `backend/WeUP.Api/Endpoints/SaveEndpoints.cs`
  - `backend/WeUP.Contracts/Events/EventContracts.cs`
  - `backend/WeUP.Contracts/Saves/SaveContracts.cs`
  - `docs/frontend-api-migration.md` — how-to swap mock data for real API

**Bundle 3 Summary:** Full backend framework. 42 API contract DTOs. EF Core schema complete. All endpoints tested with Swagger.

---

### **Bundle 4 — Ingestion Engine (v0.4)**

#### P10 — Build Source Adapter Framework for Venue Pages, Event Links, and Manual Submission
**What:** Pluggable ingestion pipeline. Each source kind (manual, URL, venue page) has its own adapter.
- **Adapter pattern:** `IIngestionAdapter` → implementations for each source kind
- **Adapters:**
  - `ManualSubmissionAdapter` — parse user-submitted form data
  - `LinkAdapter` — fetch + parse URL metadata (og:title, og:description, etc.)
  - `VenuePageAdapter` — scrape venue websites (stub, extensible)
- **Job tracking:** `IIngestionJobRepository` tracks job status (Queued → Running → Succeeded / Failed)
- **Endpoints:**
  - `POST /api/ingestion/manual` (body: EventDraft) → returns ingestionJobId
  - `POST /api/ingestion/link` (body: { url, sourceKind }) → queues job
  - `POST /api/ingestion/venue-page` (body: { venueId, pageUrl }) → queues job
  - `GET /api/ingestion/jobs/{id}` → job status + result payload
- **Key Files:**
  - `backend/WeUP.Infrastructure/Ingestion/Adapters/*.cs` — adapter implementations
  - `backend/WeUP.Application/Ingestion/IngestionDispatcher.cs` — DI-driven dispatcher
  - `backend/WeUP.Api/Endpoints/IngestionEndpoints.cs`
  - `backend/WeUP.Domain/Ingestion/IIngestionAdapter.cs` — interface

#### P11 — Implement Flyer OCR + LLM Normalization Pipeline
**What:** 7-stage pipeline: store asset → OCR → text cleanup → LLM normalization → geocoding → confidence eval → job complete.
- **OCR:** Stub `IStubOcrService` (ready for Google Vision integration). Extracts raw text + field bboxes.
- **Text Post-Processing:** `FlyerTextPostProcessor` normalizes whitespace, split lines into sentences, remove noise.
- **LLM Normalization:** `HeuristicLlmNormalizer` (heuristic + stub LLM call) maps OCR text → canonical event fields (title, date, venue, address, category).
- **Geocoding:** Stub `IStubGeocodingService` (ready for Google Maps integration). Maps address → lat/lng + confidence.
- **Confidence scoring:** Multi-dimensional vector aggregated into single eligibility flag.
- **Endpoint:**
  - `POST /api/ingestion/flyers` (multipart: file) → queues FlyerIngestionJob
- **Key Files:**
  - `backend/WeUP.Infrastructure/Flyer/FlyerIngestionPipeline.cs` — 7-stage orchestrator
  - `backend/WeUP.Infrastructure/Flyer/FlyerTextPostProcessor.cs` — text cleanup
  - `backend/WeUP.Infrastructure/Flyer/HeuristicLlmNormalizer.cs` — field extraction
  - `backend/WeUP.Api/Endpoints/FlyerEndpoints.cs`
  - `backend/WeUP.Domain/Flyer/IFlyerPipeline.cs` — interface

#### P12 — Implement Deduplication, Entity Resolution, and Merge Rules
**What:** Deterministic Levenshtein-based duplicate detection. Merges high-confidence duplicates; flags probable ones for review.
- **Scoring:** Levenstein distance across title (weight 0.5), venue (weight 0.3), address (weight 0.2)
- **Thresholds:**
  - ≥ 0.80 → auto-merge (combine sources, keep highest confidence per field)
  - 0.55–0.80 → probable duplicate (flag for moderator review)
  - < 0.55 → unique event
- **Merge policy:** for each field, keep highest-confidence value; accumulate source references
- **Key Files:**
  - `backend/WeUP.Application/Dedupe/DeduplicationService.cs` — matching + merge logic
  - `backend/WeUP.Domain/Dedupe/IDeduplicationService.cs` — interface

**Bundle 4 Summary:** Complete ingestion pipeline. 3 adapter types. OCR + geocoding + deduplication stubs ready for real provider integration.

---

### **Bundle 5 — Moderation & Review System (v0.5)**

#### P13 — Build Moderation Queue and Review Dashboard API
**What:** Filterable moderation queue with cursor pagination, stats, and history.
- **Queue endpoints:**
  - `GET /api/moderation/queue` (params: status, sourceKind, cursor, limit) → paginated queue items
  - `GET /api/moderation/queue/{itemId}` → full item detail + audit trail
  - `GET /api/moderation/stats` → counts by status, avg time-in-queue, SLA breach %
  - `GET /api/moderation/review-history` (params: eventId, reviewerId) → all reviews for filtering
- **Queue states:** Ingested → PendingReview → ReviewInProgress → Resolved (Approved / Rejected / RequestedChanges / Merged / Archived)
- **Key Files:**
  - `backend/WeUP.Application/Moderation/ModerationQueueService.cs` — queue operations
  - `backend/WeUP.Infrastructure/Moderation/InMemoryModerationQueue.cs` — in-memory queue (stub)
  - `backend/WeUP.Api/Endpoints/ModerationEndpoints.cs`
  - `backend/WeUP.Contracts/Moderation/ModerationContracts.cs`

#### P14 — Implement Confidence Scoring and Publish Eligibility Rules
**What:** Multi-dimensional confidence vector. Deterministic rule engine. Auto-approve at ≥ 0.85 aggregate.
- **Confidence dimensions:**
  - Extraction (OCR quality) — 0.0–1.0
  - Geocode (address match) — 0.0–1.0
  - Temporal (date clarity) — 0.0–1.0
  - VenueMatch (venue in catalog) — 0.0–1.0
  - DupeRisk (collision probability) — 0.0–1.0 (inverted)
  - SourceTrust (adapter trust level) — 0.0–1.0
  - ReviewConfidence (moderator conviction) — 0.0–1.0 (only post-review)
- **Aggregation:** weighted average (default: equal weights)
- **Rules:** blockers (must-reject if extraction < 0.4), auto-approve (if aggregate ≥ 0.85), manual-review (0.4–0.85)
- **Key Files:**
  - `backend/WeUP.Application/Moderation/PublishEligibilityService.cs`
  - `backend/WeUP.Domain/Moderation/IPublishEligibility.cs`

#### P15 — Add Review Actions, Audit Trail, and Rollback Workflow
**What:** Full moderator action set. Every action logged. Rollback supported up to 3 versions.
- **Actions:**
  - `Approve` — move to PUBLISHED
  - `Reject` — move to REJECTED + auto-archive after 30 days
  - `RequestChanges` — move to CHANGES_REQUESTED + reopen draft
  - `MarkDuplicate` — flag as probable duplicate, suggest merge
  - `Merge` — combine event A into B, archive A, update sources
  - `Archive` — hide from public queues but keep in audit trail
  - `Reopen` — revert to PendingReview if closed prematurely
  - `Rollback` — revert to previous state (up to 3 versions back)
- **Audit trail:** every action logged with: moderator ID, timestamp, reasoning, previous state, new state
- **Endpoints:**
  - `POST /api/moderation/queue/{id}/approve` (body: { reasoning })
  - `POST /api/moderation/queue/{id}/reject` (body: { reasoning })
  - `POST /api/moderation/queue/{id}/request-changes` (body: { changeSuggestions })
  - `POST /api/moderation/queue/{id}/mark-duplicate` (body: { likelyEventId })
  - `POST /api/moderation/queue/{id}/merge` (body: { targetEventId })
  - `POST /api/moderation/queue/{id}/archive`
  - `POST /api/moderation/queue/{id}/reopen` (body: { reasoning })
  - `POST /api/moderation/events/{id}/rollback` (body: { toVersionIndex })
- **Key Files:**
  - `backend/WeUP.Application/Moderation/ReviewActionService.cs`
  - `backend/WeUP.Infrastructure/Moderation/InMemoryAuditTrail.cs` — audit log (in-memory stub)
  - `backend/WeUP.Api/Endpoints/ModerationEndpoints.cs`

**Bundle 5 Summary:** Complete moderation pipeline. 8 action types. Full audit trail. Ready for real queue DB + Postgres.

---

### **Bundle 6 — Identity & User Persistence (v0.6)**

#### P16 — Implement Auth, Sessions, and Basic Profile Backbone
**What:** Passwordless email registration. Opaque bearer tokens (64-char hex, 24h TTL). User profile CRUD.
- **Registration:**
  - `POST /auth/register` (body: { email, displayName, avatar }) → returns { userId, token, expiresAt }
  - Generates opaque bearer token; stores in-memory
  - Fails if email already registered
- **Login:**
  - `POST /auth/login` (body: { email }) → returns { token, expiresAt } (stub: no password required for Phase 0)
- **Profile:**
  - `GET /auth/me` → returns { userId, email, displayName, avatar, createdAt }
  - `PATCH /auth/profile` (body: { displayName?, avatar? }) → updates profile
- **Auth middleware:** all endpoints except `/auth/register` require `Authorization: Bearer <token>` header; token validated + user loaded into request context
- **Key Files:**
  - `backend/WeUP.Infrastructure/Auth/BearerTokenService.cs` — token generation + validation
  - `backend/WeUP.Infrastructure/Auth/InMemoryUserRepository.cs` — user store (in-memory)
  - `backend/WeUP.Application/Users/UserAuthService.cs` — registration + login orchestration
  - `backend/WeUP.Api/Endpoints/AuthEndpoints.cs`
  - `backend/WeUP.Contracts/Auth/AuthContracts.cs`
  - `docs/bundle-6-identity.md` — auth architecture

#### P17 — Persist Saved Signals, Itinerary State, and User Preferences
**What:** Authenticated saves (not anonymous). Ordered itinerary. Per-user preferences (categories, home radius, notifications).
- **Saves (upgrade from P09):**
  - All prior `/api/users/me/saves` endpoints now auth-required
  - Backend filters saves to logged-in user
- **Itinerary:**
  - `GET /api/users/me/itinerary` → ordered list of eventIds with metadata
  - `POST /api/users/me/itinerary` (body: { eventId, notes?, dueDate? }) → appends to itinerary with auto-position
  - `PATCH /api/users/me/itinerary/{itemId}` (body: { position?, notes?, dueDate? }) → reorder or update
  - `DELETE /api/users/me/itinerary/{itemId}` → remove from itinerary
  - Position shifts automatically on add/remove/reorder
- **Preferences:**
  - `GET /api/users/me/preferences` → { preferredCategories[], homeCoordinate, homeRadius, notificationsEnabled, notificationFrequency }
  - `PATCH /api/users/me/preferences` (body: partial preferences) → merge update
- **Key Files:**
  - `backend/WeUP.Infrastructure/Auth/InMemoryItineraryRepository.cs`
  - `backend/WeUP.Infrastructure/Auth/InMemoryPreferencesRepository.cs`
  - `backend/WeUP.Api/Endpoints/ItineraryEndpoints.cs`
  - `backend/WeUP.Contracts/Users/ItineraryContracts.cs`
  - `backend/WeUP.Contracts/Users/PreferencesContracts.cs`

#### P18 — Implement Event Submission Workflow with Draft, Review, Published States
**What:** Full event creation workflow: user creates draft → edits → submits for review → moderator reviews → published or rejected.
- **Submission lifecycle:**
  - `POST /api/events/submissions` (body: EventDraftPayload) → creates draft, returns { submissionId, status: DRAFT }
  - `PATCH /api/events/submissions/{id}` (body: partial EventDraftPayload) → updates draft (owner-only)
  - `POST /api/events/submissions/{id}/submit` → moves to SUBMITTED_FOR_REVIEW (adds to moderation queue)
  - `GET /api/events/submissions/{id}/status` → returns { status, moderationQueueId?, approvalReason?, rejectionReason? }
- **States:** DRAFT (editable) → SUBMITTED_FOR_REVIEW (in queue) → APPROVED/REJECTED (terminal)
- **Changes Requested flow:** moderator can POST `/api/moderation/queue/{id}/request-changes` which sets submission back to DRAFT; user is notified
- **Query endpoints:**
  - `GET /api/events/submissions` (no params) → lists user's submissions (paginated)
  - `GET /api/events/submissions/{id}` → full submission detail
- **Pre-submit validation:** title, date, address required; geocoding attempted
- **Key Files:**
  - `backend/WeUP.Infrastructure/Submissions/InMemorySubmissionRepository.cs`
  - `backend/WeUP.Api/Endpoints/SubmissionEndpoints.cs`
  - `backend/WeUP.Contracts/Events/SubmissionContracts.cs`
  - `backend/WeUP.Domain/Events/IEventSubmissionService.cs`

**Bundle 6 Summary:** Identity system complete. Auth, saves, itinerary, preferences, and user submission workflow all live. 18 new endpoints. Ready for frontend integration.

---

## Phase 0 Remaining Work (P19–P24)

**Status:** NOT STARTED  
**Blockers:** None — can begin immediately after P18

### **Bundle 7 — Spatial & Temporal Semantics (v0.7)**

#### P19 — Implement Bounding-Box, Cluster, and District Query Semantics
**What:** Production spatial queries. Bounding-box filtering with multi-scale clustering. District-level aggregation.
- **Bounding-box query:** refined from P06 contract; backend now implements actual spatial filtering via Postgres PostGIS
- **Clustering:** server-side clustering (k-means on event coordinates) at multiple zoom levels
  - Z0–5: continent-level clusters
  - Z6–10: region clusters
  - Z11–14: city clusters
  - Z15+: individual events
- **District aggregation:** events aggregated by named districts (e.g., "Downtown", "Mission", "SoMa")
- **Acceptance criteria:**
  - Map viewport can request bbox + zoom → returns clustered markers
  - `GET /api/events/map?bbox=...&zoom=Z` → `{ clusters: [{ centroid, count, eventIds }], events: [...] }`

#### P20 — Define City Partitioning, Neighborhood Taxonomy, and Market Freeze Rules
**What:** Static city config. Named neighborhoods with boundaries. Market freeze state machine.
- **City config:** bounding box, timezone, primary language, tax rates, initial state
- **Neighborhoods:** GeoJSON feature collection (name, polygon, tier: downtown/neighborhood/suburb)
- **Market freeze states:**
  - ACTIVE: normal ingestion + publishing
  - FROZEN: only existing events visible; new submissions held in queue; no new publishes
  - CLOSED: read-only API; no ingestion or publishing
- **Admin endpoint:**
  - `PATCH /api/markets/{cityId}/status` (body: { status, reason, expiresAt? }) → enforces state machine transition rules
- **Acceptance criteria:**
  - `/api/events/map` respects market-freeze state
  - Frozen events still returned; new ingest returns 503 with message

#### P21 — Convert Timeline and Calendar UX into Real Temporal Query Logic
**What:** Calendar scrubber → real temporal queries. Time filtering tied to tz-aware event times.
- **Temporal contract (from P06):** start, end, timezone
- **Calendar feed refinement:**
  - Backend returns events grouped by hour (0am–11pm) within the requested day
  - Respects event timezone (e.g., event starting 7pm PST shows at 10pm EDT for East Coast user)
  - Returns grouped payload: `{ hourBuckets: [{ hour, events: [...] }] }`
- **Query endpoint:**
  - `POST /api/events/calendar` (body: { date, timezone, ... }) → returns hourly bucketed events
- **Acceptance criteria:**
  - Calendar UI updates in real-time; no timezone mismatches
  - User in different tz sees correct hours for same event

**Bundle 7 Acceptance Criteria:**
- Bounding-box, clustering, and district queries are fast (< 100ms for 10k events)
- Market freeze enforced; frozen markets still readable
- Timezone handling consistent across all queries

### **Bundle 8 — Observability & Release Hardening (v0.8)**

#### P22 — Add OpenTelemetry, Structured Logging, and Correlation IDs
**What:** Observable backend. Structured logs. Correlation IDs trace requests through ingestion → moderation → publish.
- **OpenTelemetry (NET SDK):** Wire into `Program.cs`; export to console (dev) or Jaeger (staging/prod)
- **Spans:** trace per request, per ingestion job, per moderation action, per publish
- **Structured logging:** switch from Console logging to Serilog; log in JSON format with correlation ID
- **Correlation ID:** inject at HTTP boundary; propagate through job processing chain; logged in all messages
- **Acceptance criteria:**
  - `dotnet run | grep "correlationId"` returns valid UUID for all traced operations
  - Single user request traceable end-to-end

#### P23 — Add Product Analytics, Error Telemetry, and Feature Flags
**What:** Event analytics. Error tracking. Feature flags for safe rollout.
- **Product analytics:** track user actions
  - Event views, saves, submissions, searches
  - Aggregated per user, per day
  - Exposed via `/api/analytics` (admin endpoint)
- **Error telemetry:** all 5xx errors logged with stack trace + context + correlation ID
- **Feature flags (IFeatureFlagProvider):**
  - `FlyerOcrEnabled` — toggle OCR pipeline on/off
  - `VideoPipelineEnabled` — toggle video ingest (Phase 0.15)
  - `TrustGraphEnabled` — toggle trust-graph queries (Phase 0.20)
  - `SponsorPlacementEnabled` — toggle sponsor features (Phase 0.25)
  - Flags stored in-memory; can be overridden via env var for testing
- **Acceptance criteria:**
  - Feature flags can be toggled without restart (via future config server)
  - Error rates trackable per endpoint

#### P24 — Add Seed Data, E2E Tests, Smoke Tests, and Release Gates
**What:** Automated testing. Reproducible test data. CI/CD gates before release.
- **Seed data scripts:**
  - `scripts/seed-development.sql` — 100 sample events across 5 cities, 20 users, moderation queue snapshots
  - Run via `dotnet run --seed` or manual SQL execution
- **E2E tests (Playwright or Cypress):**
  - Login → save event → add to itinerary → submit new event → moderation flow
  - Coverage: happy path + error cases
  - Run via `npm run test:e2e`
- **Smoke tests (HTTP):**
  - Basic health check, auth endpoints, map feed, save operations
  - Run post-deploy to validate all layers
- **Release gates:**
  - TypeScript build must pass with zero errors
  - .NET must compile with warnings-as-errors
  - All unit + integration tests must pass
  - No console errors in frontend
  - E2E tests must pass on staging
  - Seed data must restore successfully
- **Acceptance criteria:**
  - CI/CD pipeline enforces all gates
  - Release can be blocked automatically if any gate fails

**Bundle 8 Acceptance Criteria:**
- All p15+ commits reference a correlation ID
- Feature flags prevent rollout of incomplete features
- E2E test suite covers all P16–P18 workflows
- Staging deployment passes smoke tests before prod release

---

## Phase 0 Completion Checklist

**Required before v1.0 Release:**

- [ ] P19–P21 (Spatial/Temporal) implemented and tested
- [ ] P22–P24 (Observability/Hardening) implemented and tested
- [ ] TypeScript builds with zero errors
- [ ] No direct `process.env` in UI code (all via `lib/env`)
- [ ] All 40+ API endpoints functional with real backend
- [ ] No mock data in production code paths (only seed scripts for testing)
- [ ] E2E tests cover: auth → save → submit → moderation → publish
- [ ] Seed data scripts restore reliably
- [ ] OpenTelemetry exported to staging collector
- [ ] Feature flags for all Phase 0.15+ features (default: disabled)
- [ ] Database schema reviewed by architect
- [ ] Security review: CORS, CSRF, rate limits, input validation
- [ ] Performance baseline established (response times, DB query times)
- [ ] Load test passed (10k concurrent map requests)
- [ ] Documentation updated: API spec, architecture, deployment guide, operations runbook

---

## Full Roadmap: Phase 0.15–0.30+

### **Phase 0.15 — Media Signal Pack (v1.6–v1.15, Jan–Feb 2027)**

**Goal:** "Can WeUP ingest and operationalize real flyer/video media?"

**Scope:**
- Flyer upload with validation and OCR integration
- Video flyer intake with frame/poster extraction
- Media moderation queue
- Venue asset library (reusable media across events)
- Media-enriched event display (better thumbnails, ranking)

**Bundles:**

| Bundle | Prompts | Features |
|--------|---------|----------|
| B9 | P25–P27 | Media Upload Service, Flyer Asset Validation, Lifecycle |
| B10 | P28–P30 | Video Flyer Intake, Frame/Poster Extraction, Preview Assets |
| B11 | P31–P32 | Media Policy Checks, Moderation Queue, Takedown States |
| B12 | P33–P35 | Venue Asset Library, Venue Ownership, Media Permissions |
| B13 | P36–P38 | Media-Derived OCR Enrichment, Thumbnail Scoring, Projections |

**Hard Constraints:**
- No client-side media publishing into canonical events
- No untracked media blobs
- No silent asset replacement without audit
- No thumbnail selection based on last-uploaded behavior
- Video pipeline must validate: size, type, duration

**Acceptance Criteria:**
- All flyer uploads survive server restart
- Validation errors are deterministic
- Video processing jobs complete + register derived assets
- Moderation queue shows only ReviewPending assets
- UI displays media-derived thumbnails (not hard-coded images)

---

### **Phase 0.20 — Trust & Invite Graph (v1.16–v1.19, Mar–Apr 2027)**

**Goal:** "Can WeUP gate access through trust and proximity?"

**Scope:**
- User trust identity graph with nodes + edges
- Invite-tier system (Public → Adjacent → Private)
- Proximity-based visibility rules
- Reputation scoring from attendance + trust
- Anti-abuse heuristics

**Bundles:**

| Bundle | Prompts | Features |
|--------|---------|----------|
| B14 | P33 | User Trust Graph, Edges, Privacy Boundaries |
| B15 | P34 | Tier Unlock Engine |
| B16 | P35 | Proximity Scoring, Venue-Adjacent Access |
| B17 | P36 | Invite Issuance, Acceptance, Expiry, Revocation |
| B18 | P37–P40 | Reputation Signals, Anti-Spam, Private Reveal UX, Moderation |

**Hard Constraints:**
- Graph must be queryable in < 50ms for 1M users
- Trust edges must be immutable once created (only expiry marks revocation)
- Private events must never leak to unauthorized users
- Abuse flags must prevent tier escalation until cleared

**Acceptance Criteria:**
- Graph computes tier per user correctly
- Private events hidden from non-tier users
- Reputation updates are deterministic
- Admin UI displays graph state without degrading API latency
- Privacy Impact Assessment completed (GDPR)

---

### **Phase 0.25 — Operator Revenue Pack (v1.20–v1.23, May–Jun 2027)**

**Goal:** "Can venues and sponsors operate inside WeUP as customers?"

**Scope:**
- Venue console (event submission, analytics, payouts)
- Sponsor console (campaign creation, attribution)
- Campaign placement rules + inventory guardrails
- Billing & settlement (CPM/CPA models)
- Operator admin dashboard

**Bundles:**

| Bundle | Prompts | Features |
|--------|---------|----------|
| B19 | P41 | Venue Portal, Analytics, Verification |
| B20 | P42 | Sponsor Portal, Campaign Builder |
| B21 | P43–P44 | Campaign Attribution, Sponsored Placement Rules |
| B22 | P45–P46 | RBAC + Verification, Billing & Settlement |
| B23 | P47–P48 | Admin Console, KPI Dashboard |

**Hard Constraints:**
- Verified venues only; verification includes legal review
- Campaign inventory must be enforced (no oversell)
- Revenue sharing percentages must be auditable
- PCI-DSS compliance for payment processing

**Acceptance Criteria:**
- Venue owners can CRUD events, see analytics, receive payouts
- Sponsors can launch geo-targeted campaigns, see attribution
- All placement rules respect inventory + tier eligibility
- Admin console shows near-real-time KPIs
- Billing generates correct invoices; edge cases tested (partial day, zero impressions)

---

### **Phase 0.30+ — Intelligence Pack (v1.24, Jun 2027)**

**Goal:** "Can WeUP become the cultural intelligence layer for a city?"

**Scope:**
- Event ranking engine (freshness, density, trust, intent signals)
- Live-signal fusion (saves, opens, shares, venue activity)
- Personalized discovery without breaking cold-start UX
- GeoAudio ingestion + ambient detection
- City expansion control plane + market-freeze
- Experimentation framework + model governance

**Bundles:**

| Bundle | Prompts | Features |
|--------|---------|----------|
| B24 | P49 | Event Ranking Engine |
| B25 | P50 | Live Signal Fusion |
| B26 | P51 | Personalized Discovery |
| B27 | P52 | GeoAudio Ingestion & Venue Matching |
| B28 | P53–P55 | Cultural Heatmap, Sponsor Recommendation, City Expansion Control |
| B29 | P56 | Experimentation Framework, Model Versioning, Safety Rollback |

**Hard Constraints:**
- Ranking algorithm must be deterministic (same inputs → same outputs)
- GeoAudio confidence must be ≥ 0.80 before "Live-Now" badge shown
- Experiments must be rollable without data loss
- Private events must never appear in personalized recommendations to non-tier users

**Acceptance Criteria:**
- Ranking API returns deterministic, reproducible order
- Live-signal fusion updates scores within 30s of user action
- Personalized recommendations respect trust-graph tier
- GeoAudio matches achieve ≥ 80% confidence on test set
- Market-freeze correctly blocks new ingest while serving existing data
- Experiments can be launched, measured, and rolled back safely
- Sponsor placement suggestions improve CPM by ≥ 5% in A/B test
- All new services covered by unit + integration tests + OpenTelemetry spans

---

## Complete API Surface Reference

**Full list of 40+ production endpoints (P07–P18).**

### **Auth Domain (P16)**

| Method | Path | Request Body | Response | Notes |
|--------|------|--------------|----------|-------|
| POST | `/auth/register` | `{ email, displayName, avatar? }` | `{ userId, token, expiresAt }` | Passwordless, email only |
| POST | `/auth/login` | `{ email }` | `{ token, expiresAt }` | Stub: no password check |
| GET | `/auth/me` | — | `{ userId, email, displayName, avatar, createdAt }` | Requires auth |
| PATCH | `/auth/profile` | `{ displayName?, avatar? }` | `{ userId, displayName, avatar }` | Requires auth |

### **Events Domain (P09, P18)**

| Method | Path | Request Body | Response | Notes |
|--------|------|--------------|----------|-------|
| POST | `/api/events/map` | `{ bbox, zoom, filters? }` | `{ clusters: [...], events: [...] }` | Spatial query |
| POST | `/api/events/calendar` | `{ date, timezone, filters? }` | `{ hourBuckets: [...] }` | Temporal query |
| GET | `/api/events/{id}` | — | `EventDetailProjection` | Detail view |
| POST | `/api/events/submissions` | `EventDraftPayload` | `{ submissionId, status }` | Create draft, requires auth |
| GET | `/api/events/submissions` | — | `SubmissionSummary[]` | User's submissions, paginated, requires auth |
| GET | `/api/events/submissions/{id}` | — | `SubmissionDetail` | Full submission, owner-only |
| PATCH | `/api/events/submissions/{id}` | `partial EventDraftPayload` | `SubmissionDetail` | Update draft, owner-only |
| POST | `/api/events/submissions/{id}/submit` | `{ }` | `{ submissionId, status: SUBMITTED_FOR_REVIEW }` | Move to review queue |
| GET | `/api/events/submissions/{id}/status` | — | `{ status, queueId?, approval?, rejection? }` | Status check |

### **Saves Domain (P09, P17)**

| Method | Path | Request Body | Response | Notes |
|--------|------|--------------|----------|-------|
| GET | `/api/users/me/saves` | — | `SavedEventProjection[]` | Requires auth |
| POST | `/api/users/me/saves/{eventId}` | `{ position?, notes? }` | `{ eventId, savedAt, position }` | Save event, requires auth |
| DELETE | `/api/users/me/saves/{eventId}` | — | `{ success: true }` | Unsave event, requires auth |

### **Itinerary Domain (P17)**

| Method | Path | Request Body | Response | Notes |
|--------|------|--------------|----------|-------|
| GET | `/api/users/me/itinerary` | — | `ItineraryItem[]` | Ordered list, requires auth |
| POST | `/api/users/me/itinerary` | `{ eventId, notes?, dueDate? }` | `{ itemId, position, ... }` | Append with auto-position |
| PATCH | `/api/users/me/itinerary/{itemId}` | `{ position?, notes?, dueDate? }` | `ItineraryItem` | Reorder or update |
| DELETE | `/api/users/me/itinerary/{itemId}` | — | `{ success: true }` | Remove, shifts positions |

### **Preferences Domain (P17)**

| Method | Path | Request Body | Response | Notes |
|--------|------|--------------|----------|-------|
| GET | `/api/users/me/preferences` | — | `{ categories, homeCoord, homeRadius, notifications, ... }` | Requires auth |
| PATCH | `/api/users/me/preferences` | `partial UserPreferences` | `UserPreferences` | Merge update, requires auth |

### **Ingestion Domain (P10, P11)**

| Method | Path | Request Body | Response | Notes |
|--------|------|--------------|----------|-------|
| POST | `/api/ingestion/manual` | `EventDraftPayload` | `{ jobId, status: Queued }` | Manual submission |
| POST | `/api/ingestion/link` | `{ url, sourceKind }` | `{ jobId, status: Queued }` | URL metadata extraction |
| POST | `/api/ingestion/venue-page` | `{ venueId, pageUrl }` | `{ jobId, status: Queued }` | Venue page scrape |
| GET | `/api/ingestion/jobs/{id}` | — | `{ jobId, status, result?, error? }` | Job status + payload |
| POST | `/api/ingestion/flyers` | `multipart: file` | `{ jobId, status: Queued }` | Flyer upload + OCR pipeline |

### **Moderation Domain (P13, P15)**

| Method | Path | Request Body | Response | Notes |
|--------|------|--------------|----------|-------|
| GET | `/api/moderation/queue` | `{ status?, sourceKind?, cursor?, limit? }` | `{ items: [...], nextCursor? }` | Paginated queue |
| GET | `/api/moderation/queue/{itemId}` | — | `ModerationQueueItem` | Detail + audit trail |
| GET | `/api/moderation/stats` | — | `{ countsByStatus, avgTimeInQueue, slaBreachPercent, ... }` | Queue stats |
| GET | `/api/moderation/review-history` | `{ eventId?, reviewerId? }` | `ReviewAction[]` | Audit trail filtered |
| POST | `/api/moderation/queue/{id}/approve` | `{ reasoning }` | `{ itemId, status: Approved }` | Approve event |
| POST | `/api/moderation/queue/{id}/reject` | `{ reasoning }` | `{ itemId, status: Rejected }` | Reject event |
| POST | `/api/moderation/queue/{id}/request-changes` | `{ changeSuggestions }` | `{ itemId, status: ChangesRequested }` | Request revisions |
| POST | `/api/moderation/queue/{id}/mark-duplicate` | `{ likelyEventId }` | `{ itemId, status: ProbableDuplicate }` | Flag duplicate |
| POST | `/api/moderation/queue/{id}/merge` | `{ targetEventId }` | `{ itemId, status: Merged, targetId }` | Merge into event |
| POST | `/api/moderation/queue/{id}/archive` | `{ }` | `{ itemId, status: Archived }` | Hide from queue |
| POST | `/api/moderation/queue/{id}/reopen` | `{ reasoning }` | `{ itemId, status: PendingReview }` | Reopen if closed |
| POST | `/api/moderation/events/{id}/rollback` | `{ toVersionIndex }` | `{ eventId, status, rolledBackFrom, rolledBackTo }` | Rollback event |

### **Health & System**

| Method | Path | Response | Notes |
|--------|------|----------|-------|
| GET | `/health` | `{ status: "Healthy", timestamp }` | Liveness probe |

---

## Next Steps

**Immediate (complete v0.7–v0.8 to reach v1.0):**
1. Assign P19–P21 to engineer (spatial/temporal semantics)
2. Assign P22–P24 to engineer (observability/hardening)
3. Create GitHub issues for each prompt
4. Establish sprint schedule (2–3 weeks per bundle)
5. Set up staging environment for E2E + performance testing

**Concurrent:**
- Begin Phase 0.15 design (flyer/video pipeline)
- Schedule architecture review for trust graph (Phase 0.20)
- Determine media storage strategy (local fs, S3, Blob)

**Post v1.0:**
- Full Phase 0 launch marketing push
- User feedback collection
- Phase 0.15 engineering begins
