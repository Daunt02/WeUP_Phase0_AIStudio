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
    private readonly IFlyerAssetLifecyclePolicy _lifecyclePolicy;
    private readonly IProvenanceRepository _provenanceRepo;
    private readonly IFlyerEvidenceRepository _evidenceRepo;

    public FlyerIntakeService(
        IFlyerAssetStore assetStore,
        IFlyerAssetValidator validator,
        IFlyerAssetLifecyclePolicy lifecyclePolicy,
        IProvenanceRepository provenanceRepo,
        IFlyerEvidenceRepository evidenceRepo)
    {
        _assetStore = assetStore;
        _validator = validator;
        _lifecyclePolicy = lifecyclePolicy;
        _provenanceRepo = provenanceRepo;
        _evidenceRepo = evidenceRepo;
    }

    public async Task<FlyerIntakeResult> IntakeAsync(
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
        CancellationToken ct = default)
    {
        // ── Step 1: Validate ─────────────────────────────────────────────────
        var validation = await _validator.ValidateAsync(
            fileStream,
            contentType,
            filename,
            submitterId,
            sourceUrl,
            allowSameSourceReupload: true,
            ct);

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
            Status           = FlyerAssetStatus.Initialized,
            ContentHash      = validation.ContentHash!,
            StorageKey       = storageKey,
            SourceReference  = sourceUrl,
            CanonicalContentType = validation.CanonicalContentType,
            WidthPx          = validation.WidthPx,
            HeightPx         = validation.HeightPx,
            IsAnimated       = validation.IsAnimated,
        };

        assetRecord = await _assetStore.SaveAsync(assetRecord, ct);
        assetRecord = await _assetStore.UpdateStatusAsync(
            assetRecord.AssetId,
            _lifecyclePolicy.Transition(assetRecord.Status, FlyerAssetStatus.Uploaded),
            ct);
        assetRecord = await _assetStore.UpdateStatusAsync(
            assetRecord.AssetId,
            _lifecyclePolicy.Transition(assetRecord.Status, FlyerAssetStatus.ProcessingPending),
            ct);

        // ── Step 3: Record provenance ────────────────────────────────────────
        var provenance = new ProvenanceRecord
        {
            ProvenanceId       = Guid.NewGuid().ToString("N"),
            AssetId            = assetRecord.AssetId,
            SourceTier         = sourceTier,
            UploaderUserId     = submitterId,
            UploadOrigin       = uploadOrigin,
            SourceType         = sourceType,
            SubmitterHash      = ProvenanceRecord.HashSubmitterId(submitterId),
            RecordedAt         = DateTimeOffset.UtcNow,
            BaselineAuthority  = ProvenanceRecord.BaselineAuthorityForTier(sourceTier),
            SubmissionId       = submissionId,
            IngestionJobId     = ingestionJobId,
            SourceUrl          = sourceUrl,
            PartnerProvider    = partnerProvider,
            SubmitterNote      = submitterNote,
        };

        await _provenanceRepo.SaveAsync(provenance, ct);

        // ── Step 4: Create evidence stub ─────────────────────────────────────
        var evidence = new FlyerEvidenceRecord
        {
            EvidenceId    = Guid.NewGuid().ToString("N"),
            AssetId       = assetRecord.AssetId,
            OriginalAssetId = assetRecord.AssetId,
            ProvenanceId  = provenance.ProvenanceId,
            SubmissionId  = submissionId,
            IngestionJobId = ingestionJobId,
            ModerationItemId = moderationItemId,
            FlyerType     = FlyerType.Unknown,   // classified by OCR pipeline later
            Status        = EvidenceStatus.Pending,
            OcrReady      = true,
            ProcessingHistory =
            [
                "intake:received",
                "asset:stored",
                "provenance:recorded",
                "evidence:created",
            ],
            LinkedWorkflowIds =
            [
                ..(string.IsNullOrWhiteSpace(submissionId) ? [] : new[] { submissionId }),
                ..(string.IsNullOrWhiteSpace(ingestionJobId) ? [] : new[] { ingestionJobId }),
                ..(string.IsNullOrWhiteSpace(moderationItemId) ? [] : new[] { moderationItemId }),
            ],
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
