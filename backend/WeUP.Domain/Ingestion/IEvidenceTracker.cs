using System;
using System.Threading.Tasks;
using WeUP.Contracts.Ingestion;

namespace WeUP.Domain.Ingestion;

/// <summary>
/// Service responsible for persisting evidence records (append-only) and
/// calculating the deterministic <see cref="ConfidenceVector"/> for a candidate.
/// </summary>
public interface IEvidenceTracker
{
    /// <summary>
    /// Append a new evidence record to the data store.
    /// Implementations must guarantee idempotency for the same
    /// (CandidateId, Source, RawMatchedText) triple.
    /// </summary>
    Task AttachEvidenceAsync(EvidenceRecord record);

    /// <summary>
    /// Compute the confidence vector for the specified candidate by aggregating all
    /// evidence records that belong to it.
    /// </summary>
    Task<ConfidenceVector> CalculateVectorAsync(Guid candidateId);
}
