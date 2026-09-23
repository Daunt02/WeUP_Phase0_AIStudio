using Microsoft.Extensions.Logging.Abstractions;
using WeUP.Application.Dedupe;
using WeUP.Contracts.Dedupe;
using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Moderation;
using WeUP.Domain.Events;
using WeUP.Domain.Moderation;
using WeightedDeduplicationStrategy = WeUP.Domain.Dedupe.WeightedDeduplicationStrategy;
using Xunit;

namespace WeUP.Tests.Dedupe;

public sealed class WeightedDeduplicationServiceTests
{
    private readonly WeightedDeduplicationService _service =
        new(new WeightedDeduplicationStrategy(), NullLogger<WeightedDeduplicationService>.Instance);

    [Fact]
    public async Task Exact_duplicate_maps_to_exact_duplicate_level()
    {
        var candidate = CreateCandidate(
            title: "Neon House Night",
            startUtc: DateTimeOffset.Parse("2026-04-15T02:00:00Z"),
            rawLocationText: "123 Main St",
            rawFields: new Dictionary<string, string>
            {
                ["SourceRef"] = "source:abc",
                ["VenueName"] = "Skyline Club",
                ["Latitude"] = "29.7604",
                ["Longitude"] = "-95.3698",
            });

        var canonical = CreateAggregate(
            canonicalEventId: "6fdb74f2-1a33-4d29-adf8-42efe816c0fd",
            title: "Neon House Night",
            venueName: "Skyline Club",
            address: "123 Main St",
            startUtc: DateTimeOffset.Parse("2026-04-15T02:00:00Z"),
            sourceRefs: ["source:abc"],
            latitude: 29.7604,
            longitude: -95.3698);

        var result = await _service.AssessAsync(candidate, [canonical]);

        Assert.Equal(DuplicateAssessmentLevel.ExactDuplicate, result.Level);
    }

    [Fact]
    public async Task Slight_title_change_maps_to_probable_or_possible_duplicate()
    {
        var candidate = CreateCandidate(
            title: "Summer Block Partyy",
            startUtc: DateTimeOffset.Parse("2026-04-20T18:00:00Z"),
            rawLocationText: "22 Commerce Ave",
            rawFields: new Dictionary<string, string>
            {
                ["SourceRef"] = "source:candidate",
                ["VenueName"] = "Warehouse 22",
            });

        var canonical = CreateAggregate(
            canonicalEventId: "cf73072f-3430-4a80-a775-1d6d915f9dd9",
            title: "Summer Block Party",
            venueName: "Warehouse 22",
            address: "22 Commerce Ave",
            startUtc: DateTimeOffset.Parse("2026-04-20T19:00:00Z"),
            sourceRefs: ["source:canonical"]);

        var result = await _service.AssessAsync(candidate, [canonical]);

        Assert.True(
            result.Level is DuplicateAssessmentLevel.ProbableDuplicate or DuplicateAssessmentLevel.PossibleDuplicate,
            $"Expected ProbableDuplicate or PossibleDuplicate but got {result.Level}.");
    }

    [Fact]
    public async Task Unrelated_event_maps_to_distinct()
    {
        var candidate = CreateCandidate(
            title: "Silent Meditation Circle",
            startUtc: DateTimeOffset.Parse("2026-04-15T04:00:00Z"),
            rawLocationText: "10 Quiet Way",
            rawFields: new Dictionary<string, string>
            {
                ["SourceRef"] = "source:x",
                ["VenueName"] = "Lot 77",
            });

        var canonical = CreateAggregate(
            canonicalEventId: "3bc04e36-4d65-4605-b723-77d7269dd20f",
            title: "Metal Night Frenzy",
            venueName: "Thunder Dome",
            address: "999 Loud St",
            startUtc: DateTimeOffset.Parse("2026-04-17T04:00:00Z"),
            sourceRefs: ["source:y"]);

        var result = await _service.AssessAsync(candidate, [canonical]);

        Assert.Equal(DuplicateAssessmentLevel.Distinct, result.Level);
    }

    private static CandidateEvent CreateCandidate(
        string title,
        DateTimeOffset startUtc,
        string rawLocationText,
        IReadOnlyDictionary<string, string> rawFields)
    {
        return new CandidateEvent(
            CandidateId: Guid.NewGuid(),
            RequestId: Guid.NewGuid(),
            Title: title,
            InferredStartUtc: startUtc,
            RawLocationText: rawLocationText,
            OverallExtractionConfidence: 0.95f,
            RawFields: rawFields);
    }

    private static EventAggregate CreateAggregate(
        string canonicalEventId,
        string title,
        string venueName,
        string address,
        DateTimeOffset startUtc,
        string[] sourceRefs,
        double latitude = 0,
        double longitude = 0)
    {
        var now = DateTimeOffset.Parse("2026-04-01T00:00:00Z");

        return new EventAggregate(
            CanonicalEventId: canonicalEventId,
            SourceEventIds: sourceRefs,
            ExternalReferences: [],
            Title: title,
            Description: null,
            Tags: [],
            Category: "music",
            VenueName: venueName,
            Address: new EventAddress(
                AddressLine1: "1 Main",
                City: "Houston",
                State: "TX",
                PostalCode: "77001",
                Country: "US",
                RawAddress: address),
            Latitude: latitude,
            Longitude: longitude,
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
                PrimarySourceRef: sourceRefs.First(),
                EvidenceRefs: [],
                FirstObservedAtUtc: now,
                LastObservedAtUtc: now,
                SourceRefs: sourceRefs),
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
}