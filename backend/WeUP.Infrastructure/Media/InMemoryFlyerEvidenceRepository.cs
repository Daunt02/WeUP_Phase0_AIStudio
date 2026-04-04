using System.Collections.Concurrent;
using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

/// <summary>
/// P27: In-memory flyer evidence repository.
/// Phase 0 dev store — replaced by EF Core + Postgres in Phase 0.5.
/// </summary>
public sealed class InMemoryFlyerEvidenceRepository : IFlyerEvidenceRepository
{
    private readonly ConcurrentDictionary<string, FlyerEvidenceRecord> _store = new();

    public Task<FlyerEvidenceRecord> SaveAsync(FlyerEvidenceRecord record, CancellationToken ct = default)
    {
        _store[record.EvidenceId] = record;
        return Task.FromResult(record);
    }

    public Task<FlyerEvidenceRecord?> GetAsync(string evidenceId, CancellationToken ct = default)
    {
        _store.TryGetValue(evidenceId, out var record);
        return Task.FromResult(record);
    }

    public Task<FlyerEvidenceRecord?> GetByAssetIdAsync(string assetId, CancellationToken ct = default)
    {
        var record = _store.Values.FirstOrDefault(r => r.AssetId == assetId);
        return Task.FromResult(record);
    }

    public Task<FlyerEvidenceRecord[]> ListByEventIdAsync(string eventId, CancellationToken ct = default)
    {
        var records = _store.Values
            .Where(r => r.EventId == eventId)
            .ToArray();
        return Task.FromResult(records);
    }

    public Task<FlyerEvidenceRecord[]> ListBySubmissionIdAsync(string submissionId, CancellationToken ct = default)
    {
        var records = _store.Values
            .Where(r => r.SubmissionId == submissionId)
            .ToArray();
        return Task.FromResult(records);
    }

    public Task<FlyerEvidenceRecord[]> ListPendingAsync(CancellationToken ct = default)
    {
        var records = _store.Values
            .Where(r => r.Status == EvidenceStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .ToArray();
        return Task.FromResult(records);
    }

    public Task<FlyerEvidenceRecord> UpdateAsync(FlyerEvidenceRecord record, CancellationToken ct = default)
    {
        _store[record.EvidenceId] = record;
        return Task.FromResult(record);
    }
}
