using WeUP.Contracts.Ingestion;

namespace WeUP.Domain.Flyer;

// ---------------------------------------------------------------------------
// Flyer pipeline stage interfaces
// ---------------------------------------------------------------------------

/// <summary>
/// Validates and stores an uploaded flyer image.
/// Returns a storage reference and asset identifier.
/// </summary>
public interface IFlyerStorageService
{
    Task<FlyerStorageResult> StoreAsync(FlyerUploadRequest request, CancellationToken ct = default);
}

public record FlyerUploadRequest(
    Stream Content,
    string FileName,
    string ContentType,
    string UploadedByUserId);

public record FlyerStorageResult(
    string AssetId,
    string StorageKey,
    string ContentType,
    long FileSizeBytes,
    string ChecksumSha256);

/// <summary>
/// Runs OCR on a stored flyer image and returns raw extracted text.
/// </summary>
public interface IOcrService
{
    Task<OcrResult> ExtractTextAsync(string assetId, string storageKey, CancellationToken ct = default);
}

public record OcrResult(
    string AssetId,
    string RawText,
    double Confidence,
    string? EngineVersion,
    bool Success,
    string? ErrorMessage);

/// <summary>
/// Post-processes raw OCR text: removes noise, normalizes whitespace and encoding issues.
/// </summary>
public interface IFlyerTextPostProcessor
{
    string Clean(string rawOcrText);
}

/// <summary>
/// Uses an LLM to extract structured event fields from cleaned flyer text.
/// </summary>
public interface ILlmEventNormalizer
{
    Task<LlmNormalizationResult> NormalizeAsync(string cleanedText, string sourceRef, CancellationToken ct = default);
}

public record LlmNormalizationResult(
    string? Title,
    string? VenueName,
    string? Address,
    string? StartDate,
    string? EndDate,
    string? Timezone,
    string? Category,
    string? Description,
    string[]? Tags,
    double ExtractionConfidence,
    double TemporalConfidence,
    string PromptVersion,
    bool Success,
    string? ErrorMessage);

/// <summary>
/// Evaluates flyer confidence vector and determines publish eligibility.
/// </summary>
public interface IFlyerConfidenceEvaluator
{
    FlyerConfidenceResult Evaluate(OcrResult ocr, LlmNormalizationResult normalization, double geocodeConfidence);
}

public record FlyerConfidenceResult(
    double Aggregate,
    double ExtractionConfidence,
    double GeocodeConfidence,
    double TemporalConfidence,
    bool RequiresManualReview,
    string[] ReviewBlockers);

/// <summary>
/// Geocodes an address string to lat/lng.
/// </summary>
public interface IGeocodingService
{
    Task<GeocodeResult> GeocodeAsync(string address, string market, CancellationToken ct = default);
}

public record GeocodeResult(
    double? Lat,
    double? Lng,
    double Confidence,
    string? NormalizedAddress,
    bool Success);

// ---------------------------------------------------------------------------
// Pipeline orchestrator
// ---------------------------------------------------------------------------

public interface IFlyerIngestionPipeline
{
    Task<FlyerIngestionResult> ProcessAsync(FlyerUploadRequest request, CancellationToken ct = default);
}

public record FlyerIngestionResult(
    string AssetId,
    string JobId,
    NormalizedEventCandidate? Candidate,
    FlyerConfidenceResult? Confidence,
    bool RequiresReview,
    string[] ReviewBlockers,
    bool Success,
    string? ErrorMessage);
