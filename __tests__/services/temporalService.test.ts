/**
 * Temporal Service Tests
 * P21: Temporal Query Logic
 */

import * as temporalService from '@/services/temporalService';

describe('temporalService', () => {
  beforeEach(() => {
    // Mock fetch
    global.fetch = jest.fn();
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  describe('getTemporalPresets', () => {
    it('should fetch and return temporal presets', async () => {
      const mockResponse = {
        presets: [
          { value: 0, name: 'NOW', label: 'NOW' },
          { value: 1, name: 'Evening6PM', label: '6PM' },
          { value: 2, name: 'Evening9PM', label: '9PM' },
        ],
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => mockResponse,
      });

      const result = await temporalService.getTemporalPresets();
      expect(result.presets).toHaveLength(3);
      expect(result.presets[0].label).toBe('NOW');
    });

    it('should handle fetch errors', async () => {
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: false,
        statusText: 'Internal Server Error',
      });

      await expect(temporalService.getTemporalPresets()).rejects.toThrow();
    });
  });

  describe('getEventsAtTime', () => {
    it('should accept valid temporal preset requests', async () => {
      const mockResponse = {
        preset: 'NOW',
        presetLabel: 'NOW',
        timeWindowStart: '2026-04-04T12:00:00Z',
        timeWindowEnd: '2026-04-04T13:00:00Z',
        timezone: 'America/Los_Angeles',
        events: [],
        count: 0,
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => mockResponse,
      });

      const result = await temporalService.getEventsAtTime({
        preset: 'NOW',
        marketTimezone: 'America/Los_Angeles',
      });

      expect(result.presetLabel).toBe('NOW');
      expect(result.timezone).toBe('America/Los_Angeles');
    });

    it('should send correct request payload', async () => {
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          preset: 'NOW',
          presetLabel: 'NOW',
          timeWindowStart: '2026-04-04T12:00:00Z',
          timeWindowEnd: '2026-04-04T13:00:00Z',
          timezone: 'America/Los_Angeles',
          events: [],
          count: 0,
        }),
      });

      await temporalService.getEventsAtTime({
        preset: 'NOW',
        marketTimezone: 'America/Los_Angeles',
      });

      expect(global.fetch).toHaveBeenCalledWith(
        '/api/temporal/events-at-time',
        expect.objectContaining({
          method: 'POST',
          headers: expect.objectContaining({
            'Content-Type': 'application/json',
          }),
        })
      );
    });
  });

  describe('PRESET_LABELS', () => {
    it('should provide correct preset labels', () => {
      expect(temporalService.PRESET_LABELS.NOW).toBe('NOW');
      expect(temporalService.PRESET_LABELS.Evening6PM).toBe('6PM');
      expect(temporalService.PRESET_LABELS.Midnight).toBe('MIDNIGHT');
    });
  });
});
