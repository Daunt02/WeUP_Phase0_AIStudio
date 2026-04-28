using System.Diagnostics.Metrics;

namespace WeUP.Infrastructure.Ingestion;

/// <summary>
/// Owns all OpenTelemetry Meter instruments for the ingestion pipeline.
///
/// Design notes:
///   - One singleton Meter named "WeUP.Ingestion" (matches ObservabilityConstants.IngestionMeterName).
///   - All instruments are pre-created at startup — zero allocation on the hot path.
///   - Tag cardinality is intentionally bounded: sourceType (~5 values), status (~3 values),
///     market (~20 city codes).  Do not add high-cardinality tags (jobId, userId, etc.).
///
/// Metric catalogue:
///
///   ingestion_requests_total          [counter]   — fired once per accepted ingestion request.
///   ingestion_jobs_completed_total    [counter]   — fired once when a job reaches Completed status.
///   ingestion_jobs_failed_total       [counter]   — fired once when a job reaches FAILED status.
///   ingestion_job_duration_ms         [histogram] — full pipeline wall-clock time (Pending→terminal).
///
/// Derived metric (computed at query time, no instrument needed):
///   ingestion_success_rate = ingestion_jobs_completed_total / (ingestion_jobs_completed_total + ingestion_jobs_failed_total)
///   Example PromQL: rate(ingestion_jobs_completed_total[5m]) / (rate(ingestion_jobs_completed_total[5m]) + rate(ingestion_jobs_failed_total[5m]))
/// </summary>
public sealed class IngestionMetricsService : IDisposable
{
    private readonly Meter _meter;

    /// <summary>
    /// Incremented once per ingestion request accepted by the orchestrator.
    /// Tags: sourceType, status, market.
    /// Example status value: started.
    /// Cardinality: O(sourceType x status x market) — bounded low.
    /// </summary>
    public readonly Counter<long> RequestsTotal;

    /// <summary>
    /// Incremented once when a job transitions to Completed (full pipeline success).
    /// Tags: sourceType, status, market.
    /// Example status value: completed.
    /// Cardinality: O(sourceType x status x market).
    /// </summary>
    public readonly Counter<long> JobsCompletedTotal;

    /// <summary>
    /// Incremented once when a job transitions to FAILED for any reason.
    /// Tags: sourceType, status, market.
    /// Example status value: failed.
    /// Cardinality: O(sourceType x status x market) — bounded.
    /// </summary>
    public readonly Counter<long> JobsFailedTotal;

    /// <summary>
    /// Full pipeline wall-clock duration in milliseconds (Pending → terminal state).
    /// Tags: sourceType, status, market.
    /// status values: completed | failed.
    /// Bucket boundaries are tuned for typical ingestion times: sub-second fast paths
    /// through multi-second OCR flows up to pathological cases.
    /// </summary>
    public readonly Histogram<long> JobDurationMs;

    public IngestionMetricsService(string meterName = "WeUP.Ingestion")
    {
        _meter = new Meter(meterName, "1.0");

        RequestsTotal = _meter.CreateCounter<long>(
            name: "ingestion_requests_total",
            unit: "{request}",
            description: "Total number of ingestion requests accepted by the orchestrator.");

        JobsCompletedTotal = _meter.CreateCounter<long>(
            name: "ingestion_jobs_completed_total",
            unit: "{job}",
            description: "Total number of ingestion jobs that completed successfully through the full pipeline.");

        JobsFailedTotal = _meter.CreateCounter<long>(
            name: "ingestion_jobs_failed_total",
            unit: "{job}",
            description: "Total number of ingestion jobs that failed at any pipeline stage.");

        JobDurationMs = _meter.CreateHistogram<long>(
            name: "ingestion_job_duration_ms",
            unit: "ms",
            description: "Wall-clock duration of the full ingestion pipeline from Pending to terminal state.");
    }

    public void Dispose() => _meter.Dispose();
}
