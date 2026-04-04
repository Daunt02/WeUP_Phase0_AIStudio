using System.Collections.Concurrent;
using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

/// <summary>
/// P27: In-memory provenance repository.
/// Phase 0 dev store — replaced by EF Core + Postgres in Phase 0.5.
/// </summary>
public sealed class InMemoryProvenanceRepository : IProvenanceRepository
{
    private readonly ConcurrentDictionary<string, ProvenanceRecord> _store = new();

    public Task<ProvenanceRecord> SaveAsync(ProvenanceRecord record, CancellationToken ct = default)
    {
        _store[record.ProvenanceId] = record;
        return Task.FromResult(record);
    }

    public Task<ProvenanceRecord?> GetAsync(string provenanceId, CancellationToken ct = default)
    {
        _store.TryGetValue(provenanceId, out var record);
        return Task.FromResult(record);
    }

    public Task<ProvenanceRecord?> GetByAssetIdAsync(string assetId, CancellationToken ct = default)
    {
        var record = _store.Values.FirstOrDefault(r => r.AssetId == assetId);
        return Task.FromResult(record);
    }

    public Task<ProvenanceRecord[]> ListBySubmitterHashAsync(string submitterHash, CancellationToken ct = default)
    {
        var records = _store.Values
            .Where(r => r.SubmitterHash == submitterHash)
            .ToArray();
        return Task.FromResult(records);
    }
}
