import { describe, expect, it } from "vitest";
import { TimeWindowPreset } from "../contracts/time-window.contracts";
import { PREFERENCE_OWNER_MODES } from "../contracts/user-context.contracts";
import { useUserContextPreferences } from "../composables/useUserContextPreferences";

describe("useUserContextPreferences", () => {
  it("tracks canonical filter and temporal preferences", () => {
    const userContext = useUserContextPreferences();

    userContext.updateFromMapDiscoveryFilters({
      district: "mission",
      categories: ["music", "invalid-category"],
      includeSavedOnly: false,
    });

    userContext.updatePreferredTemporalPreset(TimeWindowPreset.Tonight);

    expect(userContext.preferredDistrictCodes.value).toEqual(["mission"]);
    expect(userContext.preferredCategoryCodes.value).toEqual(["music"]);
    expect(userContext.preferredTemporalPresets.value).toEqual([
      TimeWindowPreset.Tonight,
    ]);
  });

  it("keeps ownership explicit across anonymous and authenticated modes", () => {
    const userContext = useUserContextPreferences();

    userContext.setOwner({
      mode: PREFERENCE_OWNER_MODES.authenticated,
      userId: "user-123",
      anonymousSessionId: null,
    });

    expect(userContext.ownershipMode.value).toBe(
      PREFERENCE_OWNER_MODES.authenticated,
    );
    expect(userContext.context.value.owner.userId).toBe("user-123");
    expect(userContext.context.value.owner.anonymousSessionId).toBeNull();
  });

  it("updates saved count summary from visible map items", () => {
    const userContext = useUserContextPreferences();

    userContext.updateSavedCountSummaryFromItems([
      {
        eventId: "a",
        title: "A",
        startUtc: "2026-04-11T23:00:00Z",
        endUtc: null,
        latitude: 0,
        longitude: 0,
        venueName: "Venue A",
        district: "mission",
        primaryCategory: "music",
        savedByCurrentUser: true,
        markerState: "saved",
      },
      {
        eventId: "b",
        title: "B",
        startUtc: "2026-04-11T23:30:00Z",
        endUtc: null,
        latitude: 0,
        longitude: 0,
        venueName: "Venue B",
        district: "soma",
        primaryCategory: "nightlife",
        savedByCurrentUser: false,
        markerState: "default",
      },
    ]);

    expect(
      userContext.savedCountSummary.value?.savedEventsInCurrentMapWindow,
    ).toBe(1);
    expect(userContext.savedCountSummary.value?.totalSavedEvents).toBe(1);
  });
});
