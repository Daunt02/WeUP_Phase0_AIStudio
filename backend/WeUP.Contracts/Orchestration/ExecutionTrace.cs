using System;
using System.Collections.Generic;

namespace WeUP.Contracts.Orchestration;

/// <summary>
/// Defines the overall trace of an execution workflow, guaranteeing complete auditability.
/// </summary>
public sealed record ExecutionTrace(
    Guid IdempotencyKey,
    DateTimeOffset StartedAtUtc)
{
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public bool IsSuccess { get; init; }
    public string? FinalErrorMessage { get; init; }
    
    public IReadOnlyList<NodeExecutionRecord> NodeRecords { get; init; } = Array.Empty<NodeExecutionRecord>();
}

/// <summary>
/// Telemetry and audit data for a single node execution within the DAG.
/// </summary>
public sealed record NodeExecutionRecord(
    DagNodeType NodeType,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    bool Success,
    string InputChecksum,
    string? OutputChecksum,
    string? ErrorMessage,
    int AttemptCount);
