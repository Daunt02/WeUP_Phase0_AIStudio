namespace WeUP.Domain.Media;

public interface IMediaIntakeRepository
{
    Task SaveAssetAsync(MediaAsset asset, CancellationToken ct = default);
    Task SaveUploadAsync(MediaUpload upload, CancellationToken ct = default);
    Task<MediaAsset?> GetAssetAsync(string assetId, CancellationToken ct = default);
    Task<MediaUpload?> GetUploadAsync(string uploadId, CancellationToken ct = default);
    Task UpdateAssetStatusAsync(string assetId, MediaAssetStatus status, string? metadataJson = null, CancellationToken ct = default);
    Task UpdateUploadAsync(MediaUpload upload, CancellationToken ct = default);
}
