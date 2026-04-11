using WeUP.Domain.Media;
using Microsoft.Extensions.Options;

namespace WeUP.Infrastructure.Media;

public sealed class LocalMediaStorageService : IMediaStorageService
{
    private readonly MediaIntakeOptions _options;

    public LocalMediaStorageService(IOptions<MediaIntakeOptions> options)
    {
        _options = options.Value;
    }

    public async Task<MediaStorageRef> SaveAsync(MediaStorageWriteRequest request, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(request.OriginalFilename);
        var safeExtension = string.IsNullOrWhiteSpace(extension) ? GuessExtension(request.ContentType) : extension;
        var directory = Path.Combine(Directory.GetCurrentDirectory(), _options.LocalStorageRoot, DateTime.UtcNow.ToString("yyyyMM"));
        Directory.CreateDirectory(directory);

        var objectKey = $"{request.AssetType.ToString().ToLowerInvariant()}/{request.AssetId}{safeExtension}";
        var fullPath = Path.Combine(directory, $"{request.AssetId}{safeExtension}");

        request.Content.Position = 0;
        await using var output = File.Create(fullPath);
        await request.Content.CopyToAsync(output, ct);

        return new MediaStorageRef(
            MediaStorageProvider.LocalFileSystem,
            directory,
            objectKey,
            fullPath);
    }

    private static string GuessExtension(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".bin",
    };
}
