namespace WeUP.Domain.Media;

/// <summary>
/// P27: Classification of the flyer's physical form.
/// Drives screenshot disassembly logic and authority scoring adjustments.
/// </summary>
public enum FlyerType
{
    Native,        // Original promotional art directly from the promoter or venue
    Screenshot,    // Screenshot of an Instagram/social post wrapping the flyer
    Collage,       // Multiple events combined into one image
    StoryCapture,  // Vertical story format (IG/TikTok story screenshot)
    Unknown,       // Not yet classified
}

/// <summary>
/// P27: Lifecycle status of a flyer evidence record.
/// </summary>
public enum EvidenceStatus
{
    Pending,   // Received, not yet linked to an event or submission
    Linked,    // Successfully linked to an EventId or SubmissionId
    Rejected,  // Rejected during review (duplicate, unreadable, policy violation)
}

/// <summary>
/// P27: Review-ready evidence record linking a flyer asset to an event or submission.
/// Stores extracted OCR text, confidence score, and flyer classification for the moderation queue.
/// </summary>
public sealed record FlyerEvidenceRecord
{
    public required string EvidenceId { get; init; }

    /// <summary>FK to FlyerAssetRecord.AssetId</summary>
    public required string AssetId { get; init; }

    /// <summary>Original uploaded asset id that all derivative artifacts trace back to.</summary>
    public required string OriginalAssetId { get; init; }

    /// <summary>FK to ProvenanceRecord.ProvenanceId</summary>
    public required string ProvenanceId { get; init; }

    /// <summary>FK to a published event ID (set after moderation approval).</summary>
    public string? EventId { get; init; }

    /// <summary>FK to an EventSubmission ID (set at intake if submitted as part of event creation).</summary>
    public string? SubmissionId { get; init; }

    public required FlyerType FlyerType { get; init; }

    public required EvidenceStatus Status { get; init; }

    /// <summary>Raw OCR text extracted from the flyer image (null until OCR is run).</summary>
    public string? OcrText { get; init; }

    /// <summary>Stable id for the OCR extraction attempt bound to this evidence record.</summary>
    public string? OcrExtractionId { get; init; }

    /// <summary>OCR engine/version used to produce the extracted text snapshot.</summary>
    public string? OcrEngineVersion { get; init; }

    /// <summary>Serialized OCR block/region metadata when available.</summary>
    public string? OcrBlocksJson { get; init; }

    /// <summary>
    /// Indicates if this asset should be consumed by OCR pipelines.
    /// </summary>
    public bool OcrReady { get; init; } = true;

    /// <summary>
    /// Confidence score 0.0–1.0 for the extracted event data.
    /// Incorporates OCR legibility, field completeness, and source authority.
    /// Null until scoring pipeline runs.
    /// </summary>
    public double? ConfidenceScore { get; init; }

    /// <summary>
    /// List of derivative asset ids (for thumbnails/crops/OCR staging) generated from OriginalAssetId.
    /// </summary>
    public string[] DerivativeAssetIds { get; init; } = [];

    /// <summary>
    /// Audit-friendly processing history entries across intake, OCR, moderation, and enrichment.
    /// </summary>
    public string[] ProcessingHistory { get; init; } = [];

    /// <summary>
    /// Captured validation failures that should remain visible to reviewers.
    /// </summary>
    public string[] ValidationFailures { get; init; } = [];

    /// <summary>Linked ingestion job id when present.</summary>
    public string? IngestionJobId { get; init; }

    /// <summary>Linked moderation queue item id when present.</summary>
    public string? ModerationItemId { get; init; }

    /// <summary>Canonical event id after reviewer linkage/approval.</summary>
    public string? CanonicalEventId { get; init; }

    /// <summary>Workflow ids connected to this evidence record (submission/ingestion/moderation/OCR).</summary>
    public string[] LinkedWorkflowIds { get; init; } = [];

    /// <summary>Stable id for the normalization run applied after OCR.</summary>
    public string? NormalizationRunId { get; init; }

    /// <summary>Normalization model or prompt version identifier.</summary>
    public string? NormalizationVersion { get; init; }

    /// <summary>Serialized normalized flyer candidate snapshot for reviewer inspection.</summary>
    public string? NormalizationSnapshotJson { get; init; }

    /// <summary>Deterministic review reasons attached by the flyer ingestion pipeline.</summary>
    public string[] ReviewReasons { get; init; } = [];

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Free-text notes from the reviewer.</summary>
    public string? ReviewNotes { get; init; }

    // ── Helpers ─────────────────────────────────────────────────────────────

    public FlyerEvidenceRecord WithEventLink(string eventId) =>
        this with
        {
            EventId = eventId,
            CanonicalEventId = eventId,
            Status = EvidenceStatus.Linked,
        };

    public FlyerEvidenceRecord WithSubmissionLink(string submissionId) =>
        this with { SubmissionId = submissionId, Status = EvidenceStatus.Linked };

    public FlyerEvidenceRecord WithOcr(string ocrText, double confidenceScore) =>
        this with { OcrText = ocrText, ConfidenceScore = confidenceScore };
}

/// <summary>
/// P27: Durable storage contract for flyer evidence records.
/// </summary>
public interface IFlyerEvidenceRepository
{
    Task<FlyerEvidenceRecord> SaveAsync(FlyerEvidenceRecord record, CancellationToken ct = default);
    Task<FlyerEvidenceRecord?> GetAsync(string evidenceId, CancellationToken ct = default);
    Task<FlyerEvidenceRecord?> GetByAssetIdAsync(string assetId, CancellationToken ct = default);
    Task<FlyerEvidenceRecord?> GetByIngestionJobIdAsync(string ingestionJobId, CancellationToken ct = default);
    Task<FlyerEvidenceRecord[]> ListByEventIdAsync(string eventId, CancellationToken ct = default);
    Task<FlyerEvidenceRecord[]> ListBySubmissionIdAsync(string submissionId, CancellationToken ct = default);
    Task<FlyerEvidenceRecord[]> ListPendingAsync(CancellationToken ct = default);
    Task<FlyerEvidenceRecord> UpdateAsync(FlyerEvidenceRecord record, CancellationToken ct = default);
}
