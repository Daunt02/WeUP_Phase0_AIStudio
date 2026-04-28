using System.Diagnostics.Metrics;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;

namespace WeUP.Infrastructure.Ingestion;

/// <summary>
/// Implements <see cref="IIngestionLifecycleObserver"/> by translating lifecycle
/// hook events into OpenTelemetry counter and histogram increments.
///
/// Hook → metric mapping:
///   job_started           → ingestion_requests_total++
///   job_duration          → ingestion_jobs_completed_total++, ingestion_job_duration_ms.Record(ms)
///   job_failed            → ingestion_jobs_failed_total++,    ingestion_job_duration_ms.Record(ms)
///
/// The observer deliberately ignores intermediate hooks (ocr_completed,
/// normalization_completed) because those do not represent terminal job outcomes.
/// Duration is always emitted at terminal transition — never from partial stages.
///
/// Tag policy:
///   sourceType   — IngestionSourceType enum value (flyer_image, manual_entry, ...)
///   status       — explicit lifecycle outcome (started | completed | failed)
///   market       — taken from job.Metadata["market"] if present, "unknown" otherwise
/// </summary>
public sealed class MetricsIngestionLifecycleObserver(IngestionMetricsService metrics) : IIngestionLifecycleObserver
{
    public Task ObserveAsync(
        string hook,
        IngestionJob job,
        IReadOnlyDictionary<string, string?> properties,
        CancellationToken ct = default)
    {
        var sourceType = job.SourceType.ToString();
        var market = ResolveMarket(job.Metadata);

        switch (hook)
        {
            case "job_started":
                // One increment per accepted request — before any processing begins.
                metrics.RequestsTotal.Add(1,
                    new KeyValuePair<string, object?>("sourceType", sourceType),
                    new KeyValuePair<string, object?>("status", "started"),
                    new KeyValuePair<string, object?>("market", market));
                break;

            case "job_duration":
                // Terminal success path: emit completed counter + full pipeline duration.
                var durationMs = ParseDurationMs(properties);

                metrics.JobsCompletedTotal.Add(1,
                    new KeyValuePair<string, object?>("sourceType", sourceType),
                    new KeyValuePair<string, object?>("status", "completed"),
                    new KeyValuePair<string, object?>("market", market));

                metrics.JobDurationMs.Record(durationMs,
                    new KeyValuePair<string, object?>("sourceType", sourceType),
                    new KeyValuePair<string, object?>("status", "completed"),
                    new KeyValuePair<string, object?>("market", market));
                break;

            case "job_failed":
                // Terminal failure path: emit failed counter + full pipeline duration.
                var failedDurationMs = ComputeDurationMs(job);

                metrics.JobsFailedTotal.Add(1,
                    new KeyValuePair<string, object?>("sourceType", sourceType),
                    new KeyValuePair<string, object?>("status", "failed"),
                    new KeyValuePair<string, object?>("market", market));

                metrics.JobDurationMs.Record(failedDurationMs,
                    new KeyValuePair<string, object?>("sourceType", sourceType),
                    new KeyValuePair<string, object?>("status", "failed"),
                    new KeyValuePair<string, object?>("market", market));
                break;
        }

        return Task.CompletedTask;
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the market tag from job metadata.
    /// Falls back to "unknown" so metric cardinality stays bounded even when
    /// the caller omits the market key.
    /// </summary>
    private static string ResolveMarket(IReadOnlyDictionary<string, string?> metadata)
    {
        if (metadata.TryGetValue("market", out var market) && !string.IsNullOrWhiteSpace(market))
            return market;
        return "unknown";
    }

    /// <summary>
    /// Reads the pre-computed duration_ms property emitted by the orchestrator's
    /// job_duration hook. Returns 0 when absent (guards against missing properties).
    /// </summary>
    private static long ParseDurationMs(IReadOnlyDictionary<string, string?> properties)
    {
        if (properties.TryGetValue("duration_ms", out var raw) &&
            raw is not null &&
            long.TryParse(raw, out var ms))
        {
            return Math.Max(0, ms);
        }

        return 0;
    }

    /// <summary>
    /// Computes elapsed time for jobs that failed before the orchestrator could
    /// emit a duration_ms property (e.g. adapter failures before normalization).
    /// </summary>
    private static long ComputeDurationMs(IngestionJob job)
        => (long)Math.Max(0, (job.UpdatedAtUtc - job.CreatedAtUtc).TotalMilliseconds);
}
