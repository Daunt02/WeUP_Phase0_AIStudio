using System;

namespace WeUP.Infrastructure.Persistence.Entities;

/// <summary>
/// Immutable record of a generated merge plan.
/// </summary>
public sealed class MergePlanEntity
{
    public Guid Id { get; set; }
    public Guid CandidateId { get; set; }
    public Guid CanonicalEventId { get; set; }
    public string TitleAction { get; set; } = default!;
    public string LocationAction { get; set; } = default!;
    public string TimeAction { get; set; } = default!;
    public string TagsAction { get; set; } = default!;
    public bool AutoApply { get; set; }
    public string ConflictsJson { get; set; } = default!;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
