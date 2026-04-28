using WeUP.Contracts.Ingestion;

namespace WeUP.Domain.Ingestion;

/// <summary>
/// Resolves a concrete <see cref="ISourceAdapter"/> based on <see cref="IngestionSourceType"/>.
/// </summary>
public static class SourceAdapterFactory
{
    public static ISourceAdapter Create(IngestionSourceType type, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var adapters = services.GetService(typeof(IEnumerable<ISourceAdapter>)) as IEnumerable<ISourceAdapter>;
        var adapter = adapters?.FirstOrDefault(candidate => candidate.SourceType == type);

        return adapter ?? throw new NotSupportedException($"Unsupported source type: {type}");
    }
}
