using WeUP.Application.Moderation;
using WeUP.Contracts.Moderation;
using WeUP.Domain.Dedupe;
using WeUP.Domain.Flyer;
using WeUP.Domain.Moderation;
using WeUP.Infrastructure.Moderation;
using Xunit;

namespace WeUP.Tests.Moderation;

public sealed class ModerationQueueWorkflowTests
{
    [Fact]
    public async Task EnqueueCandidateAsync_PersistsPendingItemWithRiskMetadata()
    {
        var service = BuildService();
        var candidate = BuildCandidate("cand-high", 0.92);
        var risk = BuildRisk(78, EventRiskLevel.High);
        var dedupe = BuildAssessment(DuplicateAssessmentLevel.ProbableDuplicate, 0.81);

        var item = await service.EnqueueCandidateAsync(candidate, risk, dedupe);

        Assert.Equal(ModerationStatus.Pending, item.Status);
        Assert.Equal("cand-high", item.CandidateId);
        Assert.Equal(EventRiskLevel.High, item.RiskScore.Level);
        Assert.True(item.Priority >= 300);
        Assert.Single(item.Transitions);
        Assert.Equal("system", item.Transitions[0].ActorId);
    }

    [Fact]
    public async Task FetchNextItemAsync_PrioritizesHighRiskBeforeLowConfidence()
    {
        var service = BuildService();

        await service.EnqueueCandidateAsync(
            BuildCandidate("cand-low-risk", 0.15),
            BuildRisk(18, EventRiskLevel.Low),
            BuildAssessment(DuplicateAssessmentLevel.Distinct, 0.12));

        await service.EnqueueCandidateAsync(
            BuildCandidate("cand-high-risk", 0.88),
            BuildRisk(85, EventRiskLevel.High),
            BuildAssessment(DuplicateAssessmentLevel.ProbableDuplicate, 0.82));

        var next = await service.FetchNextItemAsync();

        Assert.NotNull(next);
        Assert.Equal("cand-high-risk", next.CandidateId);
        Assert.Equal(EventRiskLevel.High, next.RiskScore.Level);
    }

    [Fact]
    public async Task FetchNextItemAsync_UsesLowConfidenceAsSecondaryPriority()
    {
        var service = BuildService();

        await service.EnqueueCandidateAsync(
            BuildCandidate("cand-high-confidence", 0.90),
            BuildRisk(70, EventRiskLevel.High),
            BuildAssessment(DuplicateAssessmentLevel.ProbableDuplicate, 0.70));

        await service.EnqueueCandidateAsync(
            BuildCandidate("cand-low-confidence", 0.31),
            BuildRisk(72, EventRiskLevel.High),
            BuildAssessment(DuplicateAssessmentLevel.ProbableDuplicate, 0.72));

        var next = await service.FetchNextItemAsync();

        Assert.NotNull(next);
        Assert.Equal("cand-low-confidence", next.CandidateId);
    }

    [Fact]
    public async Task AssignReviewerAsync_TracksReviewerAndTransitionsToInReview()
    {
        var service = BuildService();
        var pending = await service.EnqueueCandidateAsync(
            BuildCandidate("cand-review", 0.60),
            BuildRisk(50, EventRiskLevel.Medium),
            BuildAssessment(DuplicateAssessmentLevel.PossibleDuplicate, 0.56));

        var assigned = await service.AssignReviewerAsync(pending.ItemId, "mod-1", "lead-mod");

        Assert.NotNull(assigned);
        Assert.Equal("mod-1", assigned.AssignedReviewerId);
        Assert.Equal(ModerationStatus.InReview, assigned.Status);
        Assert.Contains(assigned.Transitions, t => t.ActorId == "lead-mod" && t.To == ModerationStatus.InReview);
    }

    [Fact]
    public async Task UpdateStatusAsync_BlocksPendingToApprovedShortcut()
    {
        var service = BuildService();
        var pending = await service.EnqueueCandidateAsync(
            BuildCandidate("cand-blocked", 0.70),
            BuildRisk(40, EventRiskLevel.Medium),
            BuildAssessment(DuplicateAssessmentLevel.Distinct, 0.10));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateStatusAsync(pending.ItemId, ModerationStatus.Approved, "mod-2"));
    }

    [Fact]
    public async Task UpdateStatusAsync_PersistsStatusTransitionAndTimestamp()
    {
        var service = BuildService();
        var pending = await service.EnqueueCandidateAsync(
            BuildCandidate("cand-ok", 0.67),
            BuildRisk(62, EventRiskLevel.High),
            BuildAssessment(DuplicateAssessmentLevel.ProbableDuplicate, 0.75));

        var inReview = await service.UpdateStatusAsync(pending.ItemId, ModerationStatus.InReview, "mod-a");
        var approved = await service.UpdateStatusAsync(pending.ItemId, ModerationStatus.Approved, "mod-a", "checked");

        Assert.NotNull(inReview);
        Assert.NotNull(approved);
        Assert.Equal(ModerationStatus.Approved, approved.Status);
        Assert.True(approved.UpdatedAtUtc >= pending.UpdatedAtUtc);
        Assert.True(approved.Transitions.Count >= 3);
        Assert.Equal(ModerationStatus.InReview, approved.Transitions[^2].To);
        Assert.Equal(ModerationStatus.Approved, approved.Transitions[^1].To);
    }

    private static ModerationQueueService BuildService()
    {
        var repo = new InMemoryModerationQueue();
        var auditRepository = new InMemoryModerationAuditRepository();
        var auditService = new ModerationAuditService(auditRepository);
        return new ModerationQueueService(repo, auditService);
    }

    private static EventCandidateV2 BuildCandidate(string candidateId, double confidence)
    {
        var collectedAt = DateTimeOffset.UtcNow;
        var sourceRefs = new[] { $"src-{candidateId}" };

        return new EventCandidateV2(
            Title: new FieldConfidence<string>("Night Session", confidence, sourceRefs, "title", "ocr"),
            StartUtc: new FieldConfidence<DateTimeOffset>(collectedAt.AddDays(1), confidence, sourceRefs, "start", "ocr"),
            EndUtc: null,
            Venue: new FieldConfidence<string>("Echo Hall", confidence, sourceRefs, "venue", "ocr"),
            Address: new FieldConfidence<string>("123 Main St", confidence, sourceRefs, "address", "ocr"),
            Category: new FieldConfidence<string>("music", confidence, sourceRefs, "category", "ocr"),
            Description: null,
            Tags: new FieldConfidence<IReadOnlyList<string>>(new[] { "house" }, confidence, sourceRefs, "tags", "ocr"),
            OverallConfidence: confidence,
            OverallConfidenceRationale: "test",
            EvidenceBundle: new EvidenceBundle(
                BundleId: candidateId,
                CollectedAtUtc: collectedAt,
                RawOcrText: "raw",
                OcrBlocks: null,
                SourceHash: null,
                SourceKind: "flyer_ocr",
                ScoringComponents: null,
                ProcessingContext: null),
            CreatedAtUtc: collectedAt,
            ScoringVersion: "1.0");
    }

    private static EventRiskScore BuildRisk(int overallScore, EventRiskLevel level)
    {
        return new EventRiskScore(
            OverallScore: overallScore,
            Level: level,
            ContributingFactors:
            [
                new EventRiskFactor("test-factor", Math.Min(overallScore, 25), "for test"),
            ],
            Explanation: "test risk");
    }

    private static DuplicateAssessment BuildAssessment(DuplicateAssessmentLevel level, double composite)
    {
        var title = new DimensionScore(MatchDimension.TitleSimilarity, composite, 0.30, composite * 0.30, "title");
        var venue = new DimensionScore(MatchDimension.VenueSimilarity, composite, 0.25, composite * 0.25, "venue");
        var address = new DimensionScore(MatchDimension.AddressSimilarity, composite, 0.15, composite * 0.15, "address");
        var temporal = new DimensionScore(MatchDimension.TemporalOverlap, composite, 0.15, composite * 0.15, "temporal");
        var geo = new DimensionScore(MatchDimension.GeoProximity, composite, 0.10, composite * 0.10, "geo");
        var hash = new DimensionScore(MatchDimension.SourceHashEvidence, composite, 0.05, composite * 0.05, "hash");

        return new DuplicateAssessment(
            CandidateSourceRef: "source-ref",
            CanonicalEventId: "evt-1",
            Level: level,
            Breakdown: new MatchScoreBreakdown(
                CompositeScore: composite,
                TitleScore: title,
                VenueScore: venue,
                AddressScore: address,
                TemporalScore: temporal,
                GeoScore: geo,
                SourceHashScore: hash,
                ActiveSignals: ["title", "venue"],
                ScoringNotes: ["note"]),
            SafetyVerdict: new MergeSafetyVerdict(
                AutoMergeAllowed: level == DuplicateAssessmentLevel.Distinct,
                ActiveBlockers: [],
                BlockerExplanations: []),
            AutoMergeAllowed: level == DuplicateAssessmentLevel.Distinct,
            Rationale: ["manual-check"],
            AssessedAtUtc: DateTimeOffset.UtcNow);
    }
}
