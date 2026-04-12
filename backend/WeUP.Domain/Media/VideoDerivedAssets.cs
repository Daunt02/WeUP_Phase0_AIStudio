namespace WeUP.Domain.Media;

public enum VideoFrameType
{
    IntervalPreview,
    SceneChangeCandidate,
    PosterSelected,
}

public enum VideoFrameExtractionStrategy
{
    FixedIntervalDeterministic,
}

public sealed record VideoMetadataSnapshot(
    int DurationSeconds,
    int WidthPx,
    int HeightPx,
    string? DetectedCodec,
    int? BitrateKbps);

public sealed record ExtractedVideoFrame(
    long TimestampOffsetMs,
    VideoFrameType FrameType,
    int WidthPx,
    int HeightPx,
    string ExtractionStage,
    string ExtractionVersion);

public sealed record PosterSelectionResult(
    long SelectedTimestampOffsetMs,
    string Reason,
    string SelectionVersion);

public sealed record VideoDerivedFrameAsset
{
    public required string SourceVideoAssetId { get; init; }
    public required string DerivedAssetId { get; init; }
    public required string ProcessingJobId { get; init; }

    public string? UploaderUserId { get; init; }
    public string? SubmissionId { get; init; }
    public string? VenueId { get; init; }
    public string? ModerationItemId { get; init; }

    public required long TimestampOffsetMs { get; init; }
    public required VideoFrameType FrameType { get; init; }
    public required int WidthPx { get; init; }
    public required int HeightPx { get; init; }

    public required MediaStorageRef Storage { get; init; }
    public required string ExtractionStage { get; init; }
    public required string ExtractionVersion { get; init; }

    public required bool IsPosterSelected { get; init; }
    public string? PosterSelectionReason { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed record VideoProcessingSummary(
    string SourceVideoAssetId,
    string ProcessingJobId,
    string? PosterAssetId,
    int DerivedFrameCount,
    int? DurationSeconds,
    int? WidthPx,
    int? HeightPx,
    string? DetectedCodec,
    int? BitrateKbps,
    string JobStatus,
    string? FailureReason,
    DateTimeOffset? CompletedAt);

public interface IVideoMetadataReader
{
    Task<VideoMetadataSnapshot> ReadAsync(VideoFlyerAsset asset, CancellationToken ct = default);
}

public interface IVideoFrameExtractor
{
    VideoFrameExtractionStrategy Strategy { get; }

    Task<IReadOnlyList<ExtractedVideoFrame>> ExtractAsync(
        VideoFlyerAsset asset,
        VideoMetadataSnapshot metadata,
        CancellationToken ct = default);
}

public interface IPosterSelectionService
{
    Task<PosterSelectionResult> SelectPosterAsync(
        VideoFlyerAsset asset,
        VideoMetadataSnapshot metadata,
        IReadOnlyList<ExtractedVideoFrame> frames,
        CancellationToken ct = default);
}

public interface IVideoDerivedAssetRegistrar
{
    Task<IReadOnlyList<VideoDerivedFrameAsset>> RegisterAsync(
        VideoFlyerAsset sourceAsset,
        VideoProcessingJob job,
        IReadOnlyList<ExtractedVideoFrame> frames,
        PosterSelectionResult poster,
        CancellationToken ct = default);
}

public interface IVideoDerivedAssetRepository
{
    Task SaveBatchAsync(IReadOnlyList<VideoDerivedFrameAsset> records, CancellationToken ct = default);
    Task<VideoDerivedFrameAsset?> GetPosterAsync(string sourceVideoAssetId, CancellationToken ct = default);
    Task<IReadOnlyList<VideoDerivedFrameAsset>> ListFramesAsync(string sourceVideoAssetId, CancellationToken ct = default);
}

public interface IVideoDerivedAssetQueryService
{
    Task<VideoDerivedFrameAsset?> GetPosterAsync(string sourceVideoAssetId, CancellationToken ct = default);
    Task<IReadOnlyList<VideoDerivedFrameAsset>> ListFramesAsync(string sourceVideoAssetId, CancellationToken ct = default);
    Task<VideoProcessingSummary?> GetProcessingSummaryAsync(string sourceVideoAssetId, CancellationToken ct = default);
}

public interface IVideoProcessingOrchestrator
{
    Task<VideoProcessingJob> ProcessAsync(VideoFlyerAsset asset, VideoProcessingJob job, CancellationToken ct = default);
}
