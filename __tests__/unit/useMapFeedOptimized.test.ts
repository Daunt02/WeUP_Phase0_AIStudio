/**
 * useMapFeedOptimized.test.ts — Unit Tests
 *
 * Tests debounce, caching, diffing, state transitions, and instrumentation
 * for the optimized map feed hook.
 *
 * Run with: npm test -- useMapFeedOptimized.test.ts
 */

import { renderHook, act, waitFor } from "@testing-library/react";
import { useMapFeedOptimized } from "../useMapFeedOptimized";
import type { GeoBoundingBox, MapFeedQuery } from "@/domains/query/contracts";
import * as eventService from "@/services/eventService";

// ============================================================================
// Mocks
// ============================================================================

jest.mock("@/services/eventService");

const mockEventService = eventService as jest.Mocked<typeof eventService>;

const defaultBounds: GeoBoundingBox = {
  minLng: -95.4,
  minLat: 29.6,
  maxLng: -95.1,
  maxLat: 29.9,
};

const defaultWindow: MapFeedQuery["window"] = {
  startUtc: "2026-04-20T00:00:00Z",
  endUtc: "2026-04-27T00:00:00Z",
  timezone: "America/Chicago",
};

// ============================================================================
// Test Suite: Debounce Behavior
// ============================================================================

describe("useMapFeedOptimized — Debounce", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
  });

  test("does not fetch immediately on viewport change", () => {
    mockEventService.fetchMapFeed.mockResolvedValue({
      events: [],
      totalCount: 0,
    });

    const { rerender } = renderHook(
      ({ bounds }) => useMapFeedOptimized(bounds, defaultWindow),
      {
        initialProps: { bounds: null },
      },
    );

    // Change bounds
    rerender({ bounds: defaultBounds });

    // Should NOT call fetch immediately
    expect(mockEventService.fetchMapFeed).not.toHaveBeenCalled();
  });

  test("fetches after debounce delay (400ms default)", () => {
    mockEventService.fetchMapFeed.mockResolvedValue({
      events: [],
      totalCount: 0,
    });

    const { rerender } = renderHook(
      ({ bounds }) => useMapFeedOptimized(bounds, defaultWindow),
      {
        initialProps: { bounds: null },
      },
    );

    // Change bounds
    rerender({ bounds: defaultBounds });

    // Advance 399ms — should not fetch
    act(() => {
      jest.advanceTimersByTime(399);
    });
    expect(mockEventService.fetchMapFeed).not.toHaveBeenCalled();

    // Advance 1ms more to 400ms — should fetch
    act(() => {
      jest.advanceTimersByTime(1);
    });
    expect(mockEventService.fetchMapFeed).toHaveBeenCalledTimes(1);
  });

  test("debounces rapid viewport changes (multiple pans)", () => {
    mockEventService.fetchMapFeed.mockResolvedValue({
      events: [],
      totalCount: 0,
    });

    const { rerender } = renderHook(
      ({ bounds }) => useMapFeedOptimized(bounds, defaultWindow),
      {
        initialProps: { bounds: null },
      },
    );

    const bounds1 = { ...defaultBounds, minLng: -95.4 };
    const bounds2 = { ...defaultBounds, minLng: -95.3 };
    const bounds3 = { ...defaultBounds, minLng: -95.2 };

    // Rapid pans: 3 viewport changes in quick succession
    rerender({ bounds: bounds1 });
    act(() => jest.advanceTimersByTime(100));
    rerender({ bounds: bounds2 });
    act(() => jest.advanceTimersByTime(100));
    rerender({ bounds: bounds3 });

    // Should NOT fetch yet
    expect(mockEventService.fetchMapFeed).not.toHaveBeenCalled();

    // Advance to debounce point (400ms from last change)
    act(() => jest.advanceTimersByTime(300));
    expect(mockEventService.fetchMapFeed).toHaveBeenCalledTimes(1);

    // Verify final bounds were used
    const lastCall = mockEventService.fetchMapFeed.mock.calls[0][0];
    expect(lastCall.bounds.minLng).toBe(bounds3.minLng);
  });

  test("respects custom debounceMs option", () => {
    mockEventService.fetchMapFeed.mockResolvedValue({
      events: [],
      totalCount: 0,
    });

    const { rerender } = renderHook(
      ({ bounds }) =>
        useMapFeedOptimized(bounds, defaultWindow, undefined, {
          debounceMs: 200, // Custom debounce
        }),
      {
        initialProps: { bounds: null },
      },
    );

    rerender({ bounds: defaultBounds });

    // Should fetch after 200ms, not 400ms
    act(() => jest.advanceTimersByTime(200));
    expect(mockEventService.fetchMapFeed).toHaveBeenCalledTimes(1);
  });
});

// ============================================================================
// Test Suite: Caching Behavior
// ============================================================================

describe("useMapFeedOptimized — Caching", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
  });

  test("returns cached result on cache hit", async () => {
    mockEventService.fetchMapFeed.mockResolvedValue({
      events: [
        {
          id: "1",
          title: "Event A",
          venueName: "Venue A",
          lat: 29.7,
          lng: -95.3,
          category: "nightlife",
          startUtc: "2026-04-25T21:00:00Z",
          endUtc: "2026-04-26T02:00:00Z",
          timezone: "America/Chicago",
          thumbnailUrl: null,
          status: "PUBLISHED",
          confidence: 0.95,
        },
      ],
      totalCount: 1,
    });

    const { rerender } = renderHook(
      ({ bounds }) => useMapFeedOptimized(bounds, defaultWindow),
      {
        initialProps: { bounds: null },
      },
    );

    // First fetch with bounds A
    rerender({ bounds: defaultBounds });
    act(() => jest.advanceTimersByTime(400));

    await waitFor(() => {
      expect(mockEventService.fetchMapFeed).toHaveBeenCalledTimes(1);
    });

    // Reset mock to track subsequent calls
    mockEventService.fetchMapFeed.mockClear();

    // Second request with SAME bounds → should hit cache
    rerender({ bounds: defaultBounds });
    act(() => jest.advanceTimersByTime(400));

    await waitFor(() => {
      // Fetch should NOT be called again
      expect(mockEventService.fetchMapFeed).not.toHaveBeenCalled();
    });
  });

  test("expires cache after TTL", async () => {
    mockEventService.fetchMapFeed.mockResolvedValue({
      events: [],
      totalCount: 0,
    });

    const { rerender } = renderHook(
      ({ bounds }) =>
        useMapFeedOptimized(bounds, defaultWindow, undefined, {
          cacheEnabled: true,
        }),
      {
        initialProps: { bounds: null },
      },
    );

    // First fetch
    rerender({ bounds: defaultBounds });
    act(() => jest.advanceTimersByTime(400));

    await waitFor(() => {
      expect(mockEventService.fetchMapFeed).toHaveBeenCalledTimes(1);
    });

    // Same bounds, but cache still valid
    mockEventService.fetchMapFeed.mockClear();
    rerender({ bounds: defaultBounds });
    act(() => jest.advanceTimersByTime(400));

    await waitFor(() => {
      expect(mockEventService.fetchMapFeed).not.toHaveBeenCalled(); // Cache hit
    });

    // Advance clock past TTL (5 minutes = 300,000ms)
    act(() => jest.advanceTimersByTime(300000 + 1));

    // Next request should fetch again (cache expired)
    rerender({ bounds: defaultBounds });
    act(() => jest.advanceTimersByTime(400));

    await waitFor(() => {
      expect(mockEventService.fetchMapFeed).toHaveBeenCalledTimes(1); // New fetch
    });
  });
});

// ============================================================================
// Test Suite: Marker Diffing
// ============================================================================

describe("useMapFeedOptimized — Marker Diffing", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
  });

  test("detects added markers", async () => {
    const mockInstrumentation = {
      onMarkersDiffed: jest.fn(),
    };

    mockEventService.fetchMapFeed.mockResolvedValue({
      events: [
        {
          id: "1",
          title: "Event 1",
          venueName: "Venue 1",
          lat: 29.7,
          lng: -95.3,
          category: "nightlife",
          startUtc: "2026-04-25T21:00:00Z",
          endUtc: "2026-04-26T02:00:00Z",
          timezone: "America/Chicago",
          thumbnailUrl: null,
          status: "PUBLISHED",
          confidence: 0.95,
        },
      ],
      totalCount: 1,
    });

    const { rerender } = renderHook(
      ({ bounds }) =>
        useMapFeedOptimized(bounds, defaultWindow, undefined, {
          instrumentation: mockInstrumentation as any,
        }),
      {
        initialProps: { bounds: null },
      },
    );

    rerender({ bounds: defaultBounds });
    act(() => jest.advanceTimersByTime(400));

    await waitFor(() => {
      expect(mockInstrumentation.onMarkersDiffed).toHaveBeenCalledWith({
        added: 1, // One marker added
        removed: 0,
        updated: 0,
      });
    });
  });

  test("detects removed markers", async () => {
    const mockInstrumentation = {
      onMarkersDiffed: jest.fn(),
    };

    // First response: 2 markers
    mockEventService.fetchMapFeed
      .mockResolvedValueOnce({
        events: [
          {
            id: "1",
            title: "Event 1",
            venueName: "Venue 1",
            lat: 29.7,
            lng: -95.3,
            category: "nightlife",
            startUtc: "2026-04-25T21:00:00Z",
            endUtc: "2026-04-26T02:00:00Z",
            timezone: "America/Chicago",
            thumbnailUrl: null,
            status: "PUBLISHED",
            confidence: 0.95,
          },
          {
            id: "2",
            title: "Event 2",
            venueName: "Venue 2",
            lat: 29.75,
            lng: -95.25,
            category: "tech",
            startUtc: "2026-04-26T14:00:00Z",
            endUtc: "2026-04-26T16:00:00Z",
            timezone: "America/Chicago",
            thumbnailUrl: null,
            status: "PUBLISHED",
            confidence: 0.88,
          },
        ],
        totalCount: 2,
      })
      // Second response: only 1 marker (removed)
      .mockResolvedValueOnce({
        events: [
          {
            id: "1",
            title: "Event 1",
            venueName: "Venue 1",
            lat: 29.7,
            lng: -95.3,
            category: "nightlife",
            startUtc: "2026-04-25T21:00:00Z",
            endUtc: "2026-04-26T02:00:00Z",
            timezone: "America/Chicago",
            thumbnailUrl: null,
            status: "PUBLISHED",
            confidence: 0.95,
          },
        ],
        totalCount: 1,
      });

    const { rerender } = renderHook(
      ({ bounds }) =>
        useMapFeedOptimized(bounds, defaultWindow, undefined, {
          instrumentation: mockInstrumentation as any,
          cacheEnabled: false, // Disable cache to force re-fetches
        }),
      {
        initialProps: { bounds: null },
      },
    );

    // First fetch: 2 markers
    rerender({ bounds: defaultBounds });
    act(() => jest.advanceTimersByTime(400));

    await waitFor(() => {
      expect(mockInstrumentation.onMarkersDiffed).toHaveBeenLastCalledWith({
        added: 2,
        removed: 0,
        updated: 0,
      });
    });

    // Clear mock for next assertion
    mockInstrumentation.onMarkersDiffed.mockClear();

    // Change viewport bounds to trigger second fetch
    const newBounds: GeoBoundingBox = {
      ...defaultBounds,
      minLng: -95.5,
    };
    rerender({ bounds: newBounds });
    act(() => jest.advanceTimersByTime(400));

    await waitFor(() => {
      expect(mockInstrumentation.onMarkersDiffed).toHaveBeenLastCalledWith({
        added: 0,
        removed: 1, // Event 2 removed
        updated: 0,
      });
    });
  });
});

// ============================================================================
// Test Suite: State Transitions
// ============================================================================

describe("useMapFeedOptimized — State Transitions", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
  });

  test("transitions: idle → loading → success", async () => {
    mockEventService.fetchMapFeed.mockResolvedValue({
      events: [
        {
          id: "1",
          title: "Event",
          venueName: "Venue",
          lat: 29.7,
          lng: -95.3,
          category: "nightlife",
          startUtc: "2026-04-25T21:00:00Z",
          endUtc: "2026-04-26T02:00:00Z",
          timezone: "America/Chicago",
          thumbnailUrl: null,
          status: "PUBLISHED",
          confidence: 0.95,
        },
      ],
      totalCount: 1,
    });

    const { result, rerender } = renderHook(
      ({ bounds }) => useMapFeedOptimized(bounds, defaultWindow),
      {
        initialProps: { bounds: null },
      },
    );

    // Initial state: idle
    expect(result.current.loadState).toBe("idle");

    rerender({ bounds: defaultBounds });

    // Should transition to loading
    expect(result.current.loadState).toBe("loading");

    act(() => jest.advanceTimersByTime(400));

    // Wait for success
    await waitFor(() => {
      expect(result.current.loadState).toBe("success");
      expect(result.current.markerCount).toBe(1);
    });
  });

  test("transitions: idle → loading → empty", async () => {
    mockEventService.fetchMapFeed.mockResolvedValue({
      events: [],
      totalCount: 0,
    });

    const { result, rerender } = renderHook(
      ({ bounds }) => useMapFeedOptimized(bounds, defaultWindow),
      {
        initialProps: { bounds: null },
      },
    );

    rerender({ bounds: defaultBounds });

    act(() => jest.advanceTimersByTime(400));

    await waitFor(() => {
      expect(result.current.loadState).toBe("empty");
      expect(result.current.markerCount).toBe(0);
    });
  });

  test("transitions: loading → error", async () => {
    const testError = new Error("Network error");
    mockEventService.fetchMapFeed.mockRejectedValue(testError);

    const { result, rerender } = renderHook(
      ({ bounds }) => useMapFeedOptimized(bounds, defaultWindow),
      {
        initialProps: { bounds: null },
      },
    );

    rerender({ bounds: defaultBounds });

    act(() => jest.advanceTimersByTime(400));

    await waitFor(() => {
      expect(result.current.loadState).toBe("error");
      expect(result.current.error?.message).toBe("Network error");
    });
  });

  test("transitions: loading → timeout", async () => {
    mockEventService.fetchMapFeed.mockImplementationOnce(
      () => new Promise(() => {}), // Never resolves
    );

    const { result, rerender } = renderHook(
      ({ bounds }) =>
        useMapFeedOptimized(bounds, defaultWindow, undefined, {
          timeoutMs: 100, // Short timeout for testing
        }),
      {
        initialProps: { bounds: null },
      },
    );

    rerender({ bounds: defaultBounds });

    act(() => jest.advanceTimersByTime(400));

    // Advance past timeout
    act(() => jest.advanceTimersByTime(100));

    await waitFor(() => {
      expect(result.current.loadState).toBe("timeout");
    });
  });
});

// ============================================================================
// Test Suite: Manual Refresh
// ============================================================================

describe("useMapFeedOptimized — Refresh", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
  });

  test("refresh() bypasses cache and fetches", async () => {
    mockEventService.fetchMapFeed.mockResolvedValue({
      events: [],
      totalCount: 0,
    });

    const { result, rerender } = renderHook(
      ({ bounds }) =>
        useMapFeedOptimized(bounds, defaultWindow, undefined, {
          cacheEnabled: true,
        }),
      {
        initialProps: { bounds: null },
      },
    );

    // First fetch
    rerender({ bounds: defaultBounds });
    act(() => jest.advanceTimersByTime(400));

    await waitFor(() => {
      expect(mockEventService.fetchMapFeed).toHaveBeenCalledTimes(1);
    });

    // Second request (same bounds) → cache hit
    rerender({ bounds: defaultBounds });
    act(() => jest.advanceTimersByTime(400));

    await waitFor(() => {
      expect(mockEventService.fetchMapFeed).toHaveBeenCalledTimes(1); // No new call
    });

    // Call refresh
    act(() => {
      result.current.refresh();
    });
    act(() => jest.advanceTimersByTime(400));

    await waitFor(() => {
      expect(mockEventService.fetchMapFeed).toHaveBeenCalledTimes(2); // New call after refresh
    });
  });

  test("clearCache() clears query cache", async () => {
    mockEventService.fetchMapFeed.mockResolvedValue({
      events: [],
      totalCount: 0,
    });

    const { result, rerender } = renderHook(
      ({ bounds }) =>
        useMapFeedOptimized(bounds, defaultWindow, undefined, {
          cacheEnabled: true,
        }),
      {
        initialProps: { bounds: null },
      },
    );

    // Populate cache
    rerender({ bounds: defaultBounds });
    act(() => jest.advanceTimersByTime(400));

    await waitFor(() => {
      expect(mockEventService.fetchMapFeed).toHaveBeenCalledTimes(1);
    });

    // Same bounds → cache hit
    mockEventService.fetchMapFeed.mockClear();
    rerender({ bounds: defaultBounds });
    act(() => jest.advanceTimersByTime(400));

    expect(mockEventService.fetchMapFeed).not.toHaveBeenCalled();

    // Clear cache
    act(() => {
      result.current.clearCache();
    });

    // Same bounds → cache miss (should fetch again)
    rerender({ bounds: defaultBounds });
    act(() => jest.advanceTimersByTime(400));

    await waitFor(() => {
      expect(mockEventService.fetchMapFeed).toHaveBeenCalledTimes(1);
    });
  });
});
