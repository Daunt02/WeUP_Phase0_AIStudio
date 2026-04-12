using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

/// <summary>
/// Phase 0.15 deterministic extractor: fixed interval frames with a scene-change seam left explicit.
/// </summary>
public sealed class DeterministicVideoFrameExtractor : IVideoFrameExtractor
{
    public VideoFrameExtractionStrategy Strategy => VideoFrameExtractionStrategy.FixedIntervalDeterministic;

    public Task<IReadOnlyList<ExtractedVideoFrame>> ExtractAsync(
        VideoFlyerAsset asset,
        VideoMetadataSnapshot metadata,
        CancellationToken ct = default)
    {
        var durationMs = metadata.DurationSeconds * 1000L;
        var offsets = BuildOffsets(durationMs);

        var frames = offsets
            .Select(offset => new ExtractedVideoFrame(
                TimestampOffsetMs: offset,
                FrameType: VideoFrameType.IntervalPreview,
                WidthPx: metadata.WidthPx,
                HeightPx: metadata.HeightPx,
                ExtractionStage: "FrameExtraction",
                ExtractionVersion: "phase0.15.v1"))
            .ToArray();

        return Task.FromResult<IReadOnlyList<ExtractedVideoFrame>>(frames);
    }

    private static IReadOnlyList<long> BuildOffsets(long durationMs)
    {
        if (durationMs <= 1500)
        {
            return [0];
        }

        if (durationMs <= 5000)
        {
            return [0, durationMs / 2];
        }

        var intervalMs = 2500L;
        var offsets = new List<long> { 0 };

        for (var offset = intervalMs; offset < durationMs && offsets.Count < 8; offset += intervalMs)
        {
            offsets.Add(offset);
        }

        var nearEnd = Math.Max(0, durationMs - 500);
        if (!offsets.Contains(nearEnd))
        {
            offsets.Add(nearEnd);
        }

        return offsets.Distinct().OrderBy(x => x).ToArray();
    }
}
