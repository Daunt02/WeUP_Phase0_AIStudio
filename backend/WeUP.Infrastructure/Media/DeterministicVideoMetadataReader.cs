using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

/// <summary>
/// Phase 0.15 metadata reader that builds a stable metadata snapshot from the asset record.
/// Future versions can replace this seam with ffprobe/media-info extraction.
/// </summary>
public sealed class DeterministicVideoMetadataReader : IVideoMetadataReader
{
    public Task<VideoMetadataSnapshot> ReadAsync(VideoFlyerAsset asset, CancellationToken ct = default)
    {
        if (asset.OriginalFilename.Contains("fail-extract", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("metadata extraction failed: synthetic Phase 0.15 test seam");
        }

        var duration = asset.DurationSeconds.GetValueOrDefault(12);
        if (duration <= 0)
        {
            duration = 1;
        }

        var width = asset.WidthPx.GetValueOrDefault(1280);
        var height = asset.HeightPx.GetValueOrDefault(720);
        if (width <= 0 || height <= 0)
        {
            width = 1280;
            height = 720;
        }

        var metadata = new VideoMetadataSnapshot(
            DurationSeconds: duration,
            WidthPx: width,
            HeightPx: height,
            DetectedCodec: asset.DetectedCodec,
            BitrateKbps: asset.BitrateKbps);

        return Task.FromResult(metadata);
    }
}
