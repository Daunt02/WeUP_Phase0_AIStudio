using WeUP.Application.Moderation;
using WeUP.Application.Resolution;
using WeUP.Contracts.Auth;
using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Moderation;
using WeUP.Contracts.Resolution;
using WeUP.Domain.Dedupe;
using WeUP.Domain.Moderation;
using WeUP.Domain.Resolution;
using WeUP.Infrastructure.Auth;
using WeUP.Infrastructure.Moderation;
using Xunit;

namespace WeUP.Tests.Moderation;

public sealed class ModerationActionServiceTests
{
    [Fact]
    public async Task Reject_RequiresReason()
    {
        var (service, queue, _) = await BuildServiceAsync();
        var itemId = await queue.AddItemAsync(BuildItem());

        var result = await service.RejectAsync(itemId, "mod-1", rejectReason: " ");

        Assert.False(result.Success);
        Assert.Contains("requires a non-empty reason", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Edit_AppendsProvenanceAndPreservesOriginalValue()
    {
        var (service, queue, _) = await BuildServiceAsync();
        var itemId = await queue.AddItemAsync(BuildItem(title: "Original Title"));

        var result = await service.EditAsync(
            itemId,
            actorId: "mod-1",
            patch: new ModerationEditPatch(Title: "Updated Title"),
            note: "normalize title");

        Assert.True(result.Success);

        var updated = await queue.GetItemAsync(itemId);
        Assert.NotNull(updated);
        Assert.Equal("Updated Title", updated!.Candidate!.Title);
        Assert.Contains(updated.ReviewReasons, r => r.Contains("edit-provenance:Title", StringComparison.Ordinal));
        Assert.Contains(updated.ReviewReasons, r => r.Contains("old=Original Title", StringComparison.Ordinal));
        Assert.Contains(updated.ReviewReasons, r => r.Contains("new=Updated Title", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Merge_RejectsPlanMarkedRejectMerge()
    {
        var (service, queue, _) = await BuildServiceAsync();
        var itemId = await queue.AddItemAsync(BuildItem());

        var mergePlan = BuildMergePlan(rejectMerge: true);
        var result = await service.MergeAsync(itemId, "mod-1", mergePlan);

        Assert.False(result.Success);
        Assert.Contains("RejectMerge", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Approve_BlockedWhenPublishGateFails()
    {
        var (service, queue, _) = await BuildServiceAsync();

        var itemId = await queue.AddItemAsync(BuildItem(
            title: "Bad Candidate",
            timezone: null,
            evidenceRefs: []));

        var result = await service.ApproveAsync(itemId, "mod-1");

        Assert.False(result.Success);
        Assert.Contains("publish eligibility gate", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Approve_WritesAuditAndResolvesItem()
    {
        var (service, queue, audit) = await BuildServiceAsync();
        var itemId = await queue.AddItemAsync(BuildItem());

        var result = await service.ApproveAsync(itemId, "mod-1", note: "looks good");

        Assert.True(result.Success);
        Assert.Equal(ModerationItemStatus.Resolved, result.NewStatus);

        var updated = await queue.GetItemAsync(itemId);
        Assert.NotNull(updated);
        Assert.Equal(ModerationItemStatus.Resolved, updated!.Status);
        Assert.Contains(updated.History, h => h.Action == "approve");

        var (entries, _) = await audit.GetEntriesAsync(25, null);
        Assert.Contains(entries, e => e.ItemId == itemId && e.Action == "approve");
    }

    private static async Task<(IModerationActionService Service, InMemoryModerationQueue Queue, InMemoryAuditTrail Audit)> BuildServiceAsync()
    {
        var queue = new InMemoryModerationQueue();
        var audit = new InMemoryAuditTrail();
        var moderationAudit = new ModerationAuditService(new InMemoryModerationAuditRepository());
        var roleRepo = new InMemoryUserRoleRepository();
        await roleRepo.SetRolesAsync("mod-1", [UserRoles.Moderator]);
        var roleResolver = new UserRoleResolver(roleRepo);

        var publishEligibility = new PublishEligibilityService(new ConfidenceScoringService());
        var provenanceService = new ProvenanceService();

        var handlers = new IModerationActionHandler[]
        {
            new ApproveHandler(publishEligibility),
            new RejectHandler(),
            new EditHandler(publishEligibility, provenanceService),
            new MergeHandler(publishEligibility, provenanceService),
        };

        var service = new ModerationActionService(queue, audit, moderationAudit, roleResolver, handlers);
        return (service, queue, audit);
    }

    private static WeUP.Domain.Moderation.ModerationQueueItem BuildItem(
        string title = "Good Candidate",
        string? timezone = "America/Chicago",
        string[]? evidenceRefs = null)
    {
        var sourceRef = "cand-1";
        return new WeUP.Domain.Moderation.ModerationQueueItem
        {
            ItemId = Guid.NewGuid().ToString("N"),
            Kind = ModerationItemKind.CandidateReview,
            Status = ModerationItemStatus.Open,
            Candidate = new CandidateSnapshotDto(
                Title: title,
                VenueName: "Echo Hall",
                Address: "123 Main",
                StartUtc: "2026-08-01T18:00:00Z",
                EndUtc: null,
                Timezone: timezone,
                Category: "music",
                Description: "desc",
                Tags: ["house"],
                SourceKind: "flyer_upload",
                SourceRef: sourceRef),
            Provenance = new ProvenanceSummaryDto(
                SourceKind: "flyer_upload",
                SourceRef: sourceRef,
                IngestionJobId: "job-1",
                EvidenceRefs: evidenceRefs ?? ["evidence-1"],
                SubmittedAt: DateTimeOffset.UtcNow.ToString("O")),
            Confidence = new ConfidenceSummaryDto(
                Extraction: 0.92,
                Geocode: 0.90,
                Temporal: 0.88,
                VenueMatch: 0.82,
                DupeRisk: 0.90,
                Aggregate: 0.89,
                Bucket: ConfidenceBucket.High,
                ReviewBlockers: []),
            DedupeMatch = new DedupeSummaryDto(
                ExistingEventId: "evt-1",
                ExistingEventTitle: "Existing Event",
                MatchScore: 0.3,
                Severity: DuplicateSeverity.None,
                MatchReasons: []),
            ReviewReasons = [],
            LinkedEventId = "evt-1",
        };
    }

    private static MergePlanDetail BuildMergePlan(bool rejectMerge)
    {
        var decisions = new Dictionary<string, MergeFieldDecision>(StringComparer.Ordinal)
        {
            ["Title"] = new MergeFieldDecision("Title", MergeFieldAction.ReplaceWithCandidate, "Merged Title", "test", null),
            ["VenueName"] = new MergeFieldDecision("VenueName", MergeFieldAction.KeepExisting, "Echo Hall", "test", null),
        };

        var candidate = new NormalizedEventCandidate(
            Title: "Merged Title",
            VenueName: "Echo Hall",
            Address: "123 Main",
            StartUtc: "2026-08-01T18:00:00Z",
            EndUtc: null,
            Timezone: "America/Chicago",
            Category: "music",
            Description: null,
            Tags: ["house"],
            SourceKind: "flyer_upload",
            SourceRef: "cand-1",
            ExtractionConfidence: 0.9,
            GeocodeConfidence: 0.9,
            TemporalConfidence: 0.9,
            EvidenceRefs: ["evidence-1"],
            ExternalSourceId: null,
            Attributes: null);

        var canonical = new EventAggregateSnapshot(
            CanonicalEventId: "evt-1",
            Title: "Existing Event",
            VenueName: "Echo Hall",
            Address: "123 Main",
            Latitude: 0,
            Longitude: 0,
            StartUtc: "2026-08-01T18:00:00Z",
            EndUtc: null,
            Timezone: "America/Chicago",
            Category: "music",
            Confidence: 0.88,
            SourceRefs: ["evt-src-1"],
            EvidenceRefs: ["evidence-1"],
            IsApproved: false,
            ExternalSourceId: null,
            Attributes: null);

        var assessment = new DuplicateAssessment(
            CandidateSourceRef: "cand-1",
            CanonicalEventId: "evt-1",
            Level: DuplicateAssessmentLevel.ProbableDuplicate,
            Breakdown: new MatchScoreBreakdown(
                CompositeScore: 0.75,
                TitleScore: new DimensionScore(MatchDimension.TitleSimilarity, 0.8, 0.3, 0.24, "title"),
                VenueScore: new DimensionScore(MatchDimension.VenueSimilarity, 0.9, 0.25, 0.225, "venue"),
                AddressScore: new DimensionScore(MatchDimension.AddressSimilarity, 0.9, 0.15, 0.135, "address"),
                TemporalScore: new DimensionScore(MatchDimension.TemporalOverlap, 0.9, 0.15, 0.135, "temporal"),
                GeoScore: new DimensionScore(MatchDimension.GeoProximity, 0.8, 0.1, 0.08, "geo"),
                SourceHashScore: new DimensionScore(MatchDimension.SourceHashEvidence, 0.7, 0.05, 0.035, "hash"),
                ActiveSignals: ["title"],
                ScoringNotes: ["test"]),
            SafetyVerdict: new MergeSafetyVerdict(AutoMergeAllowed: !rejectMerge, ActiveBlockers: [], BlockerExplanations: []),
            AutoMergeAllowed: !rejectMerge,
            Rationale: ["test"],
            AssessedAtUtc: DateTimeOffset.UtcNow);

        return new MergePlanDetail(
            CanonicalEventId: "evt-1",
            CandidateSourceRef: "cand-1",
            FieldDecisions: decisions,
            Conflicts: [],
            AutoMergeAllowed: !rejectMerge,
            RequiresManualReview: false,
            RejectMerge: rejectMerge,
            MergeRationale: ["test merge"],
            ManualReviewReasons: [],
            Audit: new MergePlanAudit(
                Assessment: assessment,
                IncomingCandidate: candidate,
                CanonicalSnapshot: canonical,
                AppliedPrecedenceRules: ["test-rule"],
                CreatedAtUtc: DateTimeOffset.UtcNow,
                MergePlannerVersion: "1.0"));
    }
}
