namespace WeUP.Infrastructure.Persistence.Entities;

public sealed class UserPreferencesEntity
{
    public Guid UserId { get; set; }
    public string PreferredCategoriesJson { get; set; } = "[]";
    public double HomeRadiusMeters { get; set; }
    public bool NotifyOnNewEvents { get; set; }
    public bool NotifyOnSaveReminders { get; set; }
    public string? PreferredTimeZone { get; set; }
    public double? LastKnownMapCenterLat { get; set; }
    public double? LastKnownMapCenterLng { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public UserProfileEntity User { get; set; } = null!;
}
