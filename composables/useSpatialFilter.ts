/**
 * Composable: useSpatialFilter
 *
 * Manages canonical spatial filter state for map, calendar, and saved discovery surfaces.
 * Ensures:
 * - All spatial filters are explicit and aligned to backend contracts.
 * - No hidden fallbacks or implicit text matching.
 * - Filter composition mode is always validated and deterministic.
 * - Frontend state matches backend query semantics exactly.
 *
 * RESPONSIBILITIES:
 * - Maintain reactive spatial filter state.
 * - Track composition mode (BboxOnly, TaxonomyOnly, BboxWithTaxonomy).
 * - Validate filter combinations and provide actionable errors.
 * - Synchronize filter state with query parameters and network requests.
 * - Infer markets from districts/neighborhoods when appropriate.
 *
 * USAGE:
 *   const spatialFilter = useSpatialFilter();
 *   spatialFilter.setMarketIds(['houston']);
 *   spatialFilter.setDistrictIds(['downtown']);
 *   const errors = spatialFilter.validationErrors;
 *   if (errors.length === 0 && spatialFilter.compositionMode === SpatialCompositionMode.TaxonomyOnly) {
 *     // Proceed with query
 *   }
 */

import { computed, ref, watch, Ref, ComputedRef } from "vue";
import type {
  SpatialQueryDto,
  EventMapFeedQueryV2Dto,
  SpatialCompositionMode,
  SpatialQueryValidationResult,
} from "@/domains/spatial/contracts";
import {
  SpatialCompositionMode as CompositionModeEnum,
  isBboxOnly,
  isTaxonomyOnly,
  isBboxWithTaxonomy,
  isValidSpatialQuery,
  toSpatialQuery,
} from "@/domains/spatial/contracts";

export interface UseSpatialFilterOptions {
  /**
   * Initial filter state. If provided, replaces default empty state.
   */
  initialQuery?: EventMapFeedQueryV2Dto;

  /**
   * Callback when composition mode changes.
   * Useful for triggering analytics or UI updates.
   */
  onModeChange?: (mode: SpatialCompositionMode | null) => void;

  /**
   * Callback when validation errors occur.
   */
  onValidationError?: (errors: readonly string[]) => void;

  /**
   * Confidence threshold for spatial resolution.
   * Default: 0.0 (no minimum).
   */
  minConfidence?: number;
}

export interface UseSpatialFilterReturn {
  // Filter state (reactive)
  marketIds: Ref<readonly string[]>;
  marketSlugs: Ref<readonly string[]>;
  districtIds: Ref<readonly string[]>;
  districtSlugs: Ref<readonly string[]>;
  neighborhoodIds: Ref<readonly string[]>;
  neighborhoodSlugs: Ref<readonly string[]>;
  bbox: Ref<string>;
  includeDescendants: Ref<boolean>;
  minConfidence: Ref<number>;

  // Setters for filter state
  setMarketIds(ids: readonly string[]): void;
  setMarketSlugs(slugs: readonly string[]): void;
  setDistrictIds(ids: readonly string[]): void;
  setDistrictSlugs(slugs: readonly string[]): void;
  setNeighborhoodIds(ids: readonly string[]): void;
  setNeighborhoodSlugs(slugs: readonly string[]): void;
  setBbox(bbox: string): void;
  setIncludeDescendants(include: boolean): void;

  // Query composition
  query: ComputedRef<SpatialQueryDto>;
  queryV2: ComputedRef<EventMapFeedQueryV2Dto>;

  // Validation
  validationResult: ComputedRef<SpatialQueryValidationResult>;
  validationErrors: ComputedRef<readonly string[]>;
  isValid: ComputedRef<boolean>;
  compositionMode: ComputedRef<SpatialCompositionMode | null>;

  // Composition mode guards
  isBboxOnly: ComputedRef<boolean>;
  isTaxonomyOnly: ComputedRef<boolean>;
  isBboxWithTaxonomy: ComputedRef<boolean>;

  // Query builders (for template/network layer)
  buildQueryString(): string;
  buildQueryObject(): Record<string, any>;

  // Reset and utilities
  reset(): void;
  hasAnyFilter: ComputedRef<boolean>;
  clearAllFilters(): void;
  clearTaxonomyFilters(): void;
  clearBboxFilter(): void;
}

/**
 * Composable: useSpatialFilter
 *
 * Core implementation. Use this in your Vue components.
 */
export function useSpatialFilter(
  options?: UseSpatialFilterOptions,
): UseSpatialFilterReturn {
  // Filter state (reactive refs)
  const marketIds = ref<readonly string[]>(
    options?.initialQuery?.marketIds ?? [],
  );
  const marketSlugs = ref<readonly string[]>(
    options?.initialQuery?.marketSlugs ?? [],
  );
  const districtIds = ref<readonly string[]>(
    options?.initialQuery?.districtIds ?? [],
  );
  const districtSlugs = ref<readonly string[]>(
    options?.initialQuery?.districtSlugs ?? [],
  );
  const neighborhoodIds = ref<readonly string[]>(
    options?.initialQuery?.neighborhoodIds ?? [],
  );
  const neighborhoodSlugs = ref<readonly string[]>(
    options?.initialQuery?.neighborhoodSlugs ?? [],
  );
  const bbox = ref<string>(options?.initialQuery?.bbox ?? "");
  const includeDescendants = ref<boolean>(
    options?.initialQuery?.includeDescendants ?? true,
  );
  const minConfidence = ref<number>(options?.minConfidence ?? 0.0);

  // Setters
  const setMarketIds = (ids: readonly string[]) => {
    marketIds.value = ids;
    // Clear slugs when IDs are set (mutual exclusivity)
    if (ids.length > 0) {
      marketSlugs.value = [];
    }
  };

  const setMarketSlugs = (slugs: readonly string[]) => {
    marketSlugs.value = slugs;
    if (slugs.length > 0) {
      marketIds.value = [];
    }
  };

  const setDistrictIds = (ids: readonly string[]) => {
    districtIds.value = ids;
    if (ids.length > 0) {
      districtSlugs.value = [];
    }
  };

  const setDistrictSlugs = (slugs: readonly string[]) => {
    districtSlugs.value = slugs;
    if (slugs.length > 0) {
      districtIds.value = [];
    }
  };

  const setNeighborhoodIds = (ids: readonly string[]) => {
    neighborhoodIds.value = ids;
    if (ids.length > 0) {
      neighborhoodSlugs.value = [];
    }
  };

  const setNeighborhoodSlugs = (slugs: readonly string[]) => {
    neighborhoodSlugs.value = slugs;
    if (slugs.length > 0) {
      neighborhoodIds.value = [];
    }
  };

  const setBbox = (bboxStr: string) => {
    bbox.value = bboxStr;
  };

  const setIncludeDescendants = (include: boolean) => {
    includeDescendants.value = include;
  };

  // Computed query object
  const query = computed<SpatialQueryDto>(() => ({
    marketIds: marketIds.value.length > 0 ? marketIds.value : undefined,
    marketSlugs: marketSlugs.value.length > 0 ? marketSlugs.value : undefined,
    districtIds: districtIds.value.length > 0 ? districtIds.value : undefined,
    districtSlugs:
      districtSlugs.value.length > 0 ? districtSlugs.value : undefined,
    neighborhoodIds:
      neighborhoodIds.value.length > 0 ? neighborhoodIds.value : undefined,
    neighborhoodSlugs:
      neighborhoodSlugs.value.length > 0 ? neighborhoodSlugs.value : undefined,
    bbox: bbox.value || undefined,
    includeDescendants: includeDescendants.value,
    minConfidence: minConfidence.value,
  }));

  // Validation (frontend-only basic validation; backend performs authoritative validation)
  const validationResult = computed<SpatialQueryValidationResult>(() => {
    const errors: string[] = [];

    // Rule 1: Mutual exclusivity of ID vs slug for each dimension
    if (marketIds.value.length > 0 && marketSlugs.value.length > 0) {
      errors.push("Cannot specify both marketIds and marketSlugs.");
    }
    if (districtIds.value.length > 0 && districtSlugs.value.length > 0) {
      errors.push("Cannot specify both districtIds and districtSlugs.");
    }
    if (
      neighborhoodIds.value.length > 0 &&
      neighborhoodSlugs.value.length > 0
    ) {
      errors.push("Cannot specify both neighborhoodIds and neighborhoodSlugs.");
    }

    // Rule 2: MinConfidence bounds
    if (minConfidence.value < 0.0 || minConfidence.value > 1.0) {
      errors.push(
        `MinConfidence must be in [0.0, 1.0], got ${minConfidence.value}.`,
      );
    }

    // Determine if we have any taxonomy or bbox filters
    const hasTaxonomy =
      marketIds.value.length > 0 ||
      marketSlugs.value.length > 0 ||
      districtIds.value.length > 0 ||
      districtSlugs.value.length > 0 ||
      neighborhoodIds.value.length > 0 ||
      neighborhoodSlugs.value.length > 0;
    const hasBbox = bbox.value.trim().length > 0;

    // Rule 3: At least one spatial dimension
    if (!hasTaxonomy && !hasBbox) {
      // No error for empty filters (allow empty initial state)
      // Backend will validate when query is submitted
      return {
        mode: null,
        errors: [],
        normalizedQuery: null,
      };
    }

    // Determine composition mode
    let mode: SpatialCompositionMode | null = null;
    if (hasBbox && !hasTaxonomy) {
      mode = CompositionModeEnum.BboxOnly;
    } else if (!hasBbox && hasTaxonomy) {
      mode = CompositionModeEnum.TaxonomyOnly;
    } else if (hasBbox && hasTaxonomy) {
      mode = CompositionModeEnum.BboxWithTaxonomy;
    }

    // Return validation result
    if (errors.length > 0) {
      return {
        mode: null,
        errors,
        normalizedQuery: null,
      };
    }

    return {
      mode,
      errors: [],
      normalizedQuery: hasTaxonomy || hasBbox ? query.value : null,
    };
  });

  const validationErrors = computed(() => validationResult.value.errors);
  const isValid = computed(() => isValidSpatialQuery(validationResult.value));
  const compositionMode = computed(() => validationResult.value.mode);

  // Composition mode guards
  const isBboxOnly = computed(() =>
    compositionMode.value !== null ? isBboxOnly(validationResult.value) : false,
  );
  const isTaxonomyOnly = computed(() =>
    compositionMode.value !== null
      ? isTaxonomyOnly(validationResult.value)
      : false,
  );
  const isBboxWithTaxonomy = computed(() =>
    compositionMode.value !== null
      ? isBboxWithTaxonomy(validationResult.value)
      : false,
  );

  // Query V2 (full query with temporal placeholders)
  const queryV2 = computed<EventMapFeedQueryV2Dto>(() => ({
    ...query.value,
    // Note: temporal fields should be set by the caller (e.g., time filter composable)
    marketIds: marketIds.value.length > 0 ? marketIds.value : undefined,
    districtIds: districtIds.value.length > 0 ? districtIds.value : undefined,
    neighborhoodIds:
      neighborhoodIds.value.length > 0 ? neighborhoodIds.value : undefined,
    marketSlugs: marketSlugs.value.length > 0 ? marketSlugs.value : undefined,
    districtSlugs:
      districtSlugs.value.length > 0 ? districtSlugs.value : undefined,
    neighborhoodSlugs:
      neighborhoodSlugs.value.length > 0 ? neighborhoodSlugs.value : undefined,
    bbox: bbox.value || undefined,
    includeDescendants: includeDescendants.value,
    minSpatialConfidence: minConfidence.value,
  }));

  // Query string builder (for URL query parameters)
  const buildQueryString = (): string => {
    const params = new URLSearchParams();

    if (marketIds.value.length > 0) {
      params.append("marketIds", marketIds.value.join(","));
    }
    if (marketSlugs.value.length > 0) {
      params.append("marketSlugs", marketSlugs.value.join(","));
    }
    if (districtIds.value.length > 0) {
      params.append("districtIds", districtIds.value.join(","));
    }
    if (districtSlugs.value.length > 0) {
      params.append("districtSlugs", districtSlugs.value.join(","));
    }
    if (neighborhoodIds.value.length > 0) {
      params.append("neighborhoodIds", neighborhoodIds.value.join(","));
    }
    if (neighborhoodSlugs.value.length > 0) {
      params.append("neighborhoodSlugs", neighborhoodSlugs.value.join(","));
    }
    if (bbox.value.trim().length > 0) {
      params.append("bbox", bbox.value);
    }
    if (!includeDescendants.value) {
      params.append("includeDescendants", "false");
    }
    if (minConfidence.value > 0.0) {
      params.append("minSpatialConfidence", minConfidence.value.toString());
    }

    return params.toString();
  };

  // Query object builder (for JSON payloads)
  const buildQueryObject = (): Record<string, any> => {
    const obj: Record<string, any> = {};

    if (marketIds.value.length > 0) obj.marketIds = marketIds.value;
    if (marketSlugs.value.length > 0) obj.marketSlugs = marketSlugs.value;
    if (districtIds.value.length > 0) obj.districtIds = districtIds.value;
    if (districtSlugs.value.length > 0) obj.districtSlugs = districtSlugs.value;
    if (neighborhoodIds.value.length > 0)
      obj.neighborhoodIds = neighborhoodIds.value;
    if (neighborhoodSlugs.value.length > 0)
      obj.neighborhoodSlugs = neighborhoodSlugs.value;
    if (bbox.value.trim().length > 0) obj.bbox = bbox.value;
    if (!includeDescendants.value) obj.includeDescendants = false;
    if (minConfidence.value > 0.0)
      obj.minSpatialConfidence = minConfidence.value;

    return obj;
  };

  // Utilities
  const reset = () => {
    marketIds.value = [];
    marketSlugs.value = [];
    districtIds.value = [];
    districtSlugs.value = [];
    neighborhoodIds.value = [];
    neighborhoodSlugs.value = [];
    bbox.value = "";
    includeDescendants.value = true;
    minConfidence.value = options?.minConfidence ?? 0.0;
  };

  const clearAllFilters = () => {
    reset();
  };

  const clearTaxonomyFilters = () => {
    marketIds.value = [];
    marketSlugs.value = [];
    districtIds.value = [];
    districtSlugs.value = [];
    neighborhoodIds.value = [];
    neighborhoodSlugs.value = [];
  };

  const clearBboxFilter = () => {
    bbox.value = "";
  };

  const hasAnyFilter = computed(() => {
    return (
      marketIds.value.length > 0 ||
      marketSlugs.value.length > 0 ||
      districtIds.value.length > 0 ||
      districtSlugs.value.length > 0 ||
      neighborhoodIds.value.length > 0 ||
      neighborhoodSlugs.value.length > 0 ||
      bbox.value.trim().length > 0
    );
  });

  // Watch for composition mode changes
  watch(
    compositionMode,
    (newMode) => {
      options?.onModeChange?.(newMode);
    },
    { immediate: false },
  );

  // Watch for validation errors
  watch(
    validationErrors,
    (errors) => {
      if (errors.length > 0) {
        options?.onValidationError?.(errors);
      }
    },
    { immediate: false },
  );

  return {
    // State
    marketIds,
    marketSlugs,
    districtIds,
    districtSlugs,
    neighborhoodIds,
    neighborhoodSlugs,
    bbox,
    includeDescendants,
    minConfidence,

    // Setters
    setMarketIds,
    setMarketSlugs,
    setDistrictIds,
    setDistrictSlugs,
    setNeighborhoodIds,
    setNeighborhoodSlugs,
    setBbox,
    setIncludeDescendants,

    // Queries
    query,
    queryV2,

    // Validation
    validationResult,
    validationErrors,
    isValid,
    compositionMode,

    // Guards
    isBboxOnly,
    isTaxonomyOnly,
    isBboxWithTaxonomy,

    // Builders
    buildQueryString,
    buildQueryObject,

    // Utilities
    reset,
    hasAnyFilter,
    clearAllFilters,
    clearTaxonomyFilters,
    clearBboxFilter,
  };
}

/**
 * Type export for filter state
 */
export type SpatialFilterState = ReturnType<typeof useSpatialFilter>;
