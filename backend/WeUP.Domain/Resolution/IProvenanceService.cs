using System;
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

    /// <summary>
    /// Retrieve the complete provenance chain for a given <paramref name="eventId"/>.
    /// Results are ordered by <c>ChangedAtUtc</c> ascending.
    /// </summary>
    Task<IReadOnlyList<ProvenanceEntry>> GetLineageAsync(Guid eventId);
}
