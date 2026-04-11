namespace WeUP.Domain.Media;

public enum MediaAssetType
{
    FlyerImage,
    VenueImage,
    PromotionalPoster,
    EventMedia,
}

public enum MediaAssetStatus
{
    Initialized,
    Uploading,
    Uploaded,
    ProcessingPending,
    ProcessingComplete,
    ReviewPending,
    Rejected,
    Archived,
}

public enum MediaOwnerType
{
    User,
    Venue,
    SystemWorkflow,
}

public enum MediaStorageProvider
{
    LocalFileSystem,
    CloudObjectStorage,
}

public sealed record MediaOwnerRef(
    MediaOwnerType OwnerType,
    string? OwnerId,
    string? VenueId,
    string? IngestionWorkflowSource);

public sealed record MediaStorageRef(
    MediaStorageProvider Provider,
    string Container,
    string ObjectKey,
    string? Uri,
    string? ETag = null,
    string? VersionId = null);

public sealed record MediaAsset(
    string AssetId,
    MediaAssetType AssetType,
    MediaAssetStatus Status,
    string ContentType,
    long FileSizeBytes,
    string ChecksumSha256,
    string OriginalFilename,
    DateTimeOffset UploadedAt,
    string? UploaderUserId,
    MediaOwnerRef Owner,
    MediaStorageRef Storage,
    string? SubmissionId,
    string? MetadataJson = null);

public sealed record MediaUpload(
    string UploadId,
    string AssetId,
    MediaAssetStatus Status,
    DateTimeOffset InitializedAt,
    DateTimeOffset? CompletedAt,
    string? RequestedByUserId,
    string? FailureReason = null);
