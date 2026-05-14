namespace WeUP.Infrastructure.Persistence.Entities;

public sealed class AuditTrailEntity
{
    public Guid Id { get; set; }
    public string EntryId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ItemKind { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string PreviousStatus { get; set; } = string.Empty;
    public string NextStatus { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string EvidenceSnapshotRefsJson { get; set; } = "[]";
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }
}
