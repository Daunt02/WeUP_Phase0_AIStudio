using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

/// <summary>
/// P27: Unified flyer intake service.
/// Orchestrates: validate → store asset → record provenance → create evidence stub.
/// Single entry point for all flyer intake in Phase 0.15+.
/// </summary>
public sealed class FlyerIntakeService : IFlyerIntakeService
{
    private readonly IFlyerAssetStore _assetStore;
    private readonly IFlyerAssetValidator _validator;
    private readonly IProvenanceRepository _provenanceRepo;
    private readonly IFlyerEvidenceRepository _evidenceRepo;

    public FlyerIntakeService(
        IFlyerAssetStore assetStore,
        IFlyerAssetValidator validator,
        IProvenanceRepository provenanceRepo,
        IFlyerEvidenceRepository evidenceRepo)
    {
        _assetStore = assetStore;
        _validator = validator;
        _provenanceRepo = provenanceRepo;
        _evidenceRepo = evidenceRepo;
    }

    public async Task<FlyerIntakeResult> IntakeAsync(
        Stream fileStream,
        string filename,
        string contentType,
        string submitterId,
        SourceTier sourceTier = SourceTier.T3_Unverified,
        string? submissionId = null,
        string? sourceUrl = null,
        string? submitterNote = null,
        CancellationToken ct = default)
    {
        // ── Step 1: Validate ─────────────────────────────────────────────────
        var validation = await _validator.ValidateAsync(fileStream, contentType, filename, submitterId, ct);

        if (validation.IsDuplicate)
            throw new FlyerUploadException(
                $"Duplicate flyer — existing asset: {validation.DuplicateAssetId}",
                FlyerUploadErrorCode.Duplicate,
                validation.DuplicateAssetId);

        if (!validation.IsValid)
            throw new FlyerUploadException(
                validation.FailureReason ?? "Validation failed",
                FlyerUploadErrorCode.ValidationFailed);

        // ── Step 2: Store asset ──────────────────────────────────────────────
        // Reset stream after validation (validator reads but doesn't seek back)
        if (fileStream.CanSeek) fileStream.Position = 0;
        var fileBytes = await ReadStreamAsync(fileStream, ct);

        var storageKey = $"upload_{Guid.NewGuid():N}_{filename}";
        var assetRecord = new FlyerAssetRecord
        {
            AssetId          = Guid.NewGuid().ToString("N"),
            OriginalFilename = filename,
            FileSizeBytes    = fileBytes.Length,
            ContentType      = contentType,
            SubmitterId      = submitterId,
            UploadedAt       = DateTimeOffset.UtcNow,
            Status           = FlyerAssetStatus.ProcessingPending,
            ContentHash      = validation.ContentHash!,
            StorageKey       = storageKey,
        };

        await _assetStore.SaveAsync(assetRecord, ct);

        // ── Step 3: Record provenance ────────────────────────────────────────
        var provenance = new ProvenanceRecord
        {
            ProvenanceId       = Guid.NewGuid().ToString("N"),
            AssetId            = assetRecord.AssetId,
            SourceTier         = sourceTier,
            SubmitterHash      = ProvenanceRecord.HashSubmitterId(submitterId),
            RecordedAt         = DateTimeOffset.UtcNow,
            BaselineAuthority  = ProvenanceRecord.BaselineAuthorityForTier(sourceTier),
            SubmissionId       = submissionId,
            SourceUrl          = sourceUrl,
            SubmitterNote      = submitterNote,
        };

        await _provenanceRepo.SaveAsync(provenance, ct);

        // ── Step 4: Create evidence stub ─────────────────────────────────────
        var evidence = new FlyerEvidenceRecord
        {
            EvidenceId    = Guid.NewGuid().ToString("N"),
            AssetId       = assetRecord.AssetId,
            ProvenanceId  = provenance.ProvenanceId,
            SubmissionId  = submissionId,
            FlyerType     = FlyerType.Unknown,   // classified by OCR pipeline later
            Status        = EvidenceStatus.Pending,
            CreatedAt     = DateTimeOffset.UtcNow,
        };

        await _evidenceRepo.SaveAsync(evidence, ct);

        return new FlyerIntakeResult(
            AssetId:           assetRecord.AssetId,
            ProvenanceId:      provenance.ProvenanceId,
            EvidenceId:        evidence.EvidenceId,
            BaselineAuthority: provenance.BaselineAuthority,
            AssetStatus:       assetRecord.Status);
    }

    public async Task LinkToEventAsync(string evidenceId, string eventId, CancellationToken ct = default)
    {
        var evidence = await _evidenceRepo.GetAsync(evidenceId, ct)
            ?? throw new InvalidOperationException($"Evidence record {evidenceId} not found");

        var updated = evidence.WithEventLink(eventId);
        await _evidenceRepo.UpdateAsync(updated, ct);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static async Task<byte[]> ReadStreamAsync(Stream stream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}
