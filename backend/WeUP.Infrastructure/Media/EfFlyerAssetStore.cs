using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeUP.Domain.Media;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Media;

public sealed class EfFlyerAssetStore : IFlyerAssetStore
{
    private readonly WeUpDbContext _db;

    public EfFlyerAssetStore(WeUpDbContext db)
    {
        _db = db;
    }

    public async Task<FlyerAssetRecord> SaveAsync(FlyerAssetRecord asset, CancellationToken ct = default)
    {
        var existing = await _db.MediaAssets.FirstOrDefaultAsync(x => x.AssetId == asset.AssetId, ct);
        if (existing is null)
        {
            _db.MediaAssets.Add(ToEntity(asset));
        }
        else
        {
            UpdateEntity(existing, asset);
        }

        await _db.SaveChangesAsync(ct);
        return asset;
    }

    public async Task<FlyerAssetRecord?> GetAsync(string assetId, CancellationToken ct = default)
    {
        var entity = await _db.MediaAssets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AssetId == assetId && x.AssetType == MediaAssetType.FlyerImage.ToString(), ct);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task<FlyerAssetRecord[]> ListBySubmitterAsync(string submitterId, CancellationToken ct = default)
    {
        var entities = await _db.MediaAssets.AsNoTracking()
            .Where(x => x.AssetType == MediaAssetType.FlyerImage.ToString()
                     && x.UploaderUserId == submitterId
                     && x.Status != FlyerAssetStatus.Archived.ToString())
            .OrderByDescending(x => x.UploadedAt)
            .ToArrayAsync(ct);

        return entities.Select(ToRecord).ToArray();
    }

    public async Task<FlyerAssetRecord?> FindByHashAsync(string contentHash, string submitterId, CancellationToken ct = default)
    {
        var entity = await _db.MediaAssets.AsNoTracking()
            .Where(x => x.AssetType == MediaAssetType.FlyerImage.ToString()
                     && x.UploaderUserId == submitterId
                     && x.ContentHash == contentHash
                     && x.Status != FlyerAssetStatus.Archived.ToString()
                     && x.Status != FlyerAssetStatus.ValidationFailed.ToString())
            .OrderByDescending(x => x.UploadedAt)
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task<FlyerAssetRecord?> FindRecentByHashAsync(string contentHash, string submitterId, DateTimeOffset sinceUtc, CancellationToken ct = default)
    {
        var entity = await _db.MediaAssets.AsNoTracking()
            .Where(x => x.AssetType == MediaAssetType.FlyerImage.ToString()
                     && x.UploaderUserId == submitterId
                     && x.ContentHash == contentHash
                     && x.UploadedAt >= sinceUtc
                     && x.Status != FlyerAssetStatus.Archived.ToString())
            .OrderByDescending(x => x.UploadedAt)
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task<FlyerAssetRecord> UpdateStatusAsync(string assetId, FlyerAssetStatus next, CancellationToken ct = default)
    {
        var existing = await _db.MediaAssets.FirstOrDefaultAsync(x => x.AssetId == assetId && x.AssetType == MediaAssetType.FlyerImage.ToString(), ct)
            ?? throw new KeyNotFoundException($"Flyer asset '{assetId}' not found");

        Enum.TryParse<FlyerAssetStatus>(existing.Status, true, out var currentStatus);
        existing.Status = FlyerAssetLifecycle.Transition(currentStatus, next).ToString();
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToRecord(existing);
    }

    public async Task<FlyerAssetRecord> UpdateValidationFailureAsync(string assetId, string failureReason, CancellationToken ct = default)
    {
        var existing = await _db.MediaAssets.FirstOrDefaultAsync(x => x.AssetId == assetId && x.AssetType == MediaAssetType.FlyerImage.ToString(), ct)
            ?? throw new KeyNotFoundException($"Flyer asset '{assetId}' not found");

        Enum.TryParse<FlyerAssetStatus>(existing.Status, true, out var currentStatus);
        existing.Status = FlyerAssetLifecycle.Transition(currentStatus, FlyerAssetStatus.ValidationFailed).ToString();

        var existingMetadata = DeserializeMetadata(existing.MetadataJson);
        var metadata = new FlyerAssetMetadata
        {
            Revision = existingMetadata.Revision,
            LocalPath = existingMetadata.LocalPath,
            S3Url = existingMetadata.S3Url,
            ValidationFailureReason = failureReason,
        };
        existing.MetadataJson = SerializeMetadata(metadata);
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToRecord(existing);
    }

    public async Task DeleteAsync(string assetId, CancellationToken ct = default)
    {
        var existing = await _db.MediaAssets.FirstOrDefaultAsync(x => x.AssetId == assetId && x.AssetType == MediaAssetType.FlyerImage.ToString(), ct);
        if (existing is null)
        {
            return;
        }

        existing.Status = FlyerAssetStatus.Archived.ToString();
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static MediaAssetEntity ToEntity(FlyerAssetRecord asset)
    {
        var metadata = new FlyerAssetMetadata
        {
            Revision = asset.Revision,
            LocalPath = asset.LocalPath,
            S3Url = asset.S3Url,
            ValidationFailureReason = asset.ValidationFailureReason,
        };

        return new MediaAssetEntity
        {
            Id = Guid.NewGuid(),
            AssetId = asset.AssetId,
            AssetType = MediaAssetType.FlyerImage.ToString(),
            Status = asset.Status.ToString(),
            ContentType = asset.ContentType,
            FileSizeBytes = asset.FileSizeBytes,
            ChecksumSha256 = asset.ContentHash,
            ContentHash = asset.ContentHash,
            OriginalFilename = asset.OriginalFilename,
            CanonicalContentType = asset.CanonicalContentType,
            WidthPx = asset.WidthPx,
            HeightPx = asset.HeightPx,
            IsAnimated = asset.IsAnimated,
            UploadedAt = asset.UploadedAt,
            UploaderUserId = asset.SubmitterId,
            OwnerType = MediaOwnerType.User.ToString(),
            OwnerId = asset.SubmitterId,
            IngestionWorkflowSource = asset.SourceReference,
            StorageProvider = MediaStorageProvider.LocalFileSystem.ToString(),
            StorageContainer = "flyers",
            StorageObjectKey = asset.StorageKey,
            MetadataJson = SerializeMetadata(metadata),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    private static void UpdateEntity(MediaAssetEntity entity, FlyerAssetRecord asset)
    {
        var metadata = new FlyerAssetMetadata
        {
            Revision = asset.Revision,
            LocalPath = asset.LocalPath,
            S3Url = asset.S3Url,
            ValidationFailureReason = asset.ValidationFailureReason,
        };

        entity.Status = asset.Status.ToString();
        entity.ContentType = asset.ContentType;
        entity.FileSizeBytes = asset.FileSizeBytes;
        entity.ChecksumSha256 = asset.ContentHash;
        entity.ContentHash = asset.ContentHash;
        entity.OriginalFilename = asset.OriginalFilename;
        entity.CanonicalContentType = asset.CanonicalContentType;
        entity.WidthPx = asset.WidthPx;
        entity.HeightPx = asset.HeightPx;
        entity.IsAnimated = asset.IsAnimated;
        entity.UploadedAt = asset.UploadedAt;
        entity.UploaderUserId = asset.SubmitterId;
        entity.OwnerId = asset.SubmitterId;
        entity.IngestionWorkflowSource = asset.SourceReference;
        entity.StorageObjectKey = asset.StorageKey;
        entity.MetadataJson = SerializeMetadata(metadata);
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static FlyerAssetRecord ToRecord(MediaAssetEntity entity)
    {
        Enum.TryParse<FlyerAssetStatus>(entity.Status, true, out var status);
        var metadata = DeserializeMetadata(entity.MetadataJson);

        return new FlyerAssetRecord
        {
            AssetId = entity.AssetId,
            OriginalFilename = entity.OriginalFilename,
            FileSizeBytes = entity.FileSizeBytes,
            ContentType = entity.ContentType,
            SubmitterId = entity.UploaderUserId ?? entity.OwnerId ?? string.Empty,
            UploadedAt = entity.UploadedAt,
            Status = status,
            ContentHash = entity.ContentHash ?? entity.ChecksumSha256,
            StorageKey = entity.StorageObjectKey,
            Revision = metadata.Revision,
            SourceReference = entity.IngestionWorkflowSource,
            CanonicalContentType = entity.CanonicalContentType,
            WidthPx = entity.WidthPx,
            HeightPx = entity.HeightPx,
            IsAnimated = entity.IsAnimated,
            LocalPath = metadata.LocalPath,
            S3Url = metadata.S3Url,
            ValidationFailureReason = metadata.ValidationFailureReason,
        };
    }

    private static string SerializeMetadata(FlyerAssetMetadata metadata) => JsonSerializer.Serialize(metadata);

    private static FlyerAssetMetadata DeserializeMetadata(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return new FlyerAssetMetadata();
        }

        try
        {
            return JsonSerializer.Deserialize<FlyerAssetMetadata>(metadata) ?? new FlyerAssetMetadata();
        }
        catch
        {
            return new FlyerAssetMetadata();
        }
    }

    private sealed class FlyerAssetMetadata
    {
        public int Revision { get; init; } = 1;
        public string? LocalPath { get; init; }
        public string? S3Url { get; init; }
        public string? ValidationFailureReason { get; init; }
    }
}
