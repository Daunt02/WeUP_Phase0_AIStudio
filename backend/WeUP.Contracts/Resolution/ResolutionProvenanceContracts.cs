namespace WeUP.Contracts.Resolution;

/// <summary>
/// Immutable field lineage entry for one canonical field mutation.
///
/// Lineage semantics:
/// - OriginalSourceRef and OriginalConfidence describe where the canonical value lineage began.
/// - PriorValue is the value immediately before the current merge was applied.
/// - CurrentValue is the value immediately after the current merge was applied.
/// - CurrentSourceRef and CurrentConfidence describe the source that produced the current value.
///
/// Audit expectation:
/// Consumers should read lineage entries chronologically to explain how a field evolved.
/// No lineage entry is ever edited in place; later merges append new entries instead.
/// </summary>
public sealed record FieldLineage(
    string FieldName,
    string? PriorValue,
    string? CurrentValue,
    string OriginalSourceRef,
    double OriginalConfidence,
    string CurrentSourceRef,
    double CurrentConfidence,
    string[] EvidenceRefs,
    string[] EvidenceBundleRefs,
    DateTimeOffset ChangedAtUtc,
    string Reason);

/// <summary>
/// Immutable merge-history entry for one append-only provenance event.
///
/// Audit expectation:
/// This record is moderation-safe and human-readable. It keeps enough context to answer:
/// who merged, when they merged, why the system allowed it, and which request/candidate/evidence
/// artifacts participated in the merge.
/// </summary>
public sealed record MergeHistoryEntry(
    string MergeId,
    string[] SourceRequestIds,
    string[] CandidateIds,
    string[] EvidenceBundleRefs,
    string[] ChangedFields,
    DateTimeOffset MergedAtUtc,
    string MergeActor,
    string MergeReason);

/// <summary>
/// Structured evolution entry projected for moderation/admin review.
///
/// This projection is derived from immutable merge history and field lineage.
/// It is intentionally normalized for operational dashboards and audits.
/// </summary>
public sealed record EventEvolutionHistoryEntry(
    string CanonicalEventId,
    string MergeId,
    string EvolutionType,
    string[] ChangedFields,
    DateTimeOffset OccurredAtUtc,
    string Actor,
    string Reason,
    bool RequiresManualReview);

/// <summary>
/// Immutable provenance envelope for one canonical merge append.
///
/// Append-only guarantee:
/// - SequenceNumber increases monotonically per canonical event.
/// - PreviousEntryId chains entries together without rewriting earlier entries.
/// - FieldLineage and MergeHistory are captured at append time and are never mutated later.
///
/// Query expectation:
/// Moderation and audit workflows can reconstruct canonical evolution by reading all entries for a
/// CanonicalEventId ordered by SequenceNumber or RecordedAtUtc.
/// </summary>
public sealed record ProvenanceEntry(
    string EntryId,
    string CanonicalEventId,
    int SequenceNumber,
    string? PreviousEntryId,
    string ResolutionId,
    string[] SourceRequestIds,
    string[] CandidateIds,
    string[] EvidenceBundleRefs,
    string[] SourceRefs,
    string[] EvidenceRefs,
    FieldLineage[] FieldLineage,
    MergeHistoryEntry MergeHistory,
    DateTimeOffset RecordedAtUtc);