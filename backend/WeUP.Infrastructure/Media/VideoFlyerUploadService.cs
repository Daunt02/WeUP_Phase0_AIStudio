using System.Security.Cryptography;
using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

/// <summary>
/// P28: Video flyer upload service.
/// Validates, stores, and tracks the lifecycle of video flyer uploads.
/// Automatically creates a VideoProcessingJob (Queued) when upload completion is signalled.
///
/// Phase 0.15 flow:
///   1. POST /api/media/video-uploads  → InitiateUploadAsync  → stores file, returns UploadId/AssetId
///   2. POST /api/media/video-uploads/{id}/complete → CompleteUploadAsync → creates processing job
///
/// Note: Phase 0.15 buffers the full video file in memory to compute SHA-256.
/// Phase 0.5+: replace with a signed-URL flow so the server only handles metadata,
/// and the client uploads directly to object storage.
/// </summary>
public sealed class VideoFlyerUploadService : IVideoFlyerUploadService
{
    private readonly IVideoFlyerRepository _repository;
    private readonly IVideoStorageService _storage;
    private readonly VideoIntakeValidation _validation;

    public VideoFlyerUploadService(
        IVideoFlyerRepository repository,
        IVideoStorageService storage,
        VideoIntakeValidation validation)
    {
        _repository = repository;
        _storage = storage;
        _validation = validation;
    }

    public async Task<VideoFlyerUpload> InitiateUploadAsync(
        InitiateVideoUploadCommand command,
        Stream fileStream,
        CancellationToken ct = default)
    {
        var normalizedContentType = VideoIntakeValidation.NormalizeContentType(command.ContentType);
        var validationError = _validation.Validate(normalizedContentType, command.FileSizeBytes, command.OriginalFilename);
        if (validationError is not null)
            throw new InvalidOperationException(validationError);

        // Buffer stream to compute checksum before writing to storage.
        // Phase 0.5+: eliminate this buffer by receiving the checksum from the client
        // after a signed-URL upload, then verifying against the stored object ETag.
        if (fileStream.CanSeek)
            fileStream.Position = 0;

        using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var assetId = Guid.NewGuid().ToString("N");
        var uploadId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;

        // Stage 1 — persist the in-progress upload record so it can be recovered on failure
        var initialUpload = new VideoFlyerUpload
        {
            UploadId = uploadId,
            AssetId = assetId,
            Status = VideoFlyerAssetStatus.Uploading,
            InitializedAt = now,
            RequestedByUserId = command.UploaderUserId,
        };
        await _repository.SaveUploadAsync(initialUpload, ct);

        // Stage 2 — write file to storage layer
        buffer.Position = 0;
        var storageRef = await _storage.SaveAsync(
            new VideoStorageWriteRequest(assetId, command.OriginalFilename, normalizedContentType, buffer),
            ct);

        // Stage 3 — create asset record, status = Uploaded
        var asset = new VideoFlyerAsset
        {
            AssetId = assetId,
            Status = VideoFlyerAssetStatus.Uploaded,
            ContentType = normalizedContentType,
            FileSizeBytes = bytes.LongLength,
            ChecksumSha256 = checksum,
            OriginalFilename = command.OriginalFilename,
            UploadedAt = now,
            UploaderUserId = command.UploaderUserId,
            Provenance = command.Provenance,
            Storage = storageRef,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await _repository.SaveAssetAsync(asset, ct);

        // Stage 4 — advance the upload session to Uploaded
        var uploadedSession = initialUpload with { Status = VideoFlyerAssetStatus.Uploaded };
        await _repository.UpdateUploadAsync(uploadedSession, ct);

        return uploadedSession;
    }

    public Task<VideoFlyerUpload?> GetUploadAsync(string uploadId, CancellationToken ct = default) =>
        _repository.GetUploadAsync(uploadId, ct);

    public Task<VideoFlyerAsset?> GetAssetAsync(string assetId, CancellationToken ct = default) =>
        _repository.GetAssetAsync(assetId, ct);

    public async Task<VideoUploadCompletionResult?> CompleteUploadAsync(
        string uploadId,
        CompleteVideoUploadCommand command,
        CancellationToken ct = default)
    {
        var upload = await _repository.GetUploadAsync(uploadId, ct);
        if (upload is null)
            return null;

        if (upload.Status != VideoFlyerAssetStatus.Uploaded)
        {
            throw new InvalidOperationException(
                $"Upload '{uploadId}' is in state '{upload.Status}' and cannot be completed. Expected 'Uploaded'.");
        }

        var asset = await _repository.GetAssetAsync(upload.AssetId, ct);
        if (asset is null)
        {
            throw new InvalidOperationException(
                $"Asset '{upload.AssetId}' referenced by upload '{uploadId}' was not found.");
        }

        var now = DateTimeOffset.UtcNow;

        // — Failure path: mark rejected, no job created —
        if (!command.Success)
        {
            var failedUpload = upload with
            {
                Status = VideoFlyerAssetStatus.Rejected,
                CompletedAt = now,
                FailureReason = command.FailureReason ?? "client signalled upload failure",
            };
            await _repository.UpdateUploadAsync(failedUpload, ct);

            var rejectedAsset = asset with
            {
                Status = VideoFlyerAssetStatus.Rejected,
                UpdatedAt = now,
            };
            await _repository.UpdateAssetAsync(rejectedAsset, ct);

            return new VideoUploadCompletionResult(failedUpload, rejectedAsset, Job: null);
        }

        // — Success path: create processing job, advance to ProcessingPending —
        var jobId = Guid.NewGuid().ToString("N");
        var job = new VideoProcessingJob
        {
            JobId = jobId,
            AssetId = asset.AssetId,
            Status = VideoProcessingJobStatus.Queued,
            QueuedAt = now,
        };
        await _repository.SaveJobAsync(job, ct);

        // Attach any client-reported hints to the asset record (advisory; verified by pipeline)
        var pendingAsset = asset with
        {
            Status = VideoFlyerAssetStatus.ProcessingPending,
            ProcessingJobId = jobId,
            DetectedCodec = command.DetectedCodec ?? asset.DetectedCodec,
            DurationSeconds = command.ClientDurationSeconds ?? asset.DurationSeconds,
            WidthPx = command.ClientWidthPx ?? asset.WidthPx,
            HeightPx = command.ClientHeightPx ?? asset.HeightPx,
            UpdatedAt = now,
        };
        await _repository.UpdateAssetAsync(pendingAsset, ct);

        var completedUpload = upload with
        {
            Status = VideoFlyerAssetStatus.ProcessingPending,
            CompletedAt = now,
        };
        await _repository.UpdateUploadAsync(completedUpload, ct);

        return new VideoUploadCompletionResult(completedUpload, pendingAsset, job);
    }

    public Task<VideoProcessingJob?> GetJobAsync(string jobId, CancellationToken ct = default) =>
        _repository.GetJobAsync(jobId, ct);
}
