using System;
using System.Threading.Tasks;
using ContractDedupe = WeUP.Contracts.Dedupe;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Events;

namespace WeUP.Domain.Dedupe;

/// <summary>
/// Async merge-plan generation extension for contract-driven merge planning.
/// </summary>
public partial interface IMergePlanner
{
    /// <summary>
    /// Generate a deterministic merge plan for a candidate/canonical pair.
    /// </summary>
    Task<ContractDedupe.MergePlan> GeneratePlanAsync(
        ContractDedupe.DuplicateAssessment assessment,
        CandidateEvent candidate,
        EventAggregate canonical);
}
