using WeUP.Contracts.Ingestion;
using WeUP.Domain.Media;

namespace WeUP.Domain.Flyer;

public interface IFlyerTextPostProcessor
{
    string Clean(string rawOcrText);
}

public interface IFlyerIngestionPipeline
{
    Task<IngestionResult> SubmitAsync(FlyerUploadIngestionRequest request, CancellationToken ct = default);
    Task<FlyerIngestionJobDetailResponse?> GetJobAsync(string jobId, CancellationToken ct = default);
    Task<FlyerIngestionEvidenceResponse?> GetEvidenceAsync(string jobId, CancellationToken ct = default);
}

public interface IFlyerOcrService
{
    Task<FlyerOcrExtractionResult> ExtractAsync(FlyerAssetReference asset, string jobId, CancellationToken ct = default);
}

public sealed record FlyerNormalizationRequest(
    FlyerAssetReference Asset,
    FlyerOcrExtractionResult Ocr,
    string CleanedText,
    string JobId,
    IReadOnlyDictionary<string, string?> Metadata);

public sealed record FlyerNormalizationResult(
    FlyerNormalizedEventCandidate? Candidate,
    FlyerConfidenceVector Confidence,
    CanonicalIngestionIssue[] Issues,
    FlyerReviewTriggerReason[] ReviewTriggers,
    bool RequiresManualReview,
    string[] MissingFields,
    string[] UnresolvedAmbiguities,
    string[] Notes);

public interface IFlyerNormalizationService
{
    Task<FlyerNormalizationResult> NormalizeAsync(FlyerNormalizationRequest request, CancellationToken ct = default);
}

public interface IFlyerConfidenceEvaluator
{
    FlyerConfidenceVector Evaluate(
        FlyerAssetReference asset,
        FlyerOcrExtractionResult ocr,
        FlyerNormalizedEventCandidate? candidate,
        double sourceAuthority,
        IReadOnlyCollection<FlyerReviewTriggerReason> reviewTriggers);
}
