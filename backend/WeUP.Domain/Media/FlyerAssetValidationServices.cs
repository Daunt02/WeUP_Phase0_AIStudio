namespace WeUP.Domain.Media;

public sealed record FileSignatureInspectionResult(
    bool IsCorrupted,
    string? FailureReason,
    string? DetectedContentType,
    int? WidthPx,
    int? HeightPx,
    bool IsAnimated);

public interface IFileSignatureInspector
{
    FileSignatureInspectionResult Inspect(byte[] fileBytes);
}

public interface IMediaChecksumService
{
    string ComputeSha256Hex(byte[] fileBytes);
}

public sealed record DuplicateFlyerMatch(string ExistingAssetId, DateTimeOffset UploadedAt);

public interface IFlyerDuplicateDetector
{
    Task<DuplicateFlyerMatch?> FindDuplicateAsync(
        string contentHash,
        string submitterId,
        DateTimeOffset nowUtc,
        string? sourceReference,
        bool allowSameSourceReupload,
        CancellationToken ct = default);
}

public interface IFlyerAssetLifecyclePolicy
{
    FlyerAssetStatus Transition(FlyerAssetStatus current, FlyerAssetStatus next);
    bool CanTransition(FlyerAssetStatus current, FlyerAssetStatus next);
}
