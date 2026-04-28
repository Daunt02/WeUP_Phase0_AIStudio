using System.Diagnostics.Metrics;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Dedupe;

namespace WeUP.Infrastructure.Dedupe;

/// <summary>
/// Metric names for deduplication and merge outcome observability (M10-P48).
///
/// Design rules:
/// - Duplicate classification is always emitted with the exact assessment level.
/// - Merge outcome counters align with reviewer workload rather than vague "success".
/// - Conflict counters keep moderation-critical fields visible and also preserve all
///   edge-case conflict categories through merge_field_conflicts_total{field=...}.
/// </summary>
public static class DedupeMergeMetricNames
{
    public const string DedupAssessmentTotal = "dedup_assessment_total";
    public const string MergePlansCreatedTotal = "merge_plans_created_total";
    public const string MergeAutoApprovedTotal = "merge_auto_approved_total";
    public const string MergeRequiresReviewTotal = "merge_requires_review_total";
    public const string MergeRejectedTotal = "merge_rejected_total";
    public const string MergeFieldConflictsTotal = "merge_field_conflicts_total";
    public const string TitleConflicts = "title_conflicts";
    public const string TimeConflicts = "time_conflicts";
    public const string VenueConflicts = "venue_conflicts";
}

/// <summary>
/// Prometheus query examples for deduplication and merge observability.
///
/// Interpretation guidance:
/// - Duplicate rate above 0.30 for a sustained 15m window usually means upstream
///   replay, crawler loop, or repeated flyer submission behavior.
/// - Merge pressure above 0.20 means more than 1 in 5 plans require reviewer time;
///   moderation backlog growth becomes likely unless staffing or thresholds change.
/// - ExactDuplicate spikes with flat review pressure are usually healthy idempotency.
/// - Probable/PossibleDuplicate growth together with review pressure growth indicates
///   ambiguous ingestion and moderation queue load, not a healthy dedup gain.
/// </summary>
public static class DedupeMergeDashboardQueries
{
    public const string DuplicateRate5m =
        "sum(rate(dedup_assessment_total{level=~\"ExactDuplicate|ProbableDuplicate|PossibleDuplicate\"}[5m])) / clamp_min(sum(rate(dedup_assessment_total[5m])), 1e-9)";

    public const string ExactDuplicateRate5m =
        "sum(rate(dedup_assessment_total{level=\"ExactDuplicate\"}[5m])) / clamp_min(sum(rate(dedup_assessment_total[5m])), 1e-9)";

    public const string MergePressure5m =
        "sum(rate(merge_requires_review_total[5m])) / clamp_min(sum(rate(merge_plans_created_total[5m])), 1e-9)";

    public const string MergeRejectRate5m =
        "sum(rate(merge_rejected_total[5m])) / clamp_min(sum(rate(merge_plans_created_total[5m])), 1e-9)";

    public const string TitleConflictRate5m = "sum(rate(title_conflicts[5m]))";
    public const string TimeConflictRate5m = "sum(rate(time_conflicts[5m]))";
    public const string VenueConflictRate5m = "sum(rate(venue_conflicts[5m]))";
}

/// <summary>
/// Deterministic reduction of merge conflicts into metric-ready field counts.
///
/// The model keeps title/time/venue explicit for moderation load tracking while the
/// FieldCounts map exposes every conflict family so edge cases do not disappear.
/// </summary>
public sealed record MergeConflictTelemetryModel(
    IReadOnlyDictionary<string, int> FieldCounts,
    int TitleConflictCount,
    int TimeConflictCount,
    int VenueConflictCount)
{
    public static MergeConflictTelemetryModel FromPlan(MergePlanDetail plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var fieldCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var titleConflictCount = 0;
        var timeConflictCount = 0;
        var venueConflictCount = 0;

        foreach (var conflict in plan.Conflicts)
        {
            var field = MapField(conflict);
            fieldCounts[field] = fieldCounts.TryGetValue(field, out var count) ? count + 1 : 1;

            switch (field)
            {
                case "title":
                    titleConflictCount++;
                    break;
                case "time":
                    timeConflictCount++;
                    break;
                case "venue":
                    venueConflictCount++;
                    break;
            }
        }

        return new MergeConflictTelemetryModel(fieldCounts, titleConflictCount, timeConflictCount, venueConflictCount);
    }

    private static string MapField(ConflictDescriptor conflict)
    {
        return conflict.Type switch
        {
            ConflictType.TitleDivergence => "title",
            ConflictType.VenueDivergence => "venue",
            ConflictType.TemporalDeviation or ConflictType.EndTemporalDeviation or ConflictType.TimezoneConflict => "time",
            ConflictType.GeoDeviation => "geo",
            ConflictType.LocationConflict => "address",
            ConflictType.EvidenceGap => "evidence",
            ConflictType.SourceIdentityConflict => "source",
            ConflictType.ApprovedEventMutation => "approval",
            ConflictType.CategoryMismatch => "category",
            ConflictType.AmbiguousSafetySignal => "ambiguous",
            _ => NormalizeFieldName(conflict.FieldName),
        };
    }

    private static string NormalizeFieldName(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return "unknown";
        }

        return fieldName.Trim().ToLowerInvariant() switch
        {
            "title" => "title",
            "venuename" => "venue",
            "venue" => "venue",
            "startutc" => "time",
            "endutc" => "time",
            _ => fieldName.Trim().ToLowerInvariant(),
        };
    }
}

/// <summary>
/// Owns deduplication and merge OpenTelemetry counters for production ingestion routing.
/// </summary>
public sealed class DedupeMergeMetricsService : IDisposable
{
    private readonly Meter _meter;

    public readonly Counter<long> DedupAssessmentTotal;
    public readonly Counter<long> MergePlansCreatedTotal;
    public readonly Counter<long> MergeAutoApprovedTotal;
    public readonly Counter<long> MergeRequiresReviewTotal;
    public readonly Counter<long> MergeRejectedTotal;
    public readonly Counter<long> MergeFieldConflictsTotal;
    public readonly Counter<long> TitleConflicts;
    public readonly Counter<long> TimeConflicts;
    public readonly Counter<long> VenueConflicts;

    public DedupeMergeMetricsService(string meterName = "WeUP.DedupeMerge")
    {
        _meter = new Meter(meterName, "1.0");

        DedupAssessmentTotal = _meter.CreateCounter<long>(
            name: DedupeMergeMetricNames.DedupAssessmentTotal,
            unit: "{assessment}",
            description: "Duplicate assessments by exact classification level and merge safety.");

        MergePlansCreatedTotal = _meter.CreateCounter<long>(
            name: DedupeMergeMetricNames.MergePlansCreatedTotal,
            unit: "{plan}",
            description: "Count of merge plans created from deduplication assessments.");

        MergeAutoApprovedTotal = _meter.CreateCounter<long>(
            name: DedupeMergeMetricNames.MergeAutoApprovedTotal,
            unit: "{plan}",
            description: "Merge plans that were fully safe for automatic execution.");

        MergeRequiresReviewTotal = _meter.CreateCounter<long>(
            name: DedupeMergeMetricNames.MergeRequiresReviewTotal,
            unit: "{plan}",
            description: "Merge plans that add pressure to the moderation review queue.");

        MergeRejectedTotal = _meter.CreateCounter<long>(
            name: DedupeMergeMetricNames.MergeRejectedTotal,
            unit: "{plan}",
            description: "Merge plans rejected as unsafe and not eligible for auto-merge or review execution.");

        MergeFieldConflictsTotal = _meter.CreateCounter<long>(
            name: DedupeMergeMetricNames.MergeFieldConflictsTotal,
            unit: "{conflict}",
            description: "All merge conflicts broken down by field family so edge cases remain visible.");

        TitleConflicts = _meter.CreateCounter<long>(
            name: DedupeMergeMetricNames.TitleConflicts,
            unit: "{conflict}",
            description: "Title conflict frequency driving moderation queue load.");

        TimeConflicts = _meter.CreateCounter<long>(
            name: DedupeMergeMetricNames.TimeConflicts,
            unit: "{conflict}",
            description: "Time conflict frequency driving moderation queue load.");

        VenueConflicts = _meter.CreateCounter<long>(
            name: DedupeMergeMetricNames.VenueConflicts,
            unit: "{conflict}",
            description: "Venue conflict frequency driving moderation queue load.");
    }

    public void TrackDedupAssessment(DuplicateAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        DedupAssessmentTotal.Add(
            1,
            new KeyValuePair<string, object?>("level", assessment.Level.ToString()),
            new KeyValuePair<string, object?>("auto_merge_allowed", assessment.AutoMergeAllowed ? "true" : "false"),
            new KeyValuePair<string, object?>("has_blockers", assessment.SafetyVerdict.ActiveBlockers.Length > 0 ? "true" : "false"));
    }

    public void TrackMergePlan(MergePlanDetail plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        MergePlansCreatedTotal.Add(1);

        if (plan.RejectMerge)
        {
            MergeRejectedTotal.Add(1);
        }
        else if (plan.AutoMergeAllowed && !plan.RequiresManualReview)
        {
            MergeAutoApprovedTotal.Add(1);
        }
        else
        {
            // Review pressure is the queue-facing signal: any non-rejected plan that cannot
            // run cleanly automatically should be treated as reviewer workload.
            MergeRequiresReviewTotal.Add(1);
        }

        var conflicts = MergeConflictTelemetryModel.FromPlan(plan);
        foreach (var entry in conflicts.FieldCounts)
        {
            MergeFieldConflictsTotal.Add(entry.Value, new KeyValuePair<string, object?>("field", entry.Key));
        }

        if (conflicts.TitleConflictCount > 0)
        {
            TitleConflicts.Add(conflicts.TitleConflictCount);
        }

        if (conflicts.TimeConflictCount > 0)
        {
            TimeConflicts.Add(conflicts.TimeConflictCount);
        }

        if (conflicts.VenueConflictCount > 0)
        {
            VenueConflicts.Add(conflicts.VenueConflictCount);
        }
    }

    public void Dispose() => _meter.Dispose();
}

/// <summary>
/// Decorator that preserves pure deduplication logic while emitting system-level telemetry.
/// </summary>
public sealed class InstrumentedDeduplicationStrategy : IDeduplicationStrategy
{
    private readonly IDeduplicationStrategy _inner;
    private readonly DedupeMergeMetricsService _metrics;

    public InstrumentedDeduplicationStrategy(IDeduplicationStrategy inner, DedupeMergeMetricsService metrics)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    public DuplicateAssessment Assess(NormalizedEventCandidate incoming, EventAggregateSnapshot canonical)
    {
        var assessment = _inner.Assess(incoming, canonical);
        _metrics.TrackDedupAssessment(assessment);
        return assessment;
    }
}

/// <summary>
/// Decorator that preserves pure merge planning logic while emitting outcome telemetry.
/// </summary>
public sealed class InstrumentedMergePlanner : IMergePlanner
{
    private readonly IMergePlanner _inner;
    private readonly DedupeMergeMetricsService _metrics;

    public InstrumentedMergePlanner(IMergePlanner inner, DedupeMergeMetricsService metrics)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    public MergePlanDetail CreatePlan(
        NormalizedEventCandidate incoming,
        EventAggregateSnapshot canonical,
        DuplicateAssessment assessment)
    {
        var plan = _inner.CreatePlan(incoming, canonical, assessment);
        _metrics.TrackMergePlan(plan);
        return plan;
    }
}