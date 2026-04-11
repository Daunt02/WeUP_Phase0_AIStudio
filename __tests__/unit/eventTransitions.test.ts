import {
  canTransitionEventStatus,
  tryTransitionEventStatus,
  getMissingFieldsForStatus,
} from "@/domains/event/transitions";
import type { EventAggregate, EventStatus } from "@/domains/event/types";

const now = new Date().toISOString();

function makeBaseEvent(status: EventStatus): Partial<EventAggregate> {
  return {
    id: "evt_test_1",
    status,
    canonicalTitle: "Test Event",
    sourceRefs: [{ kind: "manual_submission", ref: "r1", ingestedAt: now }],
    confidence: 0.9,
    audit: { createdAt: now, updatedAt: now, createdBy: "test" },
  };
}

test("illegal transition is rejected by canTransitionEventStatus", () => {
  const res = canTransitionEventStatus("PUBLISHED", "DRAFT");
  expect(res.ok).toBe(false);
});

test("getMissingFieldsForStatus identifies missing fields for APPROVED", () => {
  const e = makeBaseEvent("INGESTED");
  const missing = getMissingFieldsForStatus(e, "APPROVED");
  expect(missing).toEqual(expect.arrayContaining(["venue", "address"]));
});

test("tryTransitionEventStatus fails when required fields missing", () => {
  const e = makeBaseEvent("INGESTED");
  const res = tryTransitionEventStatus(e, "APPROVED");
  expect(res.ok).toBe(false);
  expect(res.reason).toMatch(/Missing required fields/);
});

test("tryTransitionEventStatus succeeds for APPROVED->PUBLISHED when required fields present", () => {
  const e: Partial<EventAggregate> = {
    id: "evt_2",
    status: "APPROVED",
    canonicalTitle: "Event OK",
    venue: { venueId: "v1", name: "V1" },
    address: {
      line1: "123 Main St",
      city: "City",
      country: "US",
      raw: "123 Main St, City",
    },
    geo: { lat: 30.0, lng: -95.0 },
    timeRange: { startUtc: now, endUtc: null },
    timezone: "America/Chicago",
    sourceRefs: [{ kind: "manual_submission", ref: "r2", ingestedAt: now }],
    confidence: 0.9,
    audit: { createdAt: now, updatedAt: now, createdBy: "tester" },
  };
  const res = tryTransitionEventStatus(e, "PUBLISHED");
  expect(res.ok).toBe(true);
  expect(res.event?.status).toBe("PUBLISHED");
});
