namespace WeUP.Infrastructure.Persistence.Entities;

/// <summary>
/// P29: Persisted time-sliced metadata for derived video frame and poster assets.
/// </summary>
public sealed class VideoDerivedFrameEntity
{
    public Guid Id { get; set; }
    public string SourceVideoAssetId { get; set; } = string.Empty;
    public string DerivedAssetId { get; set; } = string.Empty;
    public string ProcessingJobId { get; set; } = string.Empty;

    public string? UploaderUserId { get; set; }
    public string? SubmissionId { get; set; }
    public string? VenueId { get; set; }
    public string? ModerationItemId { get; set; }

    public long TimestampOffsetMs { get; set; }
    public string FrameType { get; set; } = string.Empty;
    public int WidthPx { get; set; }
    public int HeightPx { get; set; }

    public string StorageProvider { get; set; } = string.Empty;
    public string StorageContainer { get; set; } = string.Empty;
    public string StorageObjectKey { get; set; } = string.Empty;
    public string? StorageUri { get; set; }

    public string ExtractionStage { get; set; } = string.Empty;
    public string ExtractionVersion { get; set; } = string.Empty;

    public bool IsPosterSelected { get; set; }
    public string? PosterSelectionReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
