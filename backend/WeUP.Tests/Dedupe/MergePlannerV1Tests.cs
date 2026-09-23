using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using WeUP.Application.Dedupe;
using WeUP.Contracts.Dedupe;
using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Moderation;
using WeUP.Domain.Events;
using WeUP.Domain.Moderation;
using Xunit;

namespace WeUP.Tests.Dedupe;

public sealed class MergePlannerV1Tests
{
    private readonly MergePlanner _planner = new(NullLogger<MergePlanner>.Instance);

    [Fact]
    public async Task GeneratePlanAsync_ExactDuplicate_AllKeepExisting_AutoApplyTrue()
    {
        var candidate = CreateCandidate(
            title: "Summer Fest",
            startUtc: DateTimeOffset.Parse("2026-07-10T19:00:00Z"),
            rawLocationText: "Central Park",
            tags: "music,outdoor");

        var canonical = CreateAggregate(
            canonicalEventId: Guid.NewGuid().ToString(),
            title: "Summer Fest",
            venueName: "Central Park",
            startUtc: DateTimeOffset.Parse("2026-07-10T19:00:00Z"),
            tags: ["music", "outdoor"]);

        var assessment = CreateAssessment(
            candidate.CandidateId,
            Guid.Parse(canonical.CanonicalEventId),
            DuplicateAssessmentLevel.ExactDuplicate,
            title: 1.0f,
            venue: 1.0f,
            time: 1.0f,
            location: 1.0f,
            sourceHash: 1.0f);

        var plan = await _planner.GeneratePlanAsync(assessment, candidate, canonical);

        Assert.Equal(MergeFieldAction.KeepExisting, plan.TitleAction);
        Assert.Equal(MergeFieldAction.KeepExisting, plan.LocationAction);
        Assert.Equal(MergeFieldAction.KeepExisting, plan.TimeAction);
        Assert.Equal(MergeFieldAction.KeepExisting, plan.TagsAction);
        Assert.Empty(plan.Conflicts);
        Assert.True(plan.AutoApply);
    }

    [Fact]
    public async Task GeneratePlanAsync_PartialSimilarity_UsesReplaceOrMerge()
    {
        var candidate = CreateCandidate(
            title: "Summer Festival",
            startUtc: DateTimeOffset.Parse("2026-07-10T19:30:00Z"),
            rawLocationText: "Central Park Houston",
            tags: "music,food");

        var canonical = CreateAggregate(
            canonicalEventId: Guid.NewGuid().ToString(),
            title: "Summer Fest",
            venueName: "Central Park",
            startUtc: DateTimeOffset.Parse("2026-07-10T19:00:00Z"),
            tags: ["music", "outdoor"]);

        var assessment = CreateAssessment(
            candidate.CandidateId,
            Guid.Parse(canonical.CanonicalEventId),
            DuplicateAssessmentLevel.ProbableDuplicate,
            title: 0.90f,
            venue: 0.88f,
            time: 0.82f,
            location: 0.86f,
            sourceHash: 0.85f);

        var plan = await _planner.GeneratePlanAsync(assessment, candidate, canonical);

        var hasReplaceOrMerge =
            plan.TitleAction is MergeFieldAction.ReplaceWithCandidate or MergeFieldAction.MergeValues
            || plan.LocationAction is MergeFieldAction.ReplaceWithCandidate or MergeFieldAction.MergeValues
            || plan.TimeAction is MergeFieldAction.ReplaceWithCandidate or MergeFieldAction.MergeValues
            || plan.TagsAction is MergeFieldAction.ReplaceWithCandidate or MergeFieldAction.MergeValues;

        Assert.True(hasReplaceOrMerge);
    }

    [Fact]
    public async Task GeneratePlanAsync_LowSimilarity_EmitsConflict_AndAutoApplyFalse()
    {
        var candidate = CreateCandidate(
            title: "Underground Techno Night",
            startUtc: DateTimeOffset.Parse("2026-07-11T03:00:00Z"),
            rawLocationText: "Warehouse District",
            tags: "techno,nightlife");

        var canonical = CreateAggregate(
            canonicalEventId: Guid.NewGuid().ToString(),
            title: "Family Picnic",
            venueName: "Community Park",
            startUtc: DateTimeOffset.Parse("2026-07-10T19:00:00Z"),
            tags: ["family", "outdoor"]);

        var assessment = CreateAssessment(
            candidate.CandidateId,
            Guid.Parse(canonical.CanonicalEventId),
            DuplicateAssessmentLevel.PossibleDuplicate,
            title: 0.40f,
            venue: 0.45f,
            time: 0.30f,
            location: 0.35f,
            sourceHash: 0.25f);

        var plan = await _planner.GeneratePlanAsync(assessment, candidate, canonical);

        Assert.Contains(plan.Conflicts, c =>
            string.Equals(c.FieldName, "Title", StringComparison.Ordinal)
            || string.Equals(c.FieldName, "Location", StringComparison.Ordinal)
            || string.Equals(c.FieldName, "StartTime", StringComparison.Ordinal));
        Assert.False(plan.AutoApply);
    }

    private static CandidateEvent CreateCandidate(
        string title,
        DateTimeOffset startUtc,
        string rawLocationText,
        string tags)
    {
        return new CandidateEvent(
            CandidateId: Guid.NewGuid(),
            RequestId: Guid.NewGuid(),
            Title: title,
            InferredStartUtc: startUtc,
            RawLocationText: rawLocationText,
            OverallExtractionConfidence: 0.95f,
            RawFields: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Tags"] = tags,
            });
    }

    private static EventAggregate CreateAggregate(
        string canonicalEventId,
        string title,
        string venueName,
        DateTimeOffset startUtc,
        string[] tags)
    {
        var now = DateTimeOffset.Parse("2026-04-01T00:00:00Z");

        return new EventAggregate(
            CanonicalEventId: canonicalEventId,
            SourceEventIds: ["source-1"],
            ExternalReferences: [],
            Title: title,
            Description: null,
            Tags: tags,
            Category: "music",
            VenueName: venueName,
            Address: new EventAddress(
                AddressLine1: "1 Main",
                City: "Houston",
                State: "TX",
                PostalCode: "77001",
                Country: "US",
                RawAddress: "1 Main, Houston, TX"),
            Latitude: 0,
            Longitude: 0,
            TimeZone: "UTC",
            StartUtc: startUtc,
            EndUtc: null,
            LocalStartDisplay: null,
            LocalEndDisplay: null,
            EventStatus: EventLifecycleStatus.Candidate,
            PublishStatus: EventPublishStatus.NotEligible,
            ModerationStatus: EventModerationStatus.Unreviewed,
            RiskLevel: EventRiskLevel.Low,
            ConfidenceScore: 0.90,
            Provenance: new EventProvenanceMetadata(
                PrimarySourceKind: "test",
                PrimarySourceRef: "source-1",
                EvidenceRefs: [],
                FirstObservedAtUtc: now,
                LastObservedAtUtc: now,
                SourceRefs: ["source-1"]),
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            Version: 1,
            MergeLineage: new EventMergeLineage(
                ParentCanonicalEventId: null,
                MergedCanonicalEventIds: [],
                AppliedMergePlanIds: [],
                LastMergedAtUtc: null,
                LastMergedBy: null,
                MergedSourceRefs: [],
                MergeRecords: []));
    }

    private static DuplicateAssessment CreateAssessment(
        Guid candidateId,
        Guid canonicalEventId,
        DuplicateAssessmentLevel level,
        float title,
        float venue,
        float time,
        float location,
        float sourceHash)
    {
        return new DuplicateAssessment(
            CandidateId: candidateId,
            CanonicalEventId: canonicalEventId,
            Level: level,
            Scores:
            [
                new MatchScoreBreakdown(MatchDimension.Title, title),
                new MatchScoreBreakdown(MatchDimension.Venue, venue),
                new MatchScoreBreakdown(MatchDimension.StartTime, time),
                new MatchScoreBreakdown(MatchDimension.Location, location),
                new MatchScoreBreakdown(MatchDimension.SourceHash, sourceHash),
            ],
            Explanation: "test");
    }
}
