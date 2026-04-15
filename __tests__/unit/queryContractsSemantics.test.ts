import {
  resolveTemporalPreset,
  validateBoundingBox,
  normalizeTimeWindow,
  type TimeWindow,
} from "@/domains/query/contracts";

describe("query contract semantics", () => {
  describe("validateBoundingBox", () => {
    it("rejects latitude or longitude spans above 5 degrees", () => {
      const result = validateBoundingBox({
        minLat: 10,
        maxLat: 16,
        minLng: 20,
        maxLng: 21,
      });

      expect(result.valid).toBe(false);
      expect(
        result.errors.some((e) => e.includes("span exceeds 5 degrees")),
      ).toBe(true);
    });

    it("rejects area above 8 square degrees", () => {
      const result = validateBoundingBox({
        minLat: 29,
        maxLat: 31,
        minLng: -96,
        maxLng: -91,
      });

      expect(result.valid).toBe(false);
      expect(
        result.errors.some((e) => e.includes("area exceeds 8 square degrees")),
      ).toBe(true);
    });

    it("accepts valid, bounded viewport", () => {
      const result = validateBoundingBox({
        minLat: 29.68,
        maxLat: 29.84,
        minLng: -95.46,
        maxLng: -95.25,
      });

      expect(result.valid).toBe(true);
      expect(result.errors).toHaveLength(0);
    });
  });

  describe("resolveTemporalPreset", () => {
    const timezone = "America/Chicago";
    const now = "2026-04-15T12:00:00.000Z";

    it("resolves TODAY into a market-local day window", () => {
      const window = resolveTemporalPreset("TODAY", timezone, now);
      expect(window.timezone).toBe(timezone);
      expect(
        new Date(window.endUtc).getTime() - new Date(window.startUtc).getTime(),
      ).toBe(24 * 60 * 60 * 1000);
    });

    it("resolves TONIGHT into a nine-hour nightlife window", () => {
      const window = resolveTemporalPreset("TONIGHT", timezone, now);
      expect(window.timezone).toBe(timezone);
      expect(
        new Date(window.endUtc).getTime() - new Date(window.startUtc).getTime(),
      ).toBe(9 * 60 * 60 * 1000);
    });

    it("resolves WEEKEND into Friday-evening to Monday-start window", () => {
      const window = resolveTemporalPreset("WEEKEND", timezone, now);
      expect(window.timezone).toBe(timezone);
      expect(
        new Date(window.endUtc).getTime() - new Date(window.startUtc).getTime(),
      ).toBe(54 * 60 * 60 * 1000);
    });

    it("normalizes CUSTOM windows through canonical validator", () => {
      const custom: TimeWindow = {
        startUtc: "2026-04-16T00:00:00Z",
        endUtc: "2026-04-16T03:00:00Z",
        timezone,
      };

      const resolved = resolveTemporalPreset("CUSTOM", timezone, now, custom);
      expect(resolved).toEqual(normalizeTimeWindow(custom));
    });
  });
});
