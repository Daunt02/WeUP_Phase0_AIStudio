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

    /// <summary>
    /// Confidence score 0.0–1.0 for the extracted event data.
    /// Incorporates OCR legibility, field completeness, and source authority.
    /// Null until scoring pipeline runs.
    /// </summary>
    public double? ConfidenceScore { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Free-text notes from the reviewer.</summary>
    public string? ReviewNotes { get; init; }

    // ── Helpers ─────────────────────────────────────────────────────────────

    public FlyerEvidenceRecord WithEventLink(string eventId) =>
        this with { EventId = eventId, Status = EvidenceStatus.Linked };

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
    Task<FlyerEvidenceRecord[]> ListByEventIdAsync(string eventId, CancellationToken ct = default);
    Task<FlyerEvidenceRecord[]> ListBySubmissionIdAsync(string submissionId, CancellationToken ct = default);
    Task<FlyerEvidenceRecord[]> ListPendingAsync(CancellationToken ct = default);
    Task<FlyerEvidenceRecord> UpdateAsync(FlyerEvidenceRecord record, CancellationToken ct = default);
}
