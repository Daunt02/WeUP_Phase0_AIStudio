using System;

namespace WeUP.Contracts.Orchestration;

/// <summary>
/// Defines the available stages in the WeUP DAG execution model.
/// </summary>
public enum DagNodeType
{
    Ingestion,
    Moderation,
    Resolution,
    Publish
}

/// <summary>
/// Represents a static schema mapping the deterministic workflow DAG.
/// </summary>
public static class DagSchema
{
    /// <summary>
    /// Gets the linear MVP execution pipeline.
    /// </summary>
    public static readonly DagNodeType[] DefaultPipeline = 
    [
        DagNodeType.Ingestion,
        DagNodeType.Moderation,
        DagNodeType.Resolution,
        DagNodeType.Publish
    ];
}
