namespace WeUP.Domain.Media;

/// <summary>
/// P28: Overall status of a video processing job.
/// </summary>
public enum VideoProcessingJobStatus
{
    /// <summary>Job created and waiting for a worker to pick it up.</summary>
    Queued,

    /// <summary>A worker has claimed the job and is executing stages.</summary>
    Running,

    /// <summary>All required stages completed successfully.</summary>
    Succeeded,

    /// <summary>One or more stages failed; the asset remains in ProcessingPending until retry or abandonment.</summary>
    Failed,

    /// <summary>Job was cancelled before completion (e.g., asset archived).</summary>
    Cancelled,
}

/// <summary>
/// P28: Individual stage within a video processing pipeline run.
/// Workers advance through these in order; stages after failure are not attempted.
/// </summary>
public enum VideoProcessingStage
{
    /// <summary>Extract container/codec/duration/dimension metadata from the raw file.</summary>
    MetadataExtraction,

    /// <summary>Capture and encode a representative poster frame as a MediaAsset.</summary>
    PosterGeneration,

    /// <summary>Extract a sparse set of preview frames at regular intervals.</summary>
    FrameExtraction,

    /// <summary>Re-encode to a web-safe container/codec if the source is non-compliant.</summary>
    Transcoding,

    /// <summary>Finalise asset record, update status to ProcessingComplete, emit domain event.</summary>
    Finalization,
}

/// <summary>
/// P28: Execution record for a single stage within a processing job.
/// </summary>
public sealed record VideoProcessingStageRecord(
    VideoProcessingStage Stage,
    VideoProcessingJobStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorDetail);

/// <summary>
/// P28: Artifacts and extracted metadata produced by a completed processing job.
/// </summary>
public sealed record VideoProcessingResult(
    int? DurationSeconds,
    int? WidthPx,
    int? HeightPx,
    string? DetectedCodec,
    int? BitrateKbps,
    string? PosterAssetId,
    string[]? FrameAssetIds);

/// <summary>
/// P28: Durable video processing job record.
/// Created automatically when a video upload completion is signalled successfully.
/// Workers consume queued jobs; the service layer updates state transitions.
/// </summary>
public sealed record VideoProcessingJob
{
    public required string JobId { get; init; }
    public required string AssetId { get; init; }
    public required VideoProcessingJobStatus Status { get; init; }
    public required DateTimeOffset QueuedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? FailureReason { get; init; }
    public VideoProcessingStage? CurrentStage { get; init; }

    /// <summary>Ordered log of stage executions for this job run.</summary>
    public IReadOnlyList<VideoProcessingStageRecord> StageHistory { get; init; } = [];

    /// <summary>Null until at least one processing pass completes.</summary>
    public VideoProcessingResult? Result { get; init; }
}
