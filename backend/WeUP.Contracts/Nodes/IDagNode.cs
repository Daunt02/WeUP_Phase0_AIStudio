using System.Threading;
using System.Threading.Tasks;
using WeUP.Contracts.Orchestration;

namespace WeUP.Contracts.Nodes;

/// <summary>
/// Represents a generic execution node in the WeUP DAG.
/// No domain logic is allowed outside of this strict contract boundary.
/// </summary>
/// <typeparam name="TInput">The exact contract-derived schema expected by this node.</typeparam>
/// <typeparam name="TOutput">The exact contract-derived schema output by this node.</typeparam>
public interface IDagNode<TInput, TOutput>
{
    /// <summary>
    /// Gets the NodeType defining its position in the DAG.
    /// </summary>
    DagNodeType NodeType { get; }

    /// <summary>
    /// Executes the domain logic strictly defined for this node within the bounded context.
    /// </summary>
    Task<TOutput> ExecuteAsync(TInput input, WorkflowContext context, CancellationToken cancellationToken);
}
