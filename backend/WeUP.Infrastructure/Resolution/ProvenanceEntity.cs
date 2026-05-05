using System;

namespace WeUP.Infrastructure.Resolution;

/// <summary>
/// Immutable table that stores every field‑level change performed during merges.
/// </summary>
public sealed class ProvenanceEntity
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid CandidateId { get; set; }
    public string FieldName { get; set; } = default!;
    public string OldValue { get; set; } = default!;
    public string NewValue { get; set; } = default!;
    public DateTimeOffset ChangedAtUtc { get; set; }
    public string ChangedBy { get; set; } = default!;
    public string Reason { get; set; } = default!;
}
