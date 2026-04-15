namespace WeUP.Infrastructure.Persistence.Entities;

/// <summary>
/// Basic user profile. Auth backbone (P16) manages session and identity linking.
/// </summary>
public sealed class UserProfileEntity
{
    public Guid Id { get; set; }
    public string PublicId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? HomeMarket { get; set; }
    public string OnboardingState { get; set; } = "NEW";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public ICollection<SavedEventEntity> Saves { get; set; } = [];
}
