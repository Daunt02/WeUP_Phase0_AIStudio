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
    private readonly string _uploadDir;

    public LocalFlyerUploadService(IFlyerAssetStore store, IFlyerAssetValidator validator)
    {
        _store = store;
        _validator = validator;
        _uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "flyers");
        Directory.CreateDirectory(_uploadDir);
    }

    public async Task<string> UploadFlyerAsync(
        Stream fileStream,
        string filename,
        string contentType,
        string submitterId,
        CancellationToken ct = default)
    {
        // Validate before any storage write
        var validation = await _validator.ValidateAsync(fileStream, contentType, filename, submitterId, ct);

        if (validation.IsDuplicate)
            throw new FlyerUploadException(
                $"This file has already been uploaded (asset: {validation.DuplicateAssetId})",
                FlyerUploadErrorCode.Duplicate,
                validation.DuplicateAssetId);

        if (!validation.IsValid)
            throw new FlyerUploadException(
                validation.FailureReason ?? "Validation failed",
                FlyerUploadErrorCode.ValidationFailed);

        var assetId = Guid.NewGuid().ToString("N");
        var storageKey = $"{assetId}_{SanitizeFilename(filename)}";
        var localPath = Path.Combine(_uploadDir, storageKey);

        // Write to local storage
        fileStream.Position = 0;
        await using var dest = File.OpenWrite(localPath);
        await fileStream.CopyToAsync(dest, ct);
        var fileSizeBytes = new FileInfo(localPath).Length;

        // Create record: Initialized → Uploaded → ProcessingPending (auto-advance on local dev path)
        var record = new FlyerAssetRecord
        {
            AssetId          = assetId,
            OriginalFilename = filename,
            FileSizeBytes    = fileSizeBytes,
            ContentType      = contentType,
            SubmitterId      = submitterId,
            UploadedAt       = DateTimeOffset.UtcNow,
            Status           = FlyerAssetStatus.Initialized,
            ContentHash      = validation.ContentHash!,
            StorageKey       = storageKey,
            LocalPath        = localPath,
        };

        record = record.WithStatus(FlyerAssetStatus.Uploaded);
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
        new(r.AssetId, r.OriginalFilename, r.FileSizeBytes, r.ContentType, r.SubmitterId, r.UploadedAt, r.S3Url, r.LocalPath);

    private static string SanitizeFilename(string filename)
    {
        var name = Path.GetFileNameWithoutExtension(filename);
        var ext = Path.GetExtension(filename);
        return (name.Length > 40 ? name[..40] : name) + ext;
    }
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
