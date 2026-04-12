namespace WeUP.Domain.Media;

public sealed record FlyerVisualMetadata(
    string AssetId,
    string OriginalFilename,
    string ContentType,
    long FileSizeBytes,
    string ContentHash,
    int? WidthPx,
    int? HeightPx,
    DateTimeOffset UploadedAt,
    string StorageKey);

public sealed record FlyerProvenanceSummary(
    string ProvenanceId,
    string? UploaderUserId,
    SourceTier SourceTier,
    FlyerUploadOrigin UploadOrigin,
    FlyerSourceType SourceType,
    string SubmitterHash,
    double BaselineAuthority,
    string? SubmissionId,
    string? IngestionJobId,
    string? SourceUrl,
    string? PartnerProvider,
    DateTimeOffset RecordedAt);

public sealed record FlyerEvidenceSummary(
    string EvidenceId,
    string OriginalAssetId,
    string[] DerivativeAssetIds,
    bool OcrReady,
    string[] ProcessingHistory,
    string[] ValidationFailures,
    string? OcrText,
    double? ConfidenceScore,
    EvidenceStatus Status,
    string? SubmissionId,
    string? IngestionJobId,
    string? ModerationItemId,
    string? CanonicalEventId,
    string[] LinkedWorkflowIds,
    DateTimeOffset CreatedAt);

public sealed record FlyerAssetReviewProjection(
    FlyerVisualMetadata Visual,
    FlyerAssetStatus LifecycleState,
    FlyerProvenanceSummary Provenance,
    FlyerEvidenceSummary Evidence);

public interface IFlyerEvidenceQueryService
{
    Task<FlyerAssetReviewProjection?> GetAssetDetailAsync(string assetId, CancellationToken ct = default);
    Task<FlyerAssetReviewProjection[]> ListUploaderAssetsAsync(string uploaderUserId, CancellationToken ct = default);
    Task<FlyerAssetReviewProjection[]> ListReviewSummariesAsync(int limit = 100, CancellationToken ct = default);
    Task<FlyerAssetReviewProjection[]> ListBySubmissionIdAsync(string submissionId, CancellationToken ct = default);
}
