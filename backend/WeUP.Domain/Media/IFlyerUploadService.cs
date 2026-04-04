namespace WeUP.Domain.Media;

/// <summary>
/// P25: Flyer Media Intake
/// Service for uploading, storing, and managing flyer assets.
/// </summary>
public interface IFlyerUploadService
{
    /// <summary>
    /// Upload a flyer image (JPEG/PNG).
    /// Returns asset ID for later reference.
    /// </summary>
    Task<string> UploadFlyerAsync(
        Stream fileStream,
        string filename,
        string contentType,
        string submitterId,
        CancellationToken ct = default);

    /// <summary>
    /// Get flyer asset metadata by ID.
    /// </summary>
    Task<FlyerAsset?> GetFlyerAsync(string assetId, CancellationToken ct = default);

    /// <summary>
    /// List flyers uploaded by a user.
    /// </summary>
    Task<FlyerAsset[]> ListUserFlyersAsync(string submitterId, CancellationToken ct = default);

    /// <summary>
    /// Delete a flyer asset.
    /// </summary>
    Task DeleteFlyerAsync(string assetId, CancellationToken ct = default);
}

/// <summary>
/// Flyer asset metadata.
/// </summary>
public record FlyerAsset(
    string AssetId,
    string OriginalFilename,
    long FileSizeBytes,
    string ContentType,
    string SubmitterId,
    DateTimeOffset UploadedAt,
    string? S3Url = null,
    string? LocalPath = null);
