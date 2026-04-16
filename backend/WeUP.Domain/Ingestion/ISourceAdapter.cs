using WeUP.Contracts.Ingestion;

namespace WeUP.Domain.Ingestion;

/// <summary>
/// Converts one raw external input into a deterministic canonical payload.
/// Adapters must throw for malformed requests and never apply silent fallback behavior.
/// </summary>
public interface ISourceAdapter
{
    IngestionSourceType SourceType { get; }

    Task<RawIngestionPayload> AdaptAsync(IngestionRequest request, CancellationToken ct = default);
}

/// <summary>
/// Resolves source adapters by source type without fallback.
/// </summary>
public interface ISourceAdapterResolver
{
    ISourceAdapter Resolve(IngestionSourceType sourceType);
}

/// <summary>
/// Single boundary entry point that guarantees every request passes through an adapter.
/// </summary>
public interface IRawIngestionPayloadFactory
{
    Task<RawIngestionPayload> CreateAsync(IngestionRequest request, CancellationToken ct = default);
}

/// <summary>
/// Thrown when a request fails deterministic source validation.
/// </summary>
public sealed class IngestionValidationException(string message) : InvalidOperationException(message);
