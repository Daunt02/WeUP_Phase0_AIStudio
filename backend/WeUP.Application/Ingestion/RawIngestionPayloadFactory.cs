using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;

namespace WeUP.Application.Ingestion;

/// <summary>
/// Centralized adapter resolver with fail-fast behavior.
/// No input bypasses this boundary when this factory is used.
/// </summary>
public sealed class SourceAdapterResolver(IEnumerable<ISourceAdapter> adapters) : ISourceAdapterResolver
{
    private readonly IReadOnlyDictionary<IngestionSourceType, ISourceAdapter> _adapters =
        adapters.ToDictionary(adapter => adapter.SourceType);

    public ISourceAdapter Resolve(IngestionSourceType sourceType)
    {
        if (_adapters.TryGetValue(sourceType, out var adapter))
        {
            return adapter;
        }

        throw new IngestionValidationException($"No source adapter is registered for source type '{sourceType}'.");
    }
}

/// <summary>
/// Produces canonical payloads and enforces deterministic fail-fast adaptation.
/// </summary>
public sealed class RawIngestionPayloadFactory(ISourceAdapterResolver resolver) : IRawIngestionPayloadFactory
{
    public Task<RawIngestionPayload> CreateAsync(IngestionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var adapter = resolver.Resolve(request.SourceType);
        return adapter.AdaptAsync(request, ct);
    }
}
