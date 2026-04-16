using WeUP.Contracts.Ingestion;

namespace WeUP.Domain.Ingestion;

public interface IIngestionOrchestrator
{
    Task<IngestionJob> StartAsync(IngestionRequest request, CancellationToken ct = default);
    Task<IngestionJob?> GetAsync(string jobId, CancellationToken ct = default);
}

public interface IIngestionOrchestrationRepository
{
    Task<IngestionJob> CreateAsync(IngestionJob job, CancellationToken ct = default);
    Task SaveAsync(IngestionJob job, CancellationToken ct = default);
    Task<IngestionJob?> GetAsync(string jobId, CancellationToken ct = default);
}

public interface IIngestionLifecycleObserver
{
    Task ObserveAsync(
        string hook,
        IngestionJob job,
        IReadOnlyDictionary<string, string?> properties,
        CancellationToken ct = default);
}
