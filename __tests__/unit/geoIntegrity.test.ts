import {
  PUBLIC_MAP_CONFIDENCE_THRESHOLD,
  validateGeoBoundingBoxDetailed,
  validateGeoPoint,
} from "@/domains/query/contracts";

describe("geo integrity helpers", () => {
  it("classifies high-confidence coordinates as renderable", () => {
    const result = validateGeoPoint(
      { latitude: 29.7604, longitude: -95.3698 },
      { confidence: 0.9, address: "901 Bagby St, Houston, TX" },
    );

    expect(result.category).toBe("valid");
    expect(result.isRenderableOnMap).toBe(true);
    expect(result.renderingEligibility).toBe("render");
  });

  it("classifies low-confidence coordinates as moderation-only", () => {
    const result = validateGeoPoint(
      { latitude: 29.7604, longitude: -95.3698 },
      {
        confidence: PUBLIC_MAP_CONFIDENCE_THRESHOLD - 0.01,
        address: "901 Bagby St, Houston, TX",
      },
    );

    expect(result.category).toBe("valid_low_confidence");
    expect(result.isRenderableOnMap).toBe(false);
    expect(result.requiresModeration).toBe(true);
    expect(
      result.issues.some((issue) => issue.code === "LOW_LOCATION_CONFIDENCE"),
    ).toBe(true);
  });

  it("classifies missing coordinates as fallback-only", () => {
    const result = validateGeoPoint(null, {
      confidence: 0.8,
      address: "901 Bagby St, Houston, TX",
    });

    expect(result.category).toBe("missing");
    expect(result.requiresFallbackTreatment).toBe(true);
    expect(
      result.issues.some((issue) => issue.code === "MISSING_COORDINATES"),
    ).toBe(true);
  });

  it("blocks null-island coordinates", () => {
    const result = validateGeoPoint(
      { latitude: 0, longitude: 0 },
      { confidence: 0.95, address: "Unknown" },
    );

    expect(result.category).toBe("invalid");
    expect(result.renderingEligibility).toBe("block");
    expect(
      result.issues.some((issue) => issue.code === "NULL_ISLAND_COORDINATES"),
    ).toBe(true);
  });

  it("rejects wide viewport bounding boxes", () => {
    const result = validateGeoBoundingBoxDetailed({
      minLat: 29,
      maxLat: 35,
      minLng: -96,
      maxLng: -95,
    });

    expect(result.valid).toBe(false);
    expect(
      result.issues.some((issue) => issue.code === "BBOX_SPAN_TOO_WIDE"),
    ).toBe(true);
  });
});
