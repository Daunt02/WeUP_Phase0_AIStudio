import { beforeEach, describe, expect, it, vi } from "vitest";

const { migrateAnonymousSaveState, ApiRequestError } = vi.hoisted(() => {
  class MockApiRequestError extends Error {
    readonly status: number;

    constructor(message: string, status: number) {
      super(message);
      this.name = "ApiRequestError";
      this.status = status;
    }
  }

  return {
    migrateAnonymousSaveState: vi.fn(),
    ApiRequestError: MockApiRequestError,
  };
});

vi.mock("../services/eventDetailService", () => ({
  migrateAnonymousSaveState,
  ApiRequestError,
}));

function deferredPromise<T>() {
  let resolvePromise!: (value: T) => void;
  let rejectPromise!: (reason?: unknown) => void;

  const promise = new Promise<T>((resolve, reject) => {
    resolvePromise = resolve;
    rejectPromise = reject;
  });

  return {
    promise,
    resolve: resolvePromise,
    reject: rejectPromise,
  };
}

describe("useSaveStateMigration", () => {
  beforeEach(() => {
    vi.resetModules();
    localStorage.clear();
    migrateAnonymousSaveState.mockReset();
  });

  it("normalizes local payload, locks candidate ids, and only rewrites local state after confirmation", async () => {
    const { useAnonymousLocalPersistence } =
      await import("../composables/useAnonymousLocalPersistence");
    const { useSaveStateMigration } =
      await import("../composables/useSaveStateMigration");

    localStorage.setItem(
      "weup.saved-events.anon.v1",
      JSON.stringify([" evt-b ", "evt-a", "evt-a", ""]),
    );

    const persistence = useAnonymousLocalPersistence();
    persistence.setDiscoveryContext({
      lastViewedDistrict: "mission",
      lastTemporalFilter: {
        preset: "tonight",
        timezone: "America/Los_Angeles",
      },
      recentMapViewport: {
        bbox: "-122.52,37.70,-122.37,37.85",
        capturedAtUtc: "2026-04-22T10:00:00Z",
      },
    });

    const responsePayload = {
      status: "completed-with-issues" as const,
      migrationDirection: "anonymous-to-authenticated" as const,
      ownership: "authenticated-user" as const,
      clientMigrationKey: "client-key",
      processedAtUtc: "2026-04-22T10:01:00Z",
      counts: {
        receivedLocalSavedCount: 2,
        distinctLocalSavedCount: 2,
        duplicateCollapsedCount: 0,
        migratedCount: 1,
        alreadySavedCount: 0,
        invalidLocalIdCount: 1,
        missingLocalIdCount: 0,
      },
      itemResults: [
        {
          eventId: "evt-a",
          outcome: "migrated" as const,
          message: "Event migrated to authenticated saves.",
        },
        {
          eventId: "evt-b",
          outcome: "invalid-local-event-id" as const,
          message: "Invalid userId or eventId",
        },
      ],
      retainedLocalSavedEventIds: ["evt-b"],
      discoveryContext: {
        status: "applied" as const,
        applied: true,
        message:
          "Discovery context timezone migrated into authenticated preferences.",
        resolvedPreferredTimezone: "America/Los_Angeles",
      },
    };
    const deferred = deferredPromise<typeof responsePayload>();
    migrateAnonymousSaveState.mockReturnValueOnce(deferred.promise);

    const migration = useSaveStateMigration();
    const pending = migration.triggerMigrationOnSignIn("  client-key  ");

    expect(migrateAnonymousSaveState).toHaveBeenCalledWith({
      localSavedEventIds: ["evt-a", "evt-b"],
      localDiscoveryContext: {
        lastViewedDistrict: "mission",
        lastTemporalFilter: {
          preset: "tonight",
          timezone: "America/Los_Angeles",
          customStartUtc: null,
          customEndUtc: null,
        },
        recentMapViewport: {
          bbox: "-122.52,37.70,-122.37,37.85",
          capturedAtUtc: "2026-04-22T10:00:00Z",
        },
        updatedAtUtc: expect.any(String),
      },
      clientMigrationKey: "client-key",
    });

    expect(migration.isMigrationInFlight.value).toBe(true);
    expect(migration.isEventLockedForMigration("evt-a")).toBe(true);
    expect(migration.isEventLockedForMigration("evt-b")).toBe(true);
    expect(persistence.getSavedState().savedEventIds).toEqual([
      "evt-b",
      "evt-a",
    ]);

    deferred.resolve(responsePayload);
    const result = await pending;

    expect(result.retainedLocalSavedEventIds).toEqual(["evt-b"]);
    expect(persistence.getSavedState().savedEventIds).toEqual(["evt-b"]);
    expect(migration.lastResult.value).toEqual(result);
    expect(migration.hasMigrationIssues.value).toBe(true);
    expect(migration.isMigrationInFlight.value).toBe(false);
    expect(migration.isEventLockedForMigration("evt-a")).toBe(false);
  });

  it("preserves local state and reports the failure when migration is not confirmed", async () => {
    const { useAnonymousLocalPersistence } =
      await import("../composables/useAnonymousLocalPersistence");
    const { useSaveStateMigration } =
      await import("../composables/useSaveStateMigration");

    const persistence = useAnonymousLocalPersistence();
    persistence.setSavedEventIds(["evt-z"]);

    migrateAnonymousSaveState.mockRejectedValueOnce(
      new ApiRequestError(
        "Save-state migration failed (401). Unauthorized",
        401,
      ),
    );

    const migration = useSaveStateMigration();

    await expect(
      migration.triggerMigrationOnSignIn("failure-case"),
    ).rejects.toThrow("Save-state migration failed (401). Unauthorized");

    expect(persistence.getSavedState().savedEventIds).toEqual(["evt-z"]);
    expect(migration.lastError.value).toBe(
      "Save-state migration failed (401). Unauthorized",
    );
    expect(migration.lastResult.value).toBeNull();
    expect(migration.isMigrationInFlight.value).toBe(false);
  });
});
