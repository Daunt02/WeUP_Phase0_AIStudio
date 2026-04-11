namespace WeUP.Domain.Media;

public sealed record CreateMediaUploadCommand(
    MediaAssetType AssetType,
    string ContentType,
    string OriginalFilename,
    long FileSizeBytes,
    string? UploaderUserId,
    MediaOwnerRef Owner,
    string? SubmissionId,
    string? MetadataJson);

public sealed record CompleteMediaUploadCommand(
    bool ProcessingSucceeded,
    bool QueueForReview,
    string? FailureReason,
    string? MetadataJson);

public interface IMediaIntakeService
{
    Task<MediaUpload> CreateUploadAsync(
        CreateMediaUploadCommand command,
        Stream fileStream,
        CancellationToken ct = default);

    Task<MediaUpload?> GetUploadAsync(string uploadId, CancellationToken ct = default);
    Task<MediaAsset?> GetAssetAsync(string assetId, CancellationToken ct = default);
    Task<MediaUpload?> CompleteUploadAsync(string uploadId, CompleteMediaUploadCommand command, CancellationToken ct = default);
}
