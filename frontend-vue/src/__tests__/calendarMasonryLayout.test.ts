import { describe, expect, it } from "vitest";

import { computeCalendarMasonryLayout } from "../composables/useCalendarMasonryLayout";
import type { CalendarEventItemDto } from "../contracts/calendar-overlay.contracts";

function buildEvent(
  partial: Partial<CalendarEventItemDto> & { eventId: string },
): CalendarEventItemDto {
  return {
    eventId: partial.eventId,
    title: partial.title ?? partial.eventId,
    startUtc: partial.startUtc ?? "2026-04-20T18:00:00Z",
    endUtc: partial.endUtc ?? "2026-04-20T19:00:00Z",
    timezone: partial.timezone ?? "UTC",
    venueName: partial.venueName ?? "Venue",
    district: partial.district ?? null,
    primaryCategory: partial.primaryCategory ?? "music",
    savedByCurrentUser: partial.savedByCurrentUser ?? false,
    markerState: partial.markerState ?? "default",
    thumbnailUrl: partial.thumbnailUrl ?? null,
  };
}

describe("computeCalendarMasonryLayout", () => {
  it("is deterministic for identical input", () => {
    const items = [
      buildEvent({
        eventId: "evt-a",
        startUtc: "2026-04-20T18:00:00Z",
        endUtc: "2026-04-20T19:00:00Z",
      }),
      buildEvent({
        eventId: "evt-b",
        startUtc: "2026-04-20T18:00:00Z",
        endUtc: "2026-04-20T20:00:00Z",
      }),
      buildEvent({
        eventId: "evt-c",
        startUtc: "2026-04-20T18:30:00Z",
        endUtc: "2026-04-20T19:30:00Z",
      }),
    ];

    const first = computeCalendarMasonryLayout(items);
    const second = computeCalendarMasonryLayout(items);

    expect(second).toEqual(first);
  });

  it("packs simultaneous starts into separate columns", () => {
    const items = [
      buildEvent({
        eventId: "evt-a",
        startUtc: "2026-04-20T18:00:00Z",
        endUtc: "2026-04-20T19:00:00Z",
      }),
      buildEvent({
        eventId: "evt-b",
        startUtc: "2026-04-20T18:00:00Z",
        endUtc: "2026-04-20T19:30:00Z",
      }),
      buildEvent({
        eventId: "evt-c",
        startUtc: "2026-04-20T18:00:00Z",
        endUtc: "2026-04-20T20:00:00Z",
      }),
    ];

    const layout = computeCalendarMasonryLayout(items);
    const firstBucket = layout.dayBuckets[0]?.buckets[0];

    expect(firstBucket?.visibleEvents).toHaveLength(3);
    expect(
      firstBucket?.visibleEvents.map((event) => event.columnIndex),
    ).toEqual([0, 1, 2]);
  });

  it("signals overflow in overloaded periods while preserving event identity", () => {
    const items = Array.from({ length: 14 }, (_, index) => {
      const minute = index % 4;
      return buildEvent({
        eventId: `evt-${index + 1}`,
        startUtc: `2026-04-20T18:0${minute}:00Z`,
        endUtc: "2026-04-20T19:40:00Z",
      });
    });

    const layout = computeCalendarMasonryLayout(items, {
      denseThreshold: 8,
      overloadedThreshold: 12,
      maxVisibleInOverloadedBucket: 5,
      maxColumnsPerBucket: 3,
    });

    const bucket = layout.dayBuckets[0]?.buckets[0];
    expect(bucket?.densityLevel).toBe("overloaded");
    expect(bucket?.overflowCount).toBeGreaterThan(0);

    const visibleIds = new Set(
      bucket?.visibleEvents.map((event) => event.eventId),
    );
    const overflowIds = new Set(
      bucket?.overflowEvents.map((event) => event.eventId),
    );

    for (const event of items) {
      expect(
        visibleIds.has(event.eventId) || overflowIds.has(event.eventId),
      ).toBe(true);
    }
  });
});
