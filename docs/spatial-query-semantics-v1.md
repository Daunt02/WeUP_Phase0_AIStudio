# Spatial Query Semantics v1.0 — Production-Grade Documentation

**Status**: M9-P43 — Build Spatial Query Semantics and Filtering Logic v1.0  
**Version**: 1.0  
**Last Updated**: 2026-04-27

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Core Concepts](#core-concepts)
3. [Composition Modes](#composition-modes)
4. [Filter Dimensions](#filter-dimensions)
5. [Precedence Rules](#precedence-rules)
6. [Validation Rules](#validation-rules)
7. [Frontend Integration](#frontend-integration)
8. [Backend Integration](#backend-integration)
9. [Examples](#examples)
10. [Error Handling](#error-handling)

---

## Architecture Overview

Spatial query semantics defines **how spatial filters interact** across map feed, calendar feed, and saved discovery surfaces. The system ensures:

- **Consistency**: One canonical spatial contract across all event discovery surfaces.
- **Explicitness**: No hidden fallbacks, no implicit text matching.
- **Validation**: Invalid or ambiguous combinations are rejected with actionable errors.
- **Determinism**: Same query always produces same composition mode and filters.

### Components

| Component                     | Language   | Location                          | Purpose                                        |
| ----------------------------- | ---------- | --------------------------------- | ---------------------------------------------- |
| `SpatialQueryDto`             | C#         | `WeUP.Contracts.Spatial`          | Canonical spatial query contract               |
| `SpatialCompositionMode` enum | C#         | `WeUP.Contracts.Spatial`          | Defines filter interaction semantics           |
| `SpatialQueryValidator`       | C#         | `WeUP.Application.Spatial`        | Validates and normalizes queries               |
| `EventMapFeedQueryV2Dto`      | C#         | `WeUP.Contracts.Events`           | Enhanced query contract with spatial semantics |
| `SpatialQueryDto` interface   | TypeScript | `domains/spatial/contracts.ts`    | Frontend mirror of backend contract            |
| `useSpatialFilter` composable | Vue 3      | `composables/useSpatialFilter.ts` | Manages frontend filter state                  |
| Example Controllers           | C#         | `WeUP.Api.Controllers`            | Shows binding and validation patterns          |

---

## Core Concepts

### Spatial Dimensions

A spatial query can include one or more spatial dimensions:

1. **Market (marketIds)**: Highest-level administrative region. Each market contains districts.
   - Example: `["houston", "dallas"]`
   - Semantics: Events from ANY of these markets (OR logic).
   - Precedence: Market is most restrictive (narrowest scope).

2. **District (districtIds)**: Mid-level administrative region under a market. Each district contains neighborhoods.
   - Example: `["downtown", "midtown"]`
   - Semantics: Events from ANY of these districts (OR logic).
   - Precedence: District is more restrictive than neighborhood, less restrictive than market.

3. **Neighborhood (neighborhoodIds)**: Finest-grained administrative region under a district.
   - Example: `["heights", "washington"]`
   - Semantics: Events from ANY of these neighborhoods (OR logic).
   - Precedence: Neighborhood is least restrictive (widest scope within taxonomy).

4. **Bounding Box (bbox)**: Geometric envelope in WGS84 degrees.
   - Format: `"minLng,minLat,maxLng,maxLat"` (e.g., `"-95.7129,29.7589,-95.0681,30.2271"`)
   - Semantics: Events within this geographic envelope.
   - Precedence: In BboxWithTaxonomy mode, bbox is applied FIRST (outer filter), then taxonomy.

### Slug-Based Identifiers

Each dimension can use either **IDs** or **slugs**:

- **IDs** (primary): Canonical, immutable, database-backed. Example: `market-uuid-123`
- **Slugs** (secondary): URL-friendly, human-readable. Example: `houston`, `downtown`

**Rule**: In a single query, you may use EITHER IDs OR slugs for each dimension, NOT BOTH.

Example (valid):

```
marketIds=market-1,market-2
districtSlugs=downtown,midtown
```

Example (invalid):

```
marketIds=market-1 AND marketSlugs=houston   ❌ Both IDs and slugs
```

---

## Composition Modes

The **composition mode** determines how spatial filters interact. It is **resolved by the backend** based on which dimensions are present.

### Mode: BboxOnly (0)

**Triggered when**: Bbox is present AND no taxonomy filters.  
**Semantics**: Bbox overrides all taxonomy. Events are filtered ONLY by geometry.  
**Precedence**: Bbox takes absolute precedence.  
**Use cases**: Freeform map exploration, raw geo-spatial discovery.

**Validation**:

- Bbox must be non-empty and valid WGS84.
- All taxonomy dimensions (marketIds, districtIds, etc.) must be empty.
- Backend returns error if taxonomy is also specified.

**Example**:

```
GET /api/events/map-feed/v2?bbox=-95.7129,29.7589,-95.0681,30.2271&preset=now&timezone=America/Chicago

Response composition mode: BboxOnly
Events matching: All events within the bounding box, regardless of market/district.
```

### Mode: TaxonomyOnly (1)

**Triggered when**: Taxonomy filters are present AND Bbox is empty.  
**Semantics**: Events are filtered by market/district/neighborhood administrative boundaries.  
**Precedence hierarchy** (market is MOST restrictive):

1. **Market most restrictive**: If market is specified, ONLY events from that market.
2. **District less restrictive**: If district is specified (without market), ONLY events from that district.
3. **Neighborhood least restrictive**: If neighborhood is specified (without district), ONLY events from that neighborhood.

**Use cases**: Curated feeds, administrative drill-down, market/district-specific discovery.

**Validation**:

- At least one taxonomy dimension must be non-empty.
- Bbox must be empty or not specified.
- Backend can infer missing dimensions. Example: If only neighborhoodIds are provided, backend infers parentDistrictIds and parentMarketIds.

**Example 1: Market-only**:

```
GET /api/events/map-feed/v2?marketIds=houston&preset=now&timezone=America/Chicago

Response composition mode: TaxonomyOnly
Events matching: All events in Houston market, any district, any neighborhood.
```

**Example 2: District-only (market inferred)**:

```
GET /api/events/map-feed/v2?districtIds=downtown,midtown&preset=now&timezone=America/Chicago

Response composition mode: TaxonomyOnly
Events matching: All events in downtown OR midtown districts.
Backend infers: marketIds=[parent market of downtown, parent market of midtown].
```

**Example 3: Neighborhood-only (district and market inferred)**:

```
GET /api/events/map-feed/v2?neighborhoodIds=heights,washington&preset=now&timezone=America/Chicago

Response composition mode: TaxonomyOnly
Events matching: All events in heights OR washington neighborhoods.
Backend infers: districtIds=[parent districts], marketIds=[parent markets].
```

### Mode: BboxWithTaxonomy (2)

**Triggered when**: BOTH Bbox AND taxonomy filters are present.  
**Semantics**: Bbox applies FIRST (outer geometric filter), then taxonomy filters within that box.  
**Precedence**:

1. **First**: Bbox defines the spatial envelope (outer filter).
2. **Then**: Taxonomy filters are applied within that envelope.

**Use cases**: Refined searches. Example: "Events in downtown district within the current map view."

**Validation**:

- Bbox must be non-empty and valid.
- At least one taxonomy dimension must be non-empty.

**Example**:

```
GET /api/events/map-feed/v2
  ?bbox=-95.7129,29.7589,-95.0681,30.2271
  &districtIds=downtown
  &preset=now
  &timezone=America/Chicago

Response composition mode: BboxWithTaxonomy
Events matching:
  1. Within bbox geometry (-95.7129,29.7589,-95.0681,30.2271)
  2. AND in downtown district

If events exist in downtown but outside the bbox, they are EXCLUDED.
```

### Mode: ProximityReady (99)

**Status**: Reserved for v2.0+. Not supported in v1.0.

---

## Filter Dimensions

### marketIds / marketSlugs

```typescript
interface SpatialQueryDto {
  marketIds?: string[]; // Canonical market IDs
  marketSlugs?: string[]; // URL-friendly slugs (e.g., "houston", "dallas")
}
```

**Semantics**:

- **OR logic**: Events from ANY market in the array.
- **Multiple allowed**: `["houston", "dallas"]` → Events from Houston OR Dallas.
- **Mutual exclusivity**: Cannot specify both `marketIds` and `marketSlugs` in the same query.

**Example**:

```
marketIds=market-1,market-2
→ Events in market-1 OR market-2

marketSlugs=houston,dallas
→ Backend resolves to marketIds, then: Events in Houston OR Dallas
```

### districtIds / districtSlugs

```typescript
interface SpatialQueryDto {
  districtIds?: string[]; // Canonical district IDs
  districtSlugs?: string[]; // URL-friendly slugs (e.g., "downtown", "midtown")
}
```

**Semantics**:

- **OR logic**: Events from ANY district in the array.
- **includeDescendants flag**:
  - When `true` (default): District filter includes all child neighborhoods.
  - When `false`: District filter includes only direct members (neighborhoods that list this district as parent).

**Example**:

```
districtIds=downtown&includeDescendants=true
→ Events in downtown district AND all neighborhoods under downtown

districtIds=downtown&includeDescendants=false
→ Events with district=downtown, excluding nested neighborhoods
```

### neighborhoodIds / neighborhoodSlugs

```typescript
interface SpatialQueryDto {
  neighborhoodIds?: string[]; // Canonical neighborhood IDs
  neighborhoodSlugs?: string[]; // URL-friendly slugs
}
```

**Semantics**:

- **OR logic**: Events from ANY neighborhood in the array.
- **Parent inference**: If only `neighborhoodIds` are provided without `districtIds`, backend infers the parent districts and markets.

**Example**:

```
neighborhoodIds=heights,washington
→ Events in heights OR washington
→ Backend infers: districtIds=[parent districts]
```

### bbox

```typescript
interface SpatialQueryDto {
  bbox?: string; // WGS84 format: "minLng,minLat,maxLng,maxLat"
}
```

**Format**:

```
"minLng,minLat,maxLng,maxLat"
 -95.7129, 29.7589, -95.0681, 30.2271
```

**Validation**:

- Longitude range: [-180, 180]
- Latitude range: [-90, 90]
- minLng < maxLng
- minLat < maxLat

**Example**:

```
bbox=-95.7129,29.7589,-95.0681,30.2271
→ Events within this geographic envelope
```

---

## Precedence Rules

### Composition Mode Precedence

| Scenario           | Precedence                                  | Outcome               |
| ------------------ | ------------------------------------------- | --------------------- |
| Bbox only          | Bbox                                        | BboxOnly mode         |
| Taxonomy only      | marketId > districtId > neighborhoodId      | TaxonomyOnly mode     |
| Bbox + Taxonomy    | Bbox first (outer), taxonomy second (inner) | BboxWithTaxonomy mode |
| Neither            | ❌ ERROR                                    | Validation fails      |
| Both IDs and slugs | ❌ ERROR                                    | Validation fails      |

### Within TaxonomyOnly: Precedence Hierarchy

When multiple taxonomy dimensions are present:

```
1. marketIds (MOST restrictive)
   ↓
2. districtIds (LESS restrictive)
   ↓
3. neighborhoodIds (LEAST restrictive)
```

**Example**:

```
marketIds=houston AND districtIds=downtown
→ Only events in Houston's downtown (intersection)

districtIds=downtown AND neighborhoodIds=heights
→ Only events in downtown's heights neighborhood (intersection)
```

---

## Validation Rules

### Frontend Validation (useSpatialFilter composable)

Frontend performs **basic** validation to catch obvious errors early:

1. Mutual exclusivity: Cannot specify both IDs and slugs for the same dimension.
2. MinConfidence bounds: Must be in [0.0, 1.0].
3. At least one filter: Error only if attempting to query with no filters.

**Frontend validation is NOT authoritative**. Backend performs comprehensive validation.

### Backend Validation (SpatialQueryValidator service)

Backend performs **authoritative** validation:

1. **Mutual exclusivity**: IDs vs slugs for each dimension.
2. **MinConfidence bounds**: [0.0, 1.0].
3. **At least one spatial dimension**: Bbox or taxonomy required.
4. **Slug resolution**: All slugs resolved to IDs. Error if slug not found.
5. **Taxonomy hierarchy**:
   - Districts must belong to specified markets (or inferred markets).
   - Neighborhoods must belong to specified districts (or inferred districts).
6. **Composition mode determination**: Bbox only, taxonomy only, or both.
7. **Normalization**: Infer missing dimensions when appropriate.

### Example: Invalid Combinations

```csharp
// ❌ INVALID: Both IDs and slugs
new SpatialQueryDto
{
    MarketIds = new[] { "market-1" },
    MarketSlugs = new[] { "houston" }
}
// Error: Cannot specify both MarketIds and MarketSlugs.

// ❌ INVALID: No spatial dimension
new SpatialQueryDto
{
    MarketIds = null,
    DistrictIds = null,
    NeighborhoodIds = null,
    Bbox = null
}
// Error: At least one spatial dimension must be specified.

// ✅ VALID: BboxOnly
new SpatialQueryDto { Bbox = "-95.7129,29.7589,-95.0681,30.2271" }

// ✅ VALID: TaxonomyOnly (neighborhood infers districts and markets)
new SpatialQueryDto { NeighborhoodIds = new[] { "heights" } }

// ✅ VALID: BboxWithTaxonomy
new SpatialQueryDto
{
    Bbox = "-95.7129,29.7589,-95.0681,30.2271",
    DistrictIds = new[] { "downtown" }
}
```

---

## Frontend Integration

### Using useSpatialFilter Composable

```typescript
import { useSpatialFilter } from "@/composables/useSpatialFilter";

export default {
  setup() {
    const spatialFilter = useSpatialFilter({
      initialQuery: { marketIds: ["houston"] },
      onModeChange: (mode) => console.log("Mode:", mode),
      onValidationError: (errors) => console.log("Errors:", errors),
      minConfidence: 0.7,
    });

    // Set filters
    spatialFilter.setMarketIds(["houston", "dallas"]);
    spatialFilter.setDistrictIds(["downtown"]);
    spatialFilter.setBbox("-95.7129,29.7589,-95.0681,30.2271");

    // Check composition mode
    if (spatialFilter.isBboxWithTaxonomy.value) {
      console.log("Refined search mode (bbox + taxonomy)");
    }

    // Get query for network request
    const queryString = spatialFilter.buildQueryString();
    // "marketIds=houston%2Cdallas&districtIds=downtown&bbox=-95.7129%2C29.7589%2C-95.0681%2C30.2271"

    return { spatialFilter };
  },
};
```

### Template Usage

```vue
<template>
  <div class="spatial-filter">
    <!-- Market selector -->
    <select v-model="selectedMarkets" multiple>
      <option value="houston">Houston</option>
      <option value="dallas">Dallas</option>
    </select>

    <!-- District selector -->
    <select v-model="selectedDistricts" multiple>
      <option value="downtown">Downtown</option>
      <option value="midtown">Midtown</option>
    </select>

    <!-- Validation feedback -->
    <div v-if="spatialFilter.validationErrors.length > 0" class="error">
      {{ spatialFilter.validationErrors.join("; ") }}
    </div>

    <!-- Mode display -->
    <div class="mode-badge">
      {{ spatialFilter.compositionMode }}
    </div>

    <!-- Clear buttons -->
    <button @click="spatialFilter.clearTaxonomyFilters">Clear Taxonomy</button>
    <button @click="spatialFilter.clearBboxFilter">Clear Bbox</button>
    <button @click="spatialFilter.reset">Reset All</button>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useSpatialFilter } from "@/composables/useSpatialFilter";

const spatialFilter = useSpatialFilter();

const selectedMarkets = computed({
  get: () => Array.from(spatialFilter.marketIds.value),
  set: (val) => spatialFilter.setMarketIds(val),
});

const selectedDistricts = computed({
  get: () => Array.from(spatialFilter.districtIds.value),
  set: (val) => spatialFilter.setDistrictIds(val),
});
</script>
```

---

## Backend Integration

### Binding Query Parameters

```csharp
// Example controller endpoint
[HttpGet("map-feed")]
public async Task<ActionResult> GetMapFeed(
    [FromQuery] string[]? marketIds,
    [FromQuery] string[]? districtIds,
    [FromQuery] string? bbox,
    [FromQuery] string? timezone)
{
    // 1. Bind parameters into DTO
    var query = new EventMapFeedQueryV2Dto
    {
        MarketIds = marketIds,
        DistrictIds = districtIds,
        Bbox = bbox,
        Timezone = timezone ?? string.Empty,
        Preset = TimeWindowPreset.Now,
    };

    // 2. Extract spatial query
    var spatialQuery = query.ToSpatialQuery();

    // 3. Validate
    var validation = await _spatialValidator.ValidateAsync(spatialQuery);
    if (!validation.IsValid())
    {
        return UnprocessableEntity(new ProblemDetails
        {
            Detail = string.Join("; ", validation.Errors)
        });
    }

    // 4. Use normalized query for execution
    var normalizedSpatial = validation.NormalizedQuery!;
    var events = await _eventService.QueryBySpacialAsync(
        normalizedSpatial,
        timeWindow,
        userId);

    return Ok(events);
}
```

### Query Execution

Once a query is validated and normalized:

```csharp
public async Task<EventMapFeedV1ResponseDto> QueryBySpatialAsync(
    SpatialQueryDto spatial,
    TimeWindowRequest window,
    string? userId)
{
    IQueryable<Event> query = _dbContext.Events;

    // Apply spatial filters based on composition mode
    switch (spatial.ResolvedMode)
    {
        case SpatialCompositionMode.BboxOnly:
            query = query.Where(e =>
                e.Latitude >= spatial.Bbox.MinLat &&
                e.Latitude <= spatial.Bbox.MaxLat &&
                e.Longitude >= spatial.Bbox.MinLng &&
                e.Longitude <= spatial.Bbox.MaxLng);
            break;

        case SpatialCompositionMode.TaxonomyOnly:
            if (spatial.MarketIds?.Length > 0)
            {
                query = query.Where(e => spatial.MarketIds.Contains(e.MarketId));
            }
            else if (spatial.DistrictIds?.Length > 0)
            {
                query = query.Where(e => spatial.DistrictIds.Contains(e.DistrictId));
            }
            else if (spatial.NeighborhoodIds?.Length > 0)
            {
                query = query.Where(e => spatial.NeighborhoodIds.Contains(e.NeighborhoodId));
            }
            break;

        case SpatialCompositionMode.BboxWithTaxonomy:
            // Apply bbox first
            query = query.Where(e =>
                e.Latitude >= spatial.Bbox.MinLat &&
                e.Latitude <= spatial.Bbox.MaxLat &&
                e.Longitude >= spatial.Bbox.MinLng &&
                e.Longitude <= spatial.Bbox.MaxLng);

            // Then apply taxonomy
            if (spatial.MarketIds?.Length > 0)
            {
                query = query.Where(e => spatial.MarketIds.Contains(e.MarketId));
            }
            else if (spatial.DistrictIds?.Length > 0)
            {
                query = query.Where(e => spatial.DistrictIds.Contains(e.DistrictId));
            }
            break;
    }

    // Apply confidence threshold
    if (spatial.MinConfidence > 0.0)
    {
        query = query.Where(e => e.ResolutionConfidence >= spatial.MinConfidence);
    }

    // Apply temporal window
    query = query.Where(e =>
        e.StartUtc >= window.StartUtc &&
        e.StartUtc <= window.EndUtc);

    // Execute and project
    var events = await query
        .ProjectToMapItemDto()
        .ToListAsync();

    return new EventMapFeedV1ResponseDto(
        Events: events.ToArray(),
        TotalCount: events.Count);
}
```

---

## Examples

### Example 1: Freeform Map Exploration

**User Action**: Zooms into a specific area on the map.

**Query**:

```
GET /api/events/map-feed/v2
  ?bbox=-95.7129,29.7589,-95.0681,30.2271
  &preset=now
  &timezone=America/Chicago
```

**Binding**:

```typescript
const spatialFilter = useSpatialFilter();
spatialFilter.setBbox("-95.7129,29.7589,-95.0681,30.2271");
```

**Backend Result**:

- Composition mode: **BboxOnly**
- Events: All events within the geographic envelope.

---

### Example 2: Curated District Feed

**User Action**: Selects "Downtown" from district filter.

**Query**:

```
GET /api/events/map-feed/v2
  ?districtIds=downtown
  &preset=now
  &timezone=America/Chicago
```

**Binding**:

```typescript
const spatialFilter = useSpatialFilter();
spatialFilter.setDistrictIds(["downtown"]);
```

**Backend Result**:

- Composition mode: **TaxonomyOnly**
- Events: All events in downtown district (all neighborhoods).
- Inferred: marketIds = [parent market of downtown]

---

### Example 3: Refined Search (Bbox + Taxonomy)

**User Action**: Applied district filter, then zoomed map to specific area.

**Query**:

```
GET /api/events/map-feed/v2
  ?bbox=-95.7129,29.7589,-95.0681,30.2271
  &districtIds=downtown
  &preset=now
  &timezone=America/Chicago
```

**Binding**:

```typescript
const spatialFilter = useSpatialFilter();
spatialFilter.setDistrictIds(["downtown"]);
spatialFilter.setBbox("-95.7129,29.7589,-95.0681,30.2271");
```

**Backend Result**:

- Composition mode: **BboxWithTaxonomy**
- Events: All events in downtown district AND within the bbox.
- If downtown has events outside the bbox, they are excluded.

---

### Example 4: Slug-Based Market Drill-Down

**User Action**: Selects market via URL-friendly slug.

**Query**:

```
GET /api/events/map-feed/v2
  ?marketSlugs=houston,dallas
  &preset=now
  &timezone=America/Chicago
```

**Binding**:

```typescript
const spatialFilter = useSpatialFilter();
spatialFilter.setMarketSlugs(["houston", "dallas"]);
```

**Backend Result**:

- Composition mode: **TaxonomyOnly**
- Slug resolution: `houston` → market-id-123, `dallas` → market-id-456
- Events: All events in Houston or Dallas markets.

---

### Example 5: Neighborhood Drill-Down (Parents Inferred)

**User Action**: Filters by specific neighborhoods without specifying parent district/market.

**Query**:

```
GET /api/events/map-feed/v2
  ?neighborhoodIds=heights,washington
  &preset=now
  &timezone=America/Chicago
```

**Binding**:

```typescript
const spatialFilter = useSpatialFilter();
spatialFilter.setNeighborhoodIds(["heights", "washington"]);
```

**Backend Result**:

- Composition mode: **TaxonomyOnly**
- Parent inference:
  - heights → parent district = midtown → parent market = houston
  - washington → parent district = downtown → parent market = houston
- Events: All events in heights or washington neighborhoods.

---

## Error Handling

### Frontend Validation Errors

The `useSpatialFilter` composable tracks validation errors:

```typescript
const spatialFilter = useSpatialFilter({
  onValidationError: (errors) => {
    console.error("Validation errors:", errors);
    // Show toast or error message
  },
});

// Check errors
if (spatialFilter.validationErrors.value.length > 0) {
  // Display errors to user
  console.log(spatialFilter.validationErrors.value);
}
```

**Common Frontend Errors**:

- "Cannot specify both marketIds and marketSlugs."
- "Cannot specify both districtIds and districtSlugs."
- "MinConfidence must be in [0.0, 1.0], got X."

### Backend Validation Errors

Backend returns `422 Unprocessable Entity` with detailed errors:

```json
{
  "status": 422,
  "title": "Invalid spatial query.",
  "detail": "District 'downtown' not found in market 'houston'; Market slug resolution failed: 'invalid-market' not found.",
  "type": "https://example.com/docs/spatial-query-errors"
}
```

**Common Backend Errors**:

- "Cannot specify both MarketIds and MarketSlugs in the same query."
- "At least one spatial dimension must be specified: Bbox, MarketIds, DistrictIds, or NeighborhoodIds."
- "Market slug not found: 'invalid-market'."
- "District 'downtown' not found in market 'houston'."
- "MinConfidence must be in [0.0, 1.0], got X."

### Error Recovery

**Strategy 1: Retry with corrected query**

```typescript
try {
  const result = await fetchEventMapFeed(spatialFilter.queryV2.value);
} catch (error) {
  if (error.status === 422) {
    // User corrects filters and retries
    console.log("Validation error:", error.detail);
  }
}
```

**Strategy 2: Clear problematic dimension**

```typescript
if (spatialFilter.validationErrors.value.length > 0) {
  // If error involves market slugs, fall back to market IDs
  spatialFilter.clearAllFilters();
  // Retry with user guidance
}
```

**Strategy 3: Suggest corrections**

```typescript
const errors = spatialFilter.validationErrors.value;
if (errors.some((e) => e.includes("District"))) {
  // Suggest alternative districts
  showDistrictSuggestions();
}
```

---

## Design Rationale

### Why Composition Modes?

Composition modes eliminate ambiguity. Without explicit modes, a query like:

```
bbox=... AND districtIds=... AND marketIds=...
```

Could be interpreted multiple ways:

1. Bbox overrides taxonomy (bbox-only semantics).
2. Bbox AND taxonomy apply together (intersection).
3. Taxonomy overrides bbox.

**Our solution**: Backend explicitly resolves the mode based on which dimensions are present, then documents the semantics clearly.

### Why No Hidden Fallbacks?

A query like `districtIds=downtown` should NOT silently fall back to:

- Text matching on display names
- Fuzzy matching on slugs
- Regional inference without explicit confirmation

**Our solution**: All filters are explicit canonical references. Fallbacks are documented and require explicit backend inference (e.g., inferring markets from district hierarchy).

### Why Frontend AND Backend Validation?

Frontend validation catches errors early and provides fast feedback. Backend validation is authoritative and comprehensive.

**Our solution**: Frontend provides basic validation; backend performs authoritative validation including slug resolution and taxonomy hierarchy checks.

---

## Migration from v0.x

### Old Contract (Legacy EventMapFeedQueryDto)

```csharp
public record EventMapFeedQueryDto
{
    public string Bbox { get; init; }
    public string District { get; init; }  // String-based, ambiguous
    public TimeWindowPreset Preset { get; init; }
}
```

### New Contract (EventMapFeedQueryV2Dto)

```csharp
public record EventMapFeedQueryV2Dto
{
    public string[]? MarketIds { get; init; }      // Canonical IDs
    public string[]? DistrictIds { get; init; }    // Canonical IDs
    public string[]? NeighborhoodIds { get; init; } // NEW
    public string[]? MarketSlugs { get; init; }    // Slug-based
    public string[]? DistrictSlugs { get; init; }  // Slug-based
    public string[]? NeighborhoodSlugs { get; init; } // NEW
    public string? Bbox { get; init; }
    public bool IncludeDescendants { get; init; }  // NEW
    public SpatialCompositionMode? ResolvedMode { get; init; } // NEW
}
```

### Migration Path

1. **Accept both contracts**: Keep EventMapFeedQueryDto for backward compatibility.
2. **Normalize**: Convert legacy `District` string to new `DistrictIds` array.
3. **Deprecate**: Remove legacy contract in v2.0.

---

## Testing Strategy

### Unit Tests (Backend)

```csharp
[TestClass]
public class SpatialQueryValidationTests
{
    [TestMethod]
    public async Task Validates_BboxOnly()
    {
        var query = new SpatialQueryDto { Bbox = "..." };
        var result = await _validator.ValidateAsync(query);

        Assert.IsTrue(result.IsValid());
        Assert.AreEqual(SpatialCompositionMode.BboxOnly, result.Mode);
    }

    [TestMethod]
    public async Task Rejects_Both_Ids_And_Slugs()
    {
        var query = new SpatialQueryDto
        {
            MarketIds = new[] { "id-1" },
            MarketSlugs = new[] { "slug-1" }
        };
        var result = await _validator.ValidateAsync(query);

        Assert.IsFalse(result.IsValid());
        Assert.IsTrue(result.Errors.Any(e => e.Contains("MarketIds") && e.Contains("MarketSlugs")));
    }
}
```

### Integration Tests (Frontend)

```typescript
describe("useSpatialFilter", () => {
  it("validates BboxOnly composition", () => {
    const filter = useSpatialFilter();
    filter.setBbox("-95.7129,29.7589,-95.0681,30.2271");

    expect(filter.isValid.value).toBe(true);
    expect(filter.isBboxOnly.value).toBe(true);
  });

  it("infers markets from districtIds", async () => {
    const filter = useSpatialFilter();
    filter.setDistrictIds(["downtown"]);

    // Backend would infer markets
    // Frontend validation is permissive here
    expect(filter.isTaxonomyOnly.value).toBe(true);
  });
});
```

---

## FAQ

**Q: Can I specify both bbox and district in the same query?**  
A: Yes. This is BboxWithTaxonomy mode. Events must match BOTH filters (intersection).

**Q: What if I specify marketIds but the bbox is in a different market?**  
A: BboxWithTaxonomy applies both. Results would be empty if no events in the market are within the bbox.

**Q: Can I use market slugs and district IDs in the same query?**  
A: Yes. Backend resolves slugs to IDs. After resolution, all are IDs.

**Q: What is the difference between includeDescendants=true and false?**  
A: With true, a district filter includes all neighborhoods under that district. With false, only direct members.

**Q: How do I query events saved by current user?**  
A: Use `includeSavedOnly=true` (in EventMapFeedQueryV2Dto). This is a separate filter, not spatial.

**Q: What if a slug doesn't exist?**  
A: Backend returns 422 with error "Slug not found: 'invalid-slug'."

---

## Appendix: Technical Debt & Future Work

### v2.0 Enhancements

1. **ProximityReady mode**: Support location-based proximity queries (e.g., "events within 5 km").
2. **Composite filters**: Support (A AND B) OR C composition patterns.
3. **Temporal-spatial correlation**: Query "events next week in downtown OR midtown this weekend".

### Known Limitations

1. **Single market per event**: Current architecture supports only one market per event. Cross-market events require architecture change.
2. **No exclusion filters**: Cannot specify "NOT downtown". Only inclusion supported.
3. **No fuzzy slug matching**: Slug resolution is exact match only.

---

**End of Document**
