using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Resolution;
using WeUP.Domain.Dedupe;
using WeUP.Domain.Flyer;

namespace WeUP.Domain.Resolution;

/// <summary>
/// Canonical input for building one append-only provenance entry.
///
/// The service takes explicit before/after canonical state instead of mutating provenance itself.
/// This keeps provenance generation deterministic and suitable for EF Core persistence later.
/// </summary>
public sealed record ProvenanceBuildCommand(
    string ResolutionId,
    EventAggregateSnapshot CanonicalBeforeMerge,
    EventAggregateSnapshot CanonicalAfterMerge,
    IReadOnlyDictionary<string, string?> CanonicalBeforeFields,
    IReadOnlyDictionary<string, string?> CanonicalAfterFields,
    NormalizedEventCandidate Candidate,
    MergePlan Plan,
    ProvenanceEntry[] ExistingEntries,
    string[] SourceRequestIds,
    string[] CandidateIds,
    string[] EvidenceBundleRefs,
    string MergeActor,
    string MergeReason,
    DateTimeOffset MergedAtUtc,
    EventCandidate? OriginalEventCandidate = null,
    EvidenceBundle? EvidenceBundle = null);

/// <summary>
/// Append-only provenance service for canonical event merges.
///
/// Contract:
/// - CreateAppendOnlyEntry never mutates an existing entry.
/// - Append returns a new ordered array with the new entry added at the end.
/// - Query methods are pure projections over immutable provenance entries.
/// </summary>
public interface IProvenanceService
{
    ProvenanceEntry CreateAppendOnlyEntry(ProvenanceBuildCommand command);

    ProvenanceEntry[] Append(ProvenanceEntry[] existingEntries, ProvenanceEntry nextEntry);

    FieldLineage[] GetFieldLineage(ProvenanceEntry[] entries, string? fieldName = null);

    MergeHistoryEntry[] GetMergeHistory(ProvenanceEntry[] entries);

    EventEvolutionHistoryEntry[] GetEvolutionHistory(ProvenanceEntry[] entries, string canonicalEventId);
}