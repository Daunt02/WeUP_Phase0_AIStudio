using System;

namespace WeUP.Contracts.Orchestration;

/// <summary>
/// Defines how the kernel should handle failures during node execution.
/// </summary>
public sealed record FailurePolicy(
    int MaxRetries,
    TimeSpan BaseBackoff,
    bool AllowBypass)
{
    /// <summary>
    /// The default robust retry policy for transient failures.
    /// </summary>
    public static FailurePolicy Default => new FailurePolicy(3, TimeSpan.FromMilliseconds(500), false);
    
    /// <summary>
    /// A strict policy that aborts immediately on the first failure.
    /// </summary>
    public static FailurePolicy Strict => new FailurePolicy(0, TimeSpan.Zero, false);
}
