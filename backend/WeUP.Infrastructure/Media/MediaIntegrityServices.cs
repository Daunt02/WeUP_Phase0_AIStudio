using SixLabors.ImageSharp;
using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

public sealed class ImageSharpFileSignatureInspector : IFileSignatureInspector
{
    public FileSignatureInspectionResult Inspect(byte[] fileBytes)
    {
        try
        {
            using var image = Image.Load(fileBytes);
            var mime = image.Metadata.DecodedImageFormat?.DefaultMimeType?.ToLowerInvariant();
            var width = image.Width;
            var height = image.Height;
            var isAnimated = image.Frames.Count > 1;

            return new FileSignatureInspectionResult(false, null, mime, width, height, isAnimated);
        }
        catch (Exception ex)
        {
            return new FileSignatureInspectionResult(true, ex.Message, null, null, null, false);
        }
    }
}

public sealed class Sha256ChecksumService : IMediaChecksumService
{
    public string ComputeSha256Hex(byte[] fileBytes) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(fileBytes)).ToLowerInvariant();
}

public sealed class FlyerDuplicateDetector : IFlyerDuplicateDetector
{
    private readonly IFlyerAssetStore _store;

    public FlyerDuplicateDetector(IFlyerAssetStore store)
    {
        _store = store;
    }

    public async Task<DuplicateFlyerMatch?> FindDuplicateAsync(
        string contentHash,
        string submitterId,
        DateTimeOffset nowUtc,
        string? sourceReference,
        bool allowSameSourceReupload,
        CancellationToken ct = default)
    {
        var existing = await _store.FindRecentByHashAsync(contentHash, submitterId, nowUtc - FlyerPolicy.DuplicateWindow, ct);
        if (existing is null)
        {
            return null;
        }

        if (allowSameSourceReupload && !string.IsNullOrWhiteSpace(sourceReference) &&
            string.Equals(sourceReference, existing.SourceReference, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new DuplicateFlyerMatch(existing.AssetId, existing.UploadedAt);
    }
}

public sealed class FlyerAssetLifecyclePolicy : IFlyerAssetLifecyclePolicy
{
    public FlyerAssetStatus Transition(FlyerAssetStatus current, FlyerAssetStatus next) =>
        FlyerAssetLifecycle.Transition(current, next);

    public bool CanTransition(FlyerAssetStatus current, FlyerAssetStatus next) =>
        FlyerAssetLifecycle.CanTransition(current, next);
}