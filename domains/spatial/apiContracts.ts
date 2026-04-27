/**
 * Canonical spatial taxonomy contracts aligned to WeUP.Contracts.Spatial.
 *
 * Semantics:
 * - displayName fields are user-facing labels only.
 * - IDs and slugs are canonical identity keys and query keys.
 * - boundaryRef is an internal geometry pointer, not a display primitive.
 */

export interface GeometryBoundaryRefDto {
  readonly provider: string;
  readonly datasetId: string;
  readonly featureId: string;
  readonly version: string;
  readonly hash: string | null;
}

export interface SpatialMarketDto {
  readonly marketId: string;
  readonly slug: string;
  readonly displayName: string;
  readonly timezone: string;
  readonly boundaryRef: GeometryBoundaryRefDto;
  readonly isActive: boolean;
  readonly aliasSlugs: string[];
}

export interface SpatialDistrictDto {
  readonly districtId: string;
  readonly marketId: string;
  readonly slug: string;
  readonly displayName: string;
  readonly boundaryRef: GeometryBoundaryRefDto;
  readonly sortOrder: number;
  readonly isActive: boolean;
  readonly parentDistrictId: string | null;
}

export interface SpatialNeighborhoodDto {
  readonly neighborhoodId: string;
  readonly marketId: string;
  readonly districtId: string;
  readonly slug: string;
  readonly displayName: string;
  readonly boundaryRef: GeometryBoundaryRefDto;
  readonly sortOrder: number;
  readonly isActive: boolean;
}

/**
 * One event maps to one market and zero-or-one district/neighborhood
 * according to current resolution confidence.
 */
export interface SpatialEventReferenceDto {
  readonly marketId: string;
  readonly districtId: string | null;
  readonly neighborhoodId: string | null;
  readonly resolutionConfidence: number;
  readonly resolutionSource: string;
  readonly resolvedAtUtc: string;
  readonly marketDisplayLabel: string | null;
  readonly districtDisplayLabel: string | null;
  readonly neighborhoodDisplayLabel: string | null;
}

/**
 * Canonical spatial query filter contract.
 * Display labels are intentionally excluded to avoid freeform filtering drift.
 */
export interface SpatialFilterDto {
  readonly marketIds?: string[];
  readonly districtIds?: string[];
  readonly neighborhoodIds?: string[];
  readonly marketSlugs?: string[];
  readonly districtSlugs?: string[];
  readonly neighborhoodSlugs?: string[];
  readonly includeDescendants?: boolean;
}

export interface SpatialTaxonomySnapshotDto {
  readonly taxonomyVersion: string;
  readonly effectiveAtUtc: string;
  readonly markets: SpatialMarketDto[];
  readonly districts: SpatialDistrictDto[];
  readonly neighborhoods: SpatialNeighborhoodDto[];
}
