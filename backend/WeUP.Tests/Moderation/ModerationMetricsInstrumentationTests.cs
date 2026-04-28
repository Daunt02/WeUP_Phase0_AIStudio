using System.Diagnostics.Metrics;
using WeUP.Application.Moderation;
using WeUP.Contracts.Auth;
using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Moderation;
using WeUP.Contracts.Resolution;
using WeUP.Domain.Dedupe;
using WeUP.Domain.Flyer;
using WeUP.Domain.Moderation;
using WeUP.Domain.Resolution;
using WeUP.Domain.Users;
using WeUP.Infrastructure.Auth;
using WeUP.Infrastructure.Moderation;
using WeUP.Infrastructure.Spatial;
using Xunit;

namespace WeUP.Tests.Moderation;

public sealed class ModerationMetricsInstrumentationTests
{
    [Fact]
    public async Task RefreshBacklogAsync_ExposesQueueDepthPendingAndBacklogAgeByRiskTier()
    {
        const string meterName = "WeUP.Test.Moderation.Backlog";

        var queue = new InMemoryModerationQueue();
        var now = DateTimeOffset.UtcNow;

        await queue.AddItemAsync(BuildQueueItem(
            riskLevel: EventRiskLevel.High,
            itemStatus: ModerationItemStatus.Open,
            lastAction: "enqueue",
            createdAtUtc: now.AddMinutes(-15)));

        await queue.AddItemAsync(BuildQueueItem(
            riskLevel: EventRiskLevel.Medium,
            itemStatus: ModerationItemStatus.InReview,
            lastAction: "start-review",
            createdAtUtc: now.AddMinutes(-5),
            assignedReviewerId: "mod-7"));

        await queue.AddItemAsync(BuildQueueItem(
            riskLevel: EventRiskLevel.Low,
            itemStatus: ModerationItemStatus.Resolved,
            lastAction: "reject",
            createdAtUtc: now.AddMinutes(-1)));

        using var collector = new MetricCollector(meterName);
        using var metrics = new ModerationMetricsService(queue.GetTelemetrySnapshotAsync, meterName);

        await metrics.RefreshBacklogAsync();
        collector.RecordObservableInstruments();

        Assert.Equal(1, collector.SumLong(
            ModerationMetricNames.ModerationQueueSize,
            ("risk_level", "high"),
            ("moderation_status", ModerationTelemetryDimensions.Pending)));

        Assert.Equal(1, collector.SumLong(
            ModerationMetricNames.ModerationQueueSize,
            ("risk_level", "medium"),
            ("moderation_status", ModerationTelemetryDimensions.InReview)));

        Assert.Equal(1, collector.SumLong(
            ModerationMetricNames.ModerationQueueSize,
            ("risk_level", "low"),
            ("moderation_status", ModerationTelemetryDimensions.Rejected)));

        Assert.Equal(1, collector.SumLong(
            ModerationMetricNames.ModerationItemsPendingTotal,
            ("risk_level", "high"),
            ("moderation_status", ModerationTelemetryDimensions.Pending)));

        Assert.Equal(0, collector.SumLong(
            ModerationMetricNames.ModerationItemsPendingTotal,
            ("risk_level", "low"),
            ("moderation_status", ModerationTelemetryDimensions.Rejected)));

        Assert.True(collector.MaxDouble(
            ModerationMetricNames.ModerationBacklogAgeMs,
            ("risk_level", "high"),
            ("moderation_status", ModerationTelemetryDimensions.Pending)) >= 14 * 60 * 1000d);
    }

    [Fact]
    public void TrackProcessingCompletion_RecordsProcessedTotalsAndLatency()
    {
        const string meterName = "WeUP.Test.Moderation.Processing";

        var item = BuildQueueItem(
            riskLevel: EventRiskLevel.Restricted,
            itemStatus: ModerationItemStatus.InReview,
            lastAction: "start-review",
            createdAtUtc: DateTimeOffset.UtcNow.AddMinutes(-8),
            assignedReviewerId: "mod-9");

        var startedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-4);
        var actionedAtUtc = DateTimeOffset.UtcNow;

        using var collector = new MetricCollector(meterName);
        using var metrics = new ModerationMetricsService(_ => Task.FromResult(ModerationTelemetryDimensions.Empty), meterName);

        metrics.TrackProcessingCompletion(
            item,
            action: "approve",
            moderationStatus: ModerationTelemetryDimensions.Approved,
            startedAtUtc: startedAtUtc,
            actionedAtUtc: actionedAtUtc,
            actorId: "mod-9");

        Assert.Equal(1, collector.SumLong(
            ModerationMetricNames.ModerationItemsProcessedTotal,
            ("risk_level", "restricted"),
            ("moderation_status", ModerationTelemetryDimensions.Approved),
            ("action", "approve"),
            ("actor_type", "reviewer")));

        Assert.InRange(
            collector.MaxDouble(
                ModerationMetricNames.ModerationProcessingTimeMs,
                ("risk_level", "restricted"),
                ("moderation_status", ModerationTelemetryDimensions.Approved),
                ("action", "approve")),
            239000d,
            241000d);
    }

    [Fact]
    public async Task ModerationQueueService_RefreshesBacklogAfterQueueMutations()
    {
        var queue = new InMemoryModerationQueue();
        var auditService = new ModerationAuditService(new InMemoryModerationAuditRepository());
        var telemetry = new RecordingModerationTelemetry();
        var service = new ModerationQueueService(queue, auditService, telemetry);

        var pending = await service.EnqueueCandidateAsync(
            BuildCandidate("cand-metrics", 0.71),
            BuildRisk(68, EventRiskLevel.High),
            BuildAssessment(DuplicateAssessmentLevel.ProbableDuplicate, 0.73));

        await service.AssignReviewerAsync(pending.ItemId, "mod-1", "lead-mod");
        await service.UpdateStatusAsync(pending.ItemId, ModerationStatus.NeedsEdit, "lead-mod", "needs fixes");

        Assert.Equal(3, telemetry.RefreshCalls);
        Assert.Single(telemetry.CompletedActions);
        Assert.Equal("request-changes", telemetry.CompletedActions[0].Action);
        Assert.Equal(ModerationTelemetryDimensions.NeedsEdit, telemetry.CompletedActions[0].ModerationStatus);
    }

    [Fact]
    public async Task ModerationActionService_TracksCompletedReviewerActions()
    {
        var queue = new InMemoryModerationQueue();
        var audit = new InMemoryAuditTrail();
        var moderationAudit = new ModerationAuditService(new InMemoryModerationAuditRepository());
        var roleRepo = new InMemoryUserRoleRepository();
        await roleRepo.SetRolesAsync("mod-1", [UserRoles.Moderator]);
        var telemetry = new RecordingModerationTelemetry();

        var service = new ModerationActionService(
            queue,
            audit,
            moderationAudit,
            new UserRoleResolver(roleRepo),
            [new ApproveHandler(new PublishEligibilityService(new ConfidenceScoringService(), new GeoValidationService())), new RejectHandler()],
            telemetry);

        var itemId = await queue.AddItemAsync(BuildActionItem());

        var result = await service.RejectAsync(itemId, "mod-1", rejectReason: "duplicate", note: "unsafe");

        Assert.True(result.Success);
        Assert.Single(telemetry.CompletedActions);
        Assert.Equal("reject", telemetry.CompletedActions[0].Action);
        Assert.Equal(ModerationTelemetryDimensions.Rejected, telemetry.CompletedActions[0].ModerationStatus);
        Assert.Equal(1, telemetry.RefreshCalls);
    }

    private static WeUP.Domain.Moderation.ModerationQueueItem BuildQueueItem(
        EventRiskLevel riskLevel,
        ModerationItemStatus itemStatus,
        string lastAction,
        DateTimeOffset createdAtUtc,
        string? assignedReviewerId = null)
    {
        var item = BuildActionItem(createdAtUtc, assignedReviewerId);
        item.Status = itemStatus;
        item.ReviewReasons =
        [
            $"risk-level:{riskLevel}",
        ];
        item.AppendHistory(new ReviewHistoryEntry(
            Guid.NewGuid().ToString("N"),
            lastAction,
            assignedReviewerId ?? "system",
            null,
            ModerationItemStatus.Open,
            itemStatus,
            createdAtUtc));
        return item;
    }

    private static WeUP.Domain.Moderation.ModerationQueueItem BuildActionItem(
        DateTimeOffset? createdAtUtc = null,
        string? assignedReviewerId = null)
    {
        var created = createdAtUtc ?? DateTimeOffset.UtcNow.AddMinutes(-10);
        return new WeUP.Domain.Moderation.ModerationQueueItem
        {
            ItemId = Guid.NewGuid().ToString("N"),
            Kind = ModerationItemKind.CandidateReview,
            Status = ModerationItemStatus.Open,
            Candidate = new CandidateSnapshotDto(
                Title: "Candidate",
                VenueName: "Echo Hall",
                Address: "123 Main",
                StartUtc: "2026-08-01T18:00:00Z",
                EndUtc: null,
                Timezone: "America/Chicago",
                Category: "music",
                Description: "desc",
                Tags: ["house"],
                SourceKind: "flyer_upload",
                SourceRef: "cand-1"),
            Provenance = new ProvenanceSummaryDto(
                SourceKind: "flyer_upload",
                SourceRef: "cand-1",
                IngestionJobId: "job-1",
                EvidenceRefs: ["evidence-1"],
                SubmittedAt: created.ToString("O")),
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
            AssignedReviewerId = assignedReviewerId,
            CreatedAt = created,
            UpdatedAt = created,
        };
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

    private sealed class RecordingModerationTelemetry : IModerationTelemetry
    {
        public int RefreshCalls { get; private set; }

        public List<(string Action, string ModerationStatus, string ActorId)> CompletedActions { get; } = [];

        public Task RefreshBacklogAsync(CancellationToken ct = default)
        {
            _ = ct;
            RefreshCalls++;
            return Task.CompletedTask;
        }

        public void TrackProcessingCompletion(
            WeUP.Domain.Moderation.ModerationQueueItem item,
            string action,
            string moderationStatus,
            DateTimeOffset startedAtUtc,
            DateTimeOffset actionedAtUtc,
            string actorId)
        {
            _ = item;
            _ = startedAtUtc;
            _ = actionedAtUtc;
            CompletedActions.Add((action, moderationStatus, actorId));
        }
    }

    private sealed class MetricCollector : IDisposable
    {
        private readonly object _gate = new();
        private readonly List<CapturedLongMeasurement> _longMeasurements = [];
        private readonly List<CapturedDoubleMeasurement> _doubleMeasurements = [];
        private readonly MeterListener _listener;

        public MetricCollector(string meterName)
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
                lock (_gate)
                {
                    _longMeasurements.Add(new CapturedLongMeasurement(instrument.Name, measurement, CaptureTags(tags)));
                }
            });
            _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            {
                lock (_gate)
                {
                    _doubleMeasurements.Add(new CapturedDoubleMeasurement(instrument.Name, measurement, CaptureTags(tags)));
                }
            });
            _listener.Start();
        }

        public void RecordObservableInstruments() => _listener.RecordObservableInstruments();

        public long SumLong(string instrumentName, params (string Key, string Value)[] requiredTags)
        {
            lock (_gate)
            {
                return _longMeasurements
                    .Where(measurement => measurement.Name == instrumentName)
                    .Where(measurement => HasTags(measurement.Tags, requiredTags))
                    .Sum(measurement => measurement.Value);
            }
        }

        public double MaxDouble(string instrumentName, params (string Key, string Value)[] requiredTags)
        {
            lock (_gate)
            {
                return _doubleMeasurements
                    .Where(measurement => measurement.Name == instrumentName)
                    .Where(measurement => HasTags(measurement.Tags, requiredTags))
                    .Select(measurement => measurement.Value)
                    .DefaultIfEmpty(0d)
                    .Max();
            }
        }

        public void Dispose() => _listener.Dispose();

        private static bool HasTags(IReadOnlyDictionary<string, string?> tags, (string Key, string Value)[] requiredTags)
        {
            return requiredTags.All(tag => tags.TryGetValue(tag.Key, out var value) && value == tag.Value);
        }

        private static IReadOnlyDictionary<string, string?> CaptureTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var captured = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                captured[tag.Key] = tag.Value?.ToString();
            }

            return captured;
        }

        private sealed record CapturedLongMeasurement(string Name, long Value, IReadOnlyDictionary<string, string?> Tags);

        private sealed record CapturedDoubleMeasurement(string Name, double Value, IReadOnlyDictionary<string, string?> Tags);
    }
}