/**
 * Temporal Service Tests
 * P21: Temporal Query Logic
 */

import * as temporalService from "@/services/temporalService";

describe("temporalService", () => {
  beforeEach(() => {
    // Mock fetch
    global.fetch = jest.fn();
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  describe("getTemporalPresets", () => {
    it("should fetch and return temporal presets", async () => {
      const mockResponse = {
        presets: [
          { value: 0, name: "Today", label: "TODAY" },
          { value: 1, name: "Tonight", label: "TONIGHT" },
          { value: 2, name: "Weekend", label: "WEEKEND" },
        ],
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => mockResponse,
      });

      const result = await temporalService.getTemporalPresets();
      expect(result.presets).toHaveLength(3);
      expect(result.presets[0].label).toBe("TODAY");
    });

    it("should handle fetch errors", async () => {
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: false,
        statusText: "Internal Server Error",
      });

      await expect(temporalService.getTemporalPresets()).rejects.toThrow();
    });
  });

  describe("getEventsAtTime", () => {
    it("should accept valid temporal preset requests", async () => {
      const mockResponse = {
        preset: "Today",
        presetLabel: "TODAY",
        timeWindowStart: "2026-04-04T12:00:00Z",
        timeWindowEnd: "2026-04-04T13:00:00Z",
        timezone: "America/Los_Angeles",
        events: [],
        count: 0,
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => mockResponse,
      });

      const result = await temporalService.getEventsAtTime({
        preset: "Today",
        marketTimezone: "America/Los_Angeles",
      });

      expect(result.presetLabel).toBe("TODAY");
      expect(result.timezone).toBe("America/Los_Angeles");
    });

    it("should send correct request payload", async () => {
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          preset: "Today",
          presetLabel: "TODAY",
          timeWindowStart: "2026-04-04T12:00:00Z",
          timeWindowEnd: "2026-04-04T13:00:00Z",
          timezone: "America/Los_Angeles",
          events: [],
          count: 0,
        }),
      });

      await temporalService.getEventsAtTime({
        preset: "Today",
        marketTimezone: "America/Los_Angeles",
      });

      expect(global.fetch).toHaveBeenCalledWith(
        "/api/temporal/events-at-time",
        expect.objectContaining({
          method: "POST",
          headers: expect.objectContaining({
            "Content-Type": "application/json",
          }),
        }),
      );
    });
  });

  describe("PRESET_LABELS", () => {
    it("should provide correct preset labels", () => {
      expect(temporalService.PRESET_LABELS.Today).toBe("TODAY");
      expect(temporalService.PRESET_LABELS.Tonight).toBe("TONIGHT");
      expect(temporalService.PRESET_LABELS.Weekend).toBe("WEEKEND");
    });
  });
});
