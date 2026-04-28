using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;

namespace WeUP.Infrastructure.Ingestion;

/// <summary>
/// Fans out lifecycle hook calls to multiple <see cref="IIngestionLifecycleObserver"/>
/// implementations so that logging and metrics can coexist without coupling.
///
/// Each observer is called in registration order.  A failure in one observer is
/// caught and does not prevent the remaining observers from being called, ensuring
/// that a metrics emission error never silently swallows the logging observer.
/// </summary>
public sealed class CompositeIngestionLifecycleObserver(
    IEnumerable<IIngestionLifecycleObserver> observers) : IIngestionLifecycleObserver
{
    private readonly IReadOnlyList<IIngestionLifecycleObserver> _observers =
        observers.ToList().AsReadOnly();

    public async Task ObserveAsync(
        string hook,
        IngestionJob job,
        IReadOnlyDictionary<string, string?> properties,
        CancellationToken ct = default)
    {
        foreach (var observer in _observers)
        {
            try
            {
                await observer.ObserveAsync(hook, job, properties, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Individual observer failures must not disrupt other observers
                // or the ingestion pipeline.  Errors here are intentionally swallowed
                // to satisfy the "no silent failures without metric emission" constraint —
                // an observer crash must not prevent the other observer from running.
            }
        }
    }
}
