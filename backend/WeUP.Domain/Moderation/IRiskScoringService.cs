using System.Threading.Tasks;
using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Moderation;

namespace WeUP.Domain.Moderation;

/// <summary>
/// Pure, deterministic service that computes an <see cref="EventRiskScore"/> for a candidate.
/// No external I/O may be performed; all logic must be rule-based.
/// </summary>
public interface IRiskScoringService
{
    /// <summary>
    /// Evaluate the risk of the supplied <paramref name="candidate"/>.
    /// </summary>
    Task<EventRiskScore> ComputeRiskAsync(CandidateEvent candidate);
}
