namespace WeUP.Infrastructure.Persistence.Entities;

public sealed class IngestionJobEntity
{
    public Guid Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string SourceKind { get; set; } = string.Empty;
    public string SourceRef { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public string? CandidateEventId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
