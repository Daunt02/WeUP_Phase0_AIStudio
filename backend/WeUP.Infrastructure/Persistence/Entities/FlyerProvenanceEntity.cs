namespace WeUP.Infrastructure.Persistence.Entities;

public sealed class FlyerProvenanceEntity
{
    public Guid Id { get; set; }
    public string ProvenanceId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string SourceTier { get; set; } = string.Empty;
    public string? UploaderUserId { get; set; }
    public string UploadOrigin { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SubmitterHash { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
    public double BaselineAuthority { get; set; }
    public string? SourceUrl { get; set; }
    public string? SubmissionId { get; set; }
    public string? IngestionJobId { get; set; }
    public string? PartnerProvider { get; set; }
    public string? SubmitterNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
