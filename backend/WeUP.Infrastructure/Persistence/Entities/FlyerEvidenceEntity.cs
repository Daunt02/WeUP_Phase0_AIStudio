namespace WeUP.Infrastructure.Persistence.Entities;

public sealed class FlyerEvidenceEntity
{
    public Guid Id { get; set; }
    public string EvidenceId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string OriginalAssetId { get; set; } = string.Empty;
    public string ProvenanceId { get; set; } = string.Empty;
    public string? EventId { get; set; }
    public string? SubmissionId { get; set; }
    public string FlyerType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? OcrText { get; set; }
    public string? OcrExtractionId { get; set; }
    public string? OcrEngineVersion { get; set; }
    public string? OcrBlocksJson { get; set; }
    public bool OcrReady { get; set; }
    public double? ConfidenceScore { get; set; }
    public string? DerivativeAssetIdsJson { get; set; }
    public string? ProcessingHistoryJson { get; set; }
    public string? ValidationFailuresJson { get; set; }
    public string? IngestionJobId { get; set; }
    public string? ModerationItemId { get; set; }
    public string? CanonicalEventId { get; set; }
    public string? LinkedWorkflowIdsJson { get; set; }
    public string? NormalizationRunId { get; set; }
    public string? NormalizationVersion { get; set; }
    public string? NormalizationSnapshotJson { get; set; }
    public string? ReviewReasonsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
