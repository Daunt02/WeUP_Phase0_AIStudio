using System.Security.Cryptography;
using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

public sealed class MediaIntakeService : IMediaIntakeService
{
    private readonly IMediaIntakeRepository _repository;
    private readonly IMediaStorageService _storage;
    private readonly MediaIntakeValidation _validation;

    public MediaIntakeService(
        IMediaIntakeRepository repository,
        IMediaStorageService storage,
        MediaIntakeValidation validation)
    {
        _repository = repository;
        _storage = storage;
        _validation = validation;
    }

    public async Task<MediaUpload> CreateUploadAsync(
        CreateMediaUploadCommand command,
        Stream fileStream,
        CancellationToken ct = default)
    {
        var normalizedContentType = MediaIntakeValidation.NormalizeContentType(command.ContentType);
        var validationError = _validation.Validate(command.AssetType, normalizedContentType, command.FileSizeBytes, command.OriginalFilename);
        if (validationError is not null)
        {
            throw new InvalidOperationException(validationError);
        }

        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }

        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream, ct);
        var bytes = memoryStream.ToArray();
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var assetId = Guid.NewGuid().ToString("N");
        var uploadId = Guid.NewGuid().ToString("N");

        var initialUpload = new MediaUpload(
            uploadId,
            assetId,
            MediaAssetStatus.Uploading,
            DateTimeOffset.UtcNow,
            null,
            command.UploaderUserId);

        await _repository.SaveUploadAsync(initialUpload, ct);

        memoryStream.Position = 0;
        var storageRef = await _storage.SaveAsync(
            new MediaStorageWriteRequest(assetId, command.AssetType, command.OriginalFilename, normalizedContentType, new MemoryStream(bytes)),
            ct);

        var asset = new MediaAsset(
            assetId,
            command.AssetType,
            MediaAssetStatus.Uploaded,
            normalizedContentType,
            bytes.LongLength,
            checksum,
            command.OriginalFilename,
            DateTimeOffset.UtcNow,
            command.UploaderUserId,
            command.Owner,
            storageRef,
            command.SubmissionId,
            command.MetadataJson);

        await _repository.SaveAssetAsync(asset, ct);

        var uploaded = initialUpload with { Status = MediaAssetStatus.Uploaded };
        await _repository.UpdateUploadAsync(uploaded, ct);

        return uploaded;
    }

    public Task<MediaUpload?> GetUploadAsync(string uploadId, CancellationToken ct = default) =>
        _repository.GetUploadAsync(uploadId, ct);

    public Task<MediaAsset?> GetAssetAsync(string assetId, CancellationToken ct = default) =>
        _repository.GetAssetAsync(assetId, ct);

    public async Task<MediaUpload?> CompleteUploadAsync(string uploadId, CompleteMediaUploadCommand command, CancellationToken ct = default)
    {
        var upload = await _repository.GetUploadAsync(uploadId, ct);
        if (upload is null)
        {
            return null;
        }

        if (!command.ProcessingSucceeded)
        {
            await _repository.UpdateAssetStatusAsync(upload.AssetId, MediaAssetStatus.Rejected, command.MetadataJson, ct);
            var failedUpload = upload with
            {
                Status = MediaAssetStatus.Rejected,
                CompletedAt = DateTimeOffset.UtcNow,
                FailureReason = command.FailureReason ?? "processing failed",
            };
            await _repository.UpdateUploadAsync(failedUpload, ct);
            return failedUpload;
        }

        await _repository.UpdateAssetStatusAsync(upload.AssetId, MediaAssetStatus.ProcessingPending, command.MetadataJson, ct);
        await _repository.UpdateAssetStatusAsync(upload.AssetId, MediaAssetStatus.ProcessingComplete, command.MetadataJson, ct);

        var finalStatus = command.QueueForReview ? MediaAssetStatus.ReviewPending : MediaAssetStatus.ProcessingComplete;
        await _repository.UpdateAssetStatusAsync(upload.AssetId, finalStatus, command.MetadataJson, ct);

        var completedUpload = upload with
        {
            Status = finalStatus,
            CompletedAt = DateTimeOffset.UtcNow,
            FailureReason = null,
        };
        await _repository.UpdateUploadAsync(completedUpload, ct);
        return completedUpload;
    }
}
