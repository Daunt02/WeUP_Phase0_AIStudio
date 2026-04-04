namespace WeUP.Domain.Media;

/// <summary>
/// P26: Result of server-side flyer asset validation.
/// </summary>
public sealed record FlyerValidationResult
{
    public bool IsValid { get; init; }
    public string? FailureReason { get; init; }
    public string? ContentHash { get; init; }
    public bool IsDuplicate { get; init; }
    public string? DuplicateAssetId { get; init; }

    public static FlyerValidationResult Ok(string contentHash) =>
        new() { IsValid = true, ContentHash = contentHash };

    public static FlyerValidationResult Fail(string reason) =>
        new() { IsValid = false, FailureReason = reason };

    public static FlyerValidationResult Duplicate(string contentHash, string existingAssetId) =>
        new() { IsValid = false, FailureReason = "Duplicate upload detected", ContentHash = contentHash, IsDuplicate = true, DuplicateAssetId = existingAssetId };
}

/// <summary>
/// P26: Flyer-specific validation rules and constraints.
/// </summary>
public static class FlyerPolicy
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    public const int MinWidthPx = 200;
    public const int MinHeightPx = 200;

    public static readonly IReadOnlySet<string> AllowedMimeTypes = new HashSet<string>
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };

    public static readonly IReadOnlyDictionary<string, byte[]> MagicBytes =
        new Dictionary<string, byte[]>
        {
            ["image/jpeg"] = [0xFF, 0xD8, 0xFF],
            ["image/png"]  = [0x89, 0x50, 0x4E, 0x47],
            ["image/webp"] = [0x52, 0x49, 0x46, 0x46], // RIFF header (WebP)
        };
}
