namespace WeUP.Infrastructure.Persistence.Entities;

public sealed class IngestionEvidenceEntity
{
    public Guid Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
