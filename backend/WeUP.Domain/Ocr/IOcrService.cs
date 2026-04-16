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

public interface IOcrService
{
    Task<OcrResult> ExtractAsync(OcrRequest request, CancellationToken ct = default);
}