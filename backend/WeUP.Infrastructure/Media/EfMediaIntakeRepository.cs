using Microsoft.EntityFrameworkCore;
using WeUP.Domain.Media;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Media;

public sealed class EfMediaIntakeRepository : IMediaIntakeRepository
{
    private readonly WeUpDbContext _db;

    public EfMediaIntakeRepository(WeUpDbContext db)
    {
        _db = db;
    }

    public async Task SaveAssetAsync(MediaAsset asset, CancellationToken ct = default)
    {
        var existing = await _db.MediaAssets.FirstOrDefaultAsync(a => a.AssetId == asset.AssetId, ct);
        if (existing is null)
        {
            _db.MediaAssets.Add(ToEntity(asset));
        }
        else
        {
            UpdateEntity(existing, asset);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveUploadAsync(MediaUpload upload, CancellationToken ct = default)
    {
        var existing = await _db.MediaUploads.FirstOrDefaultAsync(u => u.UploadId == upload.UploadId, ct);
        if (existing is null)
        {
            _db.MediaUploads.Add(ToEntity(upload));
        }
        else
        {
            UpdateEntity(existing, upload);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<MediaAsset?> GetAssetAsync(string assetId, CancellationToken ct = default)
    {
        var entity = await _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(a => a.AssetId == assetId, ct);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<MediaUpload?> GetUploadAsync(string uploadId, CancellationToken ct = default)
    {
        var entity = await _db.MediaUploads.AsNoTracking().FirstOrDefaultAsync(u => u.UploadId == uploadId, ct);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task UpdateAssetStatusAsync(string assetId, MediaAssetStatus status, string? metadataJson = null, CancellationToken ct = default)
    {
        var entity = await _db.MediaAssets.FirstOrDefaultAsync(a => a.AssetId == assetId, ct);
        if (entity is null)
        {
            return;
        }

        entity.Status = status.ToString();
        if (metadataJson is not null)
        {
            entity.MetadataJson = metadataJson;
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public Task UpdateUploadAsync(MediaUpload upload, CancellationToken ct = default) => SaveUploadAsync(upload, ct);

    private static MediaAssetEntity ToEntity(MediaAsset asset) => new()
    {
        Id = Guid.NewGuid(),
        AssetId = asset.AssetId,
        AssetType = asset.AssetType.ToString(),
        Status = asset.Status.ToString(),
        ContentType = asset.ContentType,
        FileSizeBytes = asset.FileSizeBytes,
        ChecksumSha256 = asset.ChecksumSha256,
        OriginalFilename = asset.OriginalFilename,
        UploadedAt = asset.UploadedAt,
        UploaderUserId = asset.UploaderUserId,
        OwnerType = asset.Owner.OwnerType.ToString(),
        OwnerId = asset.Owner.OwnerId,
        VenueId = asset.Owner.VenueId,
        IngestionWorkflowSource = asset.Owner.IngestionWorkflowSource,
        StorageProvider = asset.Storage.Provider.ToString(),
        StorageContainer = asset.Storage.Container,
        StorageObjectKey = asset.Storage.ObjectKey,
        StorageUri = asset.Storage.Uri,
        StorageETag = asset.Storage.ETag,
        StorageVersionId = asset.Storage.VersionId,
        SubmissionId = asset.SubmissionId,
        MetadataJson = asset.MetadataJson,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static MediaUploadEntity ToEntity(MediaUpload upload) => new()
    {
        Id = Guid.NewGuid(),
        UploadId = upload.UploadId,
        AssetId = upload.AssetId,
        Status = upload.Status.ToString(),
        InitializedAt = upload.InitializedAt,
        CompletedAt = upload.CompletedAt,
        RequestedByUserId = upload.RequestedByUserId,
        FailureReason = upload.FailureReason,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static void UpdateEntity(MediaAssetEntity entity, MediaAsset asset)
    {
        entity.AssetType = asset.AssetType.ToString();
        entity.Status = asset.Status.ToString();
        entity.ContentType = asset.ContentType;
        entity.FileSizeBytes = asset.FileSizeBytes;
        entity.ChecksumSha256 = asset.ChecksumSha256;
        entity.OriginalFilename = asset.OriginalFilename;
        entity.UploadedAt = asset.UploadedAt;
        entity.UploaderUserId = asset.UploaderUserId;
        entity.OwnerType = asset.Owner.OwnerType.ToString();
        entity.OwnerId = asset.Owner.OwnerId;
        entity.VenueId = asset.Owner.VenueId;
        entity.IngestionWorkflowSource = asset.Owner.IngestionWorkflowSource;
        entity.StorageProvider = asset.Storage.Provider.ToString();
        entity.StorageContainer = asset.Storage.Container;
        entity.StorageObjectKey = asset.Storage.ObjectKey;
        entity.StorageUri = asset.Storage.Uri;
        entity.StorageETag = asset.Storage.ETag;
        entity.StorageVersionId = asset.Storage.VersionId;
        entity.SubmissionId = asset.SubmissionId;
        entity.MetadataJson = asset.MetadataJson;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void UpdateEntity(MediaUploadEntity entity, MediaUpload upload)
    {
        entity.AssetId = upload.AssetId;
        entity.Status = upload.Status.ToString();
        entity.InitializedAt = upload.InitializedAt;
        entity.CompletedAt = upload.CompletedAt;
        entity.RequestedByUserId = upload.RequestedByUserId;
        entity.FailureReason = upload.FailureReason;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static MediaAsset ToDomain(MediaAssetEntity entity)
    {
        Enum.TryParse<MediaAssetType>(entity.AssetType, true, out var assetType);
        Enum.TryParse<MediaAssetStatus>(entity.Status, true, out var status);
        Enum.TryParse<MediaOwnerType>(entity.OwnerType, true, out var ownerType);
        Enum.TryParse<MediaStorageProvider>(entity.StorageProvider, true, out var storageProvider);

        return new MediaAsset(
            entity.AssetId,
            assetType,
            status,
            entity.ContentType,
            entity.FileSizeBytes,
            entity.ChecksumSha256,
            entity.OriginalFilename,
            entity.UploadedAt,
            entity.UploaderUserId,
            new MediaOwnerRef(ownerType, entity.OwnerId, entity.VenueId, entity.IngestionWorkflowSource),
            new MediaStorageRef(storageProvider, entity.StorageContainer, entity.StorageObjectKey, entity.StorageUri, entity.StorageETag, entity.StorageVersionId),
            entity.SubmissionId,
            entity.MetadataJson);
    }

    private static MediaUpload ToDomain(MediaUploadEntity entity)
    {
        Enum.TryParse<MediaAssetStatus>(entity.Status, true, out var status);

        return new MediaUpload(
            entity.UploadId,
            entity.AssetId,
            status,
            entity.InitializedAt,
            entity.CompletedAt,
            entity.RequestedByUserId,
            entity.FailureReason);
    }
}
