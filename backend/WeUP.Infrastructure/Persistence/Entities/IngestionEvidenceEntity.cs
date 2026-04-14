namespace WeUP.Infrastructure.Persistence.Entities;

public sealed class IngestionEvidenceEntity
{
    public Guid Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string EvidenceId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public string? Payload { get; set; }
    public double Confidence { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}
