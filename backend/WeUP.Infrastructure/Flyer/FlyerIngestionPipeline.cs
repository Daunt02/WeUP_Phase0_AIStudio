using WeUP.Contracts.Ingestion;
using WeUP.Domain.Flyer;
using WeUP.Domain.Ingestion;

namespace WeUP.Infrastructure.Flyer;

/// <summary>
/// Orchestrates the full flyer ingestion pipeline:
/// 1. Store asset
/// 2. OCR
/// 3. Post-process text
/// 4. LLM / heuristic normalization
/// 5. Geocoding
/// 6. Confidence evaluation
/// 7. Job status update
/// </summary>
public sealed class FlyerIngestionPipeline(
    IFlyerStorageService storage,
    IOcrService ocr,
    IFlyerTextPostProcessor postProcessor,
    ILlmEventNormalizer normalizer,
    IGeocodingService geocoding,
    IFlyerConfidenceEvaluator confidenceEvaluator,
    IIngestionJobRepository jobs,
    IIngestionAuditWriter audit) : IFlyerIngestionPipeline
{
    public async Task<FlyerIngestionResult> ProcessAsync(FlyerUploadRequest request, CancellationToken ct = default)
    {
        var envelope = new IngestionRequestEnvelope(
            Guid.NewGuid().ToString("N"),
            IngestionSourceKind.ManualSubmission,
            request.FileName,
            request.UploadedByUserId,
            System.Text.Json.JsonSerializer.Serialize(new { request.FileName, request.ContentType, request.UploadedByUserId }),
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow,
            new Dictionary<string, string?>
            {
                ["pipeline"] = "flyer",
                ["contentType"] = request.ContentType,
            });
        var job = await jobs.CreateAsync(envelope, ct);
        var jobId = job.JobId;

        try
        {
            job = Transition(job, IngestionJobStatus.VALIDATING, "Flyer asset validation and storage started.");
            await jobs.SaveAsync(job, ct);
            var stored = await storage.StoreAsync(request, ct);
            await audit.WriteAsync(jobId, "Stored", $"assetId={stored.AssetId}", ct);

            job = Transition(job, IngestionJobStatus.NORMALIZING, "Flyer OCR and normalization started.");
            await jobs.SaveAsync(job, ct);
            var ocrResult = await ocr.ExtractTextAsync(stored.AssetId, stored.StorageKey, ct);
            await audit.WriteAsync(jobId, "OCR", $"confidence={ocrResult.Confidence:F2} success={ocrResult.Success}", ct);

            if (!ocrResult.Success)
            {
                job = Transition(
                    job,
                    IngestionJobStatus.FAILED,
                    ocrResult.ErrorMessage,
                    issues:
                    [
                        new CanonicalIngestionIssue(
                            "flyer_ocr_failed",
                            ocrResult.ErrorMessage ?? "OCR failed.",
                            IngestionIssueSeverity.Error,
                            false,
                            null,
                            new Dictionary<string, string?>())
                    ]);
                await jobs.SaveAsync(job, ct);
                return new FlyerIngestionResult(stored.AssetId, jobId, null, null, true,
                    [$"OCR failed: {ocrResult.ErrorMessage}"], false, ocrResult.ErrorMessage);
            }

            var cleanedText = postProcessor.Clean(ocrResult.RawText);

            var normalized = await normalizer.NormalizeAsync(cleanedText, stored.AssetId, ct);
            await audit.WriteAsync(jobId, "Normalized", $"confidence={normalized.ExtractionConfidence:F2}", ct);

            var geocodeResult = await geocoding.GeocodeAsync(normalized.Address ?? string.Empty, "austin-tx", ct);
            await audit.WriteAsync(jobId, "Geocoded", $"confidence={geocodeResult.Confidence:F2}", ct);

            var confidence = confidenceEvaluator.Evaluate(ocrResult, normalized, geocodeResult.Confidence);

            var candidate = new NormalizedEventCandidate(
                Title: normalized.Title,
                VenueName: normalized.VenueName,
                Address: geocodeResult.NormalizedAddress ?? normalized.Address,
                StartUtc: normalized.StartDate,
                EndUtc: normalized.EndDate,
                Timezone: normalized.Timezone,
                Category: normalized.Category,
                Description: normalized.Description,
                Tags: normalized.Tags,
                SourceKind: "flyer_upload",
                SourceRef: stored.AssetId,
                ExtractionConfidence: confidence.ExtractionConfidence,
                GeocodeConfidence: confidence.GeocodeConfidence,
                TemporalConfidence: confidence.TemporalConfidence,
                EvidenceRefs: [stored.AssetId, jobId]);

            job = Transition(
                job,
                IngestionJobStatus.REQUIRES_REVIEW,
                "Flyer candidate requires review before aggregate promotion.",
                candidate,
                [
                    new CanonicalSourceEvidence(
                        $"flyer-asset-{jobId}",
                        "flyer-asset",
                        stored.AssetId,
                        stored.ContentType,
                        stored.StorageKey,
                        DateTimeOffset.UtcNow,
                        0.9,
                        new Dictionary<string, string?>
                        {
                            ["checksum"] = stored.ChecksumSha256,
                        })
                ],
                Array.Empty<CanonicalIngestionIssue>());
            await jobs.SaveAsync(job, ct);
            await audit.WriteAsync(jobId, "ReviewPending", $"requiresReview={confidence.RequiresManualReview}", ct);

            return new FlyerIngestionResult(
                stored.AssetId, jobId, candidate, confidence,
                confidence.RequiresManualReview, confidence.ReviewBlockers, true, null);
        }
        catch (Exception ex)
        {
            job = Transition(
                job,
                IngestionJobStatus.FAILED,
                ex.Message,
                issues:
                [
                    new CanonicalIngestionIssue(
                        "flyer_pipeline_failed",
                        ex.Message,
                        IngestionIssueSeverity.Error,
                        false,
                        null,
                        new Dictionary<string, string?>())
                ]);
            await jobs.SaveAsync(job, ct);
            await audit.WriteAsync(jobId, "Failed", ex.Message, ct);
            return new FlyerIngestionResult(string.Empty, jobId, null, null, true,
                [$"Pipeline error: {ex.Message}"], false, ex.Message);
        }
    }

    private static IngestionResult Transition(
        IngestionResult current,
        IngestionJobStatus status,
        string? detail,
        CanonicalEventCandidate? candidate = null,
        CanonicalSourceEvidence[]? evidence = null,
        CanonicalIngestionIssue[]? issues = null)
    {
        var now = DateTimeOffset.UtcNow;
        return current with
        {
            Status = status,
            Candidate = candidate ?? current.Candidate,
            Evidence = evidence ?? current.Evidence,
            Issues = issues ?? current.Issues,
            Lifecycle = [.. current.Lifecycle, new IngestionStatusRecord(status, now, detail)],
            UpdatedAtUtc = now,
        };
    }
}

// ---------------------------------------------------------------------------
// Stub implementations (replaced by real OCR / storage providers)
// ---------------------------------------------------------------------------

public sealed class LocalFileStorageService : IFlyerStorageService
{
    public async Task<FlyerStorageResult> StoreAsync(FlyerUploadRequest request, CancellationToken ct = default)
    {
        var assetId = Guid.NewGuid().ToString("N");
        var storageKey = $"flyers/{assetId}/{request.FileName}";
        using var ms = new MemoryStream();
        await request.Content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var checksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        return new FlyerStorageResult(assetId, storageKey, request.ContentType, bytes.Length, checksum);
    }
}

public sealed class StubOcrService : IOcrService
{
    public Task<OcrResult> ExtractTextAsync(string assetId, string storageKey, CancellationToken ct = default)
    {
        // Stub: return empty success for dev — real OCR provider wired via Tesseract or Azure Vision
        return Task.FromResult(new OcrResult(
            assetId, "STUB OCR TEXT — replace with real Tesseract or Azure Vision provider.",
            0.5, "stub-v0.0", true, null));
    }
}

public sealed class FlyerConfidenceEvaluator : IFlyerConfidenceEvaluator
{
    private const double MinDimension = 0.50;
    private const double AutoApproveThreshold = 0.85;

    public FlyerConfidenceResult Evaluate(OcrResult ocr, LlmNormalizationResult norm, double geocodeConfidence)
    {
        var extraction = (ocr.Confidence + norm.ExtractionConfidence) / 2.0;
        var temporal = norm.TemporalConfidence;
        var geocode = geocodeConfidence;
        var aggregate = extraction * 0.40 + geocode * 0.30 + temporal * 0.30;

        var blockers = new List<string>();
        if (aggregate < AutoApproveThreshold) blockers.Add($"Aggregate confidence {aggregate:F2} < {AutoApproveThreshold}");
        if (extraction < MinDimension) blockers.Add($"Extraction confidence {extraction:F2} < {MinDimension}");
        if (geocode < MinDimension) blockers.Add($"Geocode confidence {geocode:F2} < {MinDimension}");
        if (temporal < MinDimension) blockers.Add($"Temporal confidence {temporal:F2} < {MinDimension}");

        return new FlyerConfidenceResult(aggregate, extraction, geocode, temporal,
            blockers.Count > 0, [.. blockers]);
    }
}

public sealed class StubGeocodingService : IGeocodingService
{
    public Task<GeocodeResult> GeocodeAsync(string address, string market, CancellationToken ct = default)
        => Task.FromResult(new GeocodeResult(null, null, 0.0, address, false));
}
