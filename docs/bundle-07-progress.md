# Bundle 7 — Spatial and Temporal Semantics
## Implementation Progress Log

**Status:** In Progress  
**Current Phase:** P19 (Bounding Box, Cluster, and District Query Semantics)  
**Target Completion:** All three prompts (P19, P20, P21)

---

## Overview

Bundle 7 converts the map and time UI from cosmetic decorations into operational semantic systems. The goal is to make the RadarMap and TimelineControl reflect real backend query contracts instead of loose frontend-only logic.

### Scope
- **P19**: Replace client-side spatial filtering with canonical bounding-box, cluster, and district semantics
- **P20**: Define market/city partitioning, neighborhood taxonomy, and market freeze rules
- **P21**: Convert timeline/calendar labels into real temporal query windows

### Success Criteria
- Map feeds are contract-driven with explicit spatial semantics
- Market identity and district taxonomy are canonical and queryable
- Timeline labels map to deterministic temporal windows
- Frontend controls integrate with real backend queries

---

## P19 — Implement Bounding Box, Cluster, and District Query Semantics

### Status: ✅ BACKEND COMPLETE (commit a7ab1fe)

### Goal
Replace loose frontend-only viewport behavior with canonical backend-backed bounding-box, cluster, and district query semantics.

### Summary
**Implemented spatial domain models and services to formalize geographic query contracts.** The backend can now handle bounding-box queries with validation, district-based filtering, and coordinate-to-district resolution. Contracts are formally defined and extensible for future PostGIS integration.

### What Gets Built (COMPLETED)
1. **Spatial Query Contracts** ✅
   - GeoBoundingBox exists in EventContracts (already defined in P06)
   - MapFeedRequest with bounds, window, categories, district filters
   - MapFeedResponse with events and total count
   - Extension methods: Validate(), Contains(), GetCenter()

2. **Backend Services** ✅
   - Domain model: BoundingBox record with validation and containment checks
   - Domain model: District class with hierarchical support (parent districts)
   - IViewportQueryService interface for spatial queries
   - ViewportQueryService implementation with in-memory district registry
   - Services registered in dependency injection
   - Spatial validation rules (latitude ±90, longitude ±180, min < max)

3. **Frontend Integration** (TODO - next phase)
   - RadarMap consumes canonical spatial contracts
   - Remove ad hoc filtering from client clustering
   - Add district filter UI integration seam

4. **Tests/Verification** (TODO - P24)
   - Valid bounding-box query examples
   - Invalid bounding-box rejection
   - Zoomed-in dense area behavior
   - District-filtered query results
   - Empty viewport handling

### Key Decisions to Document
- Where should clustering happen: backend pre-aggregation or frontend over results?
- How should district approximation work (polygon vs bounding box)?
- What zoom levels matter for map behavior?

### Files to Create/Modify
**Backend:**
- `backend/WeUP.Domain/Spatial/BoundingBox.cs`
- `backend/WeUP.Domain/Spatial/District.cs`
- `backend/WeUP.Domain/Spatial/IViewportQueryService.cs`
- `backend/WeUP.Contracts/Spatial/MapFeedRequest.cs`
- `backend/WeUP.Contracts/Spatial/MapFeedResponse.cs`
- `backend/WeUP.Infrastructure/Spatial/ViewportQueryService.cs`
- `backend/WeUP.Api/Endpoints/MapEndpoints.cs` (enhance)

**Frontend:**
- `lib/spatial/boundingBox.ts`
- `lib/spatial/districtQueries.ts`
- `services/spatialService.ts` (new/enhance)
- `components/RadarMap.tsx` (refactor to use contracts)

### Dependencies
- None (no P20/P21 dependencies)

### Commit Message Pattern
```
P19 — Implement Bounding Box, Cluster, and District Query Semantics

- Add spatial query domain models (BoundingBox, District)
- Create ViewportQueryService for geography-aware event queries
- Define MapFeedRequest/Response contracts with validation
- Integrate district filtering into backend query layer
- Refactor RadarMap to consume canonical spatial contracts
- Add validation tests for bounding-box semantics
```

---

## P20 — Define City Partitioning, Neighborhood Taxonomy, and Market Freeze Rules

### Status: Not Started

### Goal
Create a canonical city partitioning model, neighborhood taxonomy, and market freeze rules so the system behaves like a market-specific city system, not a loose geo demo.

### What Gets Built
1. **Market Domain Models**
   - Market entity (identity, status, boundary)
   - District entity (canonical names, aliases, parent relationships)
   - Neighborhood entity (taxonomy layer)
   - MarketBoundary and MarketStatus

2. **Market Policy Services**
   - IMarketAssignmentService: assign events to geography
   - IMarketPolicyService: enforce freeze rules
   - IMarketTaxonomyService: resolve aliases, query taxonomy
   - Rule evaluator for market eligibility

3. **Persistence Models**
   - Market configuration storage
   - Taxonomy metadata (aliases, display names, sort order)
   - Market freeze state

4. **Read Endpoints/Contracts**
   - GET /api/markets/active
   - GET /api/districts/taxonomy
   - GET /api/markets/{id}/metadata

5. **Verification Examples**
   - Event inside active launch market → assigned correctly
   - Event near market border → assignment rules
   - Unknown neighborhood with known district → fallback behavior
   - Inactive market submission → freeze enforced
   - Alias resolution → canonical form

### Key Decisions to Document
- How many taxonomy layers: just District or District+Neighborhood?
- Is market freeze a soft warning or hard block?
- What city/market is Phase 0 focused on?
- How do we handle multi-city expansion seams?

### Files to Create/Modify
**Backend:**
- `backend/WeUP.Domain/Markets/Market.cs`
- `backend/WeUP.Domain/Markets/District.cs`
- `backend/WeUP.Domain/Markets/Neighborhood.cs`
- `backend/WeUP.Domain/Markets/IMarketPolicyService.cs`
- `backend/WeUP.Domain/Markets/IMarketTaxonomyService.cs`
- `backend/WeUP.Infrastructure/Markets/MarketPolicyService.cs`
- `backend/WeUP.Infrastructure/Markets/MarketTaxonomyService.cs`
- `backend/WeUP.Api/Endpoints/MarketEndpoints.cs`
- `backend/WeUP.Infrastructure/Persistence/Configuration/MarketConfiguration.cs` (EF)

**Frontend:**
- `lib/markets/marketQueries.ts`
- `types/market.ts`

### Dependencies
- P19 (spatial queries provide the geographic foundation)

### Commit Message Pattern
```
P20 — Define City Partitioning, Neighborhood Taxonomy, and Market Freeze Rules

- Add Market, District, Neighborhood domain models
- Create IMarketPolicyService for freeze rule enforcement
- Create IMarketTaxonomyService for alias resolution
- Add market assignment logic based on geographic coordinates
- Create GET /api/markets/active and taxonomy endpoints
- Add configuration for Phase 0 launch market
- Add tests for market assignment and freeze enforcement
```

---

## P21 — Convert Timeline and Calendar UX into Real Temporal Query Logic

### Status: Not Started

### Goal
Convert the current timeline scrubber and calendar UI into real temporal query logic so time-based exploration maps cleanly to backend event queries.

### What Gets Built
1. **Temporal Query Contracts**
   - TimeWindow (startUtc, endUtc, timeZone)
   - DateWindow (date, timeZone)
   - TemporalPreset (enum: NOW, EVENING_6PM, EVENING_9PM, MIDNIGHT, EARLY_MORNING_3AM, FRI, SAT, SUN)
   - TemporalQueryMode (POINT_IN_TIME vs RANGE vs PRESET)

2. **Timeline Label Mapping**
   - Each UI label (NOW, 6PM, 9PM, MIDNIGHT, 3AM, FRI, SAT, SUN) → real TimeWindow
   - Market-local timezone handling
   - Relative vs absolute window semantics

3. **Event Temporal Rules**
   - Upcoming events (start > now)
   - Ongoing events (start <= now < end)
   - Cross-midnight events (end < start)
   - Multi-day events
   - Archival visibility

4. **Backend Query Services**
   - ITemporalQueryService: query by time window, preset, date
   - Query for "what's live now"
   - Query for evening windows
   - Query for specific calendar date
   - Query for weekend/day presets

5. **Frontend Integration**
   - TimelineControl → TemporalPreset mapping
   - Calendar selection → DateWindow query
   - Combined timeline + calendar interaction
   - Real feed requests instead of client-side filtering

6. **Tests/Verification**
   - Ongoing event spanning midnight
   - Same-day evening query
   - Weekend preset results
   - Market-local timezone edge cases
   - Empty time window handling

### Key Decisions to Document
- Are presets relative to "now" or absolute time?
- How does market-local time affect browser-local display?
- How should timeline and calendar combine/conflict?
- How do cross-midnight nightlife events appear?

### Files to Create/Modify
**Backend:**
- `backend/WeUP.Domain/Temporal/TimeWindow.cs`
- `backend/WeUP.Domain/Temporal/DateWindow.cs`
- `backend/WeUP.Domain/Temporal/TemporalPreset.cs`
- `backend/WeUP.Domain/Temporal/ITemporalQueryService.cs`
- `backend/WeUP.Contracts/Temporal/TemporalFeedRequest.cs`
- `backend/WeUP.Infrastructure/Temporal/TemporalQueryService.cs`
- `backend/WeUP.Api/Endpoints/TemporalEndpoints.cs` (enhance existing)

**Frontend:**
- `lib/temporal/presetMapping.ts`
- `lib/temporal/timeWindows.ts`
- `types/temporal.ts`
- `components/TimelineControl.tsx` (refactor)
- `components/CulturalCalendar.tsx` (refactor)
- `services/temporalService.ts` (new/enhance)

### Dependencies
- P19 (map feed queries + spatial = full picture)
- P20 (market-local timezone depends on market config)

### Commit Message Pattern
```
P21 — Convert Timeline and Calendar UX into Real Temporal Query Logic

- Add TemporalPreset, TimeWindow, DateWindow contracts
- Create ITemporalQueryService for time-based event queries
- Map UI labels (NOW, 6PM, MIDNIGHT, etc.) to real time windows
- Add rules for ongoing, cross-midnight, multi-day events
- Create TemporalFeedRequest with preset/window options
- Refactor TimelineControl and CulturalCalendar to use contracts
- Add timezone-aware temporal validation
- Add tests for edge cases (midnight spanning, timezone boundaries)
```

---

## Completion Checklist

- [ ] P19 implementation complete & tested
- [ ] P19 committed with clean history
- [ ] P20 implementation complete & tested
- [ ] P20 committed with clean history
- [ ] P21 implementation complete & tested
- [ ] P21 committed with clean history
- [ ] All tests passing (backend + frontend)
- [ ] Bundle 7 progress document updated with results
- [ ] Bundle 7 README: architecture decisions documented
- [ ] Ready for Bundle 8 (Observability)

---

## Notes

### Current Map/Time UX Issues (Pre-P19/P20/P21)
- RadarMap runs client-side clustering via useSupercluster on all loaded events
- Bounds tracking is local state, not tied to canonical spatial contracts
- Event visibility dimming is cosmetic, not tied to real queries
- TimelineControl drag → labels (NOW, 6PM, etc.) but labels don't drive queries
- Calendar selection is visual, not mapped to temporal windows
- No district/neighborhood model exists yet
- No market freeze rules
- No timezone awareness

### Risks & Mitigations
| Risk | Mitigation |
|------|-----------|
| Temporal semantics stay in UI as string labels | Tests verify label → window mapping; backend enforces semantics |
| Mix multiple cities without market model | P20 defines strict market identity; ingestion validates market assignment |
| Timezone bugs with cross-midnight events | Explicit test cases; market-local time as source of truth, not browser-local |
| Frontend remains source of truth for geography | P19 flips: backend contracts are truth; frontend consumes them |
| Clustering strategy unclear | P19 decides: frontend over canonical results (keeps UX intact, data real) |

