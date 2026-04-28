using System.Text.Json;
using SixLabors.ImageSharp;
using WeUP.Contracts.Ocr;
using WeUP.Infrastructure.Flyer;
using WeUP.Domain.Ocr;

namespace WeUP.Infrastructure.Ocr;

public interface IOcrProvider
{
    string Name { get; }
    string Version { get; }

    Task<OcrResult> ExtractAsync(OcrProviderRequest request, CancellationToken ct = default);
}

public sealed record OcrProviderRequest(
    string ExtractionId,
    string JobId,
    string AssetId,
    string StorageKey,
    string ContentType,
    string? OriginalFilename,
    string LocalPath,
    IReadOnlyDictionary<string, string?> Metadata,
    int AttemptCount,
    DateTimeOffset StartedAtUtc);

public sealed class ProviderBackedOcrService(IOcrProvider provider, IOcrNormalizationTelemetry? telemetry = null) : IOcrService
{
    private const int MaxAttempts = 2;
    private readonly IOcrNormalizationTelemetry _telemetry = telemetry ?? new NoopOcrNormalizationTelemetry();

    public async Task<OcrResult> ExtractAsync(OcrRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var extractionId = Guid.NewGuid().ToString("N");
        var startedAtUtc = DateTimeOffset.UtcNow;
        var localPath = request.LocalPath;
        Exception? lastError = null;

        if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
        {
            return BuildFailureResult(
                request,
                extractionId,
                startedAtUtc,
                attemptCount: 1,
                failureReason: "OCR source file was not found.");
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var providerRequest = new OcrProviderRequest(
                    ExtractionId: extractionId,
                    JobId: request.JobId,
                    AssetId: request.AssetId,
                    StorageKey: request.StorageKey,
                    ContentType: request.ContentType,
                    OriginalFilename: request.OriginalFilename,
                    LocalPath: localPath,
                    Metadata: request.Metadata,
                    AttemptCount: attempt,
                    StartedAtUtc: startedAtUtc);

                var result = await provider.ExtractAsync(providerRequest, ct);
                var normalized = NormalizeSuccess(request, extractionId, startedAtUtc, attempt, result);
                _telemetry.TrackOcrExtraction(normalized.Provider, normalized.ProviderVersion, normalized.Success, normalized.Confidence);
                return normalized;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                lastError = ex;
            }
            catch (Exception ex)
            {
                lastError = ex;
                break;
            }
        }

        var failure = BuildFailureResult(
            request,
            extractionId,
            startedAtUtc,
            attemptCount: MaxAttempts,
            failureReason: lastError?.Message ?? "OCR provider failed.");

        _telemetry.TrackOcrExtraction(failure.Provider, failure.ProviderVersion, failure.Success, failure.Confidence);
        return failure;
    }

    private OcrResult NormalizeSuccess(OcrRequest request, string extractionId, DateTimeOffset startedAtUtc, int attemptCount, OcrResult providerResult)
    {
        var metadata = MergeMetadata(
            request.Metadata,
            providerResult.Metadata,
            new Dictionary<string, string?>
            {
                ["storageKey"] = request.StorageKey,
                ["attemptCount"] = attemptCount.ToString(),
                ["provider"] = provider.Name,
                ["providerVersion"] = provider.Version,
            });

        return providerResult with
        {
            ExtractionId = extractionId,
            JobId = request.JobId,
            AssetId = request.AssetId,
            Provider = provider.Name,
            ProviderVersion = provider.Version,
            RawText = providerResult.RawText ?? string.Empty,
            Blocks = providerResult.Blocks ?? [],
            AttemptCount = attemptCount,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = providerResult.CompletedAtUtc == default ? DateTimeOffset.UtcNow : providerResult.CompletedAtUtc,
            Metadata = metadata,
        };
    }

    private OcrResult BuildFailureResult(OcrRequest request, string extractionId, DateTimeOffset startedAtUtc, int attemptCount, string failureReason)
    {
        var metadata = MergeMetadata(
            request.Metadata,
            new Dictionary<string, string?>
            {
                ["storageKey"] = request.StorageKey,
                ["attemptCount"] = attemptCount.ToString(),
                ["provider"] = provider.Name,
                ["providerVersion"] = provider.Version,
            });

        return new OcrResult(
            ExtractionId: extractionId,
            JobId: request.JobId,
            AssetId: request.AssetId,
            Provider: provider.Name,
            ProviderVersion: provider.Version,
            Confidence: 0.0,
            Success: false,
            RawText: string.Empty,
            Blocks: [],
            FailureReason: failureReason,
            AttemptCount: attemptCount,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            Metadata: metadata);
    }

    private static IReadOnlyDictionary<string, string?> MergeMetadata(params IReadOnlyDictionary<string, string?>[] sources)
    {
        var merged = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            foreach (var entry in source)
            {
                merged[entry.Key] = entry.Value;
            }
        }

        return merged;
    }

    private sealed class NoopOcrNormalizationTelemetry : IOcrNormalizationTelemetry
    {
        public void TrackOcrExtraction(string provider, string providerVersion, bool success, double confidence)
        {
        }

        public void TrackNormalization(WeUP.Domain.Flyer.EventCandidate candidate)
        {
        }
    }
}

public sealed class SidecarOcrProvider : IOcrProvider
{
    public string Name => "sidecar-ocr";
    public string Version => "v1";

    public async Task<OcrResult> ExtractAsync(OcrProviderRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var image = await Image.LoadAsync(request.LocalPath, ct);
        var completedAtUtc = DateTimeOffset.UtcNow;
        var sidecarPath = request.LocalPath + ".ocr.json";
        var baseMetadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["contentType"] = request.ContentType,
            ["originalFilename"] = request.OriginalFilename,
            ["imageWidth"] = image.Width.ToString(),
            ["imageHeight"] = image.Height.ToString(),
            ["sidecarPath"] = sidecarPath,
        };

        if (!File.Exists(sidecarPath))
        {
            return new OcrResult(
                ExtractionId: request.ExtractionId,
                JobId: request.JobId,
                AssetId: request.AssetId,
                Provider: Name,
                ProviderVersion: Version,
                Confidence: 0.0,
                Success: true,
                RawText: string.Empty,
                Blocks: [],
                FailureReason: null,
                AttemptCount: request.AttemptCount,
                StartedAtUtc: request.StartedAtUtc,
                CompletedAtUtc: completedAtUtc,
                Metadata: baseMetadata);
        }

        await using var sidecarStream = File.OpenRead(sidecarPath);
        var payload = await JsonSerializer.DeserializeAsync<SidecarOcrPayload>(sidecarStream, cancellationToken: ct)
            ?? throw new InvalidOperationException($"OCR sidecar '{sidecarPath}' was empty or invalid.");

        var blocks = payload.Blocks?
            .Select(block => new OcrTextBlock(
                Index: block.Index,
                Text: block.Text ?? string.Empty,
                Confidence: block.Confidence,
                X: block.X,
                Y: block.Y,
                Width: block.Width,
                Height: block.Height,
                Metadata: block.Metadata ?? new Dictionary<string, string?>()))
            .ToArray() ?? [];

        var metadata = new Dictionary<string, string?>(baseMetadata)
        {
            ["providerOperationId"] = payload.ProviderOperationId,
            ["source"] = "sidecar",
        };

        return new OcrResult(
            ExtractionId: request.ExtractionId,
            JobId: request.JobId,
            AssetId: request.AssetId,
            Provider: Name,
            ProviderVersion: Version,
            Confidence: payload.Confidence,
            Success: payload.Success,
            RawText: payload.RawText ?? string.Empty,
            Blocks: blocks,
            FailureReason: payload.FailureReason,
            AttemptCount: request.AttemptCount,
            StartedAtUtc: request.StartedAtUtc,
            CompletedAtUtc: completedAtUtc,
            Metadata: metadata);
    }

    private sealed record SidecarOcrPayload(
        string? RawText,
        double Confidence,
        bool Success,
        string? FailureReason,
        string? ProviderOperationId,
        SidecarOcrBlock[]? Blocks);

    private sealed record SidecarOcrBlock(
        int Index,
        string? Text,
        double Confidence,
        int X,
        int Y,
        int Width,
        int Height,
        Dictionary<string, string?>? Metadata);
}