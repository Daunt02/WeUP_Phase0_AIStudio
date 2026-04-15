namespace WeUP.Infrastructure.Persistence.Entities;

public sealed class EventSubmissionEntity
{
    public Guid Id { get; set; }
    public string SubmissionId { get; set; } = string.Empty;
    public string SubmittedByUserId { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string? Title { get; set; }
    public string? VenueName { get; set; }
    public string? Address { get; set; }
    public DateTimeOffset? StartUtc { get; set; }
    public DateTimeOffset? EndUtc { get; set; }
    public string? Timezone { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? TagsJson { get; set; }
    public string? FlyerAssetIdsJson { get; set; }
    public string? ReviewNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}