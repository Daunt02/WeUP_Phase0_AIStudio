using WeUP.Contracts.Ocr;

namespace WeUP.Domain.Ocr;

public sealed record OcrRequest(
    string JobId,
    string AssetId,
    string StorageKey,
    string ContentType,
    string? OriginalFilename,
    string? LocalPath,
    IReadOnlyDictionary<string, string?> Metadata);

/// <summary>
/// OCR service abstraction. Implementations may wrap provider engines and should
/// return a non-throwing low-confidence result for empty or unreadable payloads.
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Extract text from the supplied OCR request.
    /// </summary>
    Task<OcrResult> ExtractAsync(OcrRequest request, CancellationToken ct = default);
}