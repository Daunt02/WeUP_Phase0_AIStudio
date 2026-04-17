/**
 * M4-P17: Frontend DTO Contract Parity Tests
 *
 * Validates that the TypeScript interface definitions in domains/event/apiContracts.ts
 * stay in sync with the backend contract manifest (contracts/backend-contract-manifest.json).
 *
 * Strategy:
 *  1. Read the manifest JSON and extract field lists for each DTO type.
 *  2. Create sample objects typed as each DTO interface.
 *  3. Assert that the runtime keys on the sample object exactly match
 *     the manifest field list — catching drift in either direction.
 *  4. Verify that projection adapters (fromMapCardDto, fromCalendarCardDto,
 *     fromDetailDto) in projections.ts compile and produce the expected shape.
 *
 * This test runs at compile-time via TypeScript type-checking AND at runtime
 * via the key-set assertions, providing two independent guard layers.
 */

import * as fs from "fs";
import * as path from "path";
import {
  EventMapCardDto,
  EventCalendarCardDto,
  MediaRefDto,
  EventDetailDto,
  EventMergeLineageSummaryDto,
  EventModerationDto,
  EventPublishEligibilityDto,
  PublishBlockerSummaryDto,
  EligibilityConfidenceSummaryDto,
  EligibilityFieldCompletenessSummaryDto,
} from "../../domains/event/apiContracts";
import {
  fromMapCardDto,
  fromCalendarCardDto,
  fromDetailDto,
} from "../../domains/event/projections";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function loadManifest(): Record<string, string[]> {
  const candidates = [
    path.resolve(__dirname, "../../contracts/backend-contract-manifest.json"),
    path.resolve(
      __dirname,
      "../../../contracts/backend-contract-manifest.json",
    ),
  ];
  for (const p of candidates) {
    if (fs.existsSync(p)) {
      const raw = JSON.parse(fs.readFileSync(p, "utf-8")) as {
        contracts: Record<string, string[]>;
      };
      return raw.contracts;
    }
  }
  throw new Error(
    `Contract manifest not found. Tried:\n${candidates.join("\n")}`,
  );
}

function sortedKeys(obj: Record<string, unknown>): string[] {
  return Object.keys(obj).sort();
}

function assertKeysMatchManifest(
  manifest: Record<string, string[]>,
  dtoName: string,
  sample: Record<string, unknown>,
): void {
  const expected = [...(manifest[dtoName] ?? [])].sort();
  const actual = sortedKeys(sample);
  expect(actual).toEqual(expected);
}

// ---------------------------------------------------------------------------
// Fixtures — typed literals for each DTO shape (compile-time guard)
// ---------------------------------------------------------------------------

const sampleMapCard: EventMapCardDto = {
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

const sampleCalendarCard: EventCalendarCardDto = {
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

const sampleMedia: MediaRefDto = {
  url: "https://cdn.test/img.jpg",
  kind: "image",
};

const sampleDetail: EventDetailDto = {
  id: "evt-001",
  title: "Test Event",
  description: "A description.",
  venueName: "Test Venue",
  address: "123 Main St, Houston, TX",
  lat: 29.76,
  lng: -95.36,
  category: "nightlife",
  startUtc: "2024-08-01T22:00:00Z",
  endUtc: null,
  timezone: "America/Chicago",
  mediaRefs: [sampleMedia],
  tags: ["jazz"],
  status: "PUBLISHED",
  confidence: 0.9,
  sourceKind: "manual submission",
  version: 3,
  lastChangeType: null,
  concurrencyToken: "evt-001:v3",
};

const sampleMergeLineage: EventMergeLineageSummaryDto = {
  parentCanonicalEventId: "parent-001",
  mergedEventCount: 2,
  lastMergedAtUtc: "2024-07-20T12:00:00Z",
};

const sampleModeration: EventModerationDto = {
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

const sampleBlocker: PublishBlockerSummaryDto = {
  code: "LowExtractionConfidence",
  message: "Extraction confidence too low.",
  isHardBlock: true,
  field: "confidence",
};

const sampleConfidenceSummary: EligibilityConfidenceSummaryDto = {
  aggregate: 0.88,
  band: "AutoPublishable",
  extraction: 0.9,
  geocode: 0.85,
  temporal: 0.95,
  venueMatch: 0.8,
  dupeRisk: 0.92,
  meetsAutoPublishThreshold: true,
  requiresManualReview: false,
};

const sampleFieldCompleteness: EligibilityFieldCompletenessSummaryDto = {
  isComplete: true,
  missingRequiredFields: [],
  completenessRatio: 1.0,
};

const sampleEligibility: EventPublishEligibilityDto = {
  canonicalEventId: "evt-001",
  eligible: true,
  eligibilityBand: "AutoPublishable",
  recommendedAction: "AutoPublish",
  blockers: [],
  confidenceSummary: sampleConfidenceSummary,
  fieldCompleteness: sampleFieldCompleteness,
  notes: [],
};

// ---------------------------------------------------------------------------
// 1. Manifest key-set parity tests
// ---------------------------------------------------------------------------

describe("DTO Contract Parity — manifest key alignment", () => {
  let manifest: Record<string, string[]>;

  beforeAll(() => {
    manifest = loadManifest();
  });

  it("EventMapCardDto keys match manifest", () => {
    assertKeysMatchManifest(
      manifest,
      "EventMapCardDto",
      sampleMapCard as Record<string, unknown>,
    );
  });

  it("EventCalendarCardDto keys match manifest", () => {
    assertKeysMatchManifest(
      manifest,
      "EventCalendarCardDto",
      sampleCalendarCard as Record<string, unknown>,
    );
  });

  it("MediaRefDto keys match manifest", () => {
    assertKeysMatchManifest(
      manifest,
      "MediaRefDto",
      sampleMedia as Record<string, unknown>,
    );
  });

  it("EventDetailDto keys match manifest", () => {
    assertKeysMatchManifest(
      manifest,
      "EventDetailDto",
      sampleDetail as Record<string, unknown>,
    );
  });

  it("EventMergeLineageSummaryDto keys match manifest", () => {
    assertKeysMatchManifest(
      manifest,
      "EventMergeLineageSummaryDto",
      sampleMergeLineage as Record<string, unknown>,
    );
  });

  it("EventModerationDto keys match manifest", () => {
    assertKeysMatchManifest(
      manifest,
      "EventModerationDto",
      sampleModeration as Record<string, unknown>,
    );
  });

  it("PublishBlockerSummaryDto keys match manifest", () => {
    assertKeysMatchManifest(
      manifest,
      "PublishBlockerSummaryDto",
      sampleBlocker as Record<string, unknown>,
    );
  });

  it("EligibilityConfidenceSummaryDto keys match manifest", () => {
    assertKeysMatchManifest(
      manifest,
      "EligibilityConfidenceSummaryDto",
      sampleConfidenceSummary as Record<string, unknown>,
    );
  });

  it("EligibilityFieldCompletenessSummaryDto keys match manifest", () => {
    assertKeysMatchManifest(
      manifest,
      "EligibilityFieldCompletenessSummaryDto",
      sampleFieldCompleteness as Record<string, unknown>,
    );
  });

  it("EventPublishEligibilityDto keys match manifest", () => {
    assertKeysMatchManifest(
      manifest,
      "EventPublishEligibilityDto",
      sampleEligibility as Record<string, unknown>,
    );
  });
});

// ---------------------------------------------------------------------------
// 2. Manifest completeness — new DTOs must exist in manifest
// ---------------------------------------------------------------------------

describe("DTO Contract Parity — manifest completeness", () => {
  const requiredEntries = [
    "EventMapCardDto",
    "EventCalendarCardDto",
    "EventDetailDto",
    "EventModerationDto",
    "EventPublishEligibilityDto",
    "EventMergeLineageSummaryDto",
    "PublishBlockerSummaryDto",
    "EligibilityConfidenceSummaryDto",
    "EligibilityFieldCompletenessSummaryDto",
  ];

  let manifest: Record<string, string[]>;

  beforeAll(() => {
    manifest = loadManifest();
  });

  it.each(requiredEntries)("%s is registered in the manifest", (name) => {
    expect(manifest).toHaveProperty(name);
    expect((manifest[name] ?? []).length).toBeGreaterThan(0);
  });
});

// ---------------------------------------------------------------------------
// 3. Projection adapter shape tests
// ---------------------------------------------------------------------------

describe("Projection adapters — fromDto shape", () => {
  it("fromMapCardDto produces a valid projection", () => {
    const projection = fromMapCardDto(sampleMapCard);
    expect(projection.id).toBe("evt-001");
    expect(projection.title).toBe("Test Event");
    expect(projection.lat).toBe(29.76);
    expect(projection.status).toBe("PUBLISHED");
  });

  it("fromCalendarCardDto produces a valid projection", () => {
    const projection = fromCalendarCardDto(sampleCalendarCard);
    expect(projection.id).toBe("evt-001");
    expect(projection.timezone).toBe("America/Chicago");
    expect(projection.status).toBe("PUBLISHED");
  });

  it("fromDetailDto produces a valid projection", () => {
    const projection = fromDetailDto(sampleDetail);
    expect(projection.id).toBe("evt-001");
    expect(projection.description).toBe("A description.");
    expect(projection.tags).toEqual(["jazz"]);
    expect(projection.mediaRefs).toHaveLength(1);
  });

  it("fromMapCardDto and fromCalendarCardDto produce the same id", () => {
    const mapCard = fromMapCardDto(sampleMapCard);
    const calCard = fromCalendarCardDto(sampleCalendarCard);
    expect(mapCard.id).toBe(calCard.id);
  });

  it("fromDetailDto does not expose internal fields", () => {
    const projection = fromDetailDto(sampleDetail) as Record<string, unknown>;
    expect(Object.keys(projection)).not.toContain("provenance");
    expect(Object.keys(projection)).not.toContain("sourceEventIds");
    expect(Object.keys(projection)).not.toContain("mergeLineage");
    expect(Object.keys(projection)).not.toContain("moderationStatus");
    expect(Object.keys(projection)).not.toContain("riskLevel");
  });
});
