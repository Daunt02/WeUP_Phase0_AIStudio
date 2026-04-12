using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

public sealed class FlyerEvidenceQueryService : IFlyerEvidenceQueryService
{
    private readonly IFlyerAssetStore _assetStore;
    private readonly IProvenanceRepository _provenanceRepository;
    private readonly IFlyerEvidenceRepository _evidenceRepository;

    public FlyerEvidenceQueryService(
        IFlyerAssetStore assetStore,
        IProvenanceRepository provenanceRepository,
        IFlyerEvidenceRepository evidenceRepository)
    {
        _assetStore = assetStore;
        _provenanceRepository = provenanceRepository;
        _evidenceRepository = evidenceRepository;
    }

    public async Task<FlyerAssetReviewProjection?> GetAssetDetailAsync(string assetId, CancellationToken ct = default)
    {
        var asset = await _assetStore.GetAsync(assetId, ct);
        if (asset is null)
        {
            return null;
        }

        var provenance = await _provenanceRepository.GetByAssetIdAsync(assetId, ct);
        var evidence = await _evidenceRepository.GetByAssetIdAsync(assetId, ct);
        if (provenance is null || evidence is null)
        {
            return null;
        }

        return BuildProjection(asset, provenance, evidence);
    }

    public async Task<FlyerAssetReviewProjection[]> ListUploaderAssetsAsync(string uploaderUserId, CancellationToken ct = default)
    {
        var assets = await _assetStore.ListBySubmitterAsync(uploaderUserId, ct);
        var projections = new List<FlyerAssetReviewProjection>(assets.Length);

        foreach (var asset in assets)
        {
            var provenance = await _provenanceRepository.GetByAssetIdAsync(asset.AssetId, ct);
            var evidence = await _evidenceRepository.GetByAssetIdAsync(asset.AssetId, ct);
            if (provenance is null || evidence is null)
            {
                continue;
            }

            projections.Add(BuildProjection(asset, provenance, evidence));
        }

        return projections.ToArray();
    }

    public async Task<FlyerAssetReviewProjection[]> ListReviewSummariesAsync(int limit = 100, CancellationToken ct = default)
    {
        var pendingEvidence = await _evidenceRepository.ListPendingAsync(ct);
        var projections = new List<FlyerAssetReviewProjection>(Math.Min(limit, pendingEvidence.Length));

        foreach (var evidence in pendingEvidence.Take(limit))
        {
            var asset = await _assetStore.GetAsync(evidence.AssetId, ct);
            var provenance = await _provenanceRepository.GetAsync(evidence.ProvenanceId, ct);
            if (asset is null || provenance is null)
            {
                continue;
            }

            projections.Add(BuildProjection(asset, provenance, evidence));
        }

        return projections.ToArray();
    }

    public async Task<FlyerAssetReviewProjection[]> ListBySubmissionIdAsync(string submissionId, CancellationToken ct = default)
    {
        var evidenceRecords = await _evidenceRepository.ListBySubmissionIdAsync(submissionId, ct);
        var projections = new List<FlyerAssetReviewProjection>(evidenceRecords.Length);

        foreach (var evidence in evidenceRecords)
        {
            var asset = await _assetStore.GetAsync(evidence.AssetId, ct);
            var provenance = await _provenanceRepository.GetByAssetIdAsync(evidence.AssetId, ct);
            if (asset is null || provenance is null)
            {
                continue;
            }

            projections.Add(BuildProjection(asset, provenance, evidence));
        }

        return projections.ToArray();
    }

    private static FlyerAssetReviewProjection BuildProjection(
        FlyerAssetRecord asset,
        ProvenanceRecord provenance,
        FlyerEvidenceRecord evidence)
    {
        var visual = new FlyerVisualMetadata(
            asset.AssetId,
            asset.OriginalFilename,
            asset.ContentType,
            asset.FileSizeBytes,
            asset.ContentHash,
            asset.WidthPx,
            asset.HeightPx,
            asset.UploadedAt,
            asset.StorageKey);

        var provenanceSummary = new FlyerProvenanceSummary(
            provenance.ProvenanceId,
            provenance.UploaderUserId,
            provenance.SourceTier,
            provenance.UploadOrigin,
            provenance.SourceType,
            provenance.SubmitterHash,
            provenance.BaselineAuthority,
            provenance.SubmissionId,
            provenance.IngestionJobId,
            provenance.SourceUrl,
            provenance.PartnerProvider,
            provenance.RecordedAt);

        var evidenceSummary = new FlyerEvidenceSummary(
            evidence.EvidenceId,
            evidence.OriginalAssetId,
            evidence.DerivativeAssetIds,
            evidence.OcrReady,
            evidence.ProcessingHistory,
            evidence.ValidationFailures,
            evidence.OcrText,
            evidence.ConfidenceScore,
            evidence.Status,
            evidence.SubmissionId,
            evidence.IngestionJobId,
            evidence.ModerationItemId,
            evidence.CanonicalEventId,
            evidence.LinkedWorkflowIds,
            evidence.CreatedAt);

        return new FlyerAssetReviewProjection(visual, asset.Status, provenanceSummary, evidenceSummary);
    }
}
