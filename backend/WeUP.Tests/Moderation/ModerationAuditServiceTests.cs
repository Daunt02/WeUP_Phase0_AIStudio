using WeUP.Application.Moderation;
using WeUP.Contracts.Moderation;
using WeUP.Domain.Moderation;
using WeUP.Infrastructure.Moderation;
using Xunit;

namespace WeUP.Tests.Moderation;

public sealed class ModerationAuditServiceTests
{
    [Fact]
    public async Task AppendAsync_PreservesAllEntriesAndReturnsCandidateHistoryInOrder()
    {
        var repository = new InMemoryModerationAuditRepository();
        var service = new ModerationAuditService(repository);
        var item = BuildItem(candidateId: "cand-1", eventId: "evt-1");

        await service.AppendAsync(item, "mod-1", "mod-1", "start-review", ModerationItemStatus.Open, ModerationItemStatus.InReview, "opened", new DateTimeOffset(2026, 4, 17, 12, 0, 0, TimeSpan.Zero));
        await service.AppendAsync(item, "mod-1", "mod-1", "approve", ModerationItemStatus.InReview, ModerationItemStatus.Resolved, "approved", new DateTimeOffset(2026, 4, 17, 12, 5, 0, TimeSpan.Zero));

        var history = await service.GetFullHistoryByCandidateAsync("cand-1");

        Assert.Equal(2, history.Count);
        Assert.Equal("start-review", history[0].Action);
        Assert.Equal("approve", history[1].Action);
        Assert.Equal("approved", history[1].ReasonComment);
    }

    [Fact]
    public async Task GetHistoryByReviewerAsync_FiltersByReviewerIdentity()
    {
        var repository = new InMemoryModerationAuditRepository();
        var service = new ModerationAuditService(repository);

        await service.AppendAsync(BuildItem(candidateId: "cand-1", eventId: "evt-1"), "mod-1", "lead-1", "assign-reviewer", ModerationItemStatus.Open, ModerationItemStatus.InReview, "assigned", new DateTimeOffset(2026, 4, 17, 9, 0, 0, TimeSpan.Zero));
        await service.AppendAsync(BuildItem(candidateId: "cand-2", eventId: "evt-2"), "mod-2", "mod-2", "reject", ModerationItemStatus.InReview, ModerationItemStatus.Resolved, "rejected", new DateTimeOffset(2026, 4, 17, 10, 0, 0, TimeSpan.Zero));

        var history = await service.GetHistoryByReviewerAsync("mod-1");

        var entry = Assert.Single(history);
        Assert.Equal("assign-reviewer", entry.Action);
        Assert.Equal("lead-1", entry.ActorId);
    }

    [Fact]
    public async Task GetHistoryByEventAsync_ReturnsOnlyEntriesForRequestedEvent()
    {
        var repository = new InMemoryModerationAuditRepository();
        var service = new ModerationAuditService(repository);

        await service.AppendAsync(BuildItem(candidateId: "cand-1", eventId: "evt-keep"), "mod-1", "mod-1", "approve", ModerationItemStatus.InReview, ModerationItemStatus.Resolved, "ok", new DateTimeOffset(2026, 4, 17, 8, 0, 0, TimeSpan.Zero));
        await service.AppendAsync(BuildItem(candidateId: "cand-2", eventId: "evt-skip"), "mod-2", "mod-2", "reject", ModerationItemStatus.InReview, ModerationItemStatus.Resolved, "bad", new DateTimeOffset(2026, 4, 17, 8, 30, 0, TimeSpan.Zero));

        var history = await service.GetHistoryByEventAsync("evt-keep");

        var entry = Assert.Single(history);
        Assert.Equal("cand-1", entry.CandidateId);
        Assert.Equal("evt-keep", entry.EventId);
    }

    [Fact]
    public async Task ModerationQueueService_AppendsAuditEntryForEveryMutation()
    {
        var queueRepository = new InMemoryModerationQueue();
        var auditRepository = new InMemoryModerationAuditRepository();
        var auditService = new ModerationAuditService(auditRepository);
        var queueService = new ModerationQueueService(queueRepository, auditService);

        var candidate = BuildCandidate();
        var risk = new EventRiskScore(50, EventRiskLevel.Medium, [], "test");
        var assessment = new WeUP.Domain.Dedupe.DuplicateAssessment(
            CandidateSourceRef: "src-1",
            CanonicalEventId: "evt-1",
            Level: WeUP.Domain.Dedupe.DuplicateAssessmentLevel.PossibleDuplicate,
            Breakdown: new WeUP.Domain.Dedupe.MatchScoreBreakdown(
                0.5,
                new WeUP.Domain.Dedupe.DimensionScore(WeUP.Domain.Dedupe.MatchDimension.TitleSimilarity, 0.5, 0.3, 0.15, "title"),
                new WeUP.Domain.Dedupe.DimensionScore(WeUP.Domain.Dedupe.MatchDimension.VenueSimilarity, 0.5, 0.25, 0.125, "venue"),
                new WeUP.Domain.Dedupe.DimensionScore(WeUP.Domain.Dedupe.MatchDimension.AddressSimilarity, 0.5, 0.15, 0.075, "address"),
                new WeUP.Domain.Dedupe.DimensionScore(WeUP.Domain.Dedupe.MatchDimension.TemporalOverlap, 0.5, 0.15, 0.075, "temporal"),
                new WeUP.Domain.Dedupe.DimensionScore(WeUP.Domain.Dedupe.MatchDimension.GeoProximity, 0.5, 0.10, 0.05, "geo"),
                new WeUP.Domain.Dedupe.DimensionScore(WeUP.Domain.Dedupe.MatchDimension.SourceHashEvidence, 0.5, 0.05, 0.025, "hash"),
                ["title"],
                ["note"]),
            SafetyVerdict: new WeUP.Domain.Dedupe.MergeSafetyVerdict(false, [], []),
            AutoMergeAllowed: false,
            Rationale: ["manual-review"],
            AssessedAtUtc: new DateTimeOffset(2026, 4, 17, 11, 0, 0, TimeSpan.Zero));

        var item = await queueService.EnqueueCandidateAsync(candidate, risk, assessment);
        await queueService.AssignReviewerAsync(item.ItemId, "mod-9", "lead-9");
        await queueService.UpdateStatusAsync(item.ItemId, ModerationStatus.Approved, "mod-9", "verified");

        var candidateHistory = await auditService.GetFullHistoryByCandidateAsync("cand-audit");
        var reviewerHistory = await auditService.GetHistoryByReviewerAsync("mod-9");

        Assert.Equal(3, candidateHistory.Count);
        string[] expectedActions = ["enqueue", "assign-reviewer", "approve"];
        Assert.Equal(expectedActions, candidateHistory.Select(h => h.Action).ToArray());
        Assert.Equal(2, reviewerHistory.Count);
        Assert.All(candidateHistory, entry => Assert.Equal("cand-audit", entry.CandidateId));
    }

    private static WeUP.Domain.Moderation.ModerationQueueItem BuildItem(string candidateId, string? eventId)
    {
        return new WeUP.Domain.Moderation.ModerationQueueItem
        {
            ItemId = Guid.NewGuid().ToString("N"),
            Kind = ModerationItemKind.CandidateReview,
            Status = ModerationItemStatus.Open,
            Candidate = new CandidateSnapshotDto(
                "Title",
                "Venue",
                "123 Main",
                "2026-08-01T18:00:00Z",
                null,
                null,
                "music",
                null,
                null,
                "flyer_ocr",
                candidateId),
            Provenance = new ProvenanceSummaryDto("flyer_ocr", candidateId, null, [candidateId], DateTimeOffset.UtcNow.ToString("O")),
            Confidence = new ConfidenceSummaryDto(0.9, 0.9, 0.9, 0.9, 0.1, 0.9, ConfidenceBucket.High, []),
            ReviewReasons = [$"candidate-id:{candidateId}"],
            LinkedEventId = eventId,
        };
    }

    private static WeUP.Domain.Flyer.EventCandidateV2 BuildCandidate()
    {
        var now = new DateTimeOffset(2026, 4, 17, 11, 0, 0, TimeSpan.Zero);
        return new WeUP.Domain.Flyer.EventCandidateV2(
            Title: new WeUP.Domain.Flyer.FieldConfidence<string>("Audit Candidate", 0.9, ["cand-audit"], "title", "ocr"),
            StartUtc: new WeUP.Domain.Flyer.FieldConfidence<DateTimeOffset>(now.AddDays(1), 0.9, ["cand-audit"], "startUtc", "ocr"),
            EndUtc: null,
            Venue: new WeUP.Domain.Flyer.FieldConfidence<string>("Echo Hall", 0.9, ["cand-audit"], "venue", "ocr"),
            Address: new WeUP.Domain.Flyer.FieldConfidence<string>("123 Main", 0.9, ["cand-audit"], "address", "ocr"),
            Category: new WeUP.Domain.Flyer.FieldConfidence<string>("music", 0.9, ["cand-audit"], "category", "ocr"),
            Description: null,
            Tags: new WeUP.Domain.Flyer.FieldConfidence<IReadOnlyList<string>>(["house"], 0.9, ["cand-audit"], "tags", "ocr"),
            OverallConfidence: 0.9,
            OverallConfidenceRationale: "test",
            EvidenceBundle: new WeUP.Domain.Flyer.EvidenceBundle(
                BundleId: "cand-audit",
                CollectedAtUtc: now,
                RawOcrText: null,
                OcrBlocks: null,
                SourceHash: null,
                SourceKind: "flyer_ocr",
                ScoringComponents: null,
                ProcessingContext: null),
            CreatedAtUtc: now,
            ScoringVersion: "1.0");
    }
}