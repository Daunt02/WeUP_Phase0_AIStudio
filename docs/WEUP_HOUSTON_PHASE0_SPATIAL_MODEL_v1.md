# WEUP Houston Phase 0 Spatial Model v1.0

**Status**: Canonical and authoritative  
**Version**: 1.0  
**Last Updated**: 2026-04-27

## Purpose

This document is the single authority for Houston Phase 0 spatial semantics across ingestion, canonical event storage, backend APIs, backend query validation, and frontend discovery UI.

It defines the implementation rules for:

- Houston Phase 0 market boundaries
- district and neighborhood taxonomy
- event-to-spatial resolution
- filter and query behavior
- coordinate integrity and fallback behavior
- moderation consequences of uncertain spatial assignments

This document is implementation-oriented. It is intended to drive code, contract validation, and tests. If another document conflicts with this one, this document wins.

## Scope

This specification covers Houston only for Phase 0.

It does not define:

- multi-city architecture beyond the required taxonomy shape
- polygon execution beyond current Phase 0 boundary references
- speculative future ranking or proximity systems

## Normative Alignment

This document consolidates and supersedes overlapping Houston spatial guidance while remaining aligned with the current implemented surfaces:

For delivery tracking, this document is the canonical written consolidation of the M9-P41 through M9-P44 spatial outputs: Houston taxonomy definition, cross-layer spatial contracts, authoritative query semantics, and coordinate integrity enforcement.

- canonical taxonomy seed in `seed/houston-spatial-taxonomy.v1.json`
- spatial taxonomy contracts in `backend/WeUP.Contracts/Spatial/SpatialTaxonomyContracts.cs`
- spatial query semantics in `backend/WeUP.Contracts/Spatial/SpatialQueryDto.cs`, `backend/WeUP.Application/Spatial/SpatialQueryValidator.cs`, and `domains/spatial/contracts.ts`
- coordinate integrity semantics in `domains/query/contracts.ts`
- canonical event storage linkage in `backend/WeUP.Domain/Events/EventAggregate.cs`
- moderation publish gating in `docs/publish-eligibility-gate.md`

## Canonical Entities

### Entity Set

Houston Phase 0 uses a strict spatial hierarchy with exactly three canonical entity types:

| Entity       | Purpose                                                                           | Cardinality           | Canonical identity fields | Display-only fields |
| ------------ | --------------------------------------------------------------------------------- | --------------------- | ------------------------- | ------------------- |
| Market       | Top-level operational partition for policy, timezone, and discovery scope         | One per event         | `marketId`, `slug`        | `displayName`       |
| District     | Queryable market subdivision used for discovery, filtering, and inventory scoping | Zero or one per event | `districtId`, `slug`      | `displayName`       |
| Neighborhood | Optional finer-grained locality refinement under a district                       | Zero or one per event | `neighborhoodId`, `slug`  | `displayName`       |

### Canonical Houston Market Definition

Houston Phase 0 has one active market:

| Field                   | Value                       |
| ----------------------- | --------------------------- |
| `marketId`              | `mkt-us-tx-hou`             |
| `slug`                  | `houston`                   |
| `displayName`           | `Houston`                   |
| `timezone`              | `America/Chicago`           |
| `taxonomyVersion`       | `spatial-taxonomy-v1.0.0`   |
| `boundaryRef.provider`  | `internal-boundary-catalog` |
| `boundaryRef.datasetId` | `houston-phase0-boundaries` |
| `boundaryRef.featureId` | `market-houston`            |
| `aliasSlugs`            | `hou`, `houston-tx`         |

### District Taxonomy

Houston Phase 0 district taxonomy is canonical, queryable, and finite for this version.

| `districtId`             | `marketId`      | `slug`     | `displayName` | `sortOrder` |
| ------------------------ | --------------- | ---------- | ------------- | ----------- |
| `dst-us-tx-hou-downtown` | `mkt-us-tx-hou` | `downtown` | `Downtown`    | `10`        |
| `dst-us-tx-hou-midtown`  | `mkt-us-tx-hou` | `midtown`  | `Midtown`     | `20`        |
| `dst-us-tx-hou-montrose` | `mkt-us-tx-hou` | `montrose` | `Montrose`    | `30`        |

District taxonomy rules:

- A district belongs to exactly one market.
- District identity is `districtId` or `slug`, never `displayName`.
- A district may be queried directly even when no neighborhood is assigned.
- `parentDistrictId` is `null` for all Houston Phase 0 districts.
- UI labels may vary stylistically, but persisted and queryable identity may not.

### Neighborhood Taxonomy Policy

Neighborhoods are optional canonical refinements under a district. They are not freeform tags.

Houston Phase 0 neighborhoods currently seeded:

| `neighborhoodId`                          | `districtId`             | `slug`             | `displayName`      | `sortOrder` |
| ----------------------------------------- | ------------------------ | ------------------ | ------------------ | ----------- |
| `nbh-us-tx-hou-downtown-market-square`    | `dst-us-tx-hou-downtown` | `market-square`    | `Market Square`    | `101`       |
| `nbh-us-tx-hou-midtown-mid-main`          | `dst-us-tx-hou-midtown`  | `mid-main`         | `Mid Main`         | `201`       |
| `nbh-us-tx-hou-montrose-lower-westheimer` | `dst-us-tx-hou-montrose` | `lower-westheimer` | `Lower Westheimer` | `301`       |

Neighborhood policy rules:

- A neighborhood must belong to exactly one district and one market.
- `neighborhood.marketId` must equal the parent district market.
- A neighborhood cannot exist without a district.
- A neighborhood may be absent on an event even when market and district are known.
- Unseeded neighborhood text from ingestion is evidence only, not canonical identity.
- New neighborhoods require taxonomy publication, not ad hoc string storage.

## IDs and Slugs

### Canonical Identity Rules

Canonical identifiers for implementation are:

- `marketId`, `districtId`, `neighborhoodId`
- `slug` for each taxonomy node
- `taxonomyVersion`

Display fields are never canonical keys.

### Allowed Identity Usage by Layer

| Layer                   | Allowed canonical identity                                        | Forbidden identity usage                                   |
| ----------------------- | ----------------------------------------------------------------- | ---------------------------------------------------------- |
| Seed taxonomy           | IDs, slugs, `boundaryRef`, `taxonomyVersion`                      | display-only keys                                          |
| Ingestion evidence      | raw source text plus canonical IDs when resolved                  | persisting raw neighborhood text as canonical neighborhood |
| Event aggregate storage | canonical `MarketCode`, `DistrictCode`, `NeighborhoodCode` values | using `City` or venue label as district substitute         |
| Backend query contracts | canonical IDs or canonical slugs                                  | display labels, fuzzy text                                 |
| Frontend state          | canonical IDs/slugs mirrored from backend contracts               | locally-maintained divergent taxonomy                      |

### Slug Rules

- Slugs are stable, URL-safe identity values.
- Slugs are unique within their required scope.
- Market alias slugs may resolve to the canonical market slug and ID.
- District and neighborhood slug resolution is context-aware and may depend on parent scope.
- A single query may use IDs or slugs per dimension, but not both for the same dimension.

## Hierarchy

### Required Parent-Child Invariants

The hierarchy is strict:

1. Market
2. District
3. Neighborhood

Required invariants:

- every district belongs to exactly one market
- every neighborhood belongs to exactly one district and one market
- neighborhood cannot be assigned without district assignment
- every event must resolve to exactly one market once spatial resolution is complete
- every event may resolve to zero or one district
- every event may resolve to zero or one neighborhood

### Event Hierarchy Invariants

For a canonical event:

- `NeighborhoodCode` requires `DistrictCode`
- `DistrictCode` implies `MarketCode`
- `NeighborhoodCode` implies the neighborhood's parent district and market must match the event values
- one event must never carry multiple districts or multiple neighborhoods

## Cross-Layer Representation

### Canonical Event Storage

Canonical event locality is stored in `EventAddress` and related event shapes using:

- `MarketCode`
- `DistrictCode`
- `NeighborhoodCode`

These fields represent canonical taxonomy linkage. They are not UI labels and must not be derived from presentation strings.

### Spatial Event Reference

Where the richer spatial reference contract is used, the canonical event linkage includes:

| Field                  | Meaning                                        |
| ---------------------- | ---------------------------------------------- |
| `MarketId`             | required canonical market identity             |
| `DistrictId`           | optional canonical district identity           |
| `NeighborhoodId`       | optional canonical neighborhood identity       |
| `ResolutionConfidence` | confidence in `[0,1]` for the spatial linkage  |
| `ResolutionSource`     | auditable source of the resolution decision    |
| `ResolvedAtUtc`        | timestamp of the last authoritative resolution |

`ResolutionSource` is mandatory when using the richer linkage contract because spatial assignment must be auditable.

### Backend Contract Shapes

Current backend and frontend query surfaces expose two related spatial shapes:

1. Legacy locality seam:
   - `marketCode`
   - `districtCode`
   - `neighborhoodCode`
2. Canonical query semantics shape:
   - `marketIds` or `marketSlugs`
   - `districtIds` or `districtSlugs`
   - `neighborhoodIds` or `neighborhoodSlugs`
   - `bbox`
   - `includeDescendants`
   - `minConfidence`

Phase 0 rule:

- compatibility seams may remain for request binding
- canonical execution semantics are defined by the canonical query shape and this document

## Event Linkage Rules

### Resolution Inputs

Spatial resolution may use:

- validated latitude and longitude
- normalized venue/address data
- market assignment service results
- district determination service results
- seeded taxonomy lookup
- ingestion evidence text as supporting evidence only

### Resolution Order

Event-to-spatial assignment must follow this order:

1. Validate coordinates and address integrity.
2. Determine whether the event belongs to the Houston market.
3. If the event is in market, assign canonical market identity.
4. If a district can be confidently determined, assign canonical district identity.
5. If a neighborhood can be confidently determined and is seeded, assign canonical neighborhood identity.
6. If neighborhood confidence is insufficient but district confidence is sufficient, store district only.
7. If district confidence is insufficient but market confidence is sufficient, store market only.
8. If market cannot be confidently resolved, do not invent district or neighborhood values.

### Event Assignment Rules

- Coordinates outside the Houston market boundary must not be assigned to Houston.
- A district assignment must be consistent with the assigned market.
- A neighborhood assignment must be consistent with the assigned district.
- Freeform locality strings may inform review but may not override taxonomy integrity.
- Canonical storage must prefer partial truthful linkage over precise-looking fabricated linkage.

### Fallback Behavior for Uncertain Spatial Assignments

Fallback rules are deterministic:

| Situation                                                     | Canonical result                                                              | Map behavior                               | Moderation implication                               |
| ------------------------------------------------------------- | ----------------------------------------------------------------------------- | ------------------------------------------ | ---------------------------------------------------- |
| Valid coordinates and confidence at or above public threshold | Store canonical market and resolved district/neighborhood where available     | Render on map                              | No geo-specific moderation requirement               |
| Valid coordinates but low location confidence                 | Store best supported canonical linkage if auditable                           | Do not publicly render precise map point   | Requires moderation review                           |
| Missing coordinates but address or venue text exists          | Store only spatial fields that are confidently resolved from non-geo evidence | Use non-map fallback surfaces only         | Requires fallback treatment and likely manual review |
| Invalid coordinates or null-island `(0,0)`                    | Do not trust coordinate-driven district/neighborhood resolution               | Block map rendering                        | Requires review; treat geo as unresolved             |
| Market known, district uncertain                              | Store `MarketCode`; leave `DistrictCode` and `NeighborhoodCode` null          | Allow market-scoped non-district discovery | Review optional based on confidence policy           |
| District known, neighborhood uncertain                        | Store `MarketCode` and `DistrictCode`; leave `NeighborhoodCode` null          | Queryable at district scope                | No neighborhood fabrication                          |

### Forbidden Resolution Behaviors

- Do not map `City = Houston` to a canonical district.
- Do not map venue marketing text to a canonical neighborhood without taxonomy-backed resolution.
- Do not persist unseeded neighborhood strings in `NeighborhoodCode`.
- Do not assign multiple districts to one event.
- Do not derive canonical IDs from UI labels.

## Query Semantics

### Canonical Query Dimensions

Phase 0 canonical query dimensions are:

- market
- district
- neighborhood
- bounding box
- minimum spatial confidence

All dimensions are explicit. No hidden fuzzy matching is allowed.

### Composition Modes

Canonical spatial composition mode is resolved by the backend.

| Mode               | Trigger                          | Meaning                             |
| ------------------ | -------------------------------- | ----------------------------------- |
| `BboxOnly`         | bbox present and taxonomy absent | filter only by geometry             |
| `TaxonomyOnly`     | taxonomy present and bbox absent | filter only by canonical taxonomy   |
| `BboxWithTaxonomy` | bbox and taxonomy both present   | filter by bbox first, then taxonomy |
| `ProximityReady`   | reserved                         | not supported in v1.0               |

### Taxonomy Query Semantics

Within each taxonomy dimension:

- values are OR-ed within the dimension
- dimensions combine by compatibility and intersection semantics
- district and neighborhood filters must resolve to canonical IDs before execution
- slugs are accepted only if they resolve cleanly to canonical IDs

### Hierarchy Inference Semantics

The validator may normalize queries by inferring missing parent scopes:

- district-only query may infer market
- neighborhood-only query may infer district and market
- slug-based queries resolve to IDs before execution

Inference rules are allowed only when the inferred hierarchy is unambiguous in the canonical taxonomy.

### `includeDescendants` Semantics

`includeDescendants` applies to district filters only.

- `true` means district filtering includes child neighborhoods of that district
- `false` means exact district membership only
- the default is `true`

Phase 0 note:

- Houston Phase 0 has one district level and one neighborhood level only
- `includeDescendants` therefore means including known child neighborhoods under the district

### `minConfidence` Semantics

- `minConfidence` is in range `[0,1]`
- events below the requested spatial resolution confidence are excluded from results
- low public-map confidence and query confidence filtering are related but not identical concerns
- query filtering must not override geo-integrity blocking rules

### Bounding Box Semantics

Bounding boxes are WGS84 and use the contract order:

`minLng,minLat,maxLng,maxLat`

Bounding box guardrails are mandatory:

- latitude must be within `[-90, 90]`
- longitude must be within `[-180, 180]`
- `minLat < maxLat`
- `minLng < maxLng`
- latitude span must be `<= 5`
- longitude span must be `<= 5`
- area must be `<= 8` square degrees

Queries violating these rules are invalid and must be rejected rather than silently widened.

### Query Conflict Rules

- IDs and slugs for the same dimension cannot both be provided in one query.
- Empty spatial queries are invalid.
- Taxonomy display labels are not valid query input.
- Unknown slugs are validation errors.
- Incompatible hierarchy combinations are validation errors.

## Coordinate Integrity Rules

Coordinate integrity is separate from taxonomy identity but controls whether coordinate-derived locality may be trusted.

### Geo Validation Categories

| Category               | Meaning                                                                         | Rendering eligibility |
| ---------------------- | ------------------------------------------------------------------------------- | --------------------- |
| `valid`                | coordinates are valid and confidence is sufficient                              | `render`              |
| `valid_low_confidence` | coordinates are structurally valid but confidence is below the public threshold | `require_moderation`  |
| `missing`              | coordinates are absent or incomplete                                            | `use_fallback`        |
| `invalid`              | coordinates are malformed or unusable                                           | `block`               |

### Required Geo Integrity Rules

- Latitude must be finite and in `[-90, 90]`.
- Longitude must be finite and in `[-180, 180]`.
- `(0,0)` is null-island and is invalid.
- Location confidence must be in `[0,1]` when present.
- Missing address is not by itself an invalid coordinate, but it must remain visible as an issue.

### Public Map Threshold

The public map render threshold is:

- `PUBLIC_MAP_CONFIDENCE_THRESHOLD = 0.5`

Therefore:

- confidence `>= 0.5` may be renderable if coordinates are otherwise valid
- confidence `< 0.5` is moderation-only for precise map rendering
- invalid or null-island coordinates are blocked regardless of confidence

## Canonical Taxonomy vs UI Presentation vs Ingestion Confidence

These concerns must remain separate.

### Canonical Taxonomy

Canonical taxonomy contains:

- stable IDs and slugs
- hierarchy
- `boundaryRef`
- active status
- sort order
- taxonomy version

Canonical taxonomy does not contain confidence scores or presentation-specific copy.

### UI Presentation

UI may choose:

- display labels
- sort and grouping presentation based on canonical sort order
- whether to show district chips, neighborhood chips, or breadcrumb labels

UI may not:

- create canonical districts locally
- substitute display text for canonical query values
- infer taxonomy beyond what the backend contract authorizes

### Ingestion Confidence

Ingestion confidence is evidence quality, not taxonomy identity.

It may affect:

- whether a coordinate is renderable
- whether moderation is required
- whether neighborhood assignment is omitted

It may not:

- create new canonical nodes
- justify writing fuzzy locality text into canonical ID fields

## Moderation Implications

Spatial uncertainty is not a harmless display concern. It can affect rendering, publishability, and review workflows.

### Moderation Rules

- Low-confidence coordinates require moderation for public map rendering.
- Missing coordinates require fallback treatment and should not appear as precise points.
- Invalid geo data is a blocker for geo-based publication semantics.
- Publish eligibility treats missing or invalid geo validation as a blocker or manual-review trigger depending on the broader policy state.
- If a spatial correction materially changes event locality, the change should be auditable as a state change that may require moderation review.

### Inventory and District-Scoped Policy Implications

District identity is operational, not decorative. Incorrect district assignment can affect:

- district-scoped discovery filtering
- district-scoped analytics
- district-scoped sponsored placement and inventory guardrails
- moderation queues for location disputes

For that reason, uncertain district assignment must resolve to `null`, not a best guess presented as fact.

## Example Houston Taxonomy Entries

### Market Example

| Field         | Example             |
| ------------- | ------------------- |
| `marketId`    | `mkt-us-tx-hou`     |
| `slug`        | `houston`           |
| `displayName` | `Houston`           |
| `aliasSlugs`  | `hou`, `houston-tx` |

### District Example

| Field         | Example                 |
| ------------- | ----------------------- |
| `districtId`  | `dst-us-tx-hou-midtown` |
| `marketId`    | `mkt-us-tx-hou`         |
| `slug`        | `midtown`               |
| `displayName` | `Midtown`               |

### Neighborhood Example

| Field            | Example                          |
| ---------------- | -------------------------------- |
| `neighborhoodId` | `nbh-us-tx-hou-midtown-mid-main` |
| `districtId`     | `dst-us-tx-hou-midtown`          |
| `marketId`       | `mkt-us-tx-hou`                  |
| `slug`           | `mid-main`                       |
| `displayName`    | `Mid Main`                       |

## Example Event Resolution Scenarios

### Scenario 1: High-confidence Downtown event

Input:

- venue address in Houston
- valid coordinates in Downtown
- geocode confidence `0.91`
- taxonomy lookup matches Downtown, not a seeded neighborhood

Result:

- `MarketCode = houston`
- `DistrictCode = downtown`
- `NeighborhoodCode = null`
- event is renderable on the public map

### Scenario 2: Midtown event with seeded neighborhood

Input:

- validated coordinates
- geocode confidence `0.88`
- taxonomy lookup matches Midtown district and Mid Main neighborhood

Result:

- `MarketCode = houston`
- `DistrictCode = midtown`
- `NeighborhoodCode = mid-main`
- neighborhood-level query matches this event

### Scenario 3: Market known, district uncertain

Input:

- address clearly in Houston
- coordinates missing
- venue text suggests Montrose but no reliable canonical match

Result:

- `MarketCode = houston`
- `DistrictCode = null`
- `NeighborhoodCode = null`
- event remains discoverable at market scope only

### Scenario 4: Low-confidence coordinates

Input:

- valid coordinates in Houston
- geocode confidence `0.43`
- district best match is Downtown

Result:

- canonical linkage may be stored only if the assignment is auditable
- public precise map rendering is suppressed
- moderation review is required before treating the point as trustworthy public geo

## Spatial Edge-Case Examples

| Edge case                                                                               | Required behavior                                                         |
| --------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| Event has `City = Houston` but no district evidence                                     | assign market only; do not invent district                                |
| Event has neighborhood text `EaDo` that is not seeded in Phase 0 taxonomy               | preserve as source evidence only; do not write canonical neighborhood     |
| Query passes `districtSlugs=downtown` and `districtIds=dst-us-tx-hou-downtown` together | reject query as ambiguous                                                 |
| Query passes bbox larger than 5 degrees in one dimension                                | reject query                                                              |
| Coordinates are `(0,0)`                                                                 | mark invalid, block map rendering, require fallback handling              |
| Neighborhood is provided without district in storage                                    | invalid state; neighborhood requires district                             |
| Neighborhood-only query is submitted                                                    | backend may infer district and market if taxonomy mapping is unambiguous  |
| District assignment conflicts with market assignment                                    | reject or clear the conflicting child assignment; do not persist mismatch |

## Testability Requirements

Any implementation claiming conformance to this document must be testable at these layers:

- taxonomy snapshot tests for seeded Houston IDs, slugs, parent relationships, and version
- contract parity tests for backend and frontend spatial DTOs
- validator tests for composition modes, slug resolution, inference, and invalid combinations
- geo integrity tests for missing coordinates, null-island, low-confidence, and bbox guardrails
- aggregate or persistence tests ensuring one market and optional single district/neighborhood linkage
- moderation and publish gate tests for low-confidence or invalid geo cases

## Future Expansion Notes

Phase 0 future-safe seams are allowed only where already explicit in current contracts:

- additive district and neighborhood nodes under a new taxonomy version
- geometry execution upgrades that continue honoring current IDs and slugs
- support for additional markets in future versions without repurposing Houston keys
- proximity query mode activation under a future versioned contract

Future expansion must preserve these invariants:

- Houston Phase 0 IDs and slugs stay stable
- historical event linkage remains attached to canonical IDs
- display labels may evolve without changing canonical identity
- confidence remains separate from taxonomy identity

## Non-Negotiable Rules Summary

1. Houston Phase 0 spatial identity is canonical only through taxonomy IDs and slugs.
2. One event resolves to exactly one market and at most one district and one neighborhood.
3. District and neighborhood are optional, but fabricated precision is forbidden.
4. Query semantics are explicit, validated, and backend-authoritative.
5. Bounding boxes and coordinates must pass deterministic integrity checks.
6. Low-confidence or invalid geo data affects rendering and moderation, not taxonomy truth.
7. UI presentation and ingestion evidence must never be confused with canonical spatial identity.
