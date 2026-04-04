using System.Security.Cryptography;
using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

/// <summary>
/// P26: Server-side flyer validator.
/// Checks: MIME type allowlist, magic byte signature, file size, extension mismatch, duplicate hash.
/// </summary>
public sealed class FlyerAssetValidator : IFlyerAssetValidator
{
    private readonly IFlyerAssetStore _store;

    public FlyerAssetValidator(IFlyerAssetStore store)
    {
        _store = store;
    }

    public async Task<FlyerValidationResult> ValidateAsync(
        Stream stream,
        string declaredContentType,
        string filename,
        string submitterId,
        CancellationToken ct = default)
    {
        // Normalize MIME type (strip charset etc.)
        var mime = declaredContentType.Split(';')[0].Trim().ToLowerInvariant();

        // 1. MIME type allowlist
        if (!FlyerPolicy.AllowedMimeTypes.Contains(mime))
            return FlyerValidationResult.Fail($"Content type '{mime}' is not allowed. Accepted: {string.Join(", ", FlyerPolicy.AllowedMimeTypes)}");

        // 2. Extension mismatch
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        var expectedExt = mime switch
        {
            "image/jpeg" => new[] { ".jpg", ".jpeg" },
            "image/png"  => new[] { ".png" },
            "image/webp" => new[] { ".webp" },
            _ => Array.Empty<string>()
        };
        if (expectedExt.Length > 0 && !expectedExt.Contains(ext))
            return FlyerValidationResult.Fail($"Extension '{ext}' does not match declared content type '{mime}'");

        // 3. Read file into memory for signature check, size check, and hashing
        stream.Position = 0;
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        // 4. File size
        if (bytes.Length > FlyerPolicy.MaxFileSizeBytes)
            return FlyerValidationResult.Fail($"File size {bytes.Length:N0} bytes exceeds the {FlyerPolicy.MaxFileSizeBytes / 1024 / 1024} MB limit");

        if (bytes.Length == 0)
            return FlyerValidationResult.Fail("File is empty");

        // 5. Magic byte / file signature check
        if (!FlyerPolicy.MagicBytes.TryGetValue(mime, out var magic))
            return FlyerValidationResult.Fail($"No signature definition for MIME type '{mime}'");

        if (bytes.Length < magic.Length || !bytes.Take(magic.Length).SequenceEqual(magic))
            return FlyerValidationResult.Fail("File signature does not match declared content type — file may be corrupted or misidentified");

        // 6. WebP sub-format check (bytes 8-11 must be "WEBP")
        if (mime == "image/webp" && (bytes.Length < 12 || System.Text.Encoding.ASCII.GetString(bytes, 8, 4) != "WEBP"))
            return FlyerValidationResult.Fail("File claims to be WebP but does not contain a valid WEBP marker");

        // 7. Content hash (SHA-256)
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        // 8. Duplicate detection — same hash + same submitter
        var existing = await _store.FindByHashAsync(hash, submitterId, ct);
        if (existing != null)
            return FlyerValidationResult.Duplicate(hash, existing.AssetId);

        // Reset stream position so caller can still read the bytes
        stream.Position = 0;

        return FlyerValidationResult.Ok(hash);
    }
}
