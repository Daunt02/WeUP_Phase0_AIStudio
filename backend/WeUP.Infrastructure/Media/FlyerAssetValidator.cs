using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

/// <summary>
/// P26: Server-side flyer validator.
/// Checks: MIME type allowlist, magic byte signature, file size, extension mismatch, duplicate hash.
/// </summary>
public sealed class FlyerAssetValidator : IFlyerAssetValidator
{
    private static readonly IReadOnlyDictionary<string, string[]> ExtensionMap =
        new Dictionary<string, string[]>
        {
            ["image/jpeg"] = [".jpg", ".jpeg"],
            ["image/png"] = [".png"],
            ["image/webp"] = [".webp"],
        };

    private readonly IFileSignatureInspector _signatureInspector;
    private readonly IMediaChecksumService _checksumService;
    private readonly IFlyerDuplicateDetector _duplicateDetector;

    public FlyerAssetValidator(
        IFileSignatureInspector signatureInspector,
        IMediaChecksumService checksumService,
        IFlyerDuplicateDetector duplicateDetector)
    {
        _signatureInspector = signatureInspector;
        _checksumService = checksumService;
        _duplicateDetector = duplicateDetector;
    }

    public async Task<FlyerValidationResult> ValidateAsync(
        Stream stream,
        string declaredContentType,
        string filename,
        string submitterId,
        string? sourceReference = null,
        bool allowSameSourceReupload = false,
        CancellationToken ct = default)
    {
        var declaredMime = declaredContentType.Split(';')[0].Trim().ToLowerInvariant();

        if (!FlyerPolicy.AllowedMimeTypes.Contains(declaredMime))
        {
            return FlyerValidationResult.Fail($"Content type '{declaredMime}' is not allowed. Accepted: {string.Join(", ", FlyerPolicy.AllowedMimeTypes)}");
        }

        var ext = Path.GetExtension(filename).ToLowerInvariant();
        if (ExtensionMap.TryGetValue(declaredMime, out var expectedExt) && !expectedExt.Contains(ext))
        {
            return FlyerValidationResult.Fail($"Extension '{ext}' does not match declared content type '{declaredMime}'");
        }

        stream.Position = 0;
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        if (bytes.Length == 0)
        {
            return FlyerValidationResult.Fail("File is empty");
        }

        if (bytes.Length > FlyerPolicy.MaxFileSizeBytes)
        {
            return FlyerValidationResult.Fail($"File size {bytes.Length:N0} bytes exceeds the {FlyerPolicy.MaxFileSizeBytes / 1024 / 1024} MB limit");
        }

        var signature = _signatureInspector.Inspect(bytes);
        if (signature.IsCorrupted || string.IsNullOrWhiteSpace(signature.DetectedContentType))
        {
            return FlyerValidationResult.Fail("File is corrupted or unreadable as an image.");
        }

        var detectedMime = signature.DetectedContentType.Trim().ToLowerInvariant();
        if (!FlyerPolicy.AllowedMimeTypes.Contains(detectedMime))
        {
            return FlyerValidationResult.Fail($"Detected content type '{detectedMime}' is not allowed.");
        }

        if (!string.Equals(declaredMime, detectedMime, StringComparison.OrdinalIgnoreCase))
        {
            return FlyerValidationResult.Fail($"Declared content type '{declaredMime}' does not match detected '{detectedMime}'.");
        }

        if (!signature.WidthPx.HasValue || !signature.HeightPx.HasValue)
        {
            return FlyerValidationResult.Fail("Unable to determine image dimensions.");
        }

        if (signature.WidthPx.Value < FlyerPolicy.MinWidthPx || signature.HeightPx.Value < FlyerPolicy.MinHeightPx)
        {
            return FlyerValidationResult.Fail($"Image dimensions must be at least {FlyerPolicy.MinWidthPx}x{FlyerPolicy.MinHeightPx} pixels.");
        }

        if (!FlyerPolicy.AllowAnimatedImages && signature.IsAnimated)
        {
            return FlyerValidationResult.Fail("Animated image formats are not allowed for flyers.");
        }

        var hash = _checksumService.ComputeSha256Hex(bytes);
        var duplicate = await _duplicateDetector.FindDuplicateAsync(
            hash,
            submitterId,
            DateTimeOffset.UtcNow,
            sourceReference,
            allowSameSourceReupload,
            ct);

        if (duplicate is not null)
        {
            return FlyerValidationResult.Duplicate(hash, duplicate.ExistingAssetId);
        }

        stream.Position = 0;

        return FlyerValidationResult.Ok(
            hash,
            detectedMime,
            signature.WidthPx.Value,
            signature.HeightPx.Value,
            signature.IsAnimated);
    }
}
