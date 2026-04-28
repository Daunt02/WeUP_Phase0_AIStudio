using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WeUP.Domain.Moderation;

namespace WeUP.Infrastructure.Moderation;

/// <summary>
/// Metric names and alert-ready query examples for moderation throughput and backlog health (M10-P49).
///
/// Health interpretation:
/// - moderation_queue_size shows total queue depth by risk tier and workflow state.
/// - moderation_items_pending_total isolates actionable backlog so alerts do not rely on UI inspection.
/// - moderation_backlog_age_ms shows the oldest waiting work; rising age with flat throughput indicates starvation.
/// - moderation_processing_time_ms measures actual moderator or system handling time from the current work stage.
/// </summary>
public static class ModerationMetricNames
{
    public const string ModerationQueueSize = "moderation_queue_size";
    public const string ModerationItemsProcessedTotal = "moderation_items_processed_total";
    public const string ModerationItemsPendingTotal = "moderation_items_pending_total";
    public const string ModerationProcessingTimeMs = "moderation_processing_time_ms";
    public const string ModerationBacklogAgeMs = "moderation_backlog_age_ms";
}

public static class ModerationDashboardQueries
{
    public const string HighRiskBacklog5m =
        "max_over_time(moderation_items_pending_total{risk_level=~\"high|restricted\"}[5m])";

    public const string P95ProcessingLatency15m =
        "histogram_quantile(0.95, sum(rate(moderation_processing_time_ms_bucket[15m])) by (le, risk_level))";

    public const string OldestBacklogAge10m =
        "max_over_time(moderation_backlog_age_ms{moderation_status=~\"pending|in_review|needs_edit\"}[10m])";

    public const string Throughput5m =
        "sum(rate(moderation_items_processed_total[5m])) by (risk_level, moderation_status)";
}

public static class ModerationAlertExamples
{
    public const string BacklogThreshold =
        "Alert when moderation_items_pending_total{risk_level=~\"high|restricted\"} > 25 for 10m.";

    public const string ProcessingLatencyThreshold =
        "Alert when histogram_quantile(0.95, sum(rate(moderation_processing_time_ms_bucket[15m])) by (le)) > 300000.";
}

public sealed class ModerationMetricsService : IModerationTelemetry, IDisposable
{
    private readonly Meter _meter;
    private readonly Func<CancellationToken, Task<ModerationQueueTelemetrySnapshot>> _snapshotLoader;

    private ModerationQueueTelemetrySnapshot _snapshot = ModerationTelemetryDimensions.Empty;

    private readonly Counter<long> _itemsProcessedTotal;
    private readonly Histogram<double> _processingTimeMs;

    private readonly ObservableGauge<long> _queueSize;
    private readonly ObservableGauge<long> _pendingTotal;
    private readonly ObservableGauge<double> _backlogAgeMs;

    public ModerationMetricsService(IServiceScopeFactory scopeFactory, string meterName = "WeUP.Moderation")
        : this(
            async ct =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<IModerationQueueRepository>();
                return await repository.GetTelemetrySnapshotAsync(ct);
            },
            meterName)
    {
    }

    public ModerationMetricsService(
        Func<CancellationToken, Task<ModerationQueueTelemetrySnapshot>> snapshotLoader,
        string meterName = "WeUP.Moderation")
    {
        ArgumentNullException.ThrowIfNull(snapshotLoader);

        _snapshotLoader = snapshotLoader;
        _meter = new Meter(meterName, "1.0");

        _itemsProcessedTotal = _meter.CreateCounter<long>(
            name: ModerationMetricNames.ModerationItemsProcessedTotal,
            unit: "{item}",
            description: "Completed moderator or system actions by risk tier and resulting moderation status.");

        _processingTimeMs = _meter.CreateHistogram<double>(
            name: ModerationMetricNames.ModerationProcessingTimeMs,
            unit: "ms",
            description: "Elapsed handling time from the current moderation stage to the actual action timestamp.");

        _queueSize = _meter.CreateObservableGauge(
            name: ModerationMetricNames.ModerationQueueSize,
            observeValues: ObserveQueueSize,
            unit: "{item}",
            description: "Current moderation queue depth by risk tier and workflow status.");

        _pendingTotal = _meter.CreateObservableGauge(
            name: ModerationMetricNames.ModerationItemsPendingTotal,
            observeValues: ObservePendingTotals,
            unit: "{item}",
            description: "Current actionable moderation backlog by risk tier and workflow status.");

        _backlogAgeMs = _meter.CreateObservableGauge(
            name: ModerationMetricNames.ModerationBacklogAgeMs,
            observeValues: ObserveBacklogAge,
            unit: "ms",
            description: "Age of the oldest waiting moderation item by risk tier and workflow status.");
    }

    public async Task RefreshBacklogAsync(CancellationToken ct = default)
    {
        _snapshot = await _snapshotLoader(ct);
    }

    public void TrackProcessingCompletion(
        ModerationQueueItem item,
        string action,
        string moderationStatus,
        DateTimeOffset startedAtUtc,
        DateTimeOffset actionedAtUtc,
        string actorId)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(moderationStatus);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        var elapsedMs = Math.Max(0d, (actionedAtUtc - startedAtUtc).TotalMilliseconds);
        var tags = new KeyValuePair<string, object?>[]
        {
            new("risk_level", ModerationTelemetryDimensions.ResolveRiskLevel(item)),
            new("moderation_status", moderationStatus),
            new("action", action),
            new("actor_type", string.Equals(actorId, "system", StringComparison.OrdinalIgnoreCase) ? "system" : "reviewer"),
        };

        _itemsProcessedTotal.Add(1, tags);
        _processingTimeMs.Record(elapsedMs, tags);
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    private IEnumerable<Measurement<long>> ObserveQueueSize()
    {
        foreach (var sample in _snapshot.QueueSize)
        {
            yield return new Measurement<long>(sample.Count, BuildTags(sample.RiskLevel, sample.ModerationStatus));
        }
    }

    private IEnumerable<Measurement<long>> ObservePendingTotals()
    {
        foreach (var sample in _snapshot.PendingTotals)
        {
            yield return new Measurement<long>(sample.Count, BuildTags(sample.RiskLevel, sample.ModerationStatus));
        }
    }

    private IEnumerable<Measurement<double>> ObserveBacklogAge()
    {
        foreach (var sample in _snapshot.BacklogAge)
        {
            yield return new Measurement<double>(sample.AgeMs, BuildTags(sample.RiskLevel, sample.ModerationStatus));
        }
    }

    private static KeyValuePair<string, object?>[] BuildTags(string riskLevel, string moderationStatus)
    {
        return
        [
            new("risk_level", riskLevel),
            new("moderation_status", moderationStatus),
        ];
    }
}

public sealed class ModerationMetricsWarmupService(IModerationTelemetry telemetry) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => telemetry.RefreshBacklogAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.CompletedTask;
    }
}