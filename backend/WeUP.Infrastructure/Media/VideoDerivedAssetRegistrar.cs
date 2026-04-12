using System.Text;
using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

public sealed class VideoDerivedAssetRegistrar : IVideoDerivedAssetRegistrar
{
    private readonly IMediaStorageService _mediaStorage;
    private readonly IVideoDerivedAssetRepository _repository;

    public VideoDerivedAssetRegistrar(IMediaStorageService mediaStorage, IVideoDerivedAssetRepository repository)
    {
        _mediaStorage = mediaStorage;
        _repository = repository;
    }

    public async Task<IReadOnlyList<VideoDerivedFrameAsset>> RegisterAsync(
        VideoFlyerAsset sourceAsset,
        VideoProcessingJob job,
        IReadOnlyList<ExtractedVideoFrame> frames,
        PosterSelectionResult poster,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var results = new List<VideoDerivedFrameAsset>(frames.Count);

        foreach (var frame in frames)
        {
            var derivedAssetId = Guid.NewGuid().ToString("N");
            var contentType = "image/jpeg";
            var filename = $"{sourceAsset.AssetId}_{frame.TimestampOffsetMs}.jpg";

            await using var stream = BuildPlaceholderImageStream(sourceAsset.AssetId, frame.TimestampOffsetMs);
            var storage = await _mediaStorage.SaveAsync(
                new MediaStorageWriteRequest(
                    AssetId: derivedAssetId,
                    AssetType: frame.TimestampOffsetMs == poster.SelectedTimestampOffsetMs ? MediaAssetType.PromotionalPoster : MediaAssetType.EventMedia,
                    OriginalFilename: filename,
                    ContentType: contentType,
                    Content: stream),
                ct);

            var isPoster = frame.TimestampOffsetMs == poster.SelectedTimestampOffsetMs;
            var record = new VideoDerivedFrameAsset
            {
                SourceVideoAssetId = sourceAsset.AssetId,
                DerivedAssetId = derivedAssetId,
                ProcessingJobId = job.JobId,
                UploaderUserId = sourceAsset.UploaderUserId,
                SubmissionId = sourceAsset.Provenance.SubmissionId,
                VenueId = sourceAsset.Provenance.VenueId,
                ModerationItemId = sourceAsset.Provenance.ModerationItemId,
                TimestampOffsetMs = frame.TimestampOffsetMs,
                FrameType = isPoster ? VideoFrameType.PosterSelected : frame.FrameType,
                WidthPx = frame.WidthPx,
                HeightPx = frame.HeightPx,
                Storage = storage,
                ExtractionStage = frame.ExtractionStage,
                ExtractionVersion = frame.ExtractionVersion,
                IsPosterSelected = isPoster,
                PosterSelectionReason = isPoster ? poster.Reason : null,
                CreatedAt = now,
            };

            results.Add(record);
        }

        await _repository.SaveBatchAsync(results, ct);
        return results;
    }

    private static MemoryStream BuildPlaceholderImageStream(string sourceAssetId, long timestampOffsetMs)
    {
        var payload = Encoding.UTF8.GetBytes($"derived-frame:{sourceAssetId}:{timestampOffsetMs}");
        return new MemoryStream(payload, writable: false);
    }
}
