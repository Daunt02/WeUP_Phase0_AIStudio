namespace WeUP.Infrastructure.Persistence.Entities;

public sealed class IngestionCandidateEntity
{
    public Guid Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string CandidateJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
