using WeUP.Contracts.Moderation;
using WeUP.Domain.Events;
using WeUP.Domain.Moderation;
using Xunit;

namespace WeUP.Tests.Events;

public sealed class EventAggregateVersioningTests
{
    [Fact]
    public void RepeatedUpdates_IncrementVersionAndAppendHistory()
    {
        var aggregate = BuildAggregate();

        var step1 = aggregate.ApplyUpdate(new EventAggregateUpdateRequest(
            ExpectedVersion: 1,
            ChangedAtUtc: DateTimeOffset.UtcNow,
            ChangedBy: "editor-1",
            Reason: EventVersionReason.MinorMetadataUpdate,
            ChangedFields: [nameof(EventAggregate.Tags)],
            Tags: ["music", "all-ages"]));

        var step2 = step1.ApplyUpdate(new EventAggregateUpdateRequest(
            ExpectedVersion: 2,
            ChangedAtUtc: DateTimeOffset.UtcNow.AddMinutes(1),
            ChangedBy: "editor-1",
            Reason: EventVersionReason.ContentEdit,
            ChangedFields: [nameof(EventAggregate.Title)],
            Title: "Updated Event Title",
            RequiresModerationReview: true));

        Assert.Equal(3, step2.Version);
        Assert.Equal(2, step2.EffectiveChangeHistory.Length);
        Assert.Equal(EventVersionChangeType.MinorMetadataUpdate, step2.EffectiveChangeHistory[0].ChangeType);
        Assert.Equal(EventVersionChangeType.MaterialEventChange, step2.EffectiveChangeHistory[1].ChangeType);
        Assert.Equal(EventModerationStatus.InReview, step2.ModerationStatus);
        Assert.NotEqual(aggregate.EffectiveConcurrencyToken, step2.EffectiveConcurrencyToken);
    }

    [Fact]
    public void OutOfOrderUpdate_ThrowsVersionMismatch()
    {
        var aggregate = BuildAggregate();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            aggregate.ApplyUpdate(new EventAggregateUpdateRequest(
                ExpectedVersion: 5,
                ChangedAtUtc: DateTimeOffset.UtcNow,
                ChangedBy: "editor-2",
                Reason: EventVersionReason.ContentEdit,
                ChangedFields: [nameof(EventAggregate.Title)],
                Title: "Out of order title")));

        Assert.Contains("Version mismatch", ex.Message);
    }

    [Fact]
    public void ConcurrentUpdateAttempts_SecondAttemptFailsOnStaleVersion()
    {
        var baseAggregate = BuildAggregate();

        var first = baseAggregate.ApplyUpdate(new EventAggregateUpdateRequest(
            ExpectedVersion: 1,
            ChangedAtUtc: DateTimeOffset.UtcNow,
            ChangedBy: "editor-a",
            Reason: EventVersionReason.MinorMetadataUpdate,
            ChangedFields: [nameof(EventAggregate.Description)],
            Description: "Updated once"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            first.ApplyUpdate(new EventAggregateUpdateRequest(
                ExpectedVersion: 1,
                ChangedAtUtc: DateTimeOffset.UtcNow.AddSeconds(5),
                ChangedBy: "editor-b",
                Reason: EventVersionReason.TimeEdit,
                ChangedFields: [nameof(EventAggregate.StartUtc)],
                StartUtc: first.StartUtc.AddHours(1))));

        Assert.Contains("Version mismatch", ex.Message);
    }

    [Fact]
    public void InvalidTransition_Throws()
    {
        var aggregate = BuildAggregate(status: EventLifecycleStatus.Draft);

        Assert.Throws<InvalidOperationException>(() =>
            aggregate.TransitionTo(EventLifecycleStatus.Published, DateTimeOffset.UtcNow, aggregate.Version, "moderator"));
    }

    [Fact]
    public void MergeApplied_IsClassifiedAndTrackedInLineage()
    {
        var aggregate = BuildAggregate();

        var merged = aggregate.ApplyUpdate(new EventAggregateUpdateRequest(
            ExpectedVersion: 1,
            ChangedAtUtc: DateTimeOffset.UtcNow,
            ChangedBy: "resolver",
            Reason: EventVersionReason.MergeApplied,
            ChangedFields: [nameof(EventAggregate.MergeLineage), nameof(EventAggregate.Provenance)],
            RequiresModerationReview: true,
            MergeLineage: aggregate.MergeLineage with
            {
                MergedCanonicalEventIds = ["incoming-evt-2"],
                AppliedMergePlanIds = ["evt-1:incoming-evt-2"],
                LastMergedAtUtc = DateTimeOffset.UtcNow,
                LastMergedBy = "resolver",
            },
            Provenance: aggregate.Provenance with
            {
                SourceRefs = ["evt-1", "incoming-evt-2"],
            }));

        Assert.Equal(EventVersionChangeType.MergeLineageUpdate, merged.LatestChange!.ChangeType);
        Assert.Contains("incoming-evt-2", merged.MergeLineage.MergedCanonicalEventIds);
        Assert.True(merged.LatestChange.RequiresModerationReview);
    }

    [Fact]
    public void RescheduleAndCancel_PreserveHistoricalLineage()
    {
        var aggregate = BuildAggregate(status: EventLifecycleStatus.Published, publishStatus: EventPublishStatus.Published);

        var rescheduled = aggregate.ApplyUpdate(new EventAggregateUpdateRequest(
            ExpectedVersion: 1,
            ChangedAtUtc: DateTimeOffset.UtcNow,
            ChangedBy: "ops",
            Reason: EventVersionReason.Rescheduled,
            ChangedFields: [nameof(EventAggregate.StartUtc), nameof(EventAggregate.EndUtc)],
            StartUtc: aggregate.StartUtc.AddDays(7),
            EndUtc: aggregate.EndUtc?.AddDays(7),
            RequiresModerationReview: true,
            Notes: "Venue requested next-week move."));

        var cancelled = rescheduled.ApplyUpdate(new EventAggregateUpdateRequest(
            ExpectedVersion: 2,
            ChangedAtUtc: DateTimeOffset.UtcNow.AddMinutes(5),
            ChangedBy: "ops",
            Reason: EventVersionReason.Cancelled,
            ChangedFields: [nameof(EventAggregate.EventStatus), nameof(EventAggregate.PublishStatus)],
            Notes: "Organizer cancelled."));

        Assert.Equal(EventLifecycleStatus.Cancelled, cancelled.EventStatus);
        Assert.Equal(EventPublishStatus.Unpublished, cancelled.PublishStatus);
        Assert.Equal(3, cancelled.Version);
        Assert.Equal(2, cancelled.EffectiveChangeHistory.Length);
        Assert.Equal(EventVersionReason.Rescheduled, cancelled.EffectiveChangeHistory[0].Reason);
        Assert.Equal(EventVersionReason.Cancelled, cancelled.EffectiveChangeHistory[1].Reason);
        Assert.Equal(aggregate.StartUtc, cancelled.EffectiveChangeHistory[0].Before.StartUtc);
        Assert.Equal(rescheduled.StartUtc, cancelled.EffectiveChangeHistory[0].After.StartUtc);
    }

    [Fact]
    public void RepeatedMergeUpdates_PreserveIdentityAndLineageIntegrity()
    {
        var aggregate = BuildAggregate();
        var firstMergeAt = DateTimeOffset.UtcNow;

        var firstMerge = aggregate.ApplyUpdate(new EventAggregateUpdateRequest(
            ExpectedVersion: aggregate.Version,
            ChangedAtUtc: firstMergeAt,
            ChangedBy: "resolver-1",
            Reason: EventVersionReason.MergeApplied,
            ChangedFields: [nameof(EventAggregate.MergeLineage), nameof(EventAggregate.Provenance)],
            RequiresModerationReview: true,
            MergeLineage: aggregate.MergeLineage with
            {
                AppliedMergePlanIds = ["merge-001"],
                MergedSourceRefs = ["source:seed", "source:candidate-1"],
                LastMergedAtUtc = firstMergeAt,
                LastMergedBy = "resolver-1",
                MergeRecords =
                [
                    new EventMergeRecord(
                        MergeId: "merge-001",
                        MergedAtUtc: firstMergeAt,
                        MergeReason: "Duplicate evidence confirmed.",
                        MergeActor: "resolver-1",
                        MergeOrigin: "entity_resolution_pipeline",
                        MergeConfidence: 0.91,
                        CandidateIds: ["candidate-1"],
                        SourceReferences:
                        [
                            new EventMergeSourceReference("source:seed", "manual_submission", "canonical_provenance", 0.82),
                            new EventMergeSourceReference("source:candidate-1", "flyer_ocr", "incoming_candidate", 0.91),
                        ],
                        MergeRationale: ["Title and temporal evidence matched."],
                        RequiresManualReview: false),
                ],
            },
            Provenance: aggregate.Provenance with
            {
                SourceRefs = ["source:seed", "source:candidate-1"],
            }));

        var secondMergeAt = firstMergeAt.AddMinutes(7);
        var secondMerge = firstMerge.ApplyUpdate(new EventAggregateUpdateRequest(
            ExpectedVersion: firstMerge.Version,
            ChangedAtUtc: secondMergeAt,
            ChangedBy: "resolver-2",
            Reason: EventVersionReason.MergeApplied,
            ChangedFields: [nameof(EventAggregate.MergeLineage), nameof(EventAggregate.Provenance)],
            RequiresModerationReview: true,
            MergeLineage: firstMerge.MergeLineage with
            {
                AppliedMergePlanIds = ["merge-001", "merge-002"],
                MergedSourceRefs = ["source:seed", "source:candidate-1", "source:candidate-2"],
                LastMergedAtUtc = secondMergeAt,
                LastMergedBy = "resolver-2",
                MergeRecords = firstMerge.MergeLineage.EffectiveMergeRecords
                    .Concat([
                        new EventMergeRecord(
                            MergeId: "merge-002",
                            MergedAtUtc: secondMergeAt,
                            MergeReason: "Venue correction from higher-confidence source.",
                            MergeActor: "resolver-2",
                            MergeOrigin: "entity_resolution_pipeline",
                            MergeConfidence: 0.94,
                            CandidateIds: ["candidate-2"],
                            SourceReferences:
                            [
                                new EventMergeSourceReference("source:seed", "manual_submission", "canonical_provenance", 0.82),
                                new EventMergeSourceReference("source:candidate-2", "partner_feed", "incoming_candidate", 0.94),
                            ],
                            MergeRationale: ["Incoming venue confidence exceeded canonical confidence."],
                            RequiresManualReview: false)
                    ])
                    .ToArray(),
            },
            Provenance: firstMerge.Provenance with
            {
                SourceRefs = ["source:seed", "source:candidate-1", "source:candidate-2"],
            }));

        Assert.Equal(aggregate.CanonicalEventId, secondMerge.CanonicalEventId);
        Assert.Equal(3, secondMerge.Version);
        Assert.Equal(2, secondMerge.MergeLineage.EffectiveMergeRecords.Length);
        Assert.Equal("merge-001", secondMerge.MergeLineage.EffectiveMergeRecords[0].MergeId);
        Assert.Equal("merge-002", secondMerge.MergeLineage.EffectiveMergeRecords[1].MergeId);
        Assert.Contains("source:candidate-1", secondMerge.Provenance.SourceRefs);
        Assert.Contains("source:candidate-2", secondMerge.Provenance.SourceRefs);
        Assert.Equal(EventEvolutionType.MergeApplied, secondMerge.EffectiveChangeHistory[0].EvolutionType);
        Assert.Equal(EventEvolutionType.MergeApplied, secondMerge.EffectiveChangeHistory[1].EvolutionType);
    }

    private static EventAggregate BuildAggregate(
        EventLifecycleStatus status = EventLifecycleStatus.Reviewed,
        EventPublishStatus publishStatus = EventPublishStatus.EligibilityPending)
    {
        var now = DateTimeOffset.UtcNow;
        return new EventAggregate(
            CanonicalEventId: "evt-1",
            SourceEventIds: ["evt-1"],
            ExternalReferences: [new ExternalEventReference("seed", "manual", "evt-1")],
            Title: "Original Event",
            Description: "Original description",
            Tags: ["music"],
            Category: "nightlife",
            VenueName: "Venue One",
            Address: new EventAddress("100 Main St", "Houston", "TX", "77002", "US", "100 Main St, Houston, TX"),
            Latitude: 29.7604,
            Longitude: -95.3698,
            TimeZone: "America/Chicago",
            StartUtc: now.AddDays(3),
            EndUtc: now.AddDays(3).AddHours(3),
            LocalStartDisplay: null,
            LocalEndDisplay: null,
            EventStatus: status,
            PublishStatus: publishStatus,
            ModerationStatus: EventModerationStatus.InReview,
            RiskLevel: EventRiskLevel.Medium,
            ConfidenceScore: 0.82,
            Provenance: new EventProvenanceMetadata(
                PrimarySourceKind: "manual",
                PrimarySourceRef: "evt-1",
                EvidenceRefs: [],
                FirstObservedAtUtc: now.AddDays(-2),
                LastObservedAtUtc: now,
                SourceRefs: ["evt-1"]),
            CreatedAtUtc: now.AddDays(-2),
            UpdatedAtUtc: now,
            Version: 1,
            MergeLineage: new EventMergeLineage(null, [], [], null, null),
            ConcurrencyToken: "evt-1:v1:0",
            ChangeHistory: []);
    }
}
