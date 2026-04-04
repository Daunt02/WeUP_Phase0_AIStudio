/**
 * Analytics Service Tests
 * P23: Product Analytics
 */

import * as analyticsService from '@/services/analyticsService';

describe('analyticsService', () => {
  beforeEach(() => {
    global.fetch = jest.fn();
    jest.spyOn(console, 'warn').mockImplementation(() => {});
  });

  afterEach(() => {
    jest.clearAllMocks();
    jest.restoreAllMocks();
  });

  describe('recordEvent', () => {
    it('should record an analytics event', async () => {
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
      });

      await analyticsService.recordEvent('MapViewed', 'user123', { region: 'sf' });

      expect(global.fetch).toHaveBeenCalledWith(
        '/api/analytics/events',
        expect.objectContaining({
          method: 'POST',
          headers: expect.objectContaining({
            'Content-Type': 'application/json',
          }),
        })
      );
    });

    it('should not throw on network errors', async () => {
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: false,
        status: 500,
        statusText: 'Server Error',
      });

      // Should not throw — graceful degradation
      await expect(
        analyticsService.recordEvent('MapViewed')
      ).resolves.toBeUndefined();
    });

    it('should handle missing user ID', async () => {
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
      });

      await analyticsService.recordEvent('MapViewed');

      const call = (global.fetch as jest.Mock).mock.calls[0];
      const body = JSON.parse(call[1].body);
      expect(body.userId).toBeUndefined();
    });
  });

  describe('getEventTypes', () => {
    it('should fetch available event types', async () => {
      const mockResponse = {
        eventTypes: [
          { value: 0, name: 'MapViewed' },
          { value: 1, name: 'EventSaved' },
        ],
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => mockResponse,
      });

      const result = await analyticsService.getEventTypes();
      expect(result.eventTypes).toHaveLength(2);
    });
  });

  describe('AnalyticsEventType', () => {
    it('should define all event types', () => {
      expect(analyticsService.AnalyticsEventType.MapViewed).toBe('MapViewed');
      expect(analyticsService.AnalyticsEventType.EventSaved).toBe('EventSaved');
      expect(analyticsService.AnalyticsEventType.TemporalPresetSelected).toBe(
        'TemporalPresetSelected'
      );
    });
  });
});
