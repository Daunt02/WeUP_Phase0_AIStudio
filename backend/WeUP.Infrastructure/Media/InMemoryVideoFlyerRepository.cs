using System.Collections.Concurrent;
using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

/// <summary>
/// P28: In-memory repository backing for video flyer assets, upload sessions, and processing jobs.
/// Used in development/testing when EF Core / PostgreSQL is not configured.
/// Registered as Singleton so all requests within a process share state.
/// </summary>
public sealed class InMemoryVideoFlyerRepository : IVideoFlyerRepository
{
    private readonly ConcurrentDictionary<string, VideoFlyerAsset> _assets = new();
    private readonly ConcurrentDictionary<string, VideoFlyerUpload> _uploads = new();
    private readonly ConcurrentDictionary<string, VideoProcessingJob> _jobs = new();

    // Asset-to-latest-job index for GetJobForAssetAsync
    private readonly ConcurrentDictionary<string, string> _assetJobIndex = new();

    public Task SaveAssetAsync(VideoFlyerAsset asset, CancellationToken ct = default)
    {
        _assets[asset.AssetId] = asset;
        return Task.CompletedTask;
    }

    public Task SaveUploadAsync(VideoFlyerUpload upload, CancellationToken ct = default)
    {
        _uploads[upload.UploadId] = upload;
        return Task.CompletedTask;
    }

    public Task SaveJobAsync(VideoProcessingJob job, CancellationToken ct = default)
    {
        _jobs[job.JobId] = job;
        _assetJobIndex[job.AssetId] = job.JobId;
        return Task.CompletedTask;
    }

    public Task<VideoFlyerAsset?> GetAssetAsync(string assetId, CancellationToken ct = default) =>
        Task.FromResult(_assets.TryGetValue(assetId, out var a) ? a : null);

    public Task<VideoFlyerUpload?> GetUploadAsync(string uploadId, CancellationToken ct = default) =>
        Task.FromResult(_uploads.TryGetValue(uploadId, out var u) ? u : null);

    public Task<VideoProcessingJob?> GetJobAsync(string jobId, CancellationToken ct = default) =>
        Task.FromResult(_jobs.TryGetValue(jobId, out var j) ? j : null);

    public Task<VideoProcessingJob?> GetJobForAssetAsync(string assetId, CancellationToken ct = default)
    {
        if (_assetJobIndex.TryGetValue(assetId, out var jobId) &&
            _jobs.TryGetValue(jobId, out var job))
            return Task.FromResult<VideoProcessingJob?>(job);

        return Task.FromResult<VideoProcessingJob?>(null);
    }

    public Task UpdateAssetAsync(VideoFlyerAsset asset, CancellationToken ct = default)
    {
        _assets[asset.AssetId] = asset;
        return Task.CompletedTask;
    }

    public Task UpdateUploadAsync(VideoFlyerUpload upload, CancellationToken ct = default)
    {
        _uploads[upload.UploadId] = upload;
        return Task.CompletedTask;
    }

    public Task UpdateJobAsync(VideoProcessingJob job, CancellationToken ct = default)
    {
        _jobs[job.JobId] = job;
        return Task.CompletedTask;
    }
}
