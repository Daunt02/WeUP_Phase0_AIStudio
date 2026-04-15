import { act, renderHook, waitFor } from "@testing-library/react";
import { useSavedEventsAuthority } from "@/hooks/useSavedEventsAuthority";

jest.mock("@/lib/env/public", () => ({
  publicEnv: {
    NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN: "pk.test",
    NEXT_PUBLIC_API_BASE_URL: "",
  },
}));

describe("useSavedEventsAuthority", () => {
  const originalFetch = global.fetch;

  beforeEach(() => {
    localStorage.clear();
    global.fetch = jest.fn();
  });

  afterEach(() => {
    localStorage.clear();
    jest.clearAllMocks();
    global.fetch = originalFetch;
  });

  it("uses anonymous local saves when unauthenticated", async () => {
    localStorage.setItem(
      "weup.saved-events.anon.v1",
      JSON.stringify(["evt-1"]),
    );

    const { result } = renderHook(() => useSavedEventsAuthority());

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.sessionKind).toBe("anonymous");
    expect(result.current.savedEventIds).toEqual(["evt-1"]);
    expect(global.fetch).not.toHaveBeenCalled();
  });

  it("migrates anonymous local saves into backend when authenticated", async () => {
    localStorage.setItem("weup_dev_token", "token-123");
    localStorage.setItem(
      "weup.saved-events.anon.v1",
      JSON.stringify(["evt-local-1"]),
    );

    (global.fetch as jest.Mock)
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          userId: "user-sf-camille",
          email: "camille+phase0@weup.test",
          displayName: "Camille",
          homeMarket: "sf",
          onboardingState: "COMPLETE",
          createdAt: "2026-04-01T18:00:00Z",
          roles: ["user"],
        }),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ items: [], hasNextPage: false }),
      })
      .mockResolvedValueOnce({
        ok: true,
      });

    const { result } = renderHook(() => useSavedEventsAuthority());

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.sessionKind).toBe("authenticated");
    expect(result.current.savedEventIds).toContain("evt-local-1");
    expect(localStorage.getItem("weup.saved-events.anon.v1")).toBeNull();
    expect(global.fetch).toHaveBeenNthCalledWith(
      1,
      "/auth/me",
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: "Bearer token-123",
        }),
      }),
    );
    expect(global.fetch).toHaveBeenNthCalledWith(
      2,
      "/api/users/me/saves?page=1&pageSize=100",
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: "Bearer token-123",
        }),
      }),
    );
    expect(global.fetch).toHaveBeenNthCalledWith(
      3,
      "/api/users/me/saves/evt-local-1",
      expect.objectContaining({
        method: "POST",
        headers: expect.objectContaining({
          Authorization: "Bearer token-123",
        }),
      }),
    );
  });

  it("updates local fallback cache on anonymous toggle", async () => {
    const { result } = renderHook(() => useSavedEventsAuthority());

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    await act(async () => {
      await result.current.toggleSavedEvent("evt-toggle");
    });

    expect(result.current.sessionKind).toBe("anonymous");
    expect(result.current.savedEventIds).toEqual(["evt-toggle"]);
    expect(localStorage.getItem("weup.saved-events.anon.v1")).toBe(
      JSON.stringify(["evt-toggle"]),
    );
  });
});
