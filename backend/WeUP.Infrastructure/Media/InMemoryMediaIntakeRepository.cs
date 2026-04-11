using System.Collections.Concurrent;
using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

public sealed class InMemoryMediaIntakeRepository : IMediaIntakeRepository
{
    private readonly ConcurrentDictionary<string, MediaAsset> _assets = new();
    private readonly ConcurrentDictionary<string, MediaUpload> _uploads = new();

    public Task SaveAssetAsync(MediaAsset asset, CancellationToken ct = default)
    {
        _assets[asset.AssetId] = asset;
        return Task.CompletedTask;
    }

    public Task SaveUploadAsync(MediaUpload upload, CancellationToken ct = default)
    {
        _uploads[upload.UploadId] = upload;
        return Task.CompletedTask;
    }

    public Task<MediaAsset?> GetAssetAsync(string assetId, CancellationToken ct = default)
    {
        _assets.TryGetValue(assetId, out var asset);
        return Task.FromResult(asset);
    }

    public Task<MediaUpload?> GetUploadAsync(string uploadId, CancellationToken ct = default)
    {
        _uploads.TryGetValue(uploadId, out var upload);
        return Task.FromResult(upload);
    }

    public Task UpdateAssetStatusAsync(string assetId, MediaAssetStatus status, string? metadataJson = null, CancellationToken ct = default)
    {
        if (!_assets.TryGetValue(assetId, out var asset))
        {
            return Task.CompletedTask;
        }

        _assets[assetId] = asset with
        {
            Status = status,
            MetadataJson = metadataJson ?? asset.MetadataJson,
        };

        return Task.CompletedTask;
    }

    public Task UpdateUploadAsync(MediaUpload upload, CancellationToken ct = default)
    {
        _uploads[upload.UploadId] = upload;
        return Task.CompletedTask;
    }
}
