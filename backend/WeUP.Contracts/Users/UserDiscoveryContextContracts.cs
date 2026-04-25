using WeUP.Contracts.Events;

namespace WeUP.Contracts.Users;

/// <summary>
/// Canonical mode values for discovery preference ownership.
/// Anonymous and authenticated contexts are explicitly separated.
/// </summary>
public static class PreferenceOwnerModes
{
    public const string Anonymous = "anonymous";
    public const string Authenticated = "authenticated";
}

/// <summary>
/// Canonical taxonomy codes used by discovery preferences.
/// Values are stable contract identifiers, not user-entered labels.
/// </summary>
public static class DiscoveryTaxonomyV1
{
    public static readonly string[] DistrictCodes =
    [
        "mission",
        "soma",
        "north-beach",
        "uptown",
        "jack-london",
        "temescal",
    ];

    public static readonly string[] CategoryCodes =
    [
        "nightlife",
        "rooftop",
        "concert",
        "music",
        "food",
        "community",
    ];
}

/// <summary>
/// Explicit ownership envelope for persisted discovery preferences.
/// - authenticated: UserId required, AnonymousSessionId null.
/// - anonymous: AnonymousSessionId required, UserId null.
/// </summary>
public sealed record PreferenceOwnerDto(
    string Mode,
    string? UserId,
    string? AnonymousSessionId);

/// <summary>
/// Last persisted map state used by discovery surfaces.
/// This is cross-surface context, not transient component UI state.
/// </summary>
public sealed record LastUsedMapStateDto(
    string Bbox,
    double? CenterLat,
    double? CenterLng,
    double? Zoom,
    DateTimeOffset CapturedAtUtc);

/// <summary>
/// Lightweight saved-state summary projected into discovery context.
/// This supports UX continuity and does not store event-level analytics exhaust.
/// </summary>
public sealed record SavedCountSummaryDto(
    int TotalSavedEvents,
    int SavedEventsInCurrentMapWindow,
    DateTimeOffset CapturedAtUtc);

/// <summary>
/// MVP discovery context model.
/// Preferences include only durable cross-session user intent and context:
/// district/category/temporal preferences, last map state, and saved summary.
/// </summary>
public sealed record UserDiscoveryContextDto(
    PreferenceOwnerDto Owner,
    string[] PreferredDistrictCodes,
    string[] PreferredCategoryCodes,
    TimeWindowPreset[] PreferredTemporalPresets,
    LastUsedMapStateDto? LastUsedMapState,
    SavedCountSummaryDto? SavedCountSummary,
    DateTimeOffset UpdatedAtUtc,
    int Version = 1);

/// <summary>
/// Patch-style request for discovery context updates.
/// Ownership is required to keep anonymous/authenticated preference streams isolated.
/// </summary>
public sealed record UpsertUserDiscoveryContextRequest(
    PreferenceOwnerDto Owner,
    string[]? PreferredDistrictCodes = null,
    string[]? PreferredCategoryCodes = null,
    TimeWindowPreset[]? PreferredTemporalPresets = null,
    LastUsedMapStateDto? LastUsedMapState = null,
    SavedCountSummaryDto? SavedCountSummary = null);
