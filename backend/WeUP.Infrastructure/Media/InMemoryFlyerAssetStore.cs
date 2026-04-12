using System.Collections.Concurrent;
using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

/// <summary>
/// P26: In-memory flyer asset store with lifecycle enforcement.
/// Dev/test only — replace with EF Core implementation in Phase 0.5.
/// </summary>
public sealed class InMemoryFlyerAssetStore : IFlyerAssetStore
{
    private readonly ConcurrentDictionary<string, FlyerAssetRecord> _assets = new();

    public Task<FlyerAssetRecord> SaveAsync(FlyerAssetRecord asset, CancellationToken ct = default)
    {
        _assets[asset.AssetId] = asset;
        return Task.FromResult(asset);
    }

    public Task<FlyerAssetRecord?> GetAsync(string assetId, CancellationToken ct = default)
    {
        _assets.TryGetValue(assetId, out var asset);
        return Task.FromResult<FlyerAssetRecord?>(asset);
    }

    public Task<FlyerAssetRecord[]> ListBySubmitterAsync(string submitterId, CancellationToken ct = default)
    {
        var results = _assets.Values
            .Where(a => a.SubmitterId == submitterId && a.Status != FlyerAssetStatus.Archived)
            .OrderByDescending(a => a.UploadedAt)
            .ToArray();
        return Task.FromResult(results);
    }

    public Task<FlyerAssetRecord?> FindByHashAsync(string contentHash, string submitterId, CancellationToken ct = default)
    {
        var match = _assets.Values
            .FirstOrDefault(a =>
                a.ContentHash == contentHash &&
                a.SubmitterId == submitterId &&
                a.Status != FlyerAssetStatus.Archived &&
                a.Status != FlyerAssetStatus.ValidationFailed);
        return Task.FromResult<FlyerAssetRecord?>(match);
    }

    public Task<FlyerAssetRecord?> FindRecentByHashAsync(string contentHash, string submitterId, DateTimeOffset sinceUtc, CancellationToken ct = default)
    {
        var match = _assets.Values
            .Where(a =>
                a.ContentHash == contentHash &&
                a.SubmitterId == submitterId &&
                a.UploadedAt >= sinceUtc &&
                a.Status != FlyerAssetStatus.Archived)
            .OrderByDescending(a => a.UploadedAt)
            .FirstOrDefault();

        return Task.FromResult<FlyerAssetRecord?>(match);
    }

    public Task<FlyerAssetRecord> UpdateStatusAsync(string assetId, FlyerAssetStatus next, CancellationToken ct = default)
    {
        if (!_assets.TryGetValue(assetId, out var current))
            throw new KeyNotFoundException($"Flyer asset '{assetId}' not found");

        var updated = current.WithStatus(next);
        _assets[assetId] = updated;
        return Task.FromResult(updated);
    }

    public async Task<FlyerAssetRecord> UpdateValidationFailureAsync(string assetId, string failureReason, CancellationToken ct = default)
    {
        var updated = await UpdateStatusAsync(assetId, FlyerAssetStatus.ValidationFailed, ct);
        updated = updated with { ValidationFailureReason = failureReason };
        _assets[assetId] = updated;
        return updated;
    }

    public Task DeleteAsync(string assetId, CancellationToken ct = default)
    {
        _assets.TryRemove(assetId, out _);
        return Task.CompletedTask;
    }
}
