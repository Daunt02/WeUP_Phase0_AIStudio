namespace WeUP.Infrastructure.Persistence.Entities;

/// <summary>P28: EF Core entity for video flyer assets.</summary>
public sealed class VideoFlyerAssetEntity
{
    public Guid Id { get; set; }
    public string AssetId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ChecksumSha256 { get; set; } = string.Empty;
    public string OriginalFilename { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; }

    // Provenance / ownership
    public string? UploaderUserId { get; set; }
    public string? SubmissionId { get; set; }
    public string? VenueId { get; set; }
    public string? ModerationItemId { get; set; }
    public string? IngestionJobId { get; set; }

    // Storage
    public string StorageProvider { get; set; } = string.Empty;
    public string StorageContainer { get; set; } = string.Empty;
    public string StorageObjectKey { get; set; } = string.Empty;
    public string? StorageUri { get; set; }

    // Video-specific metadata — populated by the processing pipeline
    public int? DurationSeconds { get; set; }
    public int? WidthPx { get; set; }
    public int? HeightPx { get; set; }
    public string? DetectedCodec { get; set; }
    public int? BitrateKbps { get; set; }

    // Processing refs
    public string? ProcessingJobId { get; set; }
    public string? PosterAssetId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
