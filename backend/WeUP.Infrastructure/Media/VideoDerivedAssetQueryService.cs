using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

public sealed class VideoDerivedAssetQueryService : IVideoDerivedAssetQueryService
{
    private readonly IVideoDerivedAssetRepository _derivedRepository;
    private readonly IVideoFlyerRepository _videoRepository;

    public VideoDerivedAssetQueryService(
        IVideoDerivedAssetRepository derivedRepository,
        IVideoFlyerRepository videoRepository)
    {
        _derivedRepository = derivedRepository;
        _videoRepository = videoRepository;
    }

    public Task<VideoDerivedFrameAsset?> GetPosterAsync(string sourceVideoAssetId, CancellationToken ct = default) =>
        _derivedRepository.GetPosterAsync(sourceVideoAssetId, ct);

    public Task<IReadOnlyList<VideoDerivedFrameAsset>> ListFramesAsync(string sourceVideoAssetId, CancellationToken ct = default) =>
        _derivedRepository.ListFramesAsync(sourceVideoAssetId, ct);

    public async Task<VideoProcessingSummary?> GetProcessingSummaryAsync(string sourceVideoAssetId, CancellationToken ct = default)
    {
        var asset = await _videoRepository.GetAssetAsync(sourceVideoAssetId, ct);
        if (asset is null)
        {
            return null;
        }

        var job = string.IsNullOrWhiteSpace(asset.ProcessingJobId)
            ? null
            : await _videoRepository.GetJobAsync(asset.ProcessingJobId, ct);

        var frames = await _derivedRepository.ListFramesAsync(sourceVideoAssetId, ct);

        return new VideoProcessingSummary(
            SourceVideoAssetId: sourceVideoAssetId,
            ProcessingJobId: asset.ProcessingJobId ?? string.Empty,
            PosterAssetId: asset.PosterAssetId,
            DerivedFrameCount: frames.Count,
            DurationSeconds: asset.DurationSeconds,
            WidthPx: asset.WidthPx,
            HeightPx: asset.HeightPx,
            DetectedCodec: asset.DetectedCodec,
            BitrateKbps: asset.BitrateKbps,
            JobStatus: job?.Status.ToString() ?? "NotQueued",
            FailureReason: job?.FailureReason,
            CompletedAt: job?.CompletedAt);
    }
}
