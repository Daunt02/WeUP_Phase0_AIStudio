/**
 * M8-P38: Frontend Timezone Display Contract v1.0.
 *
 * Semantics:
 * - startUtc/endUtc are canonical UTC instants (ISO 8601 with trailing Z).
 * - marketTimezone/eventTimezone provide projection context for user-visible labels.
 * - Browser timezone/locale are presentation choices only, never event semantics.
 */

export type EventTemporalPrecision =
  | "exact_utc"
  | "projected_from_market_timezone"
  | "date_only_with_timezone_context"
  | "ambiguous_or_incomplete";

export interface EventTimezoneDisplayModel {
  /** Canonical event start instant in UTC (must end with Z). */
  readonly startUtc: string;
  /** Canonical event end instant in UTC when known (must end with Z). */
  readonly endUtc?: string | null;
  /** Market timezone used for map/calendar/filter/detail parity (IANA). */
  readonly marketTimezone: string;
  /** Optional event-specific timezone override (IANA). */
  readonly eventTimezone?: string | null;
  /** Source of timezone context used for projection/audit labels. */
  readonly timezoneSource?: "event" | "market" | "fallback";
  /** Precision metadata from backend/ingestion normalization. */
  readonly precision?: EventTemporalPrecision;
  /** Unresolved ambiguity codes from ingestion when precision is incomplete. */
  readonly unresolvedAmbiguities?: readonly string[];
}

export interface EventTimeFormatOptions {
  /**
   * Locale only affects language/number formatting, not event-time semantics.
   * Defaults to en-US for deterministic snapshots and tests.
   */
  readonly locale?: string;
  /** Include short timezone name in output labels, e.g. "CDT". */
  readonly includeTimezoneName?: boolean;
}

export interface FormattedEventTime {
  readonly startDateLabel: string;
  readonly startTimeLabel: string;
  readonly endTimeLabel: string | null;
  readonly rangeLabel: string;
  readonly timezoneLabel: string;
  readonly isApproximate: boolean;
}

/**
 * Validates and normalizes display input before formatting.
 * Throws if UTC fields are not canonical ISO UTC strings (with trailing Z).
 */
export function normalizeEventTimezoneDisplayModel(
  model: EventTimezoneDisplayModel,
): EventTimezoneDisplayModel {
  assertUtcIso(model.startUtc, "startUtc");

  if (model.endUtc) {
    assertUtcIso(model.endUtc, "endUtc");
    if (new Date(model.endUtc).getTime() < new Date(model.startUtc).getTime()) {
      throw new Error("endUtc must be greater than or equal to startUtc");
    }
  }

  if (!model.marketTimezone || model.marketTimezone.trim().length === 0) {
    throw new Error("marketTimezone is required");
  }

  return {
    ...model,
    marketTimezone: model.marketTimezone.trim(),
    eventTimezone: model.eventTimezone?.trim() ?? null,
    timezoneSource: model.timezoneSource ?? "market",
    precision: model.precision ?? "exact_utc",
    unresolvedAmbiguities: model.unresolvedAmbiguities ?? [],
  };
}

/**
 * Formats canonical UTC values into market/event-local labels.
 *
 * Important:
 * - Never parse local wall-clock strings here.
 * - Never infer semantics from browser timezone.
 * - Always use explicit projection timezone from the model.
 */
export function formatEventTimeForDisplay(
  rawModel: EventTimezoneDisplayModel,
  options: EventTimeFormatOptions = {},
): FormattedEventTime {
  const model = normalizeEventTimezoneDisplayModel(rawModel);
  const timezone = model.eventTimezone || model.marketTimezone;
  const locale = options.locale ?? "en-US";

  const start = new Date(model.startUtc);
  const end = model.endUtc ? new Date(model.endUtc) : null;

  const dateFormatter = new Intl.DateTimeFormat(locale, {
    timeZone: timezone,
    month: "short",
    day: "numeric",
    year: "numeric",
  });

  const timeFormatter = new Intl.DateTimeFormat(locale, {
    timeZone: timezone,
    hour: "numeric",
    minute: "2-digit",
    hour12: true,
    timeZoneName: options.includeTimezoneName ? "short" : undefined,
  });

  const tzFormatter = new Intl.DateTimeFormat(locale, {
    timeZone: timezone,
    hour: "numeric",
    timeZoneName: "short",
  });

  const startDateLabel = dateFormatter.format(start);
  const startTimeLabel = timeFormatter.format(start);
  const endTimeLabel = end ? timeFormatter.format(end) : null;
  const timezoneLabel = extractTimezoneLabel(tzFormatter, start, timezone);
  const rangeLabel = endTimeLabel
    ? `${startTimeLabel} - ${endTimeLabel}`
    : startTimeLabel;

  return {
    startDateLabel,
    startTimeLabel,
    endTimeLabel,
    rangeLabel,
    timezoneLabel,
    isApproximate:
      model.precision === "date_only_with_timezone_context" ||
      model.precision === "ambiguous_or_incomplete",
  };
}

function assertUtcIso(value: string, fieldName: string): void {
  if (!value.endsWith("Z")) {
    throw new Error(
      `${fieldName} must be an ISO 8601 UTC string ending with 'Z'`,
    );
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    throw new Error(`${fieldName} must be a valid ISO 8601 UTC string`);
  }
}

function extractTimezoneLabel(
  formatter: Intl.DateTimeFormat,
  at: Date,
  fallback: string,
): string {
  const timezonePart = formatter
    .formatToParts(at)
    .find((part) => part.type === "timeZoneName")?.value;

  return timezonePart && timezonePart.length > 0 ? timezonePart : fallback;
}

/**
 * Example 1:
 *   startUtc = "2026-05-01T01:00:00Z", marketTimezone = "America/Chicago"
 *   => startDateLabel "Apr 30, 2026", startTimeLabel "8:00 PM"
 *
 * Example 2:
 *   startUtc = "2026-05-01T01:00:00Z", marketTimezone = "America/New_York"
 *   => startDateLabel "Apr 30, 2026", startTimeLabel "9:00 PM"
 */
