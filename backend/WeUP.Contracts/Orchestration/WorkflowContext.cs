using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WeUP.Contracts.Orchestration;

/// <summary>
/// A state wrapper that flows through the DAG execution pipeline.
/// Carries the current payload and execution state.
/// </summary>
public sealed class WorkflowContext
{
    public Guid IdempotencyKey { get; }
    
    // The current state of the payload (could be raw IngestionRequest, ModeratedRequest, etc.)
    public object CurrentPayload { get; private set; }

    private readonly ConcurrentDictionary<DagNodeType, NodeExecutionRecord> _history = new();
    
    public WorkflowContext(Guid idempotencyKey, object initialPayload)
    {
        IdempotencyKey = idempotencyKey;
        CurrentPayload = initialPayload ?? throw new ArgumentNullException(nameof(initialPayload));
    }

    /// <summary>
    /// Updates the workflow payload state after a node completes.
    /// </summary>
    public void UpdatePayload(object newPayload)
    {
        CurrentPayload = newPayload ?? throw new ArgumentNullException(nameof(newPayload));
    }

    /// <summary>
    /// Records the execution telemetry of a node.
    /// </summary>
    public void RecordExecution(NodeExecutionRecord record)
    {
        _history[record.NodeType] = record;
    }

    /// <summary>
    /// Gets the full node execution history up to the current point.
    /// </summary>
    public IReadOnlyDictionary<DagNodeType, NodeExecutionRecord> GetHistory() => _history;
}
