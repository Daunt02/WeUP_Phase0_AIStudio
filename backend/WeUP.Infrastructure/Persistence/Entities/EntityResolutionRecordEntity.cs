namespace WeUP.Infrastructure.Persistence.Entities;

public sealed class EntityResolutionRecordEntity
{
    public Guid Id { get; set; }
    public string ResolutionId { get; set; } = string.Empty;
    public string CandidateSourceRef { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ResultJson { get; set; } = string.Empty;
    public string? CanonicalEventId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
