namespace WeUP.Infrastructure.Persistence.Entities;

public sealed class MediaAssetEntity
{
    public Guid Id { get; set; }
    public string AssetId { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ChecksumSha256 { get; set; } = string.Empty;
    public string? ContentHash { get; set; }
    public string OriginalFilename { get; set; } = string.Empty;
    public string? CanonicalContentType { get; set; }
    public int? WidthPx { get; set; }
    public int? HeightPx { get; set; }
    public bool IsAnimated { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public string? UploaderUserId { get; set; }
    public string? UploadOrigin { get; set; }
    public string? SourceType { get; set; }
    public string OwnerType { get; set; } = string.Empty;
    public string? OwnerId { get; set; }
    public string? VenueId { get; set; }
    public string? IngestionWorkflowSource { get; set; }
    public string StorageProvider { get; set; } = string.Empty;
    public string StorageContainer { get; set; } = string.Empty;
    public string StorageObjectKey { get; set; } = string.Empty;
    public string? StorageUri { get; set; }
    public string? StorageETag { get; set; }
    public string? StorageVersionId { get; set; }
    public string? SubmissionId { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
