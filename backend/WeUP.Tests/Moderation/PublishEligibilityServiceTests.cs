using WeUP.Application.Moderation;
using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Moderation;
using WeUP.Domain.Events;
using WeUP.Domain.Moderation;
using WeUP.Infrastructure.Moderation;
using WeUP.Infrastructure.Spatial;
using Xunit;

namespace WeUP.Tests.Moderation;

public sealed class PublishEligibilityServiceTests
{
    private readonly IPublishEligibilityService _service = new PublishEligibilityService(new ConfidenceScoringService(), new GeoValidationService());

    [Fact]
    public void HighConfidenceCleanCandidate_WithPendingReview_IsManualReviewable()
    {
        var context = BuildContext(reviewState: PublishReviewState.NeedsReview);

        var result = _service.Evaluate(context);

        Assert.False(result.Eligible);
        Assert.Equal(PublishEligibilityBand.ManualReviewRequired, result.EligibilityBand);
        Assert.Equal(PublishRecommendedAction.RouteToManualReview, result.RecommendedNextAction);
        Assert.Contains(result.Blockers, b => b.Code == PublishBlockerCode.ManualReviewPending);
    }

    [Fact]
    public void LowConfidenceFlyerExtraction_IsBlocked()
    {
        var context = BuildContext(extraction: 0.21);

        var result = _service.Evaluate(context);

        Assert.False(result.Eligible);
        Assert.Equal(PublishEligibilityBand.Blocked, result.EligibilityBand);
        Assert.Contains(result.Blockers, b => b.Code == PublishBlockerCode.LowExtractionConfidence && b.IsHardBlock);
    }

    [Fact]
    public void MissingRequiredFields_IsBlockedWithFieldCompletenessFailures()
    {
        var context = BuildContext(
            title: null,
            venueName: null,
            address: null,
            startUtc: null,
            timezone: null,
            geoValidated: false,
            sourceIntegrityValid: false,
            evidenceChainComplete: false);

        var result = _service.Evaluate(context);

        Assert.False(result.Eligible);
        Assert.Equal(PublishEligibilityBand.Blocked, result.EligibilityBand);
        Assert.False(result.FieldCompletenessSummary.IsComplete);
        Assert.Contains("title", result.FieldCompletenessSummary.MissingRequiredFields);
        Assert.Contains(result.Blockers, b => b.Code == PublishBlockerCode.MissingTimezone);
        Assert.Contains(result.Blockers, b => b.Code == PublishBlockerCode.MissingProvenance);
    }

    [Fact]
    public void RejectedReviewState_IsBlocked()
    {
        var context = BuildContext(reviewState: PublishReviewState.Rejected);

        var result = _service.Evaluate(context);

        Assert.False(result.Eligible);
        Assert.Contains(result.Blockers, b => b.Code == PublishBlockerCode.RejectedReviewState && b.IsHardBlock);
    }

    [Fact]
    public void UnresolvedDedupeConflict_IsBlocked()
    {
        var context = BuildContext(hasUnresolvedDedupeConflict: true, dedupeMatchScore: 0.81, dupeRisk: 0.19);

        var result = _service.Evaluate(context);

        Assert.False(result.Eligible);
        Assert.Contains(result.Blockers, b => b.Code == PublishBlockerCode.DuplicateConflictUnresolved && b.IsHardBlock);
    }

    [Fact]
    public void ApprovedButIncompleteEvent_IsBlocked()
    {
        var context = BuildContext(
            reviewState: PublishReviewState.Approved,
            timezone: null,
            evidenceChainComplete: false);

        var result = _service.Evaluate(context);

        Assert.False(result.Eligible);
        Assert.Equal(PublishEligibilityBand.Blocked, result.EligibilityBand);
        Assert.Contains(result.Blockers, b => b.Code == PublishBlockerCode.MissingTimezone);
        Assert.Contains(result.Blockers, b => b.Code == PublishBlockerCode.IncompleteEvidenceChain);
    }

    [Fact]
    public void ValidAutoPublishScenario_IsEligible()
    {
        var context = BuildContext(reviewState: PublishReviewState.Approved);

        var result = _service.Evaluate(context);

        Assert.True(result.Eligible);
        Assert.Equal(PublishEligibilityBand.AutoPublishable, result.EligibilityBand);
        Assert.Equal(PublishRecommendedAction.AutoPublish, result.RecommendedNextAction);
        Assert.DoesNotContain(result.Blockers, b => b.IsHardBlock);
    }

    [Fact]
    public async Task ReviewDecisionService_DoesNotAllowApproveWhenPublishGateBlocks()
    {
        var queueRepo = new InMemoryModerationQueue();
        var audit = new InMemoryAuditTrail();
        var moderationAuditRepository = new InMemoryModerationAuditRepository();
        var moderationAuditService = new ModerationAuditService(moderationAuditRepository);
        var reviewAction = new ReviewActionService(queueRepo, audit, moderationAuditService);
        var queueService = new ModerationQueueService(queueRepo, moderationAuditService);
        var lifecycleRepo = new FakeLifecycleRepository(new Dictionary<string, string>
        {
            ["evt-1"] = "NEEDS_REVIEW",
        });

        var service = new ReviewDecisionService(
            queueRepo,
            queueService,
            reviewAction,
            lifecycleRepo,
            _service);

        await queueRepo.AddItemAsync(new WeUP.Domain.Moderation.ModerationQueueItem
        {
            ItemId = "mod-1",
            Kind = ModerationItemKind.CandidateReview,
            Status = ModerationItemStatus.Open,
            LinkedEventId = "evt-1",
            Candidate = new CandidateSnapshotDto(
                Title: "Bad Candidate",
                VenueName: "Venue X",
                Address: "123 Main",
                StartUtc: "2026-08-01T18:00:00Z",
                EndUtc: null,
                Timezone: null,
                Category: "music",
                Description: null,
                Tags: null,
                SourceKind: "flyer_upload",
                SourceRef: "asset:1"),
            Provenance = new ProvenanceSummaryDto(
                SourceKind: "flyer_upload",
                SourceRef: "asset:1",
                IngestionJobId: "job-1",
                EvidenceRefs: [],
                SubmittedAt: DateTimeOffset.UtcNow.ToString("O")),
            Confidence = new ConfidenceSummaryDto(
                Extraction: 0.91,
                Geocode: 0.90,
                Temporal: 0.88,
                VenueMatch: 0.76,
                DupeRisk: 0.95,
                Aggregate: 0.89,
                Bucket: ConfidenceBucket.High,
                ReviewBlockers: []),
            DedupeMatch = null,
            IngestionJob = null,
            ReviewReasons = [],
        });

        var decision = await service.SubmitDecisionAsync(
            "mod-1",
            new ReviewDecisionRequest("moderator-a", ReviewDecisionKind.Approve, "ship it"));

        Assert.False(decision.Accepted);
        Assert.Contains("Publish eligibility gate blocked approval", decision.ErrorMessage);
        Assert.Equal("NEEDS_REVIEW", await lifecycleRepo.GetLifecycleStatusAsync("evt-1"));
    }

    private PublishEligibilityContext BuildContext(
        string? title = "Night Session",
        string? venueName = "Echo Hall",
        string? address = "123 Main St",
        string? startUtc = "2026-08-01T18:00:00Z",
        string? timezone = "America/Chicago",
        double extraction = 0.95,
        double geocode = 0.94,
        double temporal = 0.90,
        double venueMatch = 0.82,
        double dupeRisk = 0.97,
        double sourceTrust = 0.90,
        PublishReviewState reviewState = PublishReviewState.Approved,
        string lifecycleStatus = "APPROVED",
        bool hasUnresolvedDedupeConflict = false,
        bool sourceIntegrityValid = true,
        bool evidenceChainComplete = true,
        bool venueResolved = true,
        bool geoValidated = true,
        double dedupeMatchScore = 0.03)
    {
        var candidate = new NormalizedEventCandidate(
            Title: title,
            VenueName: venueName,
            Address: address,
            StartUtc: startUtc,
            EndUtc: null,
            Timezone: timezone,
            Category: "music",
            Description: null,
            Tags: ["house"],
            SourceKind: "flyer_upload",
            SourceRef: "asset:xyz",
            ExtractionConfidence: extraction,
            GeocodeConfidence: geocode,
            TemporalConfidence: temporal,
            EvidenceRefs: ["evidence-1"],
            ExternalSourceId: null,
            Attributes: null);

        return new PublishEligibilityContext(
            Candidate: candidate,
            ReviewState: reviewState,
            LifecycleStatus: lifecycleStatus,
            HasUnresolvedDedupeConflict: hasUnresolvedDedupeConflict,
            SourceIntegrityValid: sourceIntegrityValid,
            EvidenceChainComplete: evidenceChainComplete,
            VenueResolved: venueResolved,
            GeoValidated: geoValidated,
            DedupeMatchScore: dedupeMatchScore,
            ReviewConfidence: reviewState == PublishReviewState.Approved ? 1.0 : 0.0,
            ConfidenceOverride: new PublishConfidenceVector(
                Extraction: extraction,
                Geocode: geocode,
                Temporal: temporal,
                VenueMatch: venueMatch,
                DupeRisk: dupeRisk,
                SourceTrust: sourceTrust,
                ReviewConfidence: reviewState == PublishReviewState.Approved ? 1.0 : 0.0,
                WeightsOverride: _service.GetPolicy().DimensionWeights));
    }

    private sealed class FakeLifecycleRepository(IReadOnlyDictionary<string, string> initial) : IEventLifecycleRepository
    {
        private readonly Dictionary<string, string> _statuses = new(initial, StringComparer.OrdinalIgnoreCase);

        public Task<string?> GetLifecycleStatusAsync(string eventId, CancellationToken ct = default)
        {
            _statuses.TryGetValue(eventId, out var status);
            return Task.FromResult(status);
        }

        public Task<bool> TransitionLifecycleStatusAsync(string eventId, string newStatus, CancellationToken ct = default)
        {
            if (!_statuses.ContainsKey(eventId))
            {
                return Task.FromResult(false);
            }

            _statuses[eventId] = newStatus;
            return Task.FromResult(true);
        }
    }
}
