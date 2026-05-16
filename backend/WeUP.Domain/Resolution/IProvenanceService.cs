using System.Collections.Generic;
using System.Threading.Tasks;
using WeUP.Contracts.Resolution;

namespace WeUP.Domain.Resolution;

/// <summary>
/// Service responsible for persisting provenance entries in an append‑only fashion
/// and for retrieving the full lineage of an event.
/// </summary>
public interface IProvenanceService
{
    /// <summary>
    /// Record a new provenance entry. Must be immutable – the implementation must never update existing rows.
    /// </summary>
    Task RecordAsync(ProvenanceEntry entry);

    Task<IReadOnlyList<ProvenanceEntry>> GetLineageAsync(Guid eventId);

    ProvenanceEntry CreateAppendOnlyEntry(ProvenanceBuildCommand command);

    ProvenanceEntry[] Append(ProvenanceEntry[] existingEntries, ProvenanceEntry nextEntry);
    FieldLineage[] GetFieldLineage(IEnumerable<ProvenanceEntry> entries, string? fieldName = null);
    MergeHistoryEntry[] GetMergeHistory(IEnumerable<ProvenanceEntry> entries);
    EventEvolutionHistoryEntry[] GetEvolutionHistory(IEnumerable<ProvenanceEntry> entries, string canonicalEventId);
}
