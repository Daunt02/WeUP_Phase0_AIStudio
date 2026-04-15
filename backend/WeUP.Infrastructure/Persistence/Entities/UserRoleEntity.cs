namespace WeUP.Infrastructure.Persistence.Entities;

public sealed class UserRoleEntity
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset AssignedAt { get; set; }

    public UserProfileEntity User { get; set; } = null!;
}
