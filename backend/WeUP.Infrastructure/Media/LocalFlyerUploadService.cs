using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

/// <summary>
/// P25/P26: Local file-based flyer upload service with lifecycle and validation.
/// Uses IFlyerAssetStore for durable record-keeping and IFlyerAssetValidator for safety.
/// Future: swap to cloud storage provider (S3/Cloudinary) in Phase 0.5.
/// </summary>
public sealed class LocalFlyerUploadService : IFlyerUploadService
{
    private readonly IFlyerAssetStore _store;
    private readonly IFlyerAssetValidator _validator;
    private readonly IFlyerAssetLifecyclePolicy _lifecyclePolicy;
    private readonly string _uploadDir;

    public LocalFlyerUploadService(
        IFlyerAssetStore store,
        IFlyerAssetValidator validator,
        IFlyerAssetLifecyclePolicy lifecyclePolicy)
    {
        _store = store;
        _validator = validator;
        _lifecyclePolicy = lifecyclePolicy;
        _uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "flyers");
        Directory.CreateDirectory(_uploadDir);
    }

    public async Task<string> UploadFlyerAsync(
        Stream fileStream,
        string filename,
        string contentType,
        string submitterId,
        string? sourceReference = null,
        bool allowSameSourceReupload = false,
        CancellationToken ct = default)
    {
        var assetId = Guid.NewGuid().ToString("N");
        var extension = Path.GetExtension(filename);
        var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".bin" : extension.ToLowerInvariant();
        var storageKey = $"flyers/{DateTimeOffset.UtcNow:yyyyMM}/{assetId}{safeExtension}";
        var localPath = Path.Combine(_uploadDir, storageKey);
        var localDir = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrWhiteSpace(localDir))
        {
            Directory.CreateDirectory(localDir);
        }

        // Persist upload first so all validation outcomes map to explicit lifecycle states.
        fileStream.Position = 0;
        await using var dest = File.OpenWrite(localPath);
        await fileStream.CopyToAsync(dest, ct);
        var fileSizeBytes = new FileInfo(localPath).Length;

        var record = await _store.SaveAsync(new FlyerAssetRecord
        {
            AssetId          = assetId,
            OriginalFilename = filename,
            FileSizeBytes    = fileSizeBytes,
            ContentType      = contentType,
            SubmitterId      = submitterId,
            UploadedAt       = DateTimeOffset.UtcNow,
            Status           = FlyerAssetStatus.Initialized,
            ContentHash      = string.Empty,
            StorageKey       = storageKey,
            LocalPath        = localPath,
            SourceReference  = sourceReference,
        }, ct);

        record = await _store.UpdateStatusAsync(record.AssetId, _lifecyclePolicy.Transition(record.Status, FlyerAssetStatus.Uploaded), ct);

        await using var validationStream = File.OpenRead(localPath);
        var validation = await _validator.ValidateAsync(
            validationStream,
            contentType,
            filename,
            submitterId,
            sourceReference,
            allowSameSourceReupload,
            ct);

        if (validation.IsDuplicate)
        {
            await _store.UpdateValidationFailureAsync(record.AssetId, "Duplicate upload detected", ct);
            throw new FlyerUploadException(
                $"This file has already been uploaded (asset: {validation.DuplicateAssetId})",
                FlyerUploadErrorCode.Duplicate,
                validation.DuplicateAssetId);
        }

        if (!validation.IsValid)
        {
            await _store.UpdateValidationFailureAsync(record.AssetId, validation.FailureReason ?? "Validation failed", ct);
            throw new FlyerUploadException(
                validation.FailureReason ?? "Validation failed",
                FlyerUploadErrorCode.ValidationFailed);
        }

        record = record with
        {
            ContentHash = validation.ContentHash ?? string.Empty,
            CanonicalContentType = validation.CanonicalContentType ?? contentType,
            WidthPx = validation.WidthPx,
            HeightPx = validation.HeightPx,
            IsAnimated = validation.IsAnimated,
        };

        record = record.WithStatus(FlyerAssetStatus.ProcessingPending);

        await _store.SaveAsync(record, ct);
        return assetId;
    }

    public async Task<FlyerAsset?> GetFlyerAsync(string assetId, CancellationToken ct = default)
    {
        var record = await _store.GetAsync(assetId, ct);
        return record is null ? null : ToFlyerAsset(record);
    }

    public async Task<FlyerAsset[]> ListUserFlyersAsync(string submitterId, CancellationToken ct = default)
    {
        var records = await _store.ListBySubmitterAsync(submitterId, ct);
        return records.Select(ToFlyerAsset).ToArray();
    }

    public async Task DeleteFlyerAsync(string assetId, CancellationToken ct = default)
    {
        var record = await _store.GetAsync(assetId, ct);
        if (record is null) return;

        if (FlyerAssetLifecycle.CanTransition(record.Status, FlyerAssetStatus.Archived))
            await _store.UpdateStatusAsync(assetId, FlyerAssetStatus.Archived, ct);
        else
            await _store.DeleteAsync(assetId, ct);

        if (record.LocalPath is not null && File.Exists(record.LocalPath))
        {
            try { File.Delete(record.LocalPath); }
            catch { /* log in production */ }
        }
    }

    private static FlyerAsset ToFlyerAsset(FlyerAssetRecord r) =>
        new(
            r.AssetId,
            r.OriginalFilename,
            r.FileSizeBytes,
            r.ContentType,
            r.SubmitterId,
            r.UploadedAt,
            r.Status.ToString(),
            r.ContentHash,
            r.StorageKey,
            r.Revision,
            r.ValidationFailureReason,
            r.WidthPx,
            r.HeightPx,
            r.IsAnimated,
            r.S3Url,
            r.LocalPath);
}

/// <summary>Error codes for structured flyer upload failures.</summary>
public enum FlyerUploadErrorCode { ValidationFailed, Duplicate }

/// <summary>Thrown when a flyer upload cannot proceed due to validation or duplication.</summary>
public sealed class FlyerUploadException : Exception
{
    public FlyerUploadErrorCode ErrorCode { get; }
    public string? DuplicateAssetId { get; }

    public FlyerUploadException(string message, FlyerUploadErrorCode code, string? duplicateAssetId = null)
        : base(message)
    {
        ErrorCode = code;
        DuplicateAssetId = duplicateAssetId;
    }
}
