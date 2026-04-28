using System;

namespace WeUP.Contracts.Dedupe;

/// <summary>
/// High-level classification of how two events relate to each other.
/// </summary>
public enum DuplicateAssessmentLevel
{
    ExactDuplicate,
    ProbableDuplicate,
    PossibleDuplicate,
    Distinct,
    Conflict,
}

/// <summary>
/// Individual dimension used for scoring.
/// </summary>
public enum MatchDimension
{
    Title,
    Venue,
    StartTime,
    Location,
    SourceHash,
}

/// <summary>
/// Breakdown of the similarity score per dimension.
/// </summary>
public sealed record MatchScoreBreakdown(
    MatchDimension Dimension,
    float Score);

/// <summary>
/// Full deduplication assessment for a candidate vs. a canonical event.
/// </summary>
public sealed record DuplicateAssessment(
    Guid CandidateId,
    Guid CanonicalEventId,
    DuplicateAssessmentLevel Level,
    MatchScoreBreakdown[] Scores,
    string? Explanation);