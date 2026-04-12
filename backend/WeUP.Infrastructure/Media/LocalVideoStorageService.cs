using WeUP.Domain.Media;
using Microsoft.Extensions.Options;

namespace WeUP.Infrastructure.Media;

/// <summary>
/// P28: Local filesystem storage for video flyer files.
/// Writes to uploads/video/{yyyyMM}/{assetId}{ext}.
/// Phase 0.5+: Replace with a cloud storage implementation behind IVideoStorageService.
/// </summary>
public sealed class LocalVideoStorageService : IVideoStorageService
{
    private readonly VideoIntakeOptions _options;

    public LocalVideoStorageService(IOptions<VideoIntakeOptions> options)
    {
        _options = options.Value;
    }

    public async Task<VideoStorageRef> SaveAsync(VideoStorageWriteRequest request, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(request.OriginalFilename);
        var safeExtension = string.IsNullOrWhiteSpace(extension)
            ? GuessExtension(request.ContentType)
            : extension;

        var month = DateTime.UtcNow.ToString("yyyyMM");
        var directory = Path.Combine(Directory.GetCurrentDirectory(), _options.LocalStorageRoot, month);
        Directory.CreateDirectory(directory);

        var objectKey = $"video/{month}/{request.AssetId}{safeExtension}";
        var fullPath = Path.Combine(directory, $"{request.AssetId}{safeExtension}");

        if (request.Content.CanSeek)
            request.Content.Position = 0;

        await using var output = File.Create(fullPath);
        await request.Content.CopyToAsync(output, ct);

        return new VideoStorageRef(
            Provider: "LocalFileSystem",
            Container: directory,
            ObjectKey: objectKey,
            Uri: fullPath);
    }

    private static string GuessExtension(string contentType) => contentType switch
    {
        "video/mp4" => ".mp4",
        "video/webm" => ".webm",
        "video/quicktime" => ".mov",
        "video/x-msvideo" => ".avi",
        "video/mpeg" => ".mpeg",
        "video/ogg" => ".ogv",
        _ => ".bin",
    };
}
