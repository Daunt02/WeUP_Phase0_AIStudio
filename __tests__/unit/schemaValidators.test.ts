/**
 * M4-P19: Frontend Schema Validator Tests
 *
 * Verifies that each DTO shape validator in domains/event/schemaValidators.ts
 * correctly accepts valid payloads and rejects violating ones with the
 * appropriate ruleCode and field path.
 *
 * These tests are CI-ready: they run in-process with Jest, have no I/O
 * dependencies, and emit structured failure output via SchemaValidationError.
 */

import {
  validateMapCardDto,
  validateCalendarCardDto,
  validateDetailDto,
  validateModerationDto,
  validatePublishEligibilityDto,
  validateMapFeedItems,
  validateCalendarFeedItems,
  assertValidMapCardDto,
  assertValidDetailDto,
  SchemaValidationError,
  type SchemaValidationResult,
} from "../../domains/event/schemaValidators";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function expectValid(result: SchemaValidationResult): void {
  if (!result.valid) {
    const detail = result.violations
      .map((v) => `  [${v.ruleCode}] ${v.field}: ${v.message}`)
      .join("\n");
    throw new Error(`Expected valid but got violations:\n${detail}`);
  }
}

function expectViolation(
  result: SchemaValidationResult,
  ruleCode: string,
  field?: string,
): void {
  expect(result.valid).toBe(false);
  const match = result.violations.find(
    (v) =>
      v.ruleCode === ruleCode && (field === undefined || v.field === field),
  );
  if (!match) {
    const found = result.violations
      .map((v) => `[${v.ruleCode}] ${v.field}`)
      .join(", ");
    throw new Error(
      `Expected violation ruleCode="${ruleCode}"${field ? ` field="${field}"` : ""} not found. Found: ${found}`,
    );
  }
}

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const validMapCard = {
  id: "evt-001",
  title: "Test Event",
  venueName: "Test Venue",
  category: "nightlife",
  lat: 29.76,
  lng: -95.36,
  thumbnailUrl: null,
  status: "PUBLISHED",
  confidence: 0.92,
};

const validCalendarCard = {
  id: "evt-001",
  title: "Test Event",
  venueName: "Test Venue",
  category: "nightlife",
  startUtc: "2024-08-01T22:00:00Z",
  endUtc: "2024-08-02T02:00:00Z",
  timezone: "America/Chicago",
  thumbnailUrl: null,
  status: "PUBLISHED",
};

const validDetail = {
  id: "evt-001",
  title: "Test Event",
  description: "A description.",
  venueName: "Test Venue",
  address: "123 Main St, Houston, TX",
  lat: 29.76,
  lng: -95.36,
  category: "nightlife",
  startUtc: "2024-08-01T22:00:00Z",
  endUtc: "2024-08-02T02:00:00Z",
  timezone: "America/Chicago",
  mediaRefs: [{ url: "https://cdn.test/img.jpg", kind: "image" }],
  tags: ["jazz"],
  status: "PUBLISHED",
  confidence: 0.9,
  sourceKind: "manual submission",
  version: 3,
  lastChangeType: null,
  concurrencyToken: "evt-001:v3",
};

const validModeration = {
  canonicalEventId: "evt-001",
  title: "Test Event",
  category: "nightlife",
  venueName: "Test Venue",
  startUtc: "2024-08-01T22:00:00Z",
  endUtc: null,
  timezone: "America/Chicago",
  lifecycleStatus: "Published",
  moderationStatus: "Approved",
  publishStatus: "Published",
  riskLevel: "Low",
  confidenceScore: 0.92,
  version: 3,
  updatedAtUtc: "2024-07-30T08:00:00Z",
  lastChangeType: null,
  hasPendingReview: false,
  concurrencyToken: "evt-001:v3",
  mergeLineage: null,
};

const validEligibility = {
  canonicalEventId: "evt-001",
  eligible: true,
  eligibilityBand: "AutoPublishable",
  recommendedAction: "AutoPublish",
  blockers: [],
  confidenceSummary: {
    aggregate: 0.88,
    band: "High",
    extraction: 0.9,
    geocode: 0.85,
    temporal: 0.95,
    venueMatch: 0.8,
    dupeRisk: 0.92,
    meetsAutoPublishThreshold: true,
    requiresManualReview: false,
  },
  fieldCompleteness: {
    isComplete: true,
    missingRequiredFields: [],
    completenessRatio: 1.0,
  },
  notes: [],
};

// ---------------------------------------------------------------------------
// 1. EventMapCardDto
// ---------------------------------------------------------------------------

describe("validateMapCardDto — valid payloads", () => {
  it("accepts a fully-valid map card", () => {
    expectValid(validateMapCardDto(validMapCard));
  });

  it("accepts all valid frontend status values", () => {
    for (const status of [
      "DRAFT",
      "NEEDS_REVIEW",
      "APPROVED",
      "PUBLISHED",
      "REJECTED",
      "ARCHIVED",
    ]) {
      expectValid(validateMapCardDto({ ...validMapCard, status }));
    }
  });

  it("accepts null thumbnailUrl", () => {
    expectValid(validateMapCardDto({ ...validMapCard, thumbnailUrl: null }));
  });
});

describe("validateMapCardDto — required field violations", () => {
  it("rejects missing id", () => {
    expectViolation(
      validateMapCardDto({ ...validMapCard, id: "" }),
      "REQUIRED_FIELD_EMPTY",
      "id",
    );
  });

  it("rejects missing title", () => {
    expectViolation(
      validateMapCardDto({ ...validMapCard, title: "" }),
      "REQUIRED_FIELD_EMPTY",
      "title",
    );
  });

  it("rejects missing venueName", () => {
    expectViolation(
      validateMapCardDto({ ...validMapCard, venueName: "" }),
      "REQUIRED_FIELD_EMPTY",
      "venueName",
    );
  });

  it("rejects missing category", () => {
    expectViolation(
      validateMapCardDto({ ...validMapCard, category: "" }),
      "REQUIRED_FIELD_EMPTY",
      "category",
    );
  });

  it("rejects invalid status", () => {
    expectViolation(
      validateMapCardDto({ ...validMapCard, status: "published" }),
      "INVALID_ENUM_VALUE",
      "status",
    );
  });

  it("rejects confidence > 1", () => {
    expectViolation(
      validateMapCardDto({ ...validMapCard, confidence: 1.1 }),
      "INVALID_CONFIDENCE",
      "confidence",
    );
  });

  it("rejects confidence < 0", () => {
    expectViolation(
      validateMapCardDto({ ...validMapCard, confidence: -0.01 }),
      "INVALID_CONFIDENCE",
      "confidence",
    );
  });

  it("rejects latitude out of range", () => {
    expectViolation(
      validateMapCardDto({ ...validMapCard, lat: 91 }),
      "INVALID_LATITUDE",
      "lat",
    );
  });

  it("rejects longitude out of range", () => {
    expectViolation(
      validateMapCardDto({ ...validMapCard, lng: 181 }),
      "INVALID_LONGITUDE",
      "lng",
    );
  });

  it("warns on null-island coordinates (0, 0)", () => {
    expectViolation(
      validateMapCardDto({ ...validMapCard, lat: 0, lng: 0 }),
      "NULL_ISLAND_COORDINATES",
      "lat",
    );
  });

  it("rejects non-object input", () => {
    expectViolation(validateMapCardDto(null), "NOT_OBJECT");
    expectViolation(validateMapCardDto("string"), "NOT_OBJECT");
  });
});

// ---------------------------------------------------------------------------
// 2. EventCalendarCardDto
// ---------------------------------------------------------------------------

describe("validateCalendarCardDto — valid payloads", () => {
  it("accepts a fully-valid calendar card", () => {
    expectValid(validateCalendarCardDto(validCalendarCard));
  });

  it("accepts null endUtc", () => {
    expectValid(
      validateCalendarCardDto({ ...validCalendarCard, endUtc: null }),
    );
  });
});

describe("validateCalendarCardDto — violations", () => {
  it("rejects invalid startUtc", () => {
    expectViolation(
      validateCalendarCardDto({ ...validCalendarCard, startUtc: "not-a-date" }),
      "INVALID_ISO_UTC",
      "startUtc",
    );
  });

  it("rejects endUtc before startUtc", () => {
    expectViolation(
      validateCalendarCardDto({
        ...validCalendarCard,
        startUtc: "2024-08-02T02:00:00Z",
        endUtc: "2024-08-01T22:00:00Z",
      }),
      "END_BEFORE_START",
      "endUtc",
    );
  });

  it("rejects empty timezone", () => {
    expectViolation(
      validateCalendarCardDto({ ...validCalendarCard, timezone: "" }),
      "REQUIRED_FIELD_EMPTY",
      "timezone",
    );
  });

  it("rejects invalid status (lowercase)", () => {
    expectViolation(
      validateCalendarCardDto({ ...validCalendarCard, status: "published" }),
      "INVALID_ENUM_VALUE",
      "status",
    );
  });
});

// ---------------------------------------------------------------------------
// 3. EventDetailDto
// ---------------------------------------------------------------------------

describe("validateDetailDto — valid payloads", () => {
  it("accepts a fully-valid detail DTO", () => {
    expectValid(validateDetailDto(validDetail));
  });

  it("accepts null description", () => {
    expectValid(validateDetailDto({ ...validDetail, description: null }));
  });

  it("accepts null lastChangeType", () => {
    expectValid(validateDetailDto({ ...validDetail, lastChangeType: null }));
  });

  it("accepts valid lastChangeType string", () => {
    expectValid(
      validateDetailDto({ ...validDetail, lastChangeType: "StatusTransition" }),
    );
  });

  it("accepts null concurrencyToken", () => {
    expectValid(validateDetailDto({ ...validDetail, concurrencyToken: null }));
  });

  it("accepts empty mediaRefs array", () => {
    expectValid(validateDetailDto({ ...validDetail, mediaRefs: [] }));
  });

  it("accepts empty tags array", () => {
    expectValid(validateDetailDto({ ...validDetail, tags: [] }));
  });
});

describe("validateDetailDto — violations", () => {
  it("rejects missing address", () => {
    expectViolation(
      validateDetailDto({ ...validDetail, address: "" }),
      "REQUIRED_FIELD_EMPTY",
      "address",
    );
  });

  it("rejects null mediaRefs (must be array)", () => {
    expectViolation(
      validateDetailDto({ ...validDetail, mediaRefs: null }),
      "EXPECTED_ARRAY",
      "mediaRefs",
    );
  });

  it("rejects null tags (must be array)", () => {
    expectViolation(
      validateDetailDto({ ...validDetail, tags: null }),
      "EXPECTED_ARRAY",
      "tags",
    );
  });

  it("rejects version = 0", () => {
    expectViolation(
      validateDetailDto({ ...validDetail, version: 0 }),
      "INVALID_POSITIVE_INT",
      "version",
    );
  });

  it("rejects version < 0", () => {
    expectViolation(
      validateDetailDto({ ...validDetail, version: -1 }),
      "INVALID_POSITIVE_INT",
      "version",
    );
  });

  it("rejects invalid lastChangeType string", () => {
    expectViolation(
      validateDetailDto({ ...validDetail, lastChangeType: "UNKNOWN_TYPE" }),
      "INVALID_ENUM_VALUE",
      "lastChangeType",
    );
  });

  it("rejects missing sourceKind", () => {
    expectViolation(
      validateDetailDto({ ...validDetail, sourceKind: "" }),
      "REQUIRED_FIELD_EMPTY",
      "sourceKind",
    );
  });

  it("rejects confidence NaN", () => {
    expectViolation(
      validateDetailDto({ ...validDetail, confidence: NaN }),
      "INVALID_CONFIDENCE",
      "confidence",
    );
  });

  it("rejects null-island coordinates", () => {
    expectViolation(
      validateDetailDto({ ...validDetail, lat: 0, lng: 0 }),
      "NULL_ISLAND_COORDINATES",
      "lat",
    );
  });
});

// ---------------------------------------------------------------------------
// 4. EventModerationDto
// ---------------------------------------------------------------------------

describe("validateModerationDto — valid payloads", () => {
  it("accepts a fully-valid moderation DTO", () => {
    expectValid(validateModerationDto(validModeration));
  });

  it("accepts null endUtc", () => {
    expectValid(validateModerationDto({ ...validModeration, endUtc: null }));
  });

  it("accepts all valid lifecycleStatus values", () => {
    for (const s of [
      "Draft",
      "Candidate",
      "Reviewed",
      "Approved",
      "Rejected",
      "Published",
      "Cancelled",
      "Archived",
    ]) {
      expectValid(
        validateModerationDto({ ...validModeration, lifecycleStatus: s }),
      );
    }
  });

  it("accepts all valid moderationStatus values", () => {
    for (const s of ["Unreviewed", "InReview", "Approved", "Rejected"]) {
      expectValid(
        validateModerationDto({ ...validModeration, moderationStatus: s }),
      );
    }
  });

  it("accepts all valid publishStatus values", () => {
    for (const s of [
      "NotEligible",
      "EligibilityPending",
      "Eligible",
      "Published",
      "Unpublished",
      "Archived",
    ]) {
      expectValid(
        validateModerationDto({ ...validModeration, publishStatus: s }),
      );
    }
  });

  it("accepts all valid riskLevel values", () => {
    for (const s of ["Low", "Medium", "High", "Restricted"]) {
      expectValid(validateModerationDto({ ...validModeration, riskLevel: s }));
    }
  });
});

describe("validateModerationDto — violations", () => {
  it("rejects missing canonicalEventId", () => {
    expectViolation(
      validateModerationDto({ ...validModeration, canonicalEventId: "" }),
      "REQUIRED_FIELD_EMPTY",
      "canonicalEventId",
    );
  });

  it("rejects invalid lifecycleStatus (uppercase frontend format)", () => {
    // Moderation uses PascalCase, not uppercase
    expectViolation(
      validateModerationDto({
        ...validModeration,
        lifecycleStatus: "PUBLISHED",
      }),
      "INVALID_ENUM_VALUE",
      "lifecycleStatus",
    );
  });

  it("rejects invalid moderationStatus", () => {
    expectViolation(
      validateModerationDto({
        ...validModeration,
        moderationStatus: "PendingReview",
      }),
      "INVALID_ENUM_VALUE",
      "moderationStatus",
    );
  });

  it("rejects invalid riskLevel", () => {
    expectViolation(
      validateModerationDto({ ...validModeration, riskLevel: "Critical" }),
      "INVALID_ENUM_VALUE",
      "riskLevel",
    );
  });

  it("rejects non-boolean hasPendingReview", () => {
    expectViolation(
      validateModerationDto({ ...validModeration, hasPendingReview: "false" }),
      "EXPECTED_BOOLEAN",
      "hasPendingReview",
    );
  });

  it("rejects version 0", () => {
    expectViolation(
      validateModerationDto({ ...validModeration, version: 0 }),
      "INVALID_POSITIVE_INT",
      "version",
    );
  });

  it("rejects invalid updatedAtUtc", () => {
    expectViolation(
      validateModerationDto({ ...validModeration, updatedAtUtc: "not-a-date" }),
      "INVALID_ISO_UTC",
      "updatedAtUtc",
    );
  });

  it("rejects invalid lastChangeType string when non-null", () => {
    expectViolation(
      validateModerationDto({ ...validModeration, lastChangeType: "INVALID" }),
      "INVALID_ENUM_VALUE",
      "lastChangeType",
    );
  });
});

// ---------------------------------------------------------------------------
// 5. EventPublishEligibilityDto
// ---------------------------------------------------------------------------

describe("validatePublishEligibilityDto — valid payloads", () => {
  it("accepts a fully-valid eligibility DTO", () => {
    expectValid(validatePublishEligibilityDto(validEligibility));
  });
});

describe("validatePublishEligibilityDto — violations", () => {
  it("rejects invalid eligibilityBand", () => {
    expectViolation(
      validatePublishEligibilityDto({
        ...validEligibility,
        eligibilityBand: "UnknownBand",
      }),
      "INVALID_ENUM_VALUE",
      "eligibilityBand",
    );
  });

  it("rejects invalid recommendedAction", () => {
    expectViolation(
      validatePublishEligibilityDto({
        ...validEligibility,
        recommendedAction: "DoNothing",
      }),
      "INVALID_ENUM_VALUE",
      "recommendedAction",
    );
  });

  it("rejects null blockers (must be array)", () => {
    expectViolation(
      validatePublishEligibilityDto({ ...validEligibility, blockers: null }),
      "EXPECTED_ARRAY",
      "blockers",
    );
  });

  it("rejects null confidenceSummary", () => {
    expectViolation(
      validatePublishEligibilityDto({
        ...validEligibility,
        confidenceSummary: null,
      }),
      "EXPECTED_OBJECT",
      "confidenceSummary",
    );
  });

  it("rejects confidenceSummary.aggregate out of range", () => {
    expectViolation(
      validatePublishEligibilityDto({
        ...validEligibility,
        confidenceSummary: {
          ...validEligibility.confidenceSummary,
          aggregate: 1.5,
        },
      }),
      "INVALID_CONFIDENCE",
      "confidenceSummary.aggregate",
    );
  });
});

// ---------------------------------------------------------------------------
// 6. Batch validators
// ---------------------------------------------------------------------------

describe("validateMapFeedItems", () => {
  it("passes for valid items array", () => {
    expectValid(
      validateMapFeedItems([validMapCard, { ...validMapCard, id: "evt-002" }]),
    );
  });

  it("reports per-item violations prefixed with index", () => {
    const result = validateMapFeedItems([
      validMapCard,
      { ...validMapCard, id: "", status: "bad-status" },
    ]);
    expect(result.valid).toBe(false);
    expect(result.violations.some((v) => v.field.startsWith("[1]."))).toBe(
      true,
    );
    // item 0 should be clean — no violations from it
    expect(result.violations.every((v) => !v.field.startsWith("[0]."))).toBe(
      true,
    );
  });
});

describe("validateCalendarFeedItems", () => {
  it("passes for valid items array", () => {
    expectValid(validateCalendarFeedItems([validCalendarCard]));
  });

  it("reports per-item violations", () => {
    const result = validateCalendarFeedItems([
      { ...validCalendarCard, id: "" },
    ]);
    expect(result.valid).toBe(false);
    expect(result.violations[0].field).toBe("[0].id");
  });
});

// ---------------------------------------------------------------------------
// 7. assertValid* throwing wrappers
// ---------------------------------------------------------------------------

describe("assertValid* wrappers", () => {
  it("assertValidMapCardDto does not throw for valid input", () => {
    expect(() => assertValidMapCardDto(validMapCard)).not.toThrow();
  });

  it("assertValidMapCardDto throws SchemaValidationError for invalid input", () => {
    expect(() => assertValidMapCardDto({ ...validMapCard, id: "" })).toThrow(
      SchemaValidationError,
    );
    expect(() => assertValidMapCardDto({ ...validMapCard, id: "" })).toThrow(
      /REQUIRED_FIELD_EMPTY/,
    );
  });

  it("assertValidDetailDto throws SchemaValidationError for invalid input", () => {
    expect(() => assertValidDetailDto({ ...validDetail, version: 0 })).toThrow(
      SchemaValidationError,
    );
    expect(() => assertValidDetailDto({ ...validDetail, version: 0 })).toThrow(
      /INVALID_POSITIVE_INT/,
    );
  });
});

// ---------------------------------------------------------------------------
// 8. Canonical identity consistency
// ---------------------------------------------------------------------------

describe("canonical identity alignment", () => {
  it("id field is present and non-empty on all frontend-safe card DTOs", () => {
    expectValid(validateMapCardDto(validMapCard));
    expectValid(validateCalendarCardDto(validCalendarCard));
    expectValid(validateDetailDto(validDetail));
    // All have same id — cross-surface identity check
    expect(validMapCard.id).toBe(validCalendarCard.id);
    expect(validCalendarCard.id).toBe(validDetail.id);
  });

  it("moderation DTO uses canonicalEventId aligned with frontend id", () => {
    expectValid(validateModerationDto(validModeration));
    expect(validModeration.canonicalEventId).toBe(validMapCard.id);
  });
});
