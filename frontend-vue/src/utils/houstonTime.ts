/**
 * WEUP-SYNTH:
 * source=utils/dateUtils.ts
 * destination=frontend-vue/src/utils/houstonTime.ts
 * mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
 * notes=Deterministic Houston-time boundary (mission section 17). dateUtils
 * display helpers are adapted here as pure formatters taking explicit timezone
 * arguments; the non-deterministic getHoustonDate() locale-text reconstruction
 * is NOT ported. All window arithmetic takes an explicit reference instant so
 * behavior is deterministic and testable. "America/Chicago" appears exactly
 * once in this file; components must import HOUSTON_TIMEZONE from here instead
 * of scattering the literal. No local-device timezone assumptions are made
 * anywhere in this module.
 */

/** Canonical market timezone. The single definition point for Houston time. */
export const HOUSTON_TIMEZONE = "America/Chicago";

export type HoustonTimeBucket = "MORNING" | "DAY" | "EVENING" | "NIGHT" | "LATE";

export interface HoustonWeekDay {
  /** Houston calendar day key, "YYYY-MM-DD". */
  readonly key: string;
  /** 3-letter weekday name, e.g. "FRI". */
  readonly dayName: string;
  /** Display label, e.g. "SEP 29". */
  readonly label: string;
  /** Day-of-month number in Houston time. */
  readonly dayNumber: number;
}

const MS_PER_HOUR = 3_600_000;

/** Tonight window: Houston 18:00 -> 03:00 next day (9h, backend-canonical). */
const TONIGHT_WINDOW_HOURS = 9;
const TONIGHT_ANCHOR_HOUR = 18;

function pad2(value: number): string {
  return String(value).padStart(2, "0");
}

function toDate(value: string | Date): Date {
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    throw new Error(`houstonTime: invalid instant "${String(value)}"`);
  }
  return date;
}

/**
 * IANA-zone offset in milliseconds for a given instant, derived from Intl
 * parts (never from the device timezone).
 */
export function getTimezoneOffsetMs(timeZone: string, instant: Date): number {
  const formatter = new Intl.DateTimeFormat("en-US", {
    timeZone,
    hourCycle: "h23",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  });
  const parts: Record<string, string> = {};
  for (const part of formatter.formatToParts(instant)) {
    parts[part.type] = part.value;
  }
  const asUtc = Date.UTC(
    Number(parts.year),
    Number(parts.month) - 1,
    Number(parts.day),
    Number(parts.hour),
    Number(parts.minute),
    Number(parts.second),
  );
  return asUtc - instant.getTime();
}

/**
 * Convert a Houston wall-clock instant (millis since epoch read as if UTC)
 * back to a true UTC instant. Two-step refinement keeps DST transitions
 * deterministic.
 */
function houstonWallToUtc(wallMs: number, timeZone: string): number {
  const firstPass = wallMs - getTimezoneOffsetMs(timeZone, new Date(wallMs));
  return wallMs - getTimezoneOffsetMs(timeZone, new Date(firstPass));
}

/** Houston wall-clock millis for an instant. */
function houstonWallMs(instant: Date, timeZone: string): number {
  return instant.getTime() + getTimezoneOffsetMs(timeZone, instant);
}

function wallParts(wallMs: number): {
  year: number;
  month: number;
  day: number;
  hour: number;
} {
  const date = new Date(wallMs);
  return {
    year: date.getUTCFullYear(),
    month: date.getUTCMonth(),
    day: date.getUTCDate(),
    hour: date.getUTCHours(),
  };
}

/**
 * Houston calendar day key ("YYYY-MM-DD") for an instant.
 */
export function houstonDayKey(
  instant: string | Date,
  timeZone: string = HOUSTON_TIMEZONE,
): string {
  const wall = wallParts(houstonWallMs(toDate(instant), timeZone));
  return `${wall.year}-${pad2(wall.month + 1)}-${pad2(wall.day)}`;
}

/**
 * UTC instant of Houston midnight opening the day that contains `instant`.
 */
export function houstonDayStartUtc(
  instant: string | Date,
  timeZone: string = HOUSTON_TIMEZONE,
): string {
  const wall = wallParts(houstonWallMs(toDate(instant), timeZone));
  const wallMidnight = Date.UTC(wall.year, wall.month, wall.day, 0, 0, 0);
  return new Date(houstonWallToUtc(wallMidnight, timeZone)).toISOString();
}

/**
 * UTC instant of Houston midnight closing the day that contains `instant`
 * (i.e. the next Houston midnight; 23h/25h on DST transitions).
 */
export function houstonDayEndUtc(
  instant: string | Date,
  timeZone: string = HOUSTON_TIMEZONE,
): string {
  const startMs = Date.parse(houstonDayStartUtc(instant, timeZone));
  // +36h is guaranteed to land inside the following Houston day.
  return houstonDayStartUtc(new Date(startMs + 36 * MS_PER_HOUR), timeZone);
}

/**
 * Explicit UTC bounds for one Houston calendar day key ("YYYY-MM-DD").
 * Used for TODAY mode and calendar date selection.
 */
export function houstonDayBoundsUtc(
  dayKey: string,
  timeZone: string = HOUSTON_TIMEZONE,
): { startUtc: string; endUtc: string } {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(dayKey)) {
    throw new Error(`houstonTime: invalid day key "${dayKey}"`);
  }
  const [year, month, day] = dayKey.split("-").map(Number);
  const wallMidnight = Date.UTC(year, month - 1, day, 0, 0, 0);
  const startMs = houstonWallToUtc(wallMidnight, timeZone);
  const endMs = Date.parse(
    houstonDayStartUtc(new Date(startMs + 36 * MS_PER_HOUR), timeZone),
  );
  return {
    startUtc: new Date(startMs).toISOString(),
    endUtc: new Date(endMs).toISOString(),
  };
}

/**
 * Hour of day (0-23) in the given timezone for an instant.
 */
export function houstonHourOfDay(
  instant: string | Date,
  timeZone: string = HOUSTON_TIMEZONE,
): number {
  return wallParts(houstonWallMs(toDate(instant), timeZone)).hour;
}

/**
 * Upcoming 7-day strip starting at the Houston day containing `refInstant`.
 * Deterministic port of dateUtils.getUpcomingWeek (explicit instant instead of
 * the non-deterministic getHoustonDate()).
 */
export function getUpcomingWeek(
  refInstant: string | Date = new Date(),
  timeZone: string = HOUSTON_TIMEZONE,
): readonly HoustonWeekDay[] {
  // Iterate in wall-clock space (Date.UTC day arithmetic) so DST transitions
  // can neither skip nor duplicate a Houston day.
  const wall = wallParts(houstonWallMs(toDate(refInstant), timeZone));
  const weekdayFormatter = new Intl.DateTimeFormat("en-US", {
    timeZone,
    weekday: "short",
  });
  const days: HoustonWeekDay[] = [];
  for (let index = 0; index < 7; index += 1) {
    const wallMidnight = Date.UTC(wall.year, wall.month, wall.day + index, 0, 0, 0);
    const day = wallParts(wallMidnight);
    const utcMs = houstonWallToUtc(wallMidnight, timeZone);
    // Houston noon as a true UTC instant, for tz-aware formatters.
    const probe = new Date(utcMs + 12 * MS_PER_HOUR);
    days.push({
      key: `${day.year}-${pad2(day.month + 1)}-${pad2(day.day)}`,
      dayName: weekdayFormatter.format(probe).toUpperCase(),
      label: formatHoustonDayLabel(probe, timeZone),
      dayNumber: day.day,
    });
  }
  return days;
}

/**
 * Pure display formatter: "SEP 29" in the given timezone.
 * Port of dateUtils.formatHoustonDateLabel with an explicit timezone argument.
 */
export function formatHoustonDayLabel(
  date: Date,
  timeZone: string = HOUSTON_TIMEZONE,
): string {
  const formatter = new Intl.DateTimeFormat("en-US", {
    month: "short",
    day: "numeric",
    timeZone,
  });
  const parts = formatter.formatToParts(date);
  const month = parts.find((p) => p.type === "month")?.value.toUpperCase();
  const day = parts.find((p) => p.type === "day")?.value;
  return `${month} ${day}`;
}

/**
 * Pure display formatter: "21:30" (24h) in the given timezone.
 */
export function formatHoustonTimeShort(
  date: Date,
  timeZone: string = HOUSTON_TIMEZONE,
): string {
  const formatter = new Intl.DateTimeFormat("en-GB", {
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
    timeZone,
  });
  return formatter.format(date);
}

/**
 * Time-of-day bucket ported from dateUtils.getCurrentTimeBucket, taking an
 * explicit reference instant instead of reading the device clock.
 */
export function getHoustonTimeBucket(
  refInstant: string | Date = new Date(),
  timeZone: string = HOUSTON_TIMEZONE,
): HoustonTimeBucket {
  const hour = houstonHourOfDay(refInstant, timeZone);
  if (hour >= 5 && hour < 12) return "MORNING";
  if (hour >= 12 && hour < 17) return "DAY";
  if (hour >= 17 && hour < 21) return "EVENING";
  if (hour >= 21 || hour < 2) return "NIGHT";
  return "LATE";
}

/**
 * Cursor instant inside the canonical Tonight window (Houston 18:00 -> 03:00,
 * 9h) for a scrub fraction in [0, 1]. If the reference instant is before 03:00
 * the containing (prior evening's) window is used, matching the backend
 * Tonight resolution rule.
 */
export function tonightCursorUtc(
  refInstant: string | Date,
  fraction: number,
  timeZone: string = HOUSTON_TIMEZONE,
): string {
  const bounded = Math.max(0, Math.min(1, fraction));
  const wall = wallParts(houstonWallMs(toDate(refInstant), timeZone));
  let anchorYear = wall.year;
  let anchorMonth = wall.month;
  let anchorDay = wall.day;
  if (wall.hour < 3) {
    const previous = new Date(
      Date.UTC(anchorYear, anchorMonth, anchorDay) - 24 * MS_PER_HOUR,
    );
    anchorYear = previous.getUTCFullYear();
    anchorMonth = previous.getUTCMonth();
    anchorDay = previous.getUTCDate();
  }
  const anchorWallMs = Date.UTC(
    anchorYear,
    anchorMonth,
    anchorDay,
    TONIGHT_ANCHOR_HOUR,
    0,
    0,
  );
  const cursorWallMs = anchorWallMs + bounded * TONIGHT_WINDOW_HOURS * MS_PER_HOUR;
  return new Date(houstonWallToUtc(cursorWallMs, timeZone)).toISOString();
}

/**
 * Nearest TimelineControl scrub label for a Tonight-window fraction.
 * 6PM -> 0, 9PM -> 1/3, MIDNIGHT -> 2/3, 3AM -> 1.
 */
export function tonightFractionLabel(fraction: number): string {
  const bounded = Math.max(0, Math.min(1, fraction));
  const stops = ["6PM", "9PM", "MIDNIGHT", "3AM"];
  const index = Math.round(bounded * (stops.length - 1));
  return stops[Math.min(stops.length - 1, Math.max(0, index))];
}
