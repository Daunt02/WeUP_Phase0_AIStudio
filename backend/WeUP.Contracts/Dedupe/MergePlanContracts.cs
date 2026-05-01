using System;

namespace WeUP.Contracts.Dedupe;

/// <summary>
/// Action to be taken for a single field during merge.
/// </summary>
public enum MergeFieldAction
{
    KeepExisting,
    ReplaceWithCandidate,
    MergeValues,
    RequireManualReview,
}

/// <summary>
/// Description of a concrete conflict on a field.
/// </summary>
public sealed record ConflictDescriptor(
    string FieldName,
    string ExistingValue,
    string CandidateValue,
    string Reason);

/// <summary>
/// Full merge plan for a candidate vs. a canonical event.
/// </summary>
public sealed record MergePlan(
    Guid CandidateId,
    Guid CanonicalEventId,
    MergeFieldAction TitleAction,
    MergeFieldAction LocationAction,
    MergeFieldAction TimeAction,
    MergeFieldAction TagsAction,
    ConflictDescriptor[] Conflicts,
    bool AutoApply);