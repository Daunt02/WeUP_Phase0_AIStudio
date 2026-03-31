using WeUP.Contracts.Ingestion;

namespace WeUP.Domain.Ingestion;

/// <summary>
/// All ingestion sources implement this interface.
/// Adapters produce a NormalizedEventCandidate for pipeline processing.
/// </summary>
public interface IIngestionAdapter
{
    IngestionSourceKind SourceKind { get; }
    Task<NormalizedEventCandidate> ExtractAsync(object request, CancellationToken ct = default);
}

/// <summary>
/// Routes incoming requests to the correct adapter.
/// </summary>
public interface IIngestionDispatcher
{
    Task<string> DispatchManualAsync(ManualIngestionRequest request, CancellationToken ct = default);
    Task<string> DispatchLinkAsync(LinkIngestionRequest request, CancellationToken ct = default);
    Task<string> DispatchVenuePageAsync(VenuePageIngestionRequest request, CancellationToken ct = default);
}

/// <summary>
/// Durable ingestion job store.
/// </summary>
public interface IIngestionJobRepository
{
    Task<string> CreateJobAsync(IngestionSourceKind kind, string sourceRef, CancellationToken ct = default);
    Task UpdateStatusAsync(string jobId, IngestionJobStatus status, string? failureReason = null, CancellationToken ct = default);
    Task SetCandidateAsync(string jobId, string candidateEventId, CancellationToken ct = default);
    Task<IngestionJobResponse?> GetJobAsync(string jobId, CancellationToken ct = default);
}

/// <summary>
/// Normalizes raw extraction outputs into NormalizedEventCandidate.
/// </summary>
public interface IExtractionNormalizer
{
    Task<NormalizedEventCandidate> NormalizeAsync(string rawPayload, string sourceKind, string sourceRef, CancellationToken ct = default);
}

/// <summary>
/// Writes audit records for ingestion pipeline steps.
/// </summary>
public interface IIngestionAuditWriter
{
    Task WriteAsync(string jobId, string stage, string? detail, CancellationToken ct = default);
}
