namespace WeUP.Infrastructure.Persistence.Entities;

public sealed class IngestionJobEntity
{
    public Guid Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string SourceKind { get; set; } = string.Empty;
    public string SourceRef { get; set; } = string.Empty;
    public string SubmittedBy { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RawPayloadJson { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = "{}";
    public string Status { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public string IssuesJson { get; set; } = "[]";
    public string? AdapterKey { get; set; }
    public string? AdapterVersion { get; set; }
    public bool IsRetrySafe { get; set; }
    public int AttemptCount { get; set; }
    public string? CandidateEventId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
