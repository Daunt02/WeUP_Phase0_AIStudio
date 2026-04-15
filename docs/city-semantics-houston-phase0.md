# Houston Phase 0 Canonical City Semantics

## Scope

This document is the single source of truth for Houston Phase 0 spatial, locality, and temporal discovery semantics.

It defines how market, district, neighborhood, viewport, and temporal windows are represented across:

- UI/runtime state
- TypeScript query contracts
- Backend API contracts
- Domain services
- Persistence entities

## Canonical Locality Model

### Market

- `marketCode`: top-level city market identifier.
- Houston Phase 0 canonical market code: `houston`.
- Default temporal timezone for Houston: `America/Chicago`.

### District

- `districtCode`: canonical district taxonomy key under a market.
- District codes are semantic identifiers, not display labels.
- District filtering must resolve against canonical district fields, not city-name fields.

### Neighborhood

- `neighborhoodCode`: optional finer-grained locality dimension under district.
- Neighborhood is additive and may be absent for events that only resolve to district.

### Contract Representation

Locality is represented by `LocalityFilterRequest`:

- `marketCode?: string`
- `districtCode?: string`
- `neighborhoodCode?: string`

`MapFeedRequest` and `CalendarFeedRequest` include `locality` as first-class filter input.
Legacy `districtCode` remains as compatibility seam and is normalized into `locality.districtCode` server-side.

## Spatial Discovery Semantics

### Bounding Box Validation

Canonical viewport guardrails:

- Latitude bounds in `[-90, 90]`
- Longitude bounds in `[-180, 180]`
- `minLat < maxLat`
- `minLng < maxLng`
- `latSpan <= 5`
- `lngSpan <= 5`
- `latSpan * lngSpan <= 8`

These constraints are enforced in backend `GeoBoundingBoxExtensions.Validate` and mirrored in frontend `validateBoundingBox`.

### Map Response Shape

`MapFeedResponse` includes:

- `events`
- `totalCount`
- `clusters?`
- `queryMode`

`clusters` is a Phase 0 seam for scalable aggregation and must remain optional.
`queryMode` identifies execution mode and currently defaults to `bounding_box`.

### PostGIS Upgrade Seam

Current cluster computation is quantized in repository logic for Phase 0.
Future implementation should replace this with PostGIS-native clustering (`ST_Cluster*`) and polygon-aware district geometry.

## Temporal Discovery Semantics

### Canonical Presets

Canonical temporal presets are:

- `Today`
- `Tonight`
- `Weekend`
- `Next7Days`

Custom windows are represented via explicit `TimeWindow` / `AbsoluteWindow` modes.

### Window Definitions

All windows are computed in market-local timezone and returned as UTC boundaries with the market timezone id.

- `Today`: local 00:00 to next local 00:00
- `Tonight`: local 18:00 to next local 03:00
- `Weekend`: Friday local 18:00 to Monday local 00:00
- `Next7Days`: reference instant to reference + 7 days

### Timezone Behavior

- Houston default timezone: `America/Chicago`.
- IANA IDs are accepted; mapped to platform-specific zone ids where needed.
- Unknown timezone ids fall back to UTC with returned timezone value `UTC`.

## Representation by Layer

### UI and Runtime

- `TemporalPresetSelection` uses `Today | Tonight | Weekend | Next7Days`.
- UI temporal mode supports `TODAY | TONIGHT | WEEKEND | NEXT_7_DAYS | CUSTOM_DATE`.
- Timeline control emits canonical preset names only.

### Frontend Contracts

- `EventFilters.locality` is the canonical locality filter.
- `MapFeedResponse` supports cluster-ready shape and query mode.
- Temporal preset resolver mirrors backend windows and timezone rules.

### Backend Contracts

- `LocalityFilterRequest` is canonical locality DTO.
- `MapFeedRequest` and `CalendarFeedRequest` include `Locality`.
- `MapFeedResponse` includes `Clusters` and `QueryMode`.
- Temporal contract enums use canonical presets.

### Backend Domain Services

- `TemporalPresetMapper` computes canonical windows.
- `ViewportQueryService` resolves district metadata from seed market/district data.

### Persistence

`EventEntity` canonical locality columns:

- `MarketCode`
- `DistrictCode`
- `NeighborhoodCode`

District filtering must query `DistrictCode` directly. Placeholder logic that mapped district semantics to `AddressCity` is not canonical.

## Drift Detection Rules

Changes that require coordinated updates and tests:

- Any new/removed field in locality or map-feed contracts.
- Any change to bounding-box guardrails.
- Any change to temporal preset names or window definitions.
- Any change to default Houston timezone behavior.

Parity test layers:

- Frontend manifest parity tests
- Backend manifest parity tests
- TS unit tests for temporal + spatial semantics
- C# unit tests for temporal + spatial semantics

## Non-Goals for Phase 0

- Polygon-level district boundaries in query execution.
- Full market catalog beyond seeded Houston scope.
- Real-time adaptive clustering or tile-level spatial indexes.

These are Phase 0.5+ extensions and should preserve the contracts defined here.
