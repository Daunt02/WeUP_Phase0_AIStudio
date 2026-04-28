namespace WeUP.Contracts.Ocr;

/// <summary>
/// Simple rectangular bounding box used by OCR providers.
/// </summary>
public sealed record BoundingBox(int X, int Y, int Width, int Height);

public sealed record OcrTextBlock(
    int Index,
    string Text,
    double Confidence,
    int X,
    int Y,
    int Width,
    int Height,
    IReadOnlyDictionary<string, string?> Metadata);

public sealed record OcrResult(
    string ExtractionId,
    string JobId,
    string AssetId,
    string Provider,
    string ProviderVersion,
    double Confidence,
    bool Success,
    string RawText,
    OcrTextBlock[] Blocks,
    string? FailureReason,
    int AttemptCount,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyDictionary<string, string?> Metadata);