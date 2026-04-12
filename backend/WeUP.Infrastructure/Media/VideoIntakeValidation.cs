using Microsoft.Extensions.Options;

namespace WeUP.Infrastructure.Media;

/// <summary>
/// P28: Validates video flyer upload parameters before storage.
/// Covers MIME type allowlist, file size ceiling, and extension plausibility.
/// </summary>
public sealed class VideoIntakeValidation
{
    private static readonly string[] KnownVideoExtensions =
        [".mp4", ".webm", ".mov", ".avi", ".mpeg", ".mpg", ".ogv", ".ogg"];

    private readonly VideoIntakeOptions _options;

    public VideoIntakeValidation(IOptions<VideoIntakeOptions> options)
    {
        _options = options.Value;
    }

    public string? Validate(string contentType, long fileSizeBytes, string originalFilename)
    {
        if (string.IsNullOrWhiteSpace(originalFilename))
            return "originalFilename is required.";

        if (fileSizeBytes <= 0)
            return "file must not be empty.";

        if (fileSizeBytes > _options.MaxFileSizeBytes)
        {
            var limitMb = _options.MaxFileSizeBytes / (1024 * 1024);
            return $"file size exceeds the maximum of {limitMb} MB for video flyer assets.";
        }

        var normalized = NormalizeContentType(contentType);

        if (!_options.AllowedContentTypes.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            return $"unsupported video content type '{contentType}'. Accepted: {string.Join(", ", _options.AllowedContentTypes)}.";

        if (!normalized.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return "only video/* content types are accepted as video flyer assets.";

        var ext = Path.GetExtension(originalFilename).ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(ext) && !KnownVideoExtensions.Contains(ext))
            return $"file extension '{ext}' does not match any known video container format.";

        return null;
    }

    public static string NormalizeContentType(string contentType) =>
        (contentType ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries)[0].Trim().ToLowerInvariant();
}
