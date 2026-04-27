# Spatial Taxonomy v1.0 (Market, District, Neighborhood)

## Canonical Hierarchy

Hierarchy is strict and versioned:

1. Market
2. District
3. Neighborhood

Market is the top-level operational partition for policy and temporal defaults.
District is a market-scoped taxonomy node for query partitioning and discovery UX.
Neighborhood is a district-scoped refinement node for higher-confidence locality resolution.

## Identity and Label Rules

Canonical source of truth:

- marketId, districtId, neighborhoodId
- slug values

User-facing only:

- displayName labels

Internal geometry only:

- boundaryRef (provider + datasetId + featureId + version + hash)

Display labels must never be used as keys for storage, filtering, joins, or event linkage.

## Parent-Child Invariants

Required invariants:

- every district belongs to exactly one market
- every neighborhood belongs to exactly one district and one market
- neighborhood.marketId must equal parent district marketId
- neighborhood cannot exist without a district
- IDs and slugs are stable within one taxonomyVersion

Forbidden patterns:

- mapping district to city name as a placeholder
- storing freeform district/neighborhood strings as canonical fields
- frontend-maintained district lists that diverge from backend taxonomy payload

## Event Linkage Semantics

Every event must carry canonical spatial linkage with these semantics:

- exactly one marketId
- zero-or-one districtId
- zero-or-one neighborhoodId
- neighborhoodId requires districtId
- resolutionConfidence in range [0, 1]

This makes event locality explicit and auditable while allowing partial resolution when confidence is limited.

## Query and Filter Semantics

Spatial filters must consume canonical IDs/slugs only:

- marketIds / marketSlugs
- districtIds / districtSlugs
- neighborhoodIds / neighborhoodSlugs

Do not filter by display labels.
Do not accept raw freeform district/neighborhood text as canonical query input.

## Houston Phase 0 Seed Structure

Houston Phase 0 canonical seed is in:

- seed/houston-spatial-taxonomy.v1.json

It initializes:

- market: Houston (America/Chicago)
- districts: Downtown, Midtown, Montrose
- neighborhoods: Market Square, Mid Main, Lower Westheimer

All entities include stable IDs, slugs, and boundaryRef pointers.

## Expansion Guidance

To add new markets without breaking Houston:

- publish a new taxonomyVersion with additive market entries
- keep existing IDs/slugs stable (no repurposing)
- deprecate nodes via isActive instead of deleting historical keys
- keep event history tied to canonical IDs to preserve lineage integrity
