using System;
using System.Collections.Generic;

namespace WeUP.Contracts.Moderation;

/// <summary>
/// Overall risk tier for a candidate event.
/// </summary>
public enum EventRiskLevel
{
    Low,        // Safe to auto-publish
    Medium,     // Requires manual review
    High,       // Must be reviewed; likely problematic
    Restricted  // Auto-reject unless overridden by a privileged moderator
}

/// <summary>
/// Detailed risk breakdown for a single candidate event.
/// </summary>
public sealed record EventRiskScore(
    Guid CandidateId,
    EventRiskLevel Level,
    float OverallScore,
    IReadOnlyDictionary<string, float> Factors,
    string Explanation);
