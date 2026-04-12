namespace WeUP.Domain.Media;

/// <summary>
/// P27: Result of a successful flyer intake — all three records created atomically.
/// </summary>
public sealed record FlyerIntakeResult(
    string AssetId,
    string ProvenanceId,
    string EvidenceId,
    double BaselineAuthority,
    FlyerAssetStatus AssetStatus);

/// <summary>
/// P27: Unified flyer intake service.
/// Validates → stores asset → creates provenance → creates evidence record.
/// This is the single entry point for all flyer intake in Phase 0.15+.
/// </summary>
public interface IFlyerIntakeService
{
    /// <summary>
    /// Intake a flyer: validate, store, record provenance, create evidence stub.
    /// </summary>
    Task<FlyerIntakeResult> IntakeAsync(
        Stream fileStream,
        string filename,
        string contentType,
        string submitterId,
        SourceTier sourceTier = SourceTier.T3_Unverified,
        FlyerUploadOrigin uploadOrigin = FlyerUploadOrigin.ManualUploader,
        FlyerSourceType sourceType = FlyerSourceType.DirectUpload,
        string? submissionId = null,
        string? ingestionJobId = null,
        string? moderationItemId = null,
        string? sourceUrl = null,
        string? partnerProvider = null,
        string? submitterNote = null,
        CancellationToken ct = default);

    /// <summary>
    /// Link an existing evidence record to a published event.
    /// Called by the moderation pipeline after approval.
    /// </summary>
    Task LinkToEventAsync(string evidenceId, string eventId, CancellationToken ct = default);
}
