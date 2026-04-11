namespace WeUP.Infrastructure.Persistence.Entities;

public sealed class IngestionAuditEntity
{
    public Guid Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
