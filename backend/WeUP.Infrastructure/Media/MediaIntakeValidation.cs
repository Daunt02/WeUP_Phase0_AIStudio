using WeUP.Domain.Media;
using Microsoft.Extensions.Options;

namespace WeUP.Infrastructure.Media;

public sealed class MediaIntakeValidation
{
    private readonly MediaIntakeOptions _options;

    public MediaIntakeValidation(IOptions<MediaIntakeOptions> options)
    {
        _options = options.Value;
    }

    public string? Validate(MediaAssetType assetType, string contentType, long fileSizeBytes, string originalFilename)
    {
        if (string.IsNullOrWhiteSpace(originalFilename))
        {
            return "originalFilename is required.";
        }

        if (fileSizeBytes <= 0)
        {
            return "file must not be empty.";
        }

        if (fileSizeBytes > _options.MaxFileSizeBytes)
        {
            return $"file size exceeds maximum of {_options.MaxFileSizeBytes} bytes.";
        }

        var normalized = NormalizeContentType(contentType);
        if (!_options.AllowedContentTypes.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return $"unsupported content type '{contentType}'.";
        }

        if (assetType is MediaAssetType.FlyerImage or MediaAssetType.VenueImage or MediaAssetType.PromotionalPoster or MediaAssetType.EventMedia)
        {
            if (!normalized.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return "only image uploads are currently supported for this media type.";
            }
        }

        return null;
    }

    public static string NormalizeContentType(string contentType) =>
        (contentType ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries)[0].Trim().ToLowerInvariant();
}
