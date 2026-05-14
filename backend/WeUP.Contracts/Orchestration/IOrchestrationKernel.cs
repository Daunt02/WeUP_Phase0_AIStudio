using System;
using System.Threading;
using System.Threading.Tasks;
using WeUP.Contracts.Ingestion;

namespace WeUP.Contracts.Orchestration;

/// <summary>
/// The central execution kernel interface that guarantees all workflows follow the deterministic DAG.
/// No domain logic executes outside of the boundaries enforced by this Kernel.
/// </summary>
public interface IOrchestrationKernel
{
    /// <summary>
    /// Starts or resumes a workflow. 
    /// Ensures idempotency based on the provided key.
    /// </summary>
    /// <param name="idempotencyKey">The unique key identifying this execution request to prevent duplication.</param>
    /// <param name="initialRequest">The starting payload for the DAG.</param>
    /// <param name="cancellationToken">Cancellation token to observe.</param>
    /// <returns>A full execution trace of the workflow.</returns>
    Task<ExecutionTrace> ExecuteWorkflowAsync(Guid idempotencyKey, IngestionRequest initialRequest, CancellationToken cancellationToken = default);
}
