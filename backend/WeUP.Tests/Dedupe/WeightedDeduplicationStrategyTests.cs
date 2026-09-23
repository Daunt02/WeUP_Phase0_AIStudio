using WeUP.Contracts.Events;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Dedupe;
using Xunit;

namespace WeUP.Tests.Dedupe;

public sealed class WeightedDeduplicationStrategyTests
{
    private readonly WeightedDeduplicationStrategy _strategy = new();

    [Fact]
    public void Identical_records_with_matching_source_ref_are_exact_duplicate()
    {
        var candidate = CreateCandidate(
            title: "Neon House Night",
            venueName: "Skyline Club",
            address: "123 Main St",
            startUtc: "2026-04-15T02:00:00Z",
            sourceRef: "source:abc",
            attributes: Geo(29.7604, -95.3698));

        var canonical = CreateCanonical(
            title: "Neon House Night",
            venueName: "Skyline Club",
            address: "123 Main St",
            startUtc: "2026-04-15T02:00:00Z",
            sourceRefs: ["source:abc"],
            latitude: 29.7604,
            longitude: -95.3698,
            isApproved: false);

        var assessment = _strategy.Assess(candidate, canonical);

        Assert.Equal(DuplicateAssessmentLevel.ExactDuplicate, assessment.Level);
        Assert.True(assessment.AutoMergeAllowed);
        Assert.True(assessment.Breakdown.CompositeScore >= WeightedDeduplicationStrategy.ExactDuplicateThreshold);
        Assert.Empty(assessment.SafetyVerdict.ActiveBlockers);
    }

    [Fact]
    public void Composite_score_in_probable_band_maps_to_probable_duplicate()
    {
        var candidate = CreateCandidate(
            title: "Summer Block Party",
            venueName: "Warehouse 22",
            address: "22 Commerce Ave",
            startUtc: "2026-04-20T18:00:00Z");

        var canonical = CreateCanonical(
            title: "Summer Block Party",
            venueName: "Warehouse 22",
            address: "22 Commerce Ave",
            startUtc: "2026-04-20T19:00:00Z",
            sourceRefs: ["other-source"]);

        var assessment = _strategy.Assess(candidate, canonical);

        Assert.Equal(DuplicateAssessmentLevel.ProbableDuplicate, assessment.Level);
        Assert.InRange(
            assessment.Breakdown.CompositeScore,
            WeightedDeduplicationStrategy.ProbableDuplicateThreshold,
            WeightedDeduplicationStrategy.ExactDuplicateThreshold - 0.000001);
        Assert.True(assessment.AutoMergeAllowed);
    }

    [Fact]
    public void Composite_score_in_possible_band_maps_to_possible_duplicate()
    {
        var candidate = CreateCandidate(
            title: "Rooftop Sunset Session",
            venueName: "Venue A",
            address: "Address A",
            startUtc: "2026-04-20T18:00:00Z",
            attributes: Geo(29.7604, -95.3698));

        var canonical = CreateCanonical(
            title: "Rooftop Sunset Session",
            venueName: "Completely Different Venue",
            address: "Completely Different Address",
            startUtc: "2026-04-20T19:00:00Z",
            sourceRefs: ["other-source"],
            latitude: 29.7604,
            longitude: -95.3698);

        var assessment = _strategy.Assess(candidate, canonical);

        Assert.Equal(DuplicateAssessmentLevel.PossibleDuplicate, assessment.Level);
        Assert.InRange(
            assessment.Breakdown.CompositeScore,
            WeightedDeduplicationStrategy.PossibleDuplicateThreshold,
            WeightedDeduplicationStrategy.ProbableDuplicateThreshold - 0.000001);
        Assert.False(assessment.AutoMergeAllowed);
    }

    [Fact]
    public void Distinct_is_first_class_outcome_when_score_below_possible_threshold()
    {
        var candidate = CreateCandidate(
            title: "Silent Meditation Circle",
            venueName: "Lot 77",
            address: "10 Quiet Way",
            startUtc: null,
            sourceRef: "candidate-x");

        var canonical = CreateCanonical(
            title: "Metal Night Frenzy",
            venueName: "Thunder Dome",
            address: "999 Loud St",
            startUtc: null,
            sourceRefs: ["canonical-y"],
            isApproved: false);

        var assessment = _strategy.Assess(candidate, canonical);

        Assert.Equal(DuplicateAssessmentLevel.Distinct, assessment.Level);
        Assert.False(assessment.AutoMergeAllowed);
        Assert.Contains("distinct_outcome=affirmative_non_match_not_residual", assessment.Rationale);
        Assert.Contains(MergeBlockerKind.InsufficientTemporalData, assessment.SafetyVerdict.ActiveBlockers);
    }

    [Fact]
    public void Approved_canonical_is_hard_blocker_even_when_level_is_exact()
    {
        var candidate = CreateCandidate(
            title: "City Art Walk",
            venueName: "Gallery Row",
            address: "50 Polk St",
            startUtc: "2026-05-01T19:00:00Z",
            sourceRef: "ref-approved",
            attributes: Geo(29.7604, -95.3698));

        var canonical = CreateCanonical(
            title: "City Art Walk",
            venueName: "Gallery Row",
            address: "50 Polk St",
            startUtc: "2026-05-01T19:00:00Z",
            sourceRefs: ["ref-approved"],
            latitude: 29.7604,
            longitude: -95.3698,
            isApproved: true);

        var assessment = _strategy.Assess(candidate, canonical);

        Assert.Equal(DuplicateAssessmentLevel.ExactDuplicate, assessment.Level);
        Assert.False(assessment.AutoMergeAllowed);
        Assert.Contains(MergeBlockerKind.ApprovedEventProtection, assessment.SafetyVerdict.ActiveBlockers);
    }

    [Fact]
    public void Source_identity_conflict_is_hard_blocker()
    {
        var candidate = CreateCandidate(
            title: "Poetry Reading Downtown",
            venueName: "Mercury Hall",
            address: "1 Main",
            startUtc: "2026-04-20T18:00:00Z",
            externalSourceId: "ext-1000");

        var canonical = CreateCanonical(
            title: "Underground Techno Marathon",
            venueName: "Mercury Hall",
            address: "1 Main",
            startUtc: "2026-04-20T18:30:00Z",
            sourceRefs: ["other"],
            externalSourceId: "ext-1000");

        var assessment = _strategy.Assess(candidate, canonical);

        Assert.Contains(MergeBlockerKind.SourceIdentityConflict, assessment.SafetyVerdict.ActiveBlockers);
        Assert.False(assessment.AutoMergeAllowed);
    }

    [Fact]
    public void Temporal_and_geo_outliers_raise_hard_blockers()
    {
        var candidate = CreateCandidate(
            title: "Festival Alpha",
            venueName: "Venue Alpha",
            address: "Address Alpha",
            startUtc: "2026-04-20T00:00:00Z",
            attributes: Geo(29.7604, -95.3698));

        var canonical = CreateCanonical(
            title: "Festival Alpha",
            venueName: "Venue Alpha",
            address: "Address Alpha",
            startUtc: "2026-04-21T13:00:00Z",
            sourceRefs: ["other"],
            latitude: 34.0522,
            longitude: -118.2437);

        var assessment = _strategy.Assess(candidate, canonical);

        Assert.Contains(MergeBlockerKind.TemporalDeviationExceedsLimit, assessment.SafetyVerdict.ActiveBlockers);
        Assert.Contains(MergeBlockerKind.GeoDistanceExceedsLimit, assessment.SafetyVerdict.ActiveBlockers);
        Assert.False(assessment.AutoMergeAllowed);
    }

    [Fact]
    public void Evidence_overlap_ratio_is_exposed_in_source_hash_dimension()
    {
        var candidate = CreateCandidate(
            title: "Jazz Night",
            venueName: "Blue Room",
            address: "99 Pine",
            startUtc: "2026-06-01T01:00:00Z",
            sourceRef: "candidate-source",
            evidenceRefs: ["e1", "e2"]);

        var canonical = CreateCanonical(
            title: "Jazz Night",
            venueName: "Blue Room",
            address: "99 Pine",
            startUtc: "2026-06-01T01:30:00Z",
            sourceRefs: ["canonical-source"],
            evidenceRefs: ["e2", "e3"]);

        var assessment = _strategy.Assess(candidate, canonical);

        Assert.Equal(0.3333, assessment.Breakdown.SourceHashScore.RawScore, 4);
        Assert.Contains("evidence_overlap_ratio=0.3333", assessment.Breakdown.SourceHashScore.Explanation);
    }

    private static NormalizedEventCandidate CreateCandidate(
        string? title,
        string? venueName,
        string? address,
        string? startUtc,
        string sourceRef = "candidate-source-ref",
        string? externalSourceId = null,
        string[]? evidenceRefs = null,
        IReadOnlyDictionary<string, string?>? attributes = null)
    {
        return new NormalizedEventCandidate(
            Title: title,
            VenueName: venueName,
            Address: address,
            StartUtc: startUtc,
            EndUtc: null,
            Timezone: "UTC",
            Category: "music",
            Description: null,
            Tags: null,
            SourceKind: "test",
            SourceRef: sourceRef,
            ExtractionConfidence: 0.9,
            GeocodeConfidence: 0.9,
            TemporalConfidence: 0.9,
            EvidenceRefs: evidenceRefs,
            ExternalSourceId: externalSourceId,
            Attributes: attributes);
    }

    private static EventAggregateSnapshot CreateCanonical(
        string? title,
        string? venueName,
        string? address,
        string? startUtc,
        string[] sourceRefs,
        double latitude = 0,
        double longitude = 0,
        bool isApproved = false,
        string? externalSourceId = null,
        string[]? evidenceRefs = null)
    {
        return new EventAggregateSnapshot(
            CanonicalEventId: "canonical-1",
            Title: title,
            VenueName: venueName,
            Address: address,
            Latitude: latitude,
            Longitude: longitude,
            StartUtc: startUtc,
            EndUtc: null,
            Timezone: "UTC",
            Category: "music",
            Confidence: 0.95,
            SourceRefs: sourceRefs,
            EvidenceRefs: evidenceRefs ?? [],
            IsApproved: isApproved,
            ExternalSourceId: externalSourceId,
            Attributes: null);
    }

    private static IReadOnlyDictionary<string, string?> Geo(double lat, double lon)
        => new Dictionary<string, string?>
        {
            ["latitude"] = lat.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["longitude"] = lon.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
}