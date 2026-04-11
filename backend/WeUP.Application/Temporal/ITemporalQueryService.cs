using System.Threading.Tasks;
using WeUP.Contracts.Temporal;
using WeUP.Domain.Temporal;

namespace WeUP.Application.Temporal
{
    /// <summary>
    /// Service that resolves TemporalQuery contracts into canonical TimeWindow domain objects.
    /// Implementations should honor market-local timezone and deterministic semantics.
    /// </summary>
    public interface ITemporalQueryService
    {
        Task<TimeWindow> ResolveWindowAsync(TemporalQuery query);
    }
}
