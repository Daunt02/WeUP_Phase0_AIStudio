using WeUP.Contracts.Ocr;
using WeUP.Domain.Ocr;

namespace WeUP.Infrastructure.Ocr;

/// <summary>
/// Deterministic OCR service for early pipeline integration and tests.
/// </summary>
public sealed class MockOcrService : IOcrService
{
    public Task<OcrResult> ExtractAsync(OcrRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startedAtUtc = DateTimeOffset.UtcNow;
        var extractionId = Guid.NewGuid().ToString("N");
        var rawText = "MOCK EVENT - 01/01/2025 - 123 Main St";

        var blocks = new[]
        {
            new OcrTextBlock(0, "MOCK EVENT", 0.95, 10, 10, 200, 30, new Dictionary<string, string?>()),
            new OcrTextBlock(1, "01/01/2025", 0.90, 10, 50, 150, 25, new Dictionary<string, string?>()),
            new OcrTextBlock(2, "123 Main St", 0.92, 10, 80, 180, 25, new Dictionary<string, string?>()),
        };

        var metadata = new Dictionary<string, string?>(request.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            ["storageKey"] = request.StorageKey,
            ["provider"] = "mock-ocr",
            ["providerVersion"] = "v1",
            ["source"] = "deterministic-mock",
        };

        var completedAtUtc = DateTimeOffset.UtcNow;
        var result = new OcrResult(
            ExtractionId: extractionId,
            JobId: request.JobId,
            AssetId: request.AssetId,
            Provider: "mock-ocr",
            ProviderVersion: "v1",
            Confidence: 0.92,
            Success: true,
            RawText: rawText,
            Blocks: blocks,
            FailureReason: null,
            AttemptCount: 1,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: completedAtUtc,
            Metadata: metadata);

        return Task.FromResult(result);
    }
}
