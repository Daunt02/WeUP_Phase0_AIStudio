namespace WeUP.Infrastructure.Persistence.Entities;

/// <summary>P28: EF Core entity for video flyer upload sessions.</summary>
public sealed class VideoFlyerUploadEntity
{
    public Guid Id { get; set; }
    public string UploadId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset InitializedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? RequestedByUserId { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
