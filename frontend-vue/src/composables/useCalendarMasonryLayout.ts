import { computed, type ComputedRef, type Ref } from "vue";
import type { CalendarEventItemDto } from "../contracts/calendar-overlay.contracts";

export type DensityLevel = "sparse" | "normal" | "dense" | "overloaded";

export interface CalendarMasonryLayoutOptions {
  readonly bucketMinutes?: number;
  readonly maxColumnsPerBucket?: number;
  readonly denseThreshold?: number;
  readonly overloadedThreshold?: number;
  readonly maxVisibleInDenseBucket?: number;
  readonly maxVisibleInOverloadedBucket?: number;
  readonly defaultEventDurationMinutes?: number;
}

export interface PackedCalendarEvent {
  readonly eventId: string;
  readonly source: CalendarEventItemDto;
  readonly startUtc: string;
  readonly endUtc: string;
  readonly startMs: number;
  readonly endMs: number;
  readonly startMinuteOfDay: number;
  readonly durationMinutes: number;
  readonly columnIndex: number;
  readonly rowSpan: number;
  readonly displayTimeLabel: string;
  readonly secondaryLabel: string;
}

export interface CalendarTimeBucket {
  readonly bucketKey: string;
  readonly bucketStartUtc: string;
  readonly bucketEndUtc: string;
  readonly displayLabel: string;
  readonly slotIndex: number;
  readonly densityLevel: DensityLevel;
  readonly totalCount: number;
  readonly visibleCount: number;
  readonly overflowCount: number;
  readonly maxColumnsUsed: number;
  readonly visibleEvents: PackedCalendarEvent[];
  readonly overflowEvents: PackedCalendarEvent[];
}

export interface CalendarDayBucket {
  readonly dayKey: string;
  readonly displayLabel: string;
  readonly totalCount: number;
  readonly buckets: CalendarTimeBucket[];
}

export interface CalendarMasonryLayoutModel {
  readonly totalCount: number;
  readonly dayBuckets: CalendarDayBucket[];
}

const DEFAULT_OPTIONS: Required<CalendarMasonryLayoutOptions> = {
  bucketMinutes: 60,
  maxColumnsPerBucket: 4,
  denseThreshold: 8,
  overloadedThreshold: 12,
  maxVisibleInDenseBucket: 8,
  maxVisibleInOverloadedBucket: 6,
  defaultEventDurationMinutes: 90,
};

function pad2(value: number): string {
  return String(value).padStart(2, "0");
}

function toDayKey(ms: number): string {
  const date = new Date(ms);
  return `${date.getUTCFullYear()}-${pad2(date.getUTCMonth() + 1)}-${pad2(date.getUTCDate())}`;
}

function toUtcIso(ms: number): string {
  return new Date(ms).toISOString();
}

function formatDayLabel(ms: number): string {
  return new Intl.DateTimeFormat(undefined, {
    weekday: "short",
    month: "short",
    day: "numeric",
  }).format(new Date(ms));
}

function formatTimeLabel(ms: number): string {
  return new Intl.DateTimeFormat(undefined, {
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date(ms));
}

function clampRowSpan(durationMinutes: number): number {
  if (durationMinutes <= 45) {
    return 8;
  }
  if (durationMinutes <= 90) {
    return 11;
  }
  if (durationMinutes <= 180) {
    return 14;
  }
  if (durationMinutes <= 300) {
    return 18;
  }
  return 22;
}

function resolveEndMs(
  startMs: number,
  endUtc: string | null,
  defaultDurationMinutes: number,
): number {
  if (!endUtc) {
    return startMs + defaultDurationMinutes * 60_000;
  }

  const parsedEnd = Date.parse(endUtc);
  if (!Number.isFinite(parsedEnd) || parsedEnd <= startMs) {
    return startMs + defaultDurationMinutes * 60_000;
  }

  return parsedEnd;
}

function normalizeAndSort(
  items: readonly CalendarEventItemDto[],
  options: Required<CalendarMasonryLayoutOptions>,
): PackedCalendarEvent[] {
  const normalized = items.map((item) => {
    const startMs = Date.parse(item.startUtc);
    const safeStartMs = Number.isFinite(startMs)
      ? startMs
      : Date.parse("1970-01-01T00:00:00Z");
    const endMs = resolveEndMs(
      safeStartMs,
      item.endUtc,
      options.defaultEventDurationMinutes,
    );
    const startDate = new Date(safeStartMs);
    const startMinuteOfDay =
      startDate.getUTCHours() * 60 + startDate.getUTCMinutes();
    const durationMinutes = Math.max(
      1,
      Math.round((endMs - safeStartMs) / 60_000),
    );

    return {
      eventId: item.eventId,
      source: item,
      startUtc: item.startUtc,
      endUtc: toUtcIso(endMs),
      startMs: safeStartMs,
      endMs,
      startMinuteOfDay,
      durationMinutes,
      columnIndex: 0,
      rowSpan: clampRowSpan(durationMinutes),
      displayTimeLabel: item.endUtc
        ? `${formatTimeLabel(safeStartMs)} - ${formatTimeLabel(endMs)}`
        : `${formatTimeLabel(safeStartMs)}`,
      secondaryLabel: item.district
        ? `${item.venueName} • ${item.district}`
        : item.venueName,
    } satisfies PackedCalendarEvent;
  });

  normalized.sort((left, right) => {
    const startDiff = left.startMs - right.startMs;
    if (startDiff !== 0) {
      return startDiff;
    }

    const endDiff = left.endMs - right.endMs;
    if (endDiff !== 0) {
      return endDiff;
    }

    return left.eventId.localeCompare(right.eventId);
  });

  return normalized;
}

function packColumns(events: readonly PackedCalendarEvent[]): {
  packed: PackedCalendarEvent[];
  maxColumnsUsed: number;
} {
  const columnEndTimes: number[] = [];
  let maxColumnsUsed = 0;

  const packed = events.map((event) => {
    let chosenColumn = columnEndTimes.findIndex(
      (endMs) => endMs <= event.startMs,
    );

    if (chosenColumn === -1) {
      chosenColumn = columnEndTimes.length;
      columnEndTimes.push(event.endMs);
    } else {
      columnEndTimes[chosenColumn] = event.endMs;
    }

    maxColumnsUsed = Math.max(maxColumnsUsed, chosenColumn + 1);

    return {
      ...event,
      columnIndex: chosenColumn,
    };
  });

  return { packed, maxColumnsUsed };
}

function resolveDensityLevel(
  eventCount: number,
  options: Required<CalendarMasonryLayoutOptions>,
): DensityLevel {
  if (eventCount >= options.overloadedThreshold) {
    return "overloaded";
  }

  if (eventCount >= options.denseThreshold) {
    return "dense";
  }

  if (eventCount <= 2) {
    return "sparse";
  }

  return "normal";
}

function visibleLimitForDensity(
  densityLevel: DensityLevel,
  options: Required<CalendarMasonryLayoutOptions>,
): number {
  if (densityLevel === "overloaded") {
    return options.maxVisibleInOverloadedBucket;
  }

  if (densityLevel === "dense") {
    return options.maxVisibleInDenseBucket;
  }

  return Number.MAX_SAFE_INTEGER;
}

function toBucketLabel(slotStartMs: number, slotEndMs: number): string {
  return `${formatTimeLabel(slotStartMs)} - ${formatTimeLabel(slotEndMs)}`;
}

export function computeCalendarMasonryLayout(
  items: readonly CalendarEventItemDto[],
  options: CalendarMasonryLayoutOptions = {},
): CalendarMasonryLayoutModel {
  const resolved = {
    ...DEFAULT_OPTIONS,
    ...options,
  };

  const normalized = normalizeAndSort(items, resolved);
  const dayBucketMap = new Map<
    string,
    {
      dayStartMs: number;
      events: PackedCalendarEvent[];
    }
  >();

  for (const event of normalized) {
    const dayKey = toDayKey(event.startMs);
    const dayStartMs = Date.parse(`${dayKey}T00:00:00.000Z`);
    const existing = dayBucketMap.get(dayKey);

    if (!existing) {
      dayBucketMap.set(dayKey, {
        dayStartMs,
        events: [event],
      });
      continue;
    }

    existing.events.push(event);
  }

  const dayBuckets: CalendarDayBucket[] = [...dayBucketMap.entries()]
    .sort((left, right) => left[0].localeCompare(right[0]))
    .map(([dayKey, dayData]) => {
      const slotMap = new Map<number, PackedCalendarEvent[]>();

      for (const event of dayData.events) {
        const slotIndex = Math.floor(
          event.startMinuteOfDay / resolved.bucketMinutes,
        );
        const existing = slotMap.get(slotIndex);

        if (!existing) {
          slotMap.set(slotIndex, [event]);
          continue;
        }

        existing.push(event);
      }

      const buckets: CalendarTimeBucket[] = [...slotMap.entries()]
        .sort((left, right) => left[0] - right[0])
        .map(([slotIndex, slotEvents]) => {
          const sortedInSlot = [...slotEvents].sort((left, right) => {
            const startDiff = left.startMs - right.startMs;
            if (startDiff !== 0) {
              return startDiff;
            }

            const endDiff = left.endMs - right.endMs;
            if (endDiff !== 0) {
              return endDiff;
            }

            return left.eventId.localeCompare(right.eventId);
          });

          const packedSlot = packColumns(sortedInSlot);
          const densityLevel = resolveDensityLevel(
            sortedInSlot.length,
            resolved,
          );
          const visibleLimit = visibleLimitForDensity(densityLevel, resolved);

          // Keep semantics deterministic: first trim by chronology, then cap columns for click target clarity.
          const visible = packedSlot.packed
            .filter((event) => event.columnIndex < resolved.maxColumnsPerBucket)
            .slice(0, visibleLimit);

          const visibleIds = new Set(visible.map((event) => event.eventId));
          const overflow = packedSlot.packed.filter(
            (event) => !visibleIds.has(event.eventId),
          );

          const slotStartMinute = slotIndex * resolved.bucketMinutes;
          const slotStartMs = dayData.dayStartMs + slotStartMinute * 60_000;
          const slotEndMs = slotStartMs + resolved.bucketMinutes * 60_000;

          return {
            bucketKey: `${dayKey}:${slotIndex}`,
            bucketStartUtc: toUtcIso(slotStartMs),
            bucketEndUtc: toUtcIso(slotEndMs),
            displayLabel: toBucketLabel(slotStartMs, slotEndMs),
            slotIndex,
            densityLevel,
            totalCount: packedSlot.packed.length,
            visibleCount: visible.length,
            overflowCount: overflow.length,
            maxColumnsUsed: packedSlot.maxColumnsUsed,
            visibleEvents: visible,
            overflowEvents: overflow,
          } satisfies CalendarTimeBucket;
        });

      return {
        dayKey,
        displayLabel: formatDayLabel(dayData.dayStartMs),
        totalCount: dayData.events.length,
        buckets,
      } satisfies CalendarDayBucket;
    });

  return {
    totalCount: normalized.length,
    dayBuckets,
  };
}

export function useCalendarMasonryLayout(
  items: Ref<readonly CalendarEventItemDto[]>,
  options: CalendarMasonryLayoutOptions = {},
): ComputedRef<CalendarMasonryLayoutModel> {
  return computed(() => computeCalendarMasonryLayout(items.value, options));
}
