using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

/// <summary>
/// Deterministic poster selection using timeline and quality heuristics.
/// </summary>
public sealed class HeuristicPosterSelectionService : IPosterSelectionService
{
    public Task<PosterSelectionResult> SelectPosterAsync(
        VideoFlyerAsset asset,
        VideoMetadataSnapshot metadata,
        IReadOnlyList<ExtractedVideoFrame> frames,
        CancellationToken ct = default)
    {
        if (frames.Count == 0)
        {
            throw new InvalidOperationException("Poster selection requires at least one extracted frame.");
        }

        var durationMs = Math.Max(1000, metadata.DurationSeconds * 1000L);
        var selected = frames
            .Select(frame => new
            {
                Frame = frame,
                Score = Score(frame, durationMs, metadata.WidthPx, metadata.HeightPx),
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Frame.TimestampOffsetMs)
            .First();

        var timeline = Math.Round((selected.Frame.TimestampOffsetMs / (double)durationMs) * 100.0, 1);
        var reason = $"selected via deterministic score={selected.Score:F3}; timeline={timeline}% ; strategy=fixed-interval-first-pass";

        return Task.FromResult(new PosterSelectionResult(
            SelectedTimestampOffsetMs: selected.Frame.TimestampOffsetMs,
            Reason: reason,
            SelectionVersion: "poster.phase0.15.v1"));
    }

    private static double Score(ExtractedVideoFrame frame, long durationMs, int widthPx, int heightPx)
    {
        var ratio = frame.TimestampOffsetMs / (double)durationMs;
        var centerPenalty = Math.Abs(0.45 - ratio);
        var timelineScore = 1.0 - Math.Min(1.0, centerPenalty * 1.8);
        var resolutionScore = Math.Min(1.0, (frame.WidthPx * frame.HeightPx) / (double)Math.Max(1, widthPx * heightPx));

        return (timelineScore * 0.8) + (resolutionScore * 0.2);
    }
}
