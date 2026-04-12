using System.Collections.Concurrent;
using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

public sealed class InMemoryVideoDerivedAssetRepository : IVideoDerivedAssetRepository
{
    private readonly ConcurrentDictionary<string, VideoDerivedFrameAsset> _records = new();

    public Task SaveBatchAsync(IReadOnlyList<VideoDerivedFrameAsset> records, CancellationToken ct = default)
    {
        foreach (var record in records)
        {
            _records[record.DerivedAssetId] = record;
        }

        return Task.CompletedTask;
    }

    public Task<VideoDerivedFrameAsset?> GetPosterAsync(string sourceVideoAssetId, CancellationToken ct = default)
    {
        var poster = _records.Values
            .Where(x => x.SourceVideoAssetId == sourceVideoAssetId && x.IsPosterSelected)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        return Task.FromResult<VideoDerivedFrameAsset?>(poster);
    }

    public Task<IReadOnlyList<VideoDerivedFrameAsset>> ListFramesAsync(string sourceVideoAssetId, CancellationToken ct = default)
    {
        IReadOnlyList<VideoDerivedFrameAsset> items = _records.Values
            .Where(x => x.SourceVideoAssetId == sourceVideoAssetId)
            .OrderBy(x => x.TimestampOffsetMs)
            .ThenBy(x => x.DerivedAssetId)
            .ToArray();

        return Task.FromResult(items);
    }
}
