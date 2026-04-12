using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

/// <summary>
/// Phase 0.15 synchronous processor that runs metadata read -> frame extraction -> poster selection -> registration.
/// </summary>
public sealed class VideoProcessingOrchestrator : IVideoProcessingOrchestrator
{
    private readonly IVideoFlyerRepository _videoRepository;
    private readonly IVideoMetadataReader _metadataReader;
    private readonly IVideoFrameExtractor _frameExtractor;
    private readonly IPosterSelectionService _posterSelection;
    private readonly IVideoDerivedAssetRegistrar _derivedRegistrar;

    public VideoProcessingOrchestrator(
        IVideoFlyerRepository videoRepository,
        IVideoMetadataReader metadataReader,
        IVideoFrameExtractor frameExtractor,
        IPosterSelectionService posterSelection,
        IVideoDerivedAssetRegistrar derivedRegistrar)
    {
        _videoRepository = videoRepository;
        _metadataReader = metadataReader;
        _frameExtractor = frameExtractor;
        _posterSelection = posterSelection;
        _derivedRegistrar = derivedRegistrar;
    }

    public async Task<VideoProcessingJob> ProcessAsync(VideoFlyerAsset asset, VideoProcessingJob job, CancellationToken ct = default)
    {
        var started = DateTimeOffset.UtcNow;
        var history = new List<VideoProcessingStageRecord>();

        try
        {
            var runningJob = job with
            {
                Status = VideoProcessingJobStatus.Running,
                StartedAt = started,
                CurrentStage = VideoProcessingStage.MetadataExtraction,
            };
            await _videoRepository.UpdateJobAsync(runningJob, ct);

            var metadata = await RunMetadataStage(asset, runningJob, history, ct);
            var frames = await RunFrameStage(asset, runningJob, metadata, history, ct);
            var poster = await RunPosterStage(asset, runningJob, metadata, frames, history, ct);
            var derived = await RunRegistrationStage(asset, runningJob, frames, poster, history, ct);

            var completedAt = DateTimeOffset.UtcNow;
            var result = new VideoProcessingResult(
                DurationSeconds: metadata.DurationSeconds,
                WidthPx: metadata.WidthPx,
                HeightPx: metadata.HeightPx,
                DetectedCodec: metadata.DetectedCodec,
                BitrateKbps: metadata.BitrateKbps,
                PosterAssetId: derived.FirstOrDefault(x => x.IsPosterSelected)?.DerivedAssetId,
                FrameAssetIds: derived.Select(x => x.DerivedAssetId).ToArray());

            var completedJob = runningJob with
            {
                Status = VideoProcessingJobStatus.Succeeded,
                CurrentStage = VideoProcessingStage.Finalization,
                CompletedAt = completedAt,
                StageHistory = history,
                Result = result,
            };
            await _videoRepository.UpdateJobAsync(completedJob, ct);

            var completedAsset = asset with
            {
                Status = VideoFlyerAssetStatus.ProcessingComplete,
                DurationSeconds = metadata.DurationSeconds,
                WidthPx = metadata.WidthPx,
                HeightPx = metadata.HeightPx,
                DetectedCodec = metadata.DetectedCodec,
                BitrateKbps = metadata.BitrateKbps,
                PosterAssetId = result.PosterAssetId,
                UpdatedAt = completedAt,
            };
            await _videoRepository.UpdateAssetAsync(completedAsset, ct);

            return completedJob;
        }
        catch (Exception ex)
        {
            var failedAt = DateTimeOffset.UtcNow;
            history.Add(new VideoProcessingStageRecord(
                Stage: job.CurrentStage ?? VideoProcessingStage.Finalization,
                Status: VideoProcessingJobStatus.Failed,
                StartedAt: failedAt,
                CompletedAt: failedAt,
                ErrorDetail: ex.Message));

            var failedJob = job with
            {
                Status = VideoProcessingJobStatus.Failed,
                CurrentStage = job.CurrentStage,
                StartedAt = started,
                CompletedAt = failedAt,
                FailureReason = ex.Message,
                StageHistory = history,
            };
            await _videoRepository.UpdateJobAsync(failedJob, ct);

            var failedAsset = asset with
            {
                Status = VideoFlyerAssetStatus.Rejected,
                UpdatedAt = failedAt,
            };
            await _videoRepository.UpdateAssetAsync(failedAsset, ct);

            return failedJob;
        }
    }

    private async Task<VideoMetadataSnapshot> RunMetadataStage(
        VideoFlyerAsset asset,
        VideoProcessingJob job,
        List<VideoProcessingStageRecord> history,
        CancellationToken ct)
    {
        var started = DateTimeOffset.UtcNow;
        var metadata = await _metadataReader.ReadAsync(asset, ct);
        history.Add(new VideoProcessingStageRecord(
            Stage: VideoProcessingStage.MetadataExtraction,
            Status: VideoProcessingJobStatus.Succeeded,
            StartedAt: started,
            CompletedAt: DateTimeOffset.UtcNow,
            ErrorDetail: null));
        return metadata;
    }

    private async Task<IReadOnlyList<ExtractedVideoFrame>> RunFrameStage(
        VideoFlyerAsset asset,
        VideoProcessingJob job,
        VideoMetadataSnapshot metadata,
        List<VideoProcessingStageRecord> history,
        CancellationToken ct)
    {
        var started = DateTimeOffset.UtcNow;
        var frames = await _frameExtractor.ExtractAsync(asset, metadata, ct);
        if (frames.Count == 0)
        {
            throw new InvalidOperationException("Frame extraction produced no frames.");
        }

        history.Add(new VideoProcessingStageRecord(
            Stage: VideoProcessingStage.FrameExtraction,
            Status: VideoProcessingJobStatus.Succeeded,
            StartedAt: started,
            CompletedAt: DateTimeOffset.UtcNow,
            ErrorDetail: null));

        return frames;
    }

    private async Task<PosterSelectionResult> RunPosterStage(
        VideoFlyerAsset asset,
        VideoProcessingJob job,
        VideoMetadataSnapshot metadata,
        IReadOnlyList<ExtractedVideoFrame> frames,
        List<VideoProcessingStageRecord> history,
        CancellationToken ct)
    {
        var started = DateTimeOffset.UtcNow;
        var poster = await _posterSelection.SelectPosterAsync(asset, metadata, frames, ct);
        history.Add(new VideoProcessingStageRecord(
            Stage: VideoProcessingStage.PosterGeneration,
            Status: VideoProcessingJobStatus.Succeeded,
            StartedAt: started,
            CompletedAt: DateTimeOffset.UtcNow,
            ErrorDetail: null));

        return poster;
    }

    private async Task<IReadOnlyList<VideoDerivedFrameAsset>> RunRegistrationStage(
        VideoFlyerAsset asset,
        VideoProcessingJob job,
        IReadOnlyList<ExtractedVideoFrame> frames,
        PosterSelectionResult poster,
        List<VideoProcessingStageRecord> history,
        CancellationToken ct)
    {
        var started = DateTimeOffset.UtcNow;
        var derived = await _derivedRegistrar.RegisterAsync(asset, job, frames, poster, ct);
        history.Add(new VideoProcessingStageRecord(
            Stage: VideoProcessingStage.Finalization,
            Status: VideoProcessingJobStatus.Succeeded,
            StartedAt: started,
            CompletedAt: DateTimeOffset.UtcNow,
            ErrorDetail: null));

        return derived;
    }
}
