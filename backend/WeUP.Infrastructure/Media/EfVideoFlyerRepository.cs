using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeUP.Domain.Media;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Media;

/// <summary>
/// EF-backed repository for durable video flyer assets, uploads, and processing jobs.
/// </summary>
public sealed class EfVideoFlyerRepository : IVideoFlyerRepository
{
    private readonly WeUpDbContext _db;

    public EfVideoFlyerRepository(WeUpDbContext db)
    {
        _db = db;
    }

    public async Task SaveAssetAsync(VideoFlyerAsset asset, CancellationToken ct = default)
    {
        _db.VideoFlyerAssets.Add(ToEntity(asset));
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveUploadAsync(VideoFlyerUpload upload, CancellationToken ct = default)
    {
        _db.VideoFlyerUploads.Add(ToEntity(upload));
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveJobAsync(VideoProcessingJob job, CancellationToken ct = default)
    {
        _db.VideoProcessingJobs.Add(ToEntity(job));
        await _db.SaveChangesAsync(ct);
    }

    public async Task<VideoFlyerAsset?> GetAssetAsync(string assetId, CancellationToken ct = default)
    {
        var entity = await _db.VideoFlyerAssets.AsNoTracking().FirstOrDefaultAsync(x => x.AssetId == assetId, ct);
        return entity is null ? null : ToAsset(entity);
    }

    public async Task<VideoFlyerUpload?> GetUploadAsync(string uploadId, CancellationToken ct = default)
    {
        var entity = await _db.VideoFlyerUploads.AsNoTracking().FirstOrDefaultAsync(x => x.UploadId == uploadId, ct);
        return entity is null ? null : ToUpload(entity);
    }

    public async Task<VideoProcessingJob?> GetJobAsync(string jobId, CancellationToken ct = default)
    {
        var entity = await _db.VideoProcessingJobs.AsNoTracking().FirstOrDefaultAsync(x => x.JobId == jobId, ct);
        return entity is null ? null : ToJob(entity);
    }

    public async Task<VideoProcessingJob?> GetJobForAssetAsync(string assetId, CancellationToken ct = default)
    {
        var entity = await _db.VideoProcessingJobs.AsNoTracking()
            .Where(x => x.AssetId == assetId)
            .OrderByDescending(x => x.QueuedAt)
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : ToJob(entity);
    }

    public async Task UpdateAssetAsync(VideoFlyerAsset asset, CancellationToken ct = default)
    {
        var existing = await _db.VideoFlyerAssets.FirstOrDefaultAsync(x => x.AssetId == asset.AssetId, ct)
            ?? throw new KeyNotFoundException($"Video asset '{asset.AssetId}' not found");

        UpdateEntity(existing, asset);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateUploadAsync(VideoFlyerUpload upload, CancellationToken ct = default)
    {
        var existing = await _db.VideoFlyerUploads.FirstOrDefaultAsync(x => x.UploadId == upload.UploadId, ct)
            ?? throw new KeyNotFoundException($"Video upload '{upload.UploadId}' not found");

        UpdateEntity(existing, upload);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateJobAsync(VideoProcessingJob job, CancellationToken ct = default)
    {
        var existing = await _db.VideoProcessingJobs.FirstOrDefaultAsync(x => x.JobId == job.JobId, ct)
            ?? throw new KeyNotFoundException($"Video job '{job.JobId}' not found");

        UpdateEntity(existing, job);
        await _db.SaveChangesAsync(ct);
    }

    private static VideoFlyerAssetEntity ToEntity(VideoFlyerAsset asset) => new()
    {
        Id = Guid.NewGuid(),
        AssetId = asset.AssetId,
        Status = asset.Status.ToString(),
        ContentType = asset.ContentType,
        FileSizeBytes = asset.FileSizeBytes,
        ChecksumSha256 = asset.ChecksumSha256,
        OriginalFilename = asset.OriginalFilename,
        UploadedAt = asset.UploadedAt,
        UploaderUserId = asset.UploaderUserId,
        SubmissionId = asset.Provenance.SubmissionId,
        VenueId = asset.Provenance.VenueId,
        ModerationItemId = asset.Provenance.ModerationItemId,
        IngestionJobId = asset.Provenance.IngestionJobId,
        StorageProvider = asset.Storage.Provider,
        StorageContainer = asset.Storage.Container,
        StorageObjectKey = asset.Storage.ObjectKey,
        StorageUri = asset.Storage.Uri,
        DurationSeconds = asset.DurationSeconds,
        WidthPx = asset.WidthPx,
        HeightPx = asset.HeightPx,
        DetectedCodec = asset.DetectedCodec,
        BitrateKbps = asset.BitrateKbps,
        ProcessingJobId = asset.ProcessingJobId,
        PosterAssetId = asset.PosterAssetId,
        CreatedAt = asset.CreatedAt,
        UpdatedAt = asset.UpdatedAt,
    };

    private static VideoFlyerUploadEntity ToEntity(VideoFlyerUpload upload) => new()
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

    private static VideoProcessingJobEntity ToEntity(VideoProcessingJob job) => new()
    {
        Id = Guid.NewGuid(),
        JobId = job.JobId,
        AssetId = job.AssetId,
        Status = job.Status.ToString(),
        QueuedAt = job.QueuedAt,
        StartedAt = job.StartedAt,
        CompletedAt = job.CompletedAt,
        FailureReason = job.FailureReason,
        CurrentStage = job.CurrentStage?.ToString(),
        StageHistoryJson = JsonSerializer.Serialize(job.StageHistory),
        ResultJson = job.Result is null ? null : JsonSerializer.Serialize(job.Result),
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static VideoFlyerAsset ToAsset(VideoFlyerAssetEntity entity)
    {
        Enum.TryParse<VideoFlyerAssetStatus>(entity.Status, true, out var status);

        return new VideoFlyerAsset
        {
            AssetId = entity.AssetId,
            Status = status,
            ContentType = entity.ContentType,
            FileSizeBytes = entity.FileSizeBytes,
            ChecksumSha256 = entity.ChecksumSha256,
            OriginalFilename = entity.OriginalFilename,
            UploadedAt = entity.UploadedAt,
            UploaderUserId = entity.UploaderUserId,
            Provenance = new VideoProvenanceLinks(entity.SubmissionId, entity.VenueId, entity.ModerationItemId, entity.IngestionJobId),
            Storage = new VideoStorageRef(entity.StorageProvider, entity.StorageContainer, entity.StorageObjectKey, entity.StorageUri),
            DurationSeconds = entity.DurationSeconds,
            WidthPx = entity.WidthPx,
            HeightPx = entity.HeightPx,
            DetectedCodec = entity.DetectedCodec,
            BitrateKbps = entity.BitrateKbps,
            ProcessingJobId = entity.ProcessingJobId,
            PosterAssetId = entity.PosterAssetId,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    private static VideoFlyerUpload ToUpload(VideoFlyerUploadEntity entity)
    {
        Enum.TryParse<VideoFlyerAssetStatus>(entity.Status, true, out var status);

        return new VideoFlyerUpload
        {
            UploadId = entity.UploadId,
            AssetId = entity.AssetId,
            Status = status,
            InitializedAt = entity.InitializedAt,
            CompletedAt = entity.CompletedAt,
            RequestedByUserId = entity.RequestedByUserId,
            FailureReason = entity.FailureReason,
        };
    }

    private static VideoProcessingJob ToJob(VideoProcessingJobEntity entity)
    {
        Enum.TryParse<VideoProcessingJobStatus>(entity.Status, true, out var status);
        VideoProcessingStage? currentStage = null;
        if (!string.IsNullOrWhiteSpace(entity.CurrentStage) && Enum.TryParse<VideoProcessingStage>(entity.CurrentStage, true, out var parsedStage))
        {
            currentStage = parsedStage;
        }

        var stageHistory = DeserializeStageHistory(entity.StageHistoryJson);
        var result = DeserializeResult(entity.ResultJson);

        return new VideoProcessingJob
        {
            JobId = entity.JobId,
            AssetId = entity.AssetId,
            Status = status,
            QueuedAt = entity.QueuedAt,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            FailureReason = entity.FailureReason,
            CurrentStage = currentStage,
            StageHistory = stageHistory,
            Result = result,
        };
    }

    private static void UpdateEntity(VideoFlyerAssetEntity entity, VideoFlyerAsset asset)
    {
        entity.Status = asset.Status.ToString();
        entity.ContentType = asset.ContentType;
        entity.FileSizeBytes = asset.FileSizeBytes;
        entity.ChecksumSha256 = asset.ChecksumSha256;
        entity.OriginalFilename = asset.OriginalFilename;
        entity.UploadedAt = asset.UploadedAt;
        entity.UploaderUserId = asset.UploaderUserId;
        entity.SubmissionId = asset.Provenance.SubmissionId;
        entity.VenueId = asset.Provenance.VenueId;
        entity.ModerationItemId = asset.Provenance.ModerationItemId;
        entity.IngestionJobId = asset.Provenance.IngestionJobId;
        entity.StorageProvider = asset.Storage.Provider;
        entity.StorageContainer = asset.Storage.Container;
        entity.StorageObjectKey = asset.Storage.ObjectKey;
        entity.StorageUri = asset.Storage.Uri;
        entity.DurationSeconds = asset.DurationSeconds;
        entity.WidthPx = asset.WidthPx;
        entity.HeightPx = asset.HeightPx;
        entity.DetectedCodec = asset.DetectedCodec;
        entity.BitrateKbps = asset.BitrateKbps;
        entity.ProcessingJobId = asset.ProcessingJobId;
        entity.PosterAssetId = asset.PosterAssetId;
        entity.UpdatedAt = asset.UpdatedAt;
    }

    private static void UpdateEntity(VideoFlyerUploadEntity entity, VideoFlyerUpload upload)
    {
        entity.Status = upload.Status.ToString();
        entity.CompletedAt = upload.CompletedAt;
        entity.RequestedByUserId = upload.RequestedByUserId;
        entity.FailureReason = upload.FailureReason;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void UpdateEntity(VideoProcessingJobEntity entity, VideoProcessingJob job)
    {
        entity.Status = job.Status.ToString();
        entity.StartedAt = job.StartedAt;
        entity.CompletedAt = job.CompletedAt;
        entity.FailureReason = job.FailureReason;
        entity.CurrentStage = job.CurrentStage?.ToString();
        entity.StageHistoryJson = JsonSerializer.Serialize(job.StageHistory);
        entity.ResultJson = job.Result is null ? null : JsonSerializer.Serialize(job.Result);
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static IReadOnlyList<VideoProcessingStageRecord> DeserializeStageHistory(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<VideoProcessingStageRecord>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static VideoProcessingResult? DeserializeResult(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<VideoProcessingResult>(json);
        }
        catch
        {
            return null;
        }
    }
}
