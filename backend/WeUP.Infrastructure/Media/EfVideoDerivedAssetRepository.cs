using Microsoft.EntityFrameworkCore;
using WeUP.Domain.Media;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Media;

public sealed class EfVideoDerivedAssetRepository : IVideoDerivedAssetRepository
{
    private readonly WeUpDbContext _db;

    public EfVideoDerivedAssetRepository(WeUpDbContext db)
    {
        _db = db;
    }

    public async Task SaveBatchAsync(IReadOnlyList<VideoDerivedFrameAsset> records, CancellationToken ct = default)
    {
        if (records.Count == 0)
            return;

        var ids = records.Select(x => x.DerivedAssetId).ToArray();
        var existing = await _db.VideoDerivedFrames.Where(x => ids.Contains(x.DerivedAssetId)).ToListAsync(ct);
        var existingById = existing.ToDictionary(x => x.DerivedAssetId, StringComparer.OrdinalIgnoreCase);

        foreach (var record in records)
        {
            if (existingById.TryGetValue(record.DerivedAssetId, out var entity))
            {
                Update(entity, record);
            }
            else
            {
                _db.VideoDerivedFrames.Add(ToEntity(record));
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<VideoDerivedFrameAsset?> GetPosterAsync(string sourceVideoAssetId, CancellationToken ct = default)
    {
        var entity = await _db.VideoDerivedFrames.AsNoTracking()
            .Where(x => x.SourceVideoAssetId == sourceVideoAssetId && x.IsPosterSelected)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task<IReadOnlyList<VideoDerivedFrameAsset>> ListFramesAsync(string sourceVideoAssetId, CancellationToken ct = default)
    {
        var entities = await _db.VideoDerivedFrames.AsNoTracking()
            .Where(x => x.SourceVideoAssetId == sourceVideoAssetId)
            .OrderBy(x => x.TimestampOffsetMs)
            .ThenBy(x => x.DerivedAssetId)
            .ToArrayAsync(ct);

        return entities.Select(ToRecord).ToArray();
    }

    private static VideoDerivedFrameEntity ToEntity(VideoDerivedFrameAsset record) => new()
    {
        Id = Guid.NewGuid(),
        SourceVideoAssetId = record.SourceVideoAssetId,
        DerivedAssetId = record.DerivedAssetId,
        ProcessingJobId = record.ProcessingJobId,
        UploaderUserId = record.UploaderUserId,
        SubmissionId = record.SubmissionId,
        VenueId = record.VenueId,
        ModerationItemId = record.ModerationItemId,
        TimestampOffsetMs = record.TimestampOffsetMs,
        FrameType = record.FrameType.ToString(),
        WidthPx = record.WidthPx,
        HeightPx = record.HeightPx,
        StorageProvider = record.Storage.Provider.ToString(),
        StorageContainer = record.Storage.Container,
        StorageObjectKey = record.Storage.ObjectKey,
        StorageUri = record.Storage.Uri,
        ExtractionStage = record.ExtractionStage,
        ExtractionVersion = record.ExtractionVersion,
        IsPosterSelected = record.IsPosterSelected,
        PosterSelectionReason = record.PosterSelectionReason,
        CreatedAt = record.CreatedAt,
    };

    private static VideoDerivedFrameAsset ToRecord(VideoDerivedFrameEntity entity)
    {
        Enum.TryParse<VideoFrameType>(entity.FrameType, true, out var frameType);
        Enum.TryParse<MediaStorageProvider>(entity.StorageProvider, true, out var provider);

        return new VideoDerivedFrameAsset
        {
            SourceVideoAssetId = entity.SourceVideoAssetId,
            DerivedAssetId = entity.DerivedAssetId,
            ProcessingJobId = entity.ProcessingJobId,
            UploaderUserId = entity.UploaderUserId,
            SubmissionId = entity.SubmissionId,
            VenueId = entity.VenueId,
            ModerationItemId = entity.ModerationItemId,
            TimestampOffsetMs = entity.TimestampOffsetMs,
            FrameType = frameType,
            WidthPx = entity.WidthPx,
            HeightPx = entity.HeightPx,
            Storage = new MediaStorageRef(provider, entity.StorageContainer, entity.StorageObjectKey, entity.StorageUri),
            ExtractionStage = entity.ExtractionStage,
            ExtractionVersion = entity.ExtractionVersion,
            IsPosterSelected = entity.IsPosterSelected,
            PosterSelectionReason = entity.PosterSelectionReason,
            CreatedAt = entity.CreatedAt,
        };
    }

    private static void Update(VideoDerivedFrameEntity entity, VideoDerivedFrameAsset record)
    {
        entity.ProcessingJobId = record.ProcessingJobId;
        entity.UploaderUserId = record.UploaderUserId;
        entity.SubmissionId = record.SubmissionId;
        entity.VenueId = record.VenueId;
        entity.ModerationItemId = record.ModerationItemId;
        entity.TimestampOffsetMs = record.TimestampOffsetMs;
        entity.FrameType = record.FrameType.ToString();
        entity.WidthPx = record.WidthPx;
        entity.HeightPx = record.HeightPx;
        entity.StorageProvider = record.Storage.Provider.ToString();
        entity.StorageContainer = record.Storage.Container;
        entity.StorageObjectKey = record.Storage.ObjectKey;
        entity.StorageUri = record.Storage.Uri;
        entity.ExtractionStage = record.ExtractionStage;
        entity.ExtractionVersion = record.ExtractionVersion;
        entity.IsPosterSelected = record.IsPosterSelected;
        entity.PosterSelectionReason = record.PosterSelectionReason;
        entity.CreatedAt = record.CreatedAt;
    }
}
