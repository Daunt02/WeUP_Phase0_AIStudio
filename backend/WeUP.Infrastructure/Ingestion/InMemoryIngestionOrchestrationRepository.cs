using System.Collections.Concurrent;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;

namespace WeUP.Infrastructure.Ingestion;

public sealed class InMemoryIngestionOrchestrationRepository : IIngestionOrchestrationRepository
{
    private readonly ConcurrentDictionary<string, IngestionJob> _jobs = new(StringComparer.OrdinalIgnoreCase);

    public Task<IngestionJob> CreateAsync(IngestionJob job, CancellationToken ct = default)
    {
        _jobs[job.JobId] = job;
        return Task.FromResult(job);
    }

    public Task SaveAsync(IngestionJob job, CancellationToken ct = default)
    {
        _jobs[job.JobId] = job;
        return Task.CompletedTask;
    }

    public Task<IngestionJob?> GetAsync(string jobId, CancellationToken ct = default)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }
}
