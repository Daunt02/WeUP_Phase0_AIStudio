using WeUP.Contracts.Ingestion;
using WeUP.Domain.Flyer;
using WeUP.Domain.Media;
using WeUP.Domain.Ocr;

namespace WeUP.Infrastructure.Flyer;

public sealed class FlyerOcrServiceAdapter(IOcrService ocrService) : IFlyerOcrService
{
    public async Task<FlyerOcrExtractionResult> ExtractAsync(FlyerAssetReference asset, string jobId, CancellationToken ct = default)
    {
        var request = new OcrRequest(
            JobId: jobId,
            AssetId: asset.AssetId,
            StorageKey: asset.StorageKey,
            ContentType: asset.ContentType,
            OriginalFilename: asset.OriginalFilename,
            LocalPath: ResolveLocalPath(asset),
            Metadata: asset.Metadata);

        var result = await ocrService.ExtractAsync(request, ct);
        return new FlyerOcrExtractionResult(
            ExtractionId: result.ExtractionId,
            JobId: result.JobId,
            AssetId: result.AssetId,
            Engine: result.Provider,
            EngineVersion: result.ProviderVersion,
            Confidence: result.Confidence,
            Success: result.Success,
            RawText: result.RawText,
            Blocks: result.Blocks.Select(block => new FlyerOcrTextBlock(
                Index: block.Index,
                Text: block.Text,
                Confidence: block.Confidence,
                X: block.X,
                Y: block.Y,
                Width: block.Width,
                Height: block.Height,
                Metadata: block.Metadata)).ToArray(),
            Issues: BuildIssues(result),
            StartedAtUtc: result.StartedAtUtc,
            CompletedAtUtc: result.CompletedAtUtc);
    }

    private static CanonicalIngestionIssue[] BuildIssues(WeUP.Contracts.Ocr.OcrResult result)
    {
        if (result.Success || string.IsNullOrWhiteSpace(result.FailureReason))
        {
            return [];
        }

        return
        [
            new CanonicalIngestionIssue(
                Code: "ocr_provider_failure",
                Message: result.FailureReason,
                Severity: IngestionIssueSeverity.Error,
                IsRetryable: false,
                Field: "ocr",
                Metadata: new Dictionary<string, string?>
                {
                    ["provider"] = result.Provider,
                    ["attemptCount"] = result.AttemptCount.ToString(),
                })
        ];
    }

    private static string? ResolveLocalPath(FlyerAssetReference asset)
    {
        if (asset.Metadata.TryGetValue("localPath", out var localPath) && !string.IsNullOrWhiteSpace(localPath))
        {
            return localPath;
        }

        return null;
    }
}