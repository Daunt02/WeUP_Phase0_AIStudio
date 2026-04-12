namespace WeUP.Domain.Media;

/// <summary>
/// P26: Extended flyer asset domain model with lifecycle and integrity metadata.
/// Supersedes the basic FlyerAsset record from P25.
/// </summary>
public sealed record FlyerAssetRecord
{
    public required string AssetId { get; init; }
    public required string OriginalFilename { get; init; }
    public required long FileSizeBytes { get; init; }
    public required string ContentType { get; init; }
    public required string SubmitterId { get; init; }
    public required DateTimeOffset UploadedAt { get; init; }
    public required FlyerAssetStatus Status { get; init; }
    public required string ContentHash { get; init; }
    public required string StorageKey { get; init; }
    public int Revision { get; init; } = 1;
    public string? SourceReference { get; init; }
    public string? CanonicalContentType { get; init; }
    public int? WidthPx { get; init; }
    public int? HeightPx { get; init; }
    public bool IsAnimated { get; init; }
    public string? LocalPath { get; init; }
    public string? S3Url { get; init; }
    public string? ValidationFailureReason { get; init; }

    public FlyerAssetRecord WithStatus(FlyerAssetStatus next) =>
        this with { Status = FlyerAssetLifecycle.Transition(Status, next) };
}

/// <summary>
/// P26: Durable storage contract for flyer assets with lifecycle support.
/// Implementations: in-memory (dev), EF Core + Postgres (prod).
/// </summary>
public interface IFlyerAssetStore
{
    Task<FlyerAssetRecord> SaveAsync(FlyerAssetRecord asset, CancellationToken ct = default);
    Task<FlyerAssetRecord?> GetAsync(string assetId, CancellationToken ct = default);
    Task<FlyerAssetRecord[]> ListBySubmitterAsync(string submitterId, CancellationToken ct = default);
    Task<FlyerAssetRecord?> FindByHashAsync(string contentHash, string submitterId, CancellationToken ct = default);
    Task<FlyerAssetRecord?> FindRecentByHashAsync(string contentHash, string submitterId, DateTimeOffset sinceUtc, CancellationToken ct = default);
    Task<FlyerAssetRecord> UpdateStatusAsync(string assetId, FlyerAssetStatus next, CancellationToken ct = default);
    Task<FlyerAssetRecord> UpdateValidationFailureAsync(string assetId, string failureReason, CancellationToken ct = default);
    Task DeleteAsync(string assetId, CancellationToken ct = default);
}
