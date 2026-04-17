namespace WeUP.Infrastructure.Persistence.Entities;

/// <summary>
/// Durable append-only provenance row for canonical event merges.
///
/// Storage model:
/// - One row per ProvenanceEntry append.
/// - EntryJson stores the full immutable payload for replay and audit.
/// - Indexed columns support canonical-event and temporal lookups without unpacking JSON first.
/// </summary>
public sealed class EventMergeProvenanceEntity
{
    public Guid Id { get; set; }
    public string EntryId { get; set; } = string.Empty;
    public string CanonicalEventId { get; set; } = string.Empty;
    public string ResolutionId { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public string MergeActor { get; set; } = string.Empty;
    public string MergeReason { get; set; } = string.Empty;
    public string EntryJson { get; set; } = string.Empty;
}