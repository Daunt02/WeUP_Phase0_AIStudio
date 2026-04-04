using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

/// <summary>
/// Local file-based flyer upload service (stub for Phase 0).
/// Future: Replace with S3 or cloud storage provider.
/// </summary>
public class LocalFlyerUploadService : IFlyerUploadService
{
    private readonly Dictionary<string, FlyerAsset> _assets = new();
    private readonly string _uploadDir;

    public LocalFlyerUploadService()
    {
        _uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "flyers");
        Directory.CreateDirectory(_uploadDir);
    }

    public async Task<string> UploadFlyerAsync(
        Stream fileStream,
        string filename,
        string contentType,
        string submitterId,
        CancellationToken ct = default)
    {
        // Generate asset ID
        var assetId = Guid.NewGuid().ToString();

        // Read file into memory
        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms, ct);
        var fileBytes = ms.ToArray();

        // Save to local directory (stub: in-memory only)
        var localPath = Path.Combine(_uploadDir, $"{assetId}_{filename}");
        await File.WriteAllBytesAsync(localPath, fileBytes, ct);

        // Create asset metadata
        var asset = new FlyerAsset(
            AssetId: assetId,
            OriginalFilename: filename,
            FileSizeBytes: fileBytes.Length,
            ContentType: contentType,
            SubmitterId: submitterId,
            UploadedAt: DateTimeOffset.UtcNow,
            LocalPath: localPath);

        _assets[assetId] = asset;
        return assetId;
    }

    public Task<FlyerAsset?> GetFlyerAsync(string assetId, CancellationToken ct = default)
    {
        return Task.FromResult(_assets.TryGetValue(assetId, out var asset) ? asset : null);
    }

    public Task<FlyerAsset[]> ListUserFlyersAsync(string submitterId, CancellationToken ct = default)
    {
        var userAssets = _assets.Values
            .Where(a => a.SubmitterId == submitterId)
            .ToArray();

        return Task.FromResult(userAssets);
    }

    public Task DeleteFlyerAsync(string assetId, CancellationToken ct = default)
    {
        if (_assets.TryGetValue(assetId, out var asset))
        {
            _assets.Remove(assetId);

            // Clean up local file if it exists
            if (File.Exists(asset.LocalPath))
            {
                try
                {
                    File.Delete(asset.LocalPath);
                }
                catch
                {
                    // Ignore file deletion errors
                }
            }
        }

        return Task.CompletedTask;
    }
}
