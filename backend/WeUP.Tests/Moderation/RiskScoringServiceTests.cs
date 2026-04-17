using WeUP.Application.Moderation;
using WeUP.Domain.Dedupe;
using WeUP.Domain.Flyer;
using WeUP.Domain.Moderation;
using Xunit;

namespace WeUP.Tests.Moderation;

public sealed class RiskScoringServiceTests
{
    private readonly IRiskScoringService _service = new RiskScoringService();

    [Fact]
    public void CleanCandidate_WithDistinctDedup_IsLowRisk()
    {
        var candidate = BuildCandidate();
        var assessment = BuildAssessment(DuplicateAssessmentLevel.Distinct, autoMergeAllowed: true);

        var result = _service.Score(candidate, assessment);

        Assert.Equal(0, result.OverallScore);
        Assert.Equal(EventRiskLevel.Low, result.Level);
        Assert.Empty(result.ContributingFactors);
        Assert.Equal("Risk score 0 (Low): no risk factors were triggered.", result.Explanation);
    }

    [Fact]
    public void MissingCriticalFields_AndVeryLowConfidence_IsRestricted()
    {
        var candidate = BuildCandidate(
            title: null,
            missingStartUtc: true,
            venue: null,
            address: null,
            overallConfidence: 0.20);

        var assessment = BuildAssessment(DuplicateAssessmentLevel.Distinct, autoMergeAllowed: true);
        var result = _service.Score(candidate, assessment);

        Assert.True(result.OverallScore >= 75);
        Assert.Equal(EventRiskLevel.Restricted, result.Level);
        Assert.Contains(result.ContributingFactors, f => f.Code == "missing_title");
        Assert.Contains(result.ContributingFactors, f => f.Code == "missing_start_utc");
        Assert.Contains(result.ContributingFactors, f => f.Code == "very_low_overall_confidence");
    }

    [Fact]
    public void SuspiciousKeyword_AndDuplicateAmbiguity_ProducesHighRisk()
    {
        var candidate = BuildCandidate(
            title: "Warehouse afterparty",
            description: "DM for address - secret location");

        var assessment = BuildAssessment(DuplicateAssessmentLevel.ProbableDuplicate, autoMergeAllowed: false);
        var result = _service.Score(candidate, assessment);

        Assert.True(result.OverallScore >= 45);
        Assert.Equal(EventRiskLevel.High, result.Level);
        Assert.Contains(result.ContributingFactors, f => f.Code == "suspicious_keywords");
        Assert.Contains(result.ContributingFactors, f => f.Code == "duplicate_ambiguity");
        Assert.Contains(result.ContributingFactors, f => f.Code == "duplicate_safety_blocker");
    }

    [Fact]
    public void DeterministicInput_AlwaysReturnsSameScoreAndExplanation()
    {
        var candidate = BuildCandidate(
            venue: "unknown",
            address: "TBD",
            overallConfidence: 0.52,
            startConfidence: 0.42,
            titleConfidence: 0.41);

        var assessment = BuildAssessment(
            DuplicateAssessmentLevel.PossibleDuplicate,
            autoMergeAllowed: false,
            scoringNotes: ["temporal conflict observed"]);

        var first = _service.Score(candidate, assessment);
        var second = _service.Score(candidate, assessment);

        Assert.Equal(first.OverallScore, second.OverallScore);
        Assert.Equal(first.Level, second.Level);
        Assert.Equal(first.Explanation, second.Explanation);
        Assert.Equal(first.ContributingFactors, second.ContributingFactors);
    }

    private static EventCandidateV2 BuildCandidate(
        string? title = "Night Session",
        string? description = "House and techno night",
        string? venue = "Echo Hall",
        string? address = "123 Main St",
        bool missingStartUtc = false,
        DateTimeOffset? startUtc = null,
        DateTimeOffset? endUtc = null,
        double overallConfidence = 0.91,
        double titleConfidence = 0.92,
        double startConfidence = 0.89,
        double venueConfidence = 0.88,
        double addressConfidence = 0.90)
    {
        var start = startUtc ?? new DateTimeOffset(2026, 8, 1, 18, 0, 0, TimeSpan.Zero);
        var end = endUtc ?? new DateTimeOffset(2026, 8, 1, 23, 0, 0, TimeSpan.Zero);

        return new EventCandidateV2(
            Title: title is null ? null : Field(title, titleConfidence, "title"),
            StartUtc: missingStartUtc
                ? null
                : Field(start, startConfidence, "startUtc"),
            EndUtc: Field(end, 0.87, "endUtc"),
            Venue: venue is null ? null : Field(venue, venueConfidence, "venue"),
            Address: address is null ? null : Field(address, addressConfidence, "address"),
            Category: Field("music", 0.90, "category"),
            Description: description is null ? null : Field(description, 0.81, "description"),
            Tags: Field<IReadOnlyList<string>>(["house"], 0.86, "tags"),
            OverallConfidence: overallConfidence,
            OverallConfidenceRationale: "test setup",
            EvidenceBundle: new EvidenceBundle(
                BundleId: "bundle-1",
                CollectedAtUtc: new DateTimeOffset(2026, 4, 17, 0, 0, 0, TimeSpan.Zero),
                RawOcrText: null,
                OcrBlocks: null,
                SourceHash: null,
                SourceKind: "flyer_ocr",
                ScoringComponents: null,
                ProcessingContext: null),
            CreatedAtUtc: new DateTimeOffset(2026, 4, 17, 0, 0, 0, TimeSpan.Zero),
            ScoringVersion: "1.0");
    }

    private static DuplicateAssessment BuildAssessment(
        DuplicateAssessmentLevel level,
        bool autoMergeAllowed,
        string[]? scoringNotes = null)
    {
        var title = new DimensionScore(MatchDimension.TitleSimilarity, 0.8, 0.3, 0.24, "test");
        var venue = new DimensionScore(MatchDimension.VenueSimilarity, 0.7, 0.25, 0.175, "test");
        var address = new DimensionScore(MatchDimension.AddressSimilarity, 0.7, 0.15, 0.105, "test");
        var temporal = new DimensionScore(MatchDimension.TemporalOverlap, 0.7, 0.15, 0.105, "test");
        var geo = new DimensionScore(MatchDimension.GeoProximity, 0.7, 0.10, 0.07, "test");
        var source = new DimensionScore(MatchDimension.SourceHashEvidence, 0.5, 0.05, 0.025, "test");

        return new DuplicateAssessment(
            CandidateSourceRef: "src:1",
            CanonicalEventId: "evt:1",
            Level: level,
            Breakdown: new MatchScoreBreakdown(
                CompositeScore: 0.72,
                TitleScore: title,
                VenueScore: venue,
                AddressScore: address,
                TemporalScore: temporal,
                GeoScore: geo,
                SourceHashScore: source,
                ActiveSignals: ["title", "venue"],
                ScoringNotes: scoringNotes ?? []),
            SafetyVerdict: new MergeSafetyVerdict(
                AutoMergeAllowed: autoMergeAllowed,
                ActiveBlockers: autoMergeAllowed ? [] : [MergeBlockerKind.SourceIdentityConflict],
                BlockerExplanations: autoMergeAllowed ? [] : ["blocker"]),
            AutoMergeAllowed: autoMergeAllowed,
            Rationale: ["test rationale"],
            AssessedAtUtc: new DateTimeOffset(2026, 4, 17, 0, 0, 0, TimeSpan.Zero));
    }

    private static FieldConfidence<T> Field<T>(T value, double confidence, string field)
        => new(
            Value: value,
            Confidence: confidence,
            EvidenceRefs: ["ev-1"],
            Rationale: "test",
            EvidenceSource: field,
            Metadata: null);
}