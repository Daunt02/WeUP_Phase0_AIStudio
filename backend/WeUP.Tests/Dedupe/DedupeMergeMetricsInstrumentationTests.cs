using System.Diagnostics.Metrics;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Dedupe;
using WeUP.Infrastructure.Dedupe;
using Xunit;

namespace WeUP.Tests.Dedupe;

public sealed class DedupeMergeMetricsInstrumentationTests
{
    [Fact]
    public void InstrumentedDeduplicationStrategy_TracksAssessmentLevels()
    {
        const string meterName = "WeUP.Test.DedupeMerge.Assessment";

        using var collector = new LongMetricCollector(meterName);
        using var metrics = new DedupeMergeMetricsService(meterName);
        var strategy = new InstrumentedDeduplicationStrategy(new WeightedDeduplicationStrategy(), metrics);

        strategy.Assess(
            CreateCandidate(
                title: "Neon House Night",
                venueName: "Skyline Club",
                address: "123 Main St",
                startUtc: "2026-04-15T02:00:00Z",
                sourceRef: "source:abc",
                attributes: Geo(29.7604, -95.3698)),
            CreateCanonical(
                title: "Neon House Night",
                venueName: "Skyline Club",
                address: "123 Main St",
                startUtc: "2026-04-15T02:00:00Z",
                sourceRefs: ["source:abc"],
                latitude: 29.7604,
                longitude: -95.3698));

        strategy.Assess(
            CreateCandidate(
                title: "Silent Meditation Circle",
                venueName: "Lot 77",
                address: "10 Quiet Way",
                startUtc: null),
            CreateCanonical(
                title: "Metal Night Frenzy",
                venueName: "Thunder Dome",
                address: "999 Loud St",
                startUtc: null,
                sourceRefs: ["canonical-y"]));

        Assert.Equal(1, collector.Sum(
            DedupeMergeMetricNames.DedupAssessmentTotal,
            ("level", nameof(DuplicateAssessmentLevel.ExactDuplicate))));

        Assert.Equal(1, collector.Sum(
            DedupeMergeMetricNames.DedupAssessmentTotal,
            ("level", nameof(DuplicateAssessmentLevel.Distinct)),
            ("has_blockers", "true")));
    }

    [Fact]
    public void InstrumentedMergePlanner_TracksReviewPressureAndFieldConflicts()
    {
        const string meterName = "WeUP.Test.DedupeMerge.Plan";

        using var collector = new LongMetricCollector(meterName);
        using var metrics = new DedupeMergeMetricsService(meterName);
        var planner = new InstrumentedMergePlanner(new MergePlanner(), metrics);

        var conflictedPlan = planner.CreatePlan(
            CreateCandidate(
                title: "Jazz World Summit",
                venueName: "North Hall Annex",
                address: "10 Lincoln Center",
                startUtc: "2026-09-02T07:00:00Z"),
            CreateCanonical(
                title: "International Jazz Festival 2026",
                venueName: "Lincoln Center",
                address: "10 Lincoln Center",
                startUtc: "2026-09-01T18:00:00Z"),
            CreateAssessment(
                level: DuplicateAssessmentLevel.ProbableDuplicate,
                autoMergeAllowed: true));

        var safePlan = planner.CreatePlan(
            CreateCandidate(
                title: "Warehouse Session",
                venueName: "Dock 9",
                address: "Dock 9",
                startUtc: "2026-10-15T19:00:00Z"),
            CreateCanonical(
                title: "Warehouse Session",
                venueName: "Dock 9",
                address: "Dock 9",
                startUtc: "2026-10-15T19:00:00Z"),
            CreateAssessment(
                level: DuplicateAssessmentLevel.ExactDuplicate,
                autoMergeAllowed: true));

        Assert.True(conflictedPlan.RequiresManualReview);
        Assert.True(safePlan.AutoMergeAllowed);

        Assert.Equal(2, collector.Sum(DedupeMergeMetricNames.MergePlansCreatedTotal));
        Assert.Equal(1, collector.Sum(DedupeMergeMetricNames.MergeRequiresReviewTotal));
        Assert.Equal(1, collector.Sum(DedupeMergeMetricNames.MergeAutoApprovedTotal));
        Assert.Equal(1, collector.Sum(DedupeMergeMetricNames.TitleConflicts));
        Assert.Equal(1, collector.Sum(DedupeMergeMetricNames.TimeConflicts));
        Assert.Equal(1, collector.Sum(DedupeMergeMetricNames.VenueConflicts));
        Assert.Equal(1, collector.Sum(DedupeMergeMetricNames.MergeFieldConflictsTotal, ("field", "title")));
        Assert.Equal(1, collector.Sum(DedupeMergeMetricNames.MergeFieldConflictsTotal, ("field", "time")));
        Assert.Equal(1, collector.Sum(DedupeMergeMetricNames.MergeFieldConflictsTotal, ("field", "venue")));
    }

    [Fact]
    public void MetricsService_TracksRejectedPlansSeparately()
    {
        const string meterName = "WeUP.Test.DedupeMerge.Rejected";

        using var collector = new LongMetricCollector(meterName);
        using var metrics = new DedupeMergeMetricsService(meterName);

        metrics.TrackMergePlan(CreateRejectedPlan());

        Assert.Equal(1, collector.Sum(DedupeMergeMetricNames.MergePlansCreatedTotal));
        Assert.Equal(1, collector.Sum(DedupeMergeMetricNames.MergeRejectedTotal));
        Assert.Equal(0, collector.Sum(DedupeMergeMetricNames.MergeRequiresReviewTotal));
    }

    private static NormalizedEventCandidate CreateCandidate(
        string? title,
        string? venueName,
        string? address,
        string? startUtc,
        string sourceRef = "candidate-source-ref",
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
            EvidenceRefs: null,
            ExternalSourceId: null,
            Attributes: attributes);
    }

    private static EventAggregateSnapshot CreateCanonical(
        string? title,
        string? venueName,
        string? address,
        string? startUtc,
        string[]? sourceRefs = null,
        double latitude = 0,
        double longitude = 0)
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
            SourceRefs: sourceRefs ?? ["canonical-source"],
            EvidenceRefs: [],
            IsApproved: false,
            ExternalSourceId: null,
                Attributes: null);
    }

    private static DuplicateAssessment CreateAssessment(
        DuplicateAssessmentLevel level,
        bool autoMergeAllowed)
    {
        return new DuplicateAssessment(
            CandidateSourceRef: "incoming-source",
            CanonicalEventId: "canonical-1",
            Level: level,
            Breakdown: new MatchScoreBreakdown(
                CompositeScore: level switch
                {
                    DuplicateAssessmentLevel.ExactDuplicate => 0.95,
                    DuplicateAssessmentLevel.ProbableDuplicate => 0.80,
                    DuplicateAssessmentLevel.PossibleDuplicate => 0.60,
                    _ => 0.10,
                },
                TitleScore: new DimensionScore(MatchDimension.TitleSimilarity, 0.9, 0.30, 0.27, "title"),
                VenueScore: new DimensionScore(MatchDimension.VenueSimilarity, 0.9, 0.25, 0.225, "venue"),
                AddressScore: new DimensionScore(MatchDimension.AddressSimilarity, 0.9, 0.15, 0.135, "address"),
                TemporalScore: new DimensionScore(MatchDimension.TemporalOverlap, 0.9, 0.15, 0.135, "time"),
                GeoScore: new DimensionScore(MatchDimension.GeoProximity, 0.9, 0.10, 0.09, "geo"),
                SourceHashScore: new DimensionScore(MatchDimension.SourceHashEvidence, 0.9, 0.05, 0.045, "source"),
                ActiveSignals: [nameof(MatchDimension.TitleSimilarity)],
                ScoringNotes: []),
            SafetyVerdict: new MergeSafetyVerdict(
                AutoMergeAllowed: autoMergeAllowed,
                ActiveBlockers: autoMergeAllowed ? [] : [MergeBlockerKind.SourceIdentityConflict],
                BlockerExplanations: autoMergeAllowed ? [] : ["blocker"]),
            AutoMergeAllowed: autoMergeAllowed,
            Rationale: [],
            AssessedAtUtc: DateTimeOffset.UtcNow);
    }

    private static MergePlanDetail CreateRejectedPlan()
    {
        var assessment = CreateAssessment(DuplicateAssessmentLevel.ProbableDuplicate, autoMergeAllowed: false);

        return new MergePlanDetail(
            CanonicalEventId: "canonical-1",
            CandidateSourceRef: "incoming-source",
            FieldDecisions: new Dictionary<string, MergeFieldDecision>(StringComparer.Ordinal)
            {
                ["Title"] = new(
                    FieldName: "Title",
                    Action: MergeFieldAction.RejectMerge,
                    ResultValue: null,
                    Rationale: "Rejected for test coverage.",
                    Conflict: null),
            },
            Conflicts: [],
            AutoMergeAllowed: false,
            RequiresManualReview: false,
            RejectMerge: true,
            MergeRationale: ["reject"],
            ManualReviewReasons: [],
            Audit: new MergePlanAudit(
                Assessment: assessment,
                IncomingCandidate: CreateCandidate("Rejected Event", "Venue", "Address", "2026-10-15T19:00:00Z"),
                CanonicalSnapshot: CreateCanonical("Rejected Event", "Venue", "Address", "2026-10-15T19:00:00Z"),
                AppliedPrecedenceRules: ["rule_rejected_for_test"],
                CreatedAtUtc: DateTimeOffset.UtcNow,
                MergePlannerVersion: "1.0"));
    }

    private static IReadOnlyDictionary<string, string?> Geo(double latitude, double longitude)
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["latitude"] = latitude.ToString("F6"),
            ["longitude"] = longitude.ToString("F6"),
        };
    }

    private sealed class LongMetricCollector : IDisposable
    {
        private readonly object _gate = new();
        private readonly List<CapturedMeasurement> _measurements = [];
        private readonly MeterListener _listener;

        public LongMetricCollector(string meterName)
        {
            _listener = new MeterListener();
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == meterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            {
                var capturedTags = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (var tag in tags)
                {
                    capturedTags[tag.Key] = tag.Value?.ToString();
                }

                lock (_gate)
                {
                    _measurements.Add(new CapturedMeasurement(instrument.Name, measurement, capturedTags));
                }
            });
            _listener.Start();
        }

        public long Sum(string instrumentName, params (string Key, string Value)[] requiredTags)
        {
            lock (_gate)
            {
                return _measurements
                    .Where(measurement => measurement.Name == instrumentName)
                    .Where(measurement => requiredTags.All(tag =>
                        measurement.Tags.TryGetValue(tag.Key, out var value) && value == tag.Value))
                    .Sum(measurement => measurement.Value);
            }
        }

        public void Dispose() => _listener.Dispose();

        private sealed record CapturedMeasurement(
            string Name,
            long Value,
            IReadOnlyDictionary<string, string?> Tags);
    }
}