using WeUP.Contracts.Ingestion;

namespace WeUP.Domain.Ingestion;

public interface IEventSourceAdapter
{
    AdapterCapabilityDescriptor Capability { get; }
    bool CanHandle(IngestionSourceKind sourceKind);
    Task<CanonicalIngestionIssue[]> ValidateAsync(IngestionRequestEnvelope request, CancellationToken ct = default);
    Task<AdapterExecutionResult> ExecuteAsync(IngestionRequestEnvelope request, CancellationToken ct = default);
}

public interface IEventSourceAdapterResolver
{
    IEventSourceAdapter Resolve(IngestionSourceKind sourceKind);
}

public interface IIngestionCoordinator
{
    Task<IngestionResult> SubmitManualAsync(ManualIngestionRequest request, CancellationToken ct = default);
    Task<IngestionResult> SubmitUrlAsync(UrlIngestionRequest request, CancellationToken ct = default);
    Task<IngestionResult> SubmitVenuePageAsync(VenuePageIngestionRequest request, CancellationToken ct = default);
    Task<IngestionResult?> GetJobAsync(string jobId, CancellationToken ct = default);
}

public interface IIngestionJobRepository
{
    Task<IngestionResult> CreateAsync(IngestionRequestEnvelope request, CancellationToken ct = default);
    Task SaveAsync(IngestionResult result, CancellationToken ct = default);
    Task<IngestionResult?> GetAsync(string jobId, CancellationToken ct = default);
}

public interface IIngestionAuditWriter
{
    Task WriteAsync(string jobId, string stage, string? detail, CancellationToken ct = default);
}
