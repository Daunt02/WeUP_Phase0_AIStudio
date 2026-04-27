import { computed, ref } from "vue";
import {
  isCustomRangePreset,
  type TemporalQueryDto,
} from "../contracts/temporal-query.contracts";
import { TimeWindowPreset } from "../contracts/time-window.contracts";

export interface TemporalQueryState {
  preset: TimeWindowPreset;
  marketTimezone: string;
  customStartLocal: string;
  customEndLocal: string;
  referenceInstantUtc?: string;
}

export interface UseTemporalQueryStateOptions extends Partial<TemporalQueryState> {}

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

/**
 * Single temporal contract source for map, calendar, and saved-event retrieval.
 *
 * Anti-drift rules:
 * - No component computes preset windows locally.
 * - All outputs are built from one canonical TemporalQueryDto computed value.
 * - Local datetime inputs are converted once and validated before query dispatch.
 */
export function useTemporalQueryState(
  options: UseTemporalQueryStateOptions = {},
) {
  const preset = ref(options.preset ?? TimeWindowPreset.Now);
  const marketTimezone = ref(options.marketTimezone ?? detectTimezone());
  const customStartLocal = ref(options.customStartLocal ?? "");
  const customEndLocal = ref(options.customEndLocal ?? "");
  const referenceInstantUtc = ref(options.referenceInstantUtc ?? "");

  const fromUtc = computed(() => toUtcIso(customStartLocal.value));
  const toUtc = computed(() => toUtcIso(customEndLocal.value));

  const validationError = computed<string | null>(() => {
    if (!marketTimezone.value.trim()) {
      return "marketTimezone is required for deterministic backend temporal resolution.";
    }

    if (!referenceInstantUtc.value.trim()) {
      // Optional by contract; backend will resolve from current UTC.
    } else {
      const parsed = new Date(referenceInstantUtc.value);
      if (Number.isNaN(parsed.getTime())) {
        return "referenceInstantUtc must be a valid ISO 8601 datetime when provided.";
      }
    }

    if (!isCustomRangePreset(preset.value)) {
      return null;
    }

    if (!customStartLocal.value || !customEndLocal.value) {
      return "Custom range requires both start and end values before dispatch.";
    }

    if (!fromUtc.value || !toUtc.value) {
      return "Custom range values must be valid datetimes.";
    }

    if (fromUtc.value >= toUtc.value) {
      return "Custom range start must be earlier than custom range end.";
    }

    return null;
  });

  const temporalQuery = computed<TemporalQueryDto>(() => {
    const base: TemporalQueryDto = {
      preset: preset.value,
      marketTimezone: marketTimezone.value.trim(),
      ...(referenceInstantUtc.value.trim()
        ? {
            referenceInstantUtc: new Date(
              referenceInstantUtc.value,
            ).toISOString(),
          }
        : {}),
    };

    if (!isCustomRangePreset(preset.value)) {
      return base;
    }

    return {
      ...base,
      fromUtc: fromUtc.value ?? undefined,
      toUtc: toUtc.value ?? undefined,
    };
  });

  const requestSignature = computed(() =>
    JSON.stringify({
      preset: temporalQuery.value.preset,
      fromUtc: temporalQuery.value.fromUtc ?? null,
      toUtc: temporalQuery.value.toUtc ?? null,
      marketTimezone: temporalQuery.value.marketTimezone,
      referenceInstantUtc: temporalQuery.value.referenceInstantUtc ?? null,
    }),
  );

  function shouldResetSelectionWhenOutOfScope(
    selectedEventId: string | null,
    visibleEventIds: ReadonlySet<string>,
  ): boolean {
    if (!selectedEventId) {
      return false;
    }

    return !visibleEventIds.has(selectedEventId);
  }

  return {
    preset,
    marketTimezone,
    customStartLocal,
    customEndLocal,
    referenceInstantUtc,
    validationError,
    temporalQuery,
    requestSignature,
    shouldResetSelectionWhenOutOfScope,
  };
}

export type UseTemporalQueryState = ReturnType<typeof useTemporalQueryState>;
