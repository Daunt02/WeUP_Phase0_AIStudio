namespace WeUP.Infrastructure.Persistence.Entities;

/// <summary>
/// Durable append-only moderation history row used for compliance-grade audit queries.
/// Rows are never updated after insertion.
/// </summary>
public sealed class ModerationHistoryEntryEntity
{
    public Guid Id { get; set; }
    public string EntryId { get; set; } = string.Empty;
    public string QueueItemId { get; set; } = string.Empty;
    public string? EventId { get; set; }
    public string CandidateId { get; set; } = string.Empty;
    public string ReviewerId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public DateTimeOffset ActionTimestampUtc { get; set; }
    public string? ReasonComment { get; set; }
}