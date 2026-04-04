using Xunit;
using WeUP.Domain.Media;
using WeUP.Infrastructure.Media;

namespace WeUP.Tests.Integration;

/// <summary>
/// P27: Flyer metadata persistence, provenance, and review-ready evidence.
/// </summary>
public class ProvenanceTests
{
    // ── ProvenanceRecord domain tests ────────────────────────────────────────

    [Fact]
    public void ProvenanceRecord_HashSubmitterId_IsDeterministic()
    {
        var hash1 = ProvenanceRecord.HashSubmitterId("user123");
        var hash2 = ProvenanceRecord.HashSubmitterId("user123");
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ProvenanceRecord_HashSubmitterId_IsDifferentForDifferentIds()
    {
        var hash1 = ProvenanceRecord.HashSubmitterId("user123");
        var hash2 = ProvenanceRecord.HashSubmitterId("user456");
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ProvenanceRecord_HashSubmitterId_IsLowercaseHex()
    {
        var hash = ProvenanceRecord.HashSubmitterId("user123");
        Assert.Equal(hash, hash.ToLowerInvariant());
        Assert.Equal(64, hash.Length); // SHA-256 = 32 bytes = 64 hex chars
    }

    [Theory]
    [InlineData(SourceTier.T1_Verified,   0.90)]
    [InlineData(SourceTier.T2_Approved,   0.55)]
    [InlineData(SourceTier.T3_Unverified, 0.35)]
    public void ProvenanceRecord_BaselineAuthority_MatchesTier(SourceTier tier, double expected)
    {
        Assert.Equal(expected, ProvenanceRecord.BaselineAuthorityForTier(tier));
    }

    // ── InMemoryProvenanceRepository tests ───────────────────────────────────

    [Fact]
    public async Task ProvenanceRepo_SaveAndRetrieve_ById()
    {
        var repo = new InMemoryProvenanceRepository();
        var record = MakeProvenance("p1", "a1");
        await repo.SaveAsync(record);

        var found = await repo.GetAsync("p1");
        Assert.NotNull(found);
        Assert.Equal("a1", found.AssetId);
    }

    [Fact]
    public async Task ProvenanceRepo_GetByAssetId_ReturnsCorrectRecord()
    {
        var repo = new InMemoryProvenanceRepository();
        await repo.SaveAsync(MakeProvenance("p1", "asset-A"));
        await repo.SaveAsync(MakeProvenance("p2", "asset-B"));

        var found = await repo.GetByAssetIdAsync("asset-A");
        Assert.NotNull(found);
        Assert.Equal("p1", found.ProvenanceId);
    }

    [Fact]
    public async Task ProvenanceRepo_ListBySubmitterHash_FiltersCorrectly()
    {
        var repo = new InMemoryProvenanceRepository();
        var hashA = ProvenanceRecord.HashSubmitterId("userA");
        var hashB = ProvenanceRecord.HashSubmitterId("userB");

        await repo.SaveAsync(MakeProvenance("p1", "a1", submitterHash: hashA));
        await repo.SaveAsync(MakeProvenance("p2", "a2", submitterHash: hashA));
        await repo.SaveAsync(MakeProvenance("p3", "a3", submitterHash: hashB));

        var results = await repo.ListBySubmitterHashAsync(hashA);
        Assert.Equal(2, results.Length);
        Assert.All(results, r => Assert.Equal(hashA, r.SubmitterHash));
    }

    // ── FlyerEvidenceRecord domain tests ─────────────────────────────────────

    [Fact]
    public void EvidenceRecord_WithEventLink_SetsLinkedStatus()
    {
        var evidence = MakeEvidence("e1", "a1", "p1");
        Assert.Equal(EvidenceStatus.Pending, evidence.Status);

        var linked = evidence.WithEventLink("event-42");
        Assert.Equal(EvidenceStatus.Linked, linked.Status);
        Assert.Equal("event-42", linked.EventId);
    }

    [Fact]
    public void EvidenceRecord_WithSubmissionLink_SetsLinkedStatus()
    {
        var evidence = MakeEvidence("e1", "a1", "p1");
        var linked = evidence.WithSubmissionLink("sub-99");
        Assert.Equal(EvidenceStatus.Linked, linked.Status);
        Assert.Equal("sub-99", linked.SubmissionId);
    }

    [Fact]
    public void EvidenceRecord_WithOcr_SetsTextAndScore()
    {
        var evidence = MakeEvidence("e1", "a1", "p1");
        var withOcr = evidence.WithOcr("FIRST FRIDAY April 4", 0.91);
        Assert.Equal("FIRST FRIDAY April 4", withOcr.OcrText);
        Assert.Equal(0.91, withOcr.ConfidenceScore);
    }

    // ── InMemoryFlyerEvidenceRepository tests ────────────────────────────────

    [Fact]
    public async Task EvidenceRepo_SaveAndRetrieve_ById()
    {
        var repo = new InMemoryFlyerEvidenceRepository();
        var record = MakeEvidence("e1", "a1", "p1");
        await repo.SaveAsync(record);

        var found = await repo.GetAsync("e1");
        Assert.NotNull(found);
        Assert.Equal("a1", found.AssetId);
    }

    [Fact]
    public async Task EvidenceRepo_ListPending_OnlyReturnsPending()
    {
        var repo = new InMemoryFlyerEvidenceRepository();
        await repo.SaveAsync(MakeEvidence("e1", "a1", "p1", EvidenceStatus.Pending));
        await repo.SaveAsync(MakeEvidence("e2", "a2", "p2", EvidenceStatus.Linked));
        await repo.SaveAsync(MakeEvidence("e3", "a3", "p3", EvidenceStatus.Pending));

        var pending = await repo.ListPendingAsync();
        Assert.Equal(2, pending.Length);
        Assert.All(pending, e => Assert.Equal(EvidenceStatus.Pending, e.Status));
    }

    [Fact]
    public async Task EvidenceRepo_ListByEventId_FiltersCorrectly()
    {
        var repo = new InMemoryFlyerEvidenceRepository();
        var e1 = MakeEvidence("e1", "a1", "p1").WithEventLink("event-1");
        var e2 = MakeEvidence("e2", "a2", "p2").WithEventLink("event-2");
        var e3 = MakeEvidence("e3", "a3", "p3").WithEventLink("event-1");
        await repo.SaveAsync(e1);
        await repo.SaveAsync(e2);
        await repo.SaveAsync(e3);

        var forEvent1 = await repo.ListByEventIdAsync("event-1");
        Assert.Equal(2, forEvent1.Length);
    }

    [Fact]
    public async Task EvidenceRepo_Update_OverwritesExistingRecord()
    {
        var repo = new InMemoryFlyerEvidenceRepository();
        var record = MakeEvidence("e1", "a1", "p1");
        await repo.SaveAsync(record);

        var updated = record.WithEventLink("event-X");
        await repo.UpdateAsync(updated);

        var found = await repo.GetAsync("e1");
        Assert.Equal(EvidenceStatus.Linked, found!.Status);
        Assert.Equal("event-X", found.EventId);
    }

    // ── FlyerIntakeService integration tests ─────────────────────────────────

    [Fact]
    public async Task IntakeService_ValidJpeg_CreatesAllThreeRecords()
    {
        var (service, assetStore, provenanceRepo, evidenceRepo) = BuildIntakeService();

        var jpeg = MakeMinimalJpeg(500);
        using var stream = new MemoryStream(jpeg);

        var result = await service.IntakeAsync(stream, "event.jpg", "image/jpeg", "user1");

        Assert.NotNull(result.AssetId);
        Assert.NotNull(result.ProvenanceId);
        Assert.NotNull(result.EvidenceId);
        Assert.Equal(0.35, result.BaselineAuthority); // T3_Unverified default

        // Asset stored
        var asset = await assetStore.GetAsync(result.AssetId);
        Assert.NotNull(asset);

        // Provenance stored
        var prov = await provenanceRepo.GetAsync(result.ProvenanceId);
        Assert.NotNull(prov);
        Assert.Equal(result.AssetId, prov.AssetId);
        Assert.Equal(SourceTier.T3_Unverified, prov.SourceTier);

        // Evidence stored
        var evidence = await evidenceRepo.GetAsync(result.EvidenceId);
        Assert.NotNull(evidence);
        Assert.Equal(EvidenceStatus.Pending, evidence.Status);
        Assert.Equal(FlyerType.Unknown, evidence.FlyerType);
    }

    [Fact]
    public async Task IntakeService_T1Source_HasHigherAuthority()
    {
        var (service, _, provenanceRepo, _) = BuildIntakeService();

        var jpeg = MakeMinimalJpeg(500);
        using var stream = new MemoryStream(jpeg);

        var result = await service.IntakeAsync(stream, "promo.jpg", "image/jpeg", "promoter1",
            sourceTier: SourceTier.T1_Verified);

        Assert.Equal(0.90, result.BaselineAuthority);

        var prov = await provenanceRepo.GetAsync(result.ProvenanceId);
        Assert.Equal(SourceTier.T1_Verified, prov!.SourceTier);
    }

    [Fact]
    public async Task IntakeService_WithSubmissionId_ProvenanceAndEvidenceBothLink()
    {
        var (service, _, provenanceRepo, evidenceRepo) = BuildIntakeService();

        var jpeg = MakeMinimalJpeg(500);
        using var stream = new MemoryStream(jpeg);

        var result = await service.IntakeAsync(stream, "sub.jpg", "image/jpeg", "user2",
            submissionId: "sub-001");

        var prov    = await provenanceRepo.GetAsync(result.ProvenanceId);
        var evidence = await evidenceRepo.GetAsync(result.EvidenceId);

        Assert.Equal("sub-001", prov!.SubmissionId);
        Assert.Equal("sub-001", evidence!.SubmissionId);
    }

    [Fact]
    public async Task IntakeService_SubmitterHash_IsHashNotRawId()
    {
        var (service, _, provenanceRepo, _) = BuildIntakeService();

        var jpeg = MakeMinimalJpeg(500);
        using var stream = new MemoryStream(jpeg);

        var result = await service.IntakeAsync(stream, "e.jpg", "image/jpeg", "user123");
        var prov = await provenanceRepo.GetAsync(result.ProvenanceId);

        Assert.NotEqual("user123", prov!.SubmitterHash); // never raw ID
        Assert.Equal(ProvenanceRecord.HashSubmitterId("user123"), prov.SubmitterHash);
    }

    [Fact]
    public async Task IntakeService_LinkToEvent_UpdatesEvidenceStatus()
    {
        var (service, _, _, evidenceRepo) = BuildIntakeService();

        var jpeg = MakeMinimalJpeg(500);
        using var stream = new MemoryStream(jpeg);

        var result = await service.IntakeAsync(stream, "link.jpg", "image/jpeg", "user1");

        await service.LinkToEventAsync(result.EvidenceId, "event-houston-1");

        var evidence = await evidenceRepo.GetAsync(result.EvidenceId);
        Assert.Equal(EvidenceStatus.Linked, evidence!.Status);
        Assert.Equal("event-houston-1", evidence.EventId);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ProvenanceRecord MakeProvenance(
        string provenanceId, string assetId, string? submitterHash = null) =>
        new()
        {
            ProvenanceId      = provenanceId,
            AssetId           = assetId,
            SourceTier        = SourceTier.T3_Unverified,
            SubmitterHash     = submitterHash ?? ProvenanceRecord.HashSubmitterId("test-user"),
            RecordedAt        = DateTimeOffset.UtcNow,
            BaselineAuthority = 0.35,
        };

    private static FlyerEvidenceRecord MakeEvidence(
        string evidenceId, string assetId, string provenanceId,
        EvidenceStatus status = EvidenceStatus.Pending) =>
        new()
        {
            EvidenceId   = evidenceId,
            AssetId      = assetId,
            ProvenanceId = provenanceId,
            FlyerType    = FlyerType.Unknown,
            Status       = status,
            CreatedAt    = DateTimeOffset.UtcNow,
        };

    private static byte[] MakeMinimalJpeg(int size)
    {
        var bytes = new byte[size];
        bytes[0] = 0xFF; bytes[1] = 0xD8; bytes[2] = 0xFF;
        return bytes;
    }

    private static (IFlyerIntakeService service,
                    IFlyerAssetStore assetStore,
                    IProvenanceRepository provenanceRepo,
                    IFlyerEvidenceRepository evidenceRepo)
    BuildIntakeService()
    {
        var assetStore    = new InMemoryFlyerAssetStore();
        var validator     = new FlyerAssetValidator(assetStore);
        var provenanceRepo = new InMemoryProvenanceRepository();
        var evidenceRepo  = new InMemoryFlyerEvidenceRepository();
        var service       = new FlyerIntakeService(assetStore, validator, provenanceRepo, evidenceRepo);
        return (service, assetStore, provenanceRepo, evidenceRepo);
    }
}
