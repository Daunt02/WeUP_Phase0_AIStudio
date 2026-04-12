namespace WeUP.Domain.Media;

/// <summary>
/// P28: Repository abstraction for video flyer assets, upload sessions, and processing jobs.
/// </summary>
public interface IVideoFlyerRepository
{
    Task SaveAssetAsync(VideoFlyerAsset asset, CancellationToken ct = default);
    Task SaveUploadAsync(VideoFlyerUpload upload, CancellationToken ct = default);
    Task SaveJobAsync(VideoProcessingJob job, CancellationToken ct = default);

    Task<VideoFlyerAsset?> GetAssetAsync(string assetId, CancellationToken ct = default);
    Task<VideoFlyerUpload?> GetUploadAsync(string uploadId, CancellationToken ct = default);
    Task<VideoProcessingJob?> GetJobAsync(string jobId, CancellationToken ct = default);

    /// <summary>Returns the most recently queued or running job for the given asset, if any.</summary>
    Task<VideoProcessingJob?> GetJobForAssetAsync(string assetId, CancellationToken ct = default);

    Task UpdateAssetAsync(VideoFlyerAsset asset, CancellationToken ct = default);
    Task UpdateUploadAsync(VideoFlyerUpload upload, CancellationToken ct = default);
    Task UpdateJobAsync(VideoProcessingJob job, CancellationToken ct = default);
}

/// <summary>
/// P28: Service interface for video flyer upload registration and lifecycle management.
/// </summary>
public interface IVideoFlyerUploadService
{
    /// <summary>
    /// Phase 0.15 — Direct file stream upload.
    /// Validates the file, stores it, creates the asset and upload records.
    /// Phase 0.5+: fileStream will be null; method will return a signed UploadUrl instead.
    /// </summary>
    Task<VideoFlyerUpload> InitiateUploadAsync(
        InitiateVideoUploadCommand command,
        Stream fileStream,
        CancellationToken ct = default);

    Task<VideoFlyerUpload?> GetUploadAsync(string uploadId, CancellationToken ct = default);
    Task<VideoFlyerAsset?> GetAssetAsync(string assetId, CancellationToken ct = default);

    /// <summary>
    /// Signal that the upload is complete. On success, transitions the asset to
    /// ProcessingPending and creates a queued VideoProcessingJob.
    /// Returns null when the uploadId is not found.
    /// Job is null in the result when Success = false.
    /// </summary>
    Task<VideoUploadCompletionResult?> CompleteUploadAsync(
        string uploadId,
        CompleteVideoUploadCommand command,
        CancellationToken ct = default);

    Task<VideoProcessingJob?> GetJobAsync(string jobId, CancellationToken ct = default);
}
