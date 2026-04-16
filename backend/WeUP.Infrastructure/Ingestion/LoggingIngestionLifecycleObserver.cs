using Microsoft.Extensions.Logging;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;

namespace WeUP.Infrastructure.Ingestion;

public sealed class LoggingIngestionLifecycleObserver(ILogger<LoggingIngestionLifecycleObserver> logger) : IIngestionLifecycleObserver
{
    public Task ObserveAsync(
        string hook,
        IngestionJob job,
        IReadOnlyDictionary<string, string?> properties,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "{Hook} jobId={JobId} status={Status} props={@Properties}",
            hook,
            job.JobId,
            job.Status,
            properties);

        return Task.CompletedTask;
    }
}
