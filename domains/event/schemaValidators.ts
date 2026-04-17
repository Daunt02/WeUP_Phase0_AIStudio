/**
 * WeUP Phase 0 — M4-P19: Runtime Schema Validators
 *
 * Provides deterministic, production-grade runtime validation for all
 * canonical event API DTO surfaces. These validators enforce the same rules
 * documented in docs/schema-contract-validation-v1.md.
 *
 * DESIGN PRINCIPLES:
 *   - All validators return a SchemaValidationResult (no throws) so callers
 *     can decide throw vs log vs metric.
 *   - assertValid*() wrappers DO throw — use them in dev/test entry points.
 *   - Every rule has a stable `ruleCode` for observability and CI output.
 *   - No silent fallbacks or lenient coercions — invalid = invalid.
 *
 * USAGE:
 *   import { validateMapCardDto, assertValidMapCardDto } from '@/domains/event/schemaValidators';
 *
 *   // Non-throwing (for middleware, service layer):
 *   const result = validateMapCardDto(apiResponse);
 *   if (!result.valid) { logger.warn(result); return; }
 *
 *   // Throwing (for tests and dev assertions):
 *   assertValidMapCardDto(apiResponse); // throws SchemaValidationError on failure
 */

import type {
  EventMapCardDto,
  EventCalendarCardDto,
  EventDetailDto,
  EventModerationDto,
  EventPublishEligibilityDto,
} from "./apiContracts";

// ---------------------------------------------------------------------------
// Core types
// ---------------------------------------------------------------------------

export interface SchemaViolation {
  /** Dot-path of the offending field (e.g. "lat", "address.city"). */
  readonly field: string;
  /** Stable rule identifier for CI output and observability. */
  readonly ruleCode: string;
  /** Human-readable description for developer visibility. */
  readonly message: string;
}

export interface SchemaValidationResult {
  readonly valid: boolean;
  readonly violations: SchemaViolation[];
  readonly dtoType: string;
}

export class SchemaValidationError extends Error {
  readonly violations: SchemaViolation[];
  readonly dtoType: string;
  constructor(result: SchemaValidationResult) {
    const summary = result.violations
      .map((v) => `[${v.ruleCode}] ${v.field}: ${v.message}`)
      .join("; ");
    super(`Schema validation failed for ${result.dtoType}: ${summary}`);
    this.name = "SchemaValidationError";
    this.violations = result.violations;
    this.dtoType = result.dtoType;
  }
}

// ---------------------------------------------------------------------------
// Valid enum value sets (must match backend serialization rules exactly)
// ---------------------------------------------------------------------------

/** Frontend-visible lifecycle statuses (uppercase, per EventDtoMapper.SerializeLifecycleStatus). */
const VALID_FRONTEND_STATUSES = new Set([
  "DRAFT",
  "NEEDS_REVIEW",
  "APPROVED",
  "PUBLISHED",
  "REJECTED",
  "ARCHIVED",
]);

/** PascalCase lifecycle statuses for operational DTOs (matches C# enum names). */
const VALID_LIFECYCLE_STATUSES = new Set([
  "Draft",
  "Candidate",
  "Reviewed",
  "Approved",
  "Rejected",
  "Published",
  "Cancelled",
  "Archived",
]);

const VALID_MODERATION_STATUSES = new Set([
  "Unreviewed",
  "InReview",
  "Approved",
  "Rejected",
]);

const VALID_PUBLISH_STATUSES = new Set([
  "NotEligible",
  "EligibilityPending",
  "Eligible",
  "Published",
  "Unpublished",
  "Archived",
]);

const VALID_RISK_LEVELS = new Set(["Low", "Medium", "High", "Restricted"]);

const VALID_ELIGIBILITY_BANDS = new Set([
  "AutoPublishable",
  "ManualReviewRequired",
  "Blocked",
]);

const VALID_RECOMMENDED_ACTIONS = new Set([
  "AutoPublish",
  "RouteToManualReview",
  "BlockPublish",
]);

const VALID_CHANGE_TYPES = new Set([
  "MinorMetadataUpdate",
  "MaterialEventChange",
  "StatusTransition",
  "MergeLineageUpdate",
]);

// ---------------------------------------------------------------------------
// Internal rule builders
// ---------------------------------------------------------------------------

class ViolationCollector {
  private readonly _list: SchemaViolation[] = [];

  requireNonEmpty(field: string, value: unknown): this {
    if (
      value === null ||
      value === undefined ||
      (typeof value === "string" && value.trim() === "")
    ) {
      this._list.push({
        field,
        ruleCode: "REQUIRED_FIELD_EMPTY",
        message: `${field} is required and must not be empty.`,
      });
    }
    return this;
  }

  requireString(field: string, value: unknown): this {
    if (typeof value !== "string") {
      this._list.push({
        field,
        ruleCode: "EXPECTED_STRING",
        message: `${field} must be a string, got ${typeof value}.`,
      });
    }
    return this;
  }

  requireNumber(field: string, value: unknown): this {
    if (typeof value !== "number" || isNaN(value)) {
      this._list.push({
        field,
        ruleCode: "EXPECTED_NUMBER",
        message: `${field} must be a finite number, got ${value}.`,
      });
    }
    return this;
  }

  requireBoolean(field: string, value: unknown): this {
    if (typeof value !== "boolean") {
      this._list.push({
        field,
        ruleCode: "EXPECTED_BOOLEAN",
        message: `${field} must be a boolean, got ${typeof value}.`,
      });
    }
    return this;
  }

  requireArray(field: string, value: unknown): this {
    if (!Array.isArray(value)) {
      this._list.push({
        field,
        ruleCode: "EXPECTED_ARRAY",
        message: `${field} must be an array (never null), got ${typeof value}.`,
      });
    }
    return this;
  }

  requireLatitude(field: string, value: unknown): this {
    if (
      typeof value !== "number" ||
      isNaN(value) ||
      value < -90 ||
      value > 90
    ) {
      this._list.push({
        field,
        ruleCode: "INVALID_LATITUDE",
        message: `${field} must be a number in [-90, 90], got ${value}.`,
      });
    }
    return this;
  }

  requireLongitude(field: string, value: unknown): this {
    if (
      typeof value !== "number" ||
      isNaN(value) ||
      value < -180 ||
      value > 180
    ) {
      this._list.push({
        field,
        ruleCode: "INVALID_LONGITUDE",
        message: `${field} must be a number in [-180, 180], got ${value}.`,
      });
    }
    return this;
  }

  requireConfidence(field: string, value: unknown): this {
    if (typeof value !== "number" || isNaN(value) || value < 0 || value > 1) {
      this._list.push({
        field,
        ruleCode: "INVALID_CONFIDENCE",
        message: `${field} must be a number in [0, 1], got ${value}.`,
      });
    }
    return this;
  }

  requireEnumValue(field: string, value: unknown, validSet: Set<string>): this {
    if (typeof value !== "string" || !validSet.has(value)) {
      this._list.push({
        field,
        ruleCode: "INVALID_ENUM_VALUE",
        message: `${field} must be one of [${[...validSet].join(", ")}], got "${value}".`,
      });
    }
    return this;
  }

  requireIsoUtcString(field: string, value: unknown): this {
    if (typeof value !== "string" || !isValidIsoUtc(value)) {
      this._list.push({
        field,
        ruleCode: "INVALID_ISO_UTC",
        message: `${field} must be a valid ISO 8601 UTC string, got "${value}".`,
      });
    }
    return this;
  }

  requirePositiveInt(field: string, value: unknown): this {
    if (typeof value !== "number" || !Number.isInteger(value) || value < 1) {
      this._list.push({
        field,
        ruleCode: "INVALID_POSITIVE_INT",
        message: `${field} must be an integer >= 1, got ${value}.`,
      });
    }
    return this;
  }

  /**
   * Validates that endUtc (if non-null) is >= startUtc.
   * Requires that startUtc has already been validated as a valid ISO string.
   */
  requireEndUtcAfterStart(
    startField: string,
    startUtc: string,
    endField: string,
    endUtc: string | null,
  ): this {
    if (endUtc === null) return this;
    if (!isValidIsoUtc(startUtc) || !isValidIsoUtc(endUtc)) return this;
    if (new Date(endUtc) < new Date(startUtc)) {
      this._list.push({
        field: endField,
        ruleCode: "END_BEFORE_START",
        message: `${endField} (${endUtc}) must not be before ${startField} (${startUtc}).`,
      });
    }
    return this;
  }

  /**
   * Checks that the coordinate is not exactly (0, 0) — the null-island sentinel.
   * Warns (violation) rather than blowing up for 0,0 coordinates.
   */
  warnNullIsland(
    latField: string,
    lat: unknown,
    lngField: string,
    lng: unknown,
  ): this {
    if (lat === 0 && lng === 0) {
      this._list.push({
        field: latField,
        ruleCode: "NULL_ISLAND_COORDINATES",
        message: `Coordinates (${latField}=0, ${lngField}=0) indicate a null-island sentinel — event location is unresolved.`,
      });
    }
    return this;
  }

  requireNullableString(field: string, value: unknown): this {
    if (value !== null && typeof value !== "string") {
      this._list.push({
        field,
        ruleCode: "EXPECTED_STRING_OR_NULL",
        message: `${field} must be a string or null, got ${typeof value}.`,
      });
    }
    return this;
  }

  requireObject(field: string, value: unknown): this {
    if (value === null || typeof value !== "object") {
      this._list.push({
        field,
        ruleCode: "EXPECTED_OBJECT",
        message: `${field} must be an object, got ${value === null ? "null" : typeof value}.`,
      });
    }
    return this;
  }

  build(): SchemaViolation[] {
    return [...this._list];
  }
}

function isValidIsoUtc(value: string): boolean {
  if (!value) return false;
  const d = new Date(value);
  return !isNaN(d.getTime());
}

// ---------------------------------------------------------------------------
// 1. EventMapCardDto validator
// ---------------------------------------------------------------------------

/**
 * Validates a map-card DTO from the POST /api/events/map feed response.
 *
 * Rules:
 *   REQUIRED_FIELD_EMPTY  — id, title, venueName, category must be non-empty strings
 *   INVALID_LATITUDE      — lat ∈ [-90, 90]
 *   INVALID_LONGITUDE     — lng ∈ [-180, 180]
 *   NULL_ISLAND_COORDINATES — (0,0) indicates unresolved location
 *   INVALID_ENUM_VALUE    — status must be a valid frontend lifecycle string
 *   INVALID_CONFIDENCE    — confidence ∈ [0, 1]
 */
export function validateMapCardDto(dto: unknown): SchemaValidationResult {
  const dtoType = "EventMapCardDto";
  if (dto === null || typeof dto !== "object") {
    return {
      valid: false,
      dtoType,
      violations: [
        {
          field: "(root)",
          ruleCode: "NOT_OBJECT",
          message: "Expected an object.",
        },
      ],
    };
  }
  const d = dto as Record<string, unknown>;
  const v = new ViolationCollector();
  v.requireNonEmpty("id", d.id);
  v.requireNonEmpty("title", d.title);
  v.requireNonEmpty("venueName", d.venueName);
  v.requireNonEmpty("category", d.category);
  v.requireLatitude("lat", d.lat);
  v.requireLongitude("lng", d.lng);
  v.warnNullIsland("lat", d.lat, "lng", d.lng);
  v.requireEnumValue("status", d.status, VALID_FRONTEND_STATUSES);
  v.requireConfidence("confidence", d.confidence);
  // thumbnailUrl: nullable string
  v.requireNullableString("thumbnailUrl", d.thumbnailUrl);
  const violations = v.build();
  return { valid: violations.length === 0, violations, dtoType };
}

export function assertValidMapCardDto(
  dto: unknown,
): asserts dto is EventMapCardDto {
  const result = validateMapCardDto(dto);
  if (!result.valid) throw new SchemaValidationError(result);
}

// ---------------------------------------------------------------------------
// 2. EventCalendarCardDto validator
// ---------------------------------------------------------------------------

/**
 * Validates a calendar-card DTO from the POST /api/events/calendar feed response.
 *
 * Rules (in addition to base identity/location):
 *   INVALID_ISO_UTC       — startUtc must be valid ISO 8601 UTC
 *   END_BEFORE_START      — endUtc (if set) must be >= startUtc
 *   REQUIRED_FIELD_EMPTY  — timezone must be non-empty
 *   INVALID_ENUM_VALUE    — status must be a valid frontend lifecycle string
 */
export function validateCalendarCardDto(dto: unknown): SchemaValidationResult {
  const dtoType = "EventCalendarCardDto";
  if (dto === null || typeof dto !== "object") {
    return {
      valid: false,
      dtoType,
      violations: [
        {
          field: "(root)",
          ruleCode: "NOT_OBJECT",
          message: "Expected an object.",
        },
      ],
    };
  }
  const d = dto as Record<string, unknown>;
  const v = new ViolationCollector();
  v.requireNonEmpty("id", d.id);
  v.requireNonEmpty("title", d.title);
  v.requireNonEmpty("venueName", d.venueName);
  v.requireNonEmpty("category", d.category);
  v.requireIsoUtcString("startUtc", d.startUtc);
  if (d.endUtc !== null) {
    v.requireIsoUtcString("endUtc", d.endUtc);
  }
  if (
    typeof d.startUtc === "string" &&
    (d.endUtc === null || typeof d.endUtc === "string")
  ) {
    v.requireEndUtcAfterStart(
      "startUtc",
      d.startUtc as string,
      "endUtc",
      d.endUtc as string | null,
    );
  }
  v.requireNonEmpty("timezone", d.timezone);
  v.requireEnumValue("status", d.status, VALID_FRONTEND_STATUSES);
  v.requireNullableString("thumbnailUrl", d.thumbnailUrl);
  const violations = v.build();
  return { valid: violations.length === 0, violations, dtoType };
}

export function assertValidCalendarCardDto(
  dto: unknown,
): asserts dto is EventCalendarCardDto {
  const result = validateCalendarCardDto(dto);
  if (!result.valid) throw new SchemaValidationError(result);
}

// ---------------------------------------------------------------------------
// 3. EventDetailDto validator
// ---------------------------------------------------------------------------

/**
 * Validates a detail DTO from the GET /api/events/{id} endpoint.
 *
 * Rules (in addition to calendar rules):
 *   REQUIRED_FIELD_EMPTY  — address, sourceKind must be non-empty strings
 *   INVALID_LATITUDE/LONGITUDE — coordinates in valid ranges
 *   NULL_ISLAND_COORDINATES — (0,0) unresolved location
 *   EXPECTED_ARRAY       — mediaRefs and tags must be arrays (never null)
 *   INVALID_CONFIDENCE   — confidence ∈ [0, 1]
 *   INVALID_POSITIVE_INT — version >= 1
 *   EXPECTED_STRING_OR_NULL — lastChangeType, concurrencyToken, description nullable
 *   INVALID_ENUM_VALUE   — lastChangeType (if non-null) must be a valid change type
 */
export function validateDetailDto(dto: unknown): SchemaValidationResult {
  const dtoType = "EventDetailDto";
  if (dto === null || typeof dto !== "object") {
    return {
      valid: false,
      dtoType,
      violations: [
        {
          field: "(root)",
          ruleCode: "NOT_OBJECT",
          message: "Expected an object.",
        },
      ],
    };
  }
  const d = dto as Record<string, unknown>;
  const v = new ViolationCollector();
  v.requireNonEmpty("id", d.id);
  v.requireNonEmpty("title", d.title);
  v.requireNonEmpty("venueName", d.venueName);
  v.requireNonEmpty("category", d.category);
  v.requireNonEmpty("address", d.address);
  v.requireLatitude("lat", d.lat);
  v.requireLongitude("lng", d.lng);
  v.warnNullIsland("lat", d.lat, "lng", d.lng);
  v.requireIsoUtcString("startUtc", d.startUtc);
  if (d.endUtc !== null) {
    v.requireIsoUtcString("endUtc", d.endUtc);
  }
  if (
    typeof d.startUtc === "string" &&
    (d.endUtc === null || typeof d.endUtc === "string")
  ) {
    v.requireEndUtcAfterStart(
      "startUtc",
      d.startUtc as string,
      "endUtc",
      d.endUtc as string | null,
    );
  }
  v.requireNonEmpty("timezone", d.timezone);
  v.requireArray("mediaRefs", d.mediaRefs);
  v.requireArray("tags", d.tags);
  v.requireEnumValue("status", d.status, VALID_FRONTEND_STATUSES);
  v.requireConfidence("confidence", d.confidence);
  v.requireNonEmpty("sourceKind", d.sourceKind);
  v.requirePositiveInt("version", d.version);
  v.requireNullableString("description", d.description);
  v.requireNullableString("concurrencyToken", d.concurrencyToken);
  // lastChangeType: nullable but if set must be a valid change type string
  if (d.lastChangeType !== null && d.lastChangeType !== undefined) {
    v.requireEnumValue("lastChangeType", d.lastChangeType, VALID_CHANGE_TYPES);
  } else {
    v.requireNullableString("lastChangeType", d.lastChangeType);
  }
  const violations = v.build();
  return { valid: violations.length === 0, violations, dtoType };
}

export function assertValidDetailDto(
  dto: unknown,
): asserts dto is EventDetailDto {
  const result = validateDetailDto(dto);
  if (!result.valid) throw new SchemaValidationError(result);
}

// ---------------------------------------------------------------------------
// 4. EventModerationDto validator (OPERATIONAL)
// ---------------------------------------------------------------------------

/**
 * Validates a moderation-view DTO from the moderation endpoints.
 *
 * Rules:
 *   REQUIRED_FIELD_EMPTY  — canonicalEventId, title, category, venueName, timezone
 *   INVALID_ISO_UTC       — startUtc, updatedAtUtc valid ISO strings
 *   END_BEFORE_START      — endUtc (if set) >= startUtc
 *   INVALID_ENUM_VALUE    — lifecycleStatus, moderationStatus, publishStatus, riskLevel
 *   INVALID_ENUM_VALUE    — lastChangeType (if non-null) must be valid change type
 *   INVALID_CONFIDENCE    — confidenceScore ∈ [0, 1]
 *   INVALID_POSITIVE_INT  — version >= 1
 *   EXPECTED_BOOLEAN      — hasPendingReview must be boolean
 */
export function validateModerationDto(dto: unknown): SchemaValidationResult {
  const dtoType = "EventModerationDto";
  if (dto === null || typeof dto !== "object") {
    return {
      valid: false,
      dtoType,
      violations: [
        {
          field: "(root)",
          ruleCode: "NOT_OBJECT",
          message: "Expected an object.",
        },
      ],
    };
  }
  const d = dto as Record<string, unknown>;
  const v = new ViolationCollector();
  v.requireNonEmpty("canonicalEventId", d.canonicalEventId);
  v.requireNonEmpty("title", d.title);
  v.requireNonEmpty("category", d.category);
  v.requireNonEmpty("venueName", d.venueName);
  v.requireIsoUtcString("startUtc", d.startUtc);
  if (d.endUtc !== null) {
    v.requireIsoUtcString("endUtc", d.endUtc);
  }
  if (
    typeof d.startUtc === "string" &&
    (d.endUtc === null || typeof d.endUtc === "string")
  ) {
    v.requireEndUtcAfterStart(
      "startUtc",
      d.startUtc as string,
      "endUtc",
      d.endUtc as string | null,
    );
  }
  v.requireNonEmpty("timezone", d.timezone);
  v.requireEnumValue(
    "lifecycleStatus",
    d.lifecycleStatus,
    VALID_LIFECYCLE_STATUSES,
  );
  v.requireEnumValue(
    "moderationStatus",
    d.moderationStatus,
    VALID_MODERATION_STATUSES,
  );
  v.requireEnumValue("publishStatus", d.publishStatus, VALID_PUBLISH_STATUSES);
  v.requireEnumValue("riskLevel", d.riskLevel, VALID_RISK_LEVELS);
  v.requireConfidence("confidenceScore", d.confidenceScore);
  v.requirePositiveInt("version", d.version);
  v.requireIsoUtcString("updatedAtUtc", d.updatedAtUtc);
  v.requireBoolean("hasPendingReview", d.hasPendingReview);
  v.requireNullableString("lastChangeType", d.lastChangeType);
  if (d.lastChangeType !== null && d.lastChangeType !== undefined) {
    v.requireEnumValue("lastChangeType", d.lastChangeType, VALID_CHANGE_TYPES);
  }
  v.requireNullableString("concurrencyToken", d.concurrencyToken);
  const violations = v.build();
  return { valid: violations.length === 0, violations, dtoType };
}

export function assertValidModerationDto(
  dto: unknown,
): asserts dto is EventModerationDto {
  const result = validateModerationDto(dto);
  if (!result.valid) throw new SchemaValidationError(result);
}

// ---------------------------------------------------------------------------
// 5. EventPublishEligibilityDto validator (OPERATIONAL)
// ---------------------------------------------------------------------------

/**
 * Validates the publish eligibility DTO from GET /api/events/{id}/publish-eligibility.
 *
 * Rules:
 *   REQUIRED_FIELD_EMPTY  — canonicalEventId
 *   EXPECTED_BOOLEAN      — eligible
 *   INVALID_ENUM_VALUE    — eligibilityBand, recommendedAction
 *   EXPECTED_ARRAY        — blockers, notes
 *   EXPECTED_OBJECT       — confidenceSummary, fieldCompleteness
 *   INVALID_CONFIDENCE    — confidenceSummary.aggregate ∈ [0, 1]
 */
export function validatePublishEligibilityDto(
  dto: unknown,
): SchemaValidationResult {
  const dtoType = "EventPublishEligibilityDto";
  if (dto === null || typeof dto !== "object") {
    return {
      valid: false,
      dtoType,
      violations: [
        {
          field: "(root)",
          ruleCode: "NOT_OBJECT",
          message: "Expected an object.",
        },
      ],
    };
  }
  const d = dto as Record<string, unknown>;
  const v = new ViolationCollector();
  v.requireNonEmpty("canonicalEventId", d.canonicalEventId);
  v.requireBoolean("eligible", d.eligible);
  v.requireEnumValue(
    "eligibilityBand",
    d.eligibilityBand,
    VALID_ELIGIBILITY_BANDS,
  );
  v.requireEnumValue(
    "recommendedAction",
    d.recommendedAction,
    VALID_RECOMMENDED_ACTIONS,
  );
  v.requireArray("blockers", d.blockers);
  v.requireArray("notes", d.notes);
  v.requireObject("confidenceSummary", d.confidenceSummary);
  if (d.confidenceSummary !== null && typeof d.confidenceSummary === "object") {
    const cs = d.confidenceSummary as Record<string, unknown>;
    v.requireConfidence("confidenceSummary.aggregate", cs.aggregate);
  }
  v.requireObject("fieldCompleteness", d.fieldCompleteness);
  const violations = v.build();
  return { valid: violations.length === 0, violations, dtoType };
}

export function assertValidPublishEligibilityDto(
  dto: unknown,
): asserts dto is EventPublishEligibilityDto {
  const result = validatePublishEligibilityDto(dto);
  if (!result.valid) throw new SchemaValidationError(result);
}

// ---------------------------------------------------------------------------
// 6. Batch validator — validate all DTOs in a map feed or calendar feed response
// ---------------------------------------------------------------------------

/**
 * Validate every item in a map feed events array.
 * Returns a combined result listing all item-level violations with [index] prefix on field.
 */
export function validateMapFeedItems(items: unknown[]): SchemaValidationResult {
  const dtoType = "MapFeedResponse.events[]";
  const allViolations: SchemaViolation[] = [];
  items.forEach((item, i) => {
    const result = validateMapCardDto(item);
    if (!result.valid) {
      for (const v of result.violations) {
        allViolations.push({ ...v, field: `[${i}].${v.field}` });
      }
    }
  });
  return {
    valid: allViolations.length === 0,
    violations: allViolations,
    dtoType,
  };
}

/**
 * Validate every item in a calendar feed items array.
 */
export function validateCalendarFeedItems(
  items: unknown[],
): SchemaValidationResult {
  const dtoType = "CalendarFeedResponse.items[]";
  const allViolations: SchemaViolation[] = [];
  items.forEach((item, i) => {
    const result = validateCalendarCardDto(item);
    if (!result.valid) {
      for (const v of result.violations) {
        allViolations.push({ ...v, field: `[${i}].${v.field}` });
      }
    }
  });
  return {
    valid: allViolations.length === 0,
    violations: allViolations,
    dtoType,
  };
}
