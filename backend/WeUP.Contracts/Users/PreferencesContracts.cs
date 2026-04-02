namespace WeUP.Contracts.Users;

// ---------------------------------------------------------------------------
// User Preferences — discovery and notification settings
// ---------------------------------------------------------------------------

public sealed record UserPreferencesDto(
    string UserId,
    string[] PreferredCategories,
    double HomeRadiusMeters,
    bool NotifyOnNewEvents,
    bool NotifyOnSaveReminders,
    string? PreferredTimeZone,
    DateTimeOffset UpdatedAt);

public sealed record UpdatePreferencesRequest(
    string[]? PreferredCategories,
    double? HomeRadiusMeters,
    bool? NotifyOnNewEvents,
    bool? NotifyOnSaveReminders,
    string? PreferredTimeZone);
