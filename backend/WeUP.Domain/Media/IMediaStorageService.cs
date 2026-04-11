namespace WeUP.Domain.Media;

public sealed record MediaStorageWriteRequest(
    string AssetId,
    MediaAssetType AssetType,
    string OriginalFilename,
    string ContentType,
    Stream Content);

public interface IMediaStorageService
{
    Task<MediaStorageRef> SaveAsync(MediaStorageWriteRequest request, CancellationToken ct = default);
}
