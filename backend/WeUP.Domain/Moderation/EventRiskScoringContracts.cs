using WeUP.Domain.Dedupe;
using WeUP.Domain.Flyer;

namespace WeUP.Domain.Moderation;

/// <summary>
/// Deterministic moderation risk tier used to route ingestion outcomes.
/// </summary>
public enum EventRiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    Restricted = 3,
}

/// <summary>
/// A single weighted factor that contributed to the final risk score.
/// </summary>
public sealed record EventRiskFactor(
    string Code,
    int Weight,
    string Explanation);

/// <summary>
/// Final explainable risk score used by moderation and publish gating.
/// </summary>
public sealed record EventRiskScore(
    int OverallScore,
    EventRiskLevel Level,
    IReadOnlyList<EventRiskFactor> ContributingFactors,
    string Explanation)
{
    /// <summary>
    /// Restricted and High candidates are blocked from auto-publish.
    /// </summary>
    public bool IsAutoPublishEligible => Level is EventRiskLevel.Low or EventRiskLevel.Medium;
}

/// <summary>
/// Deterministic scoring contract for ingestion moderation risk.
///
/// Hard requirements:
/// - No probabilistic/ML decisions.
/// - Same input yields same output.
/// - Every score must be explainable through ContributingFactors.
/// </summary>
public interface IRiskScoringService
{
    EventRiskScore Score(
        EventCandidateV2 candidate,
        DuplicateAssessment duplicateAssessment);
}