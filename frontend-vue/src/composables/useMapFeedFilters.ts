import { computed, ref } from "vue";

import type {
  CalendarOverlayTemporalQueryDto,
  MapFeedFilterState,
  TimeWindowFilterDto,
} from "../contracts/time-window.contracts";
import { TimeWindowPreset } from "../contracts/time-window.contracts";
import type { EventMapFeedQueryDto } from "../contracts/map-feed.contracts";

type UseMapFeedFiltersOptions = Partial<MapFeedFilterState>;

function detectTimezone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC";
}

function toUtcIso(localDateTime: string): string | null {
  if (!localDateTime.trim()) {
    return null;
  }

  const parsed = new Date(localDateTime);
  if (Number.isNaN(parsed.getTime())) {
    return null;
  }

  return parsed.toISOString();
}

export function useMapFeedFilters(options: UseMapFeedFiltersOptions = {}) {
  const preset = ref(options.preset ?? TimeWindowPreset.Now);
  const timezone = ref(options.timezone ?? detectTimezone());
  const customStartLocal = ref(options.customStartLocal ?? "");
  const customEndLocal = ref(options.customEndLocal ?? "");
  const includeSavedOnly = ref(options.includeSavedOnly ?? false);

  const customStartUtc = computed(() => toUtcIso(customStartLocal.value));
  const customEndUtc = computed(() => toUtcIso(customEndLocal.value));

  const validationError = computed<string | null>(() => {
    if (!timezone.value.trim()) {
      return "Timezone is required so preset windows resolve identically on the backend and in calendar overlays.";
    }

    if (preset.value !== TimeWindowPreset.Custom) {
      return null;
    }

    if (!customStartLocal.value || !customEndLocal.value) {
      return "Custom range requires both start and end values before dispatch.";
    }

    if (!customStartUtc.value || !customEndUtc.value) {
      return "Custom range values must be valid datetimes.";
    }

    if (customStartUtc.value >= customEndUtc.value) {
      return "Custom range start must be earlier than custom range end.";
    }

    return null;
  });

  const temporalContract = computed<TimeWindowFilterDto>(() => {
    const baseContract: TimeWindowFilterDto = {
      preset: preset.value,
      timezone: timezone.value.trim(),
    };

    if (preset.value !== TimeWindowPreset.Custom) {
      return baseContract;
    }

    return {
      ...baseContract,
      customStartUtc: customStartUtc.value ?? undefined,
      customEndUtc: customEndUtc.value ?? undefined,
    };
  });

  const requestSignature = computed(() =>
    JSON.stringify({
      preset: temporalContract.value.preset,
      timezone: temporalContract.value.timezone,
      customStartUtc: temporalContract.value.customStartUtc ?? null,
      customEndUtc: temporalContract.value.customEndUtc ?? null,
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

  return {
    preset,
    timezone,
    customStartLocal,
    customEndLocal,
    includeSavedOnly,
    validationError,
    temporalContract,
    requestSignature,
    buildMapFeedQuery,
    toCalendarOverlayTemporalQuery,
  };
}
