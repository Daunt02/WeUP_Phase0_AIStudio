import { computed, ref } from "vue";

import type {
  CalendarOverlayTemporalQueryDto,
  MapFeedFilterState,
  TimeWindowFilterDto,
} from "../contracts/time-window.contracts";
import type { EventMapFeedQueryDto } from "../contracts/map-feed.contracts";
import { useTemporalQueryState } from "./useTemporalQueryState";
import { isCustomRangePreset } from "../contracts/temporal-query.contracts";

type UseMapFeedFiltersOptions = Partial<MapFeedFilterState>;

export function useMapFeedFilters(options: UseMapFeedFiltersOptions = {}) {
  const temporal = useTemporalQueryState({
    preset: options.preset,
    marketTimezone: options.timezone,
    customStartLocal: options.customStartLocal,
    customEndLocal: options.customEndLocal,
  });

  const preset = temporal.preset;
  const timezone = temporal.marketTimezone;
  const customStartLocal = temporal.customStartLocal;
  const customEndLocal = temporal.customEndLocal;
  const includeSavedOnly = ref(options.includeSavedOnly ?? false);

  const validationError = temporal.validationError;

  const temporalContract = computed<TimeWindowFilterDto>(() => {
    const baseContract: TimeWindowFilterDto = {
      preset: temporal.temporalQuery.value.preset,
      timezone: temporal.temporalQuery.value.marketTimezone,
      marketTimezone: temporal.temporalQuery.value.marketTimezone,
      referenceInstantUtc: temporal.temporalQuery.value.referenceInstantUtc,
    };

    if (!isCustomRangePreset(temporal.temporalQuery.value.preset)) {
      return baseContract;
    }

    return {
      ...baseContract,
      customStartUtc: temporal.temporalQuery.value.fromUtc,
      customEndUtc: temporal.temporalQuery.value.toUtc,
      fromUtc: temporal.temporalQuery.value.fromUtc,
      toUtc: temporal.temporalQuery.value.toUtc,
    };
  });

  const requestSignature = computed(() =>
    JSON.stringify({
      preset: temporal.temporalQuery.value.preset,
      fromUtc: temporal.temporalQuery.value.fromUtc ?? null,
      toUtc: temporal.temporalQuery.value.toUtc ?? null,
      marketTimezone: temporal.temporalQuery.value.marketTimezone,
      referenceInstantUtc:
        temporal.temporalQuery.value.referenceInstantUtc ?? null,
      includeSavedOnly: includeSavedOnly.value,
    }),
  );

  function buildMapFeedQuery(bbox: string): EventMapFeedQueryDto {
    if (validationError.value) {
      throw new Error(validationError.value);
    }

    return {
      bbox,
      ...temporalContract.value,
      marketTimezone: temporal.temporalQuery.value.marketTimezone,
      fromUtc: temporal.temporalQuery.value.fromUtc,
      toUtc: temporal.temporalQuery.value.toUtc,
      referenceInstantUtc: temporal.temporalQuery.value.referenceInstantUtc,
      includeSavedOnly: includeSavedOnly.value,
    };
  }

  function toCalendarOverlayTemporalQuery(): CalendarOverlayTemporalQueryDto {
    if (validationError.value) {
      throw new Error(validationError.value);
    }

    return {
      ...temporalContract.value,
    };
  }

  function toSavedEventsTemporalQuery() {
    if (validationError.value) {
      throw new Error(validationError.value);
    }

    return temporal.temporalQuery.value;
  }

  return {
    preset,
    timezone,
    customStartLocal,
    customEndLocal,
    includeSavedOnly,
    validationError,
    temporalContract,
    temporalQuery: temporal.temporalQuery,
    requestSignature,
    buildMapFeedQuery,
    toCalendarOverlayTemporalQuery,
    toSavedEventsTemporalQuery,
  };
}
