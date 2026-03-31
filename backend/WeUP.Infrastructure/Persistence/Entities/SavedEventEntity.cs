namespace WeUP.Infrastructure.Persistence.Entities;

/// <summary>
/// User ↔ Event save signal. Unique per (UserId, EventId). Idempotent upsert safe.
/// </summary>
public sealed class SavedEventEntity
{
    public Guid UserId { get; set; }
    public Guid EventId { get; set; }
    public DateTimeOffset SavedAt { get; set; }

    // Navigation
    public UserProfileEntity User { get; set; } = null!;
    public EventEntity Event { get; set; } = null!;
}
