namespace WeUP.Domain.Media;

/// <summary>
/// P28: Lifecycle states for a video flyer asset.
/// Transitions are forward-only; the service layer enforces valid progressions.
/// </summary>
public enum VideoFlyerAssetStatus
{
    /// <summary>Upload record created; no file bytes received yet.</summary>
    Initialized,

    /// <summary>File transfer in progress (future: client uploading to signed URL).</summary>
    Uploading,

    /// <summary>File received and stored. Awaiting explicit completion signal.</summary>
    Uploaded,

    /// <summary>Completion signalled; a VideoProcessingJob has been queued.</summary>
    ProcessingPending,

    /// <summary>Processing job is actively running.</summary>
    Processing,

    /// <summary>Processing complete; poster/frames available. Awaiting human review.</summary>
    ProcessingComplete,

    /// <summary>Submitted for human review before publication eligibility.</summary>
    ReviewPending,

    /// <summary>Rejected at validation or review — not suitable for publication.</summary>
    Rejected,

    /// <summary>Soft-deleted or superseded by a newer upload.</summary>
    Archived,
}

/// <summary>
/// P28: Links a video flyer asset to upstream workflow records.
/// IDs here are nullable seams — only populated when the relevant workflow exists.
/// </summary>
public sealed record VideoProvenanceLinks(
    string? SubmissionId,
    string? VenueId,
    string? ModerationItemId,
    string? IngestionJobId);

/// <summary>
/// P28: Storage location for a persisted video file.
/// </summary>
public sealed record VideoStorageRef(
    string Provider,
    string Container,
    string ObjectKey,
    string? Uri = null);

/// <summary>
/// P28: Durable video flyer asset record.
/// Video-specific fields (duration, dimensions, codec) are nullable until
/// the processing pipeline populates them.
/// </summary>
public sealed record VideoFlyerAsset
{
    public required string AssetId { get; init; }
    public required VideoFlyerAssetStatus Status { get; init; }
    public required string ContentType { get; init; }
    public required long FileSizeBytes { get; init; }
    public required string ChecksumSha256 { get; init; }
    public required string OriginalFilename { get; init; }
    public required DateTimeOffset UploadedAt { get; init; }

    // Provenance / ownership
    public required string? UploaderUserId { get; init; }
    public required VideoProvenanceLinks Provenance { get; init; }
    public required VideoStorageRef Storage { get; init; }

    // Video-specific metadata —  populated by the processing pipeline
    public int? DurationSeconds { get; init; }
    public int? WidthPx { get; init; }
    public int? HeightPx { get; init; }
    public string? DetectedCodec { get; init; }
    public int? BitrateKbps { get; init; }

    // Derived media refs — populated after processing
    public string? ProcessingJobId { get; init; }
    public string? PosterAssetId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// P28: Upload session record. Tracks file transfer state from initiation through completion.
/// </summary>
public sealed record VideoFlyerUpload
{
    public required string UploadId { get; init; }
    public required string AssetId { get; init; }
    public required VideoFlyerAssetStatus Status { get; init; }
    public required DateTimeOffset InitializedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? RequestedByUserId { get; init; }
    public string? FailureReason { get; init; }

    /// <summary>
    /// Stub seam for signed URL flow (Phase 0.5+).
    /// Null in Phase 0.15 direct-upload mode — the file arrives in the initial POST.
    /// </summary>
    public string? UploadUrl { get; init; }
}

/// <summary>
/// P28: Command to register a new video upload session and accept the file stream.
/// </summary>
public sealed record InitiateVideoUploadCommand(
    string OriginalFilename,
    string ContentType,
    long FileSizeBytes,
    string? UploaderUserId,
    VideoProvenanceLinks Provenance);

/// <summary>
/// P28: Command to signal that an upload is complete and trigger job creation.
/// Client-reported hints (codec, duration, dimensions) are advisory only;
/// the processing pipeline will verify them independently.
/// </summary>
public sealed record CompleteVideoUploadCommand(
    bool Success,
    string? FailureReason = null,
    string? DetectedCodec = null,
    int? ClientDurationSeconds = null,
    int? ClientWidthPx = null,
    int? ClientHeightPx = null);

/// <summary>
/// P28: Result returned after successfully completing an upload.
/// Job is null when the completion was signalled as a failure.
/// </summary>
public sealed record VideoUploadCompletionResult(
    VideoFlyerUpload Upload,
    VideoFlyerAsset Asset,
    VideoProcessingJob? Job);

/// <summary>
/// P28: Storage write request for a video file.
/// </summary>
public sealed record VideoStorageWriteRequest(
    string AssetId,
    string OriginalFilename,
    string ContentType,
    Stream Content);

/// <summary>
/// P28: Storage abstraction for video assets.
/// Implementations: LocalVideoStorageService (Phase 0), CloudVideoStorageService (Phase 0.5+).
/// </summary>
public interface IVideoStorageService
{
    Task<VideoStorageRef> SaveAsync(VideoStorageWriteRequest request, CancellationToken ct = default);
}
