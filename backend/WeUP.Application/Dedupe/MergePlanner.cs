using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ContractDedupe = WeUP.Contracts.Dedupe;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Dedupe;
using WeUP.Domain.Events;

namespace WeUP.Application.Dedupe;

/// <summary>
/// Deterministic merge planner for contract-driven duplicate resolution.
/// </summary>
public sealed class MergePlanner : IMergePlanner
{
    private const float AutoMergeThreshold = 0.93f;
    private readonly ILogger<MergePlanner> _log;
    private readonly WeUP.Domain.Dedupe.MergePlanner _legacyPlanner = new();

    public MergePlanner(ILogger<MergePlanner> log)
    {
        _log = log;
    }

    // Legacy planner contract retained for compatibility with existing merge orchestration paths.
    public MergePlanDetail CreatePlan(
        NormalizedEventCandidate incoming,
        EventAggregateSnapshot canonical,
        global::WeUP.Domain.Dedupe.DuplicateAssessment assessment)
        => _legacyPlanner.CreatePlan(incoming, canonical, assessment);

    public Task<ContractDedupe.MergePlan> GeneratePlanAsync(
        ContractDedupe.DuplicateAssessment assessment,
        CandidateEvent candidate,
        EventAggregate canonical)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(canonical);

        var titleScore = GetScore(assessment.Scores, ContractDedupe.MatchDimension.Title);
        var timeScore = GetScore(assessment.Scores, ContractDedupe.MatchDimension.StartTime);
        var locationScore = GetScore(assessment.Scores, ContractDedupe.MatchDimension.Location);

        var conflicts = new List<ContractDedupe.ConflictDescriptor>();

        var titleAction = ResolveAction(
            titleScore,
            candidate.Title,
            canonical.Title,
            "Title",
            out var titleConflict);
        if (titleConflict is not null)
        {
            conflicts.Add(titleConflict);
        }

        var locationAction = ResolveAction(
            locationScore,
            candidate.RawLocationText,
            canonical.VenueName,
            "Location",
            out var locationConflict);
        if (locationConflict is not null)
        {
            conflicts.Add(locationConflict);
        }

        var timeAction = ResolveAction(
            timeScore,
            candidate.InferredStartUtc?.ToString("O"),
            canonical.StartUtc.ToString("O"),
            "StartTime",
            out var timeConflict);
        if (timeConflict is not null)
        {
            conflicts.Add(timeConflict);
        }

        candidate.RawFields.TryGetValue("Tags", out var candidateTags);
        var canonicalTags = canonical.Tags.Length == 0 ? null : string.Join(",", canonical.Tags);
        var tagsAction = ContractDedupe.MergeFieldAction.KeepExisting;
        if (!string.Equals(candidateTags, canonicalTags, StringComparison.OrdinalIgnoreCase))
        {
            tagsAction = titleScore >= 0.85f
                ? ContractDedupe.MergeFieldAction.MergeValues
                : ContractDedupe.MergeFieldAction.RequireManualReview;

            if (tagsAction == ContractDedupe.MergeFieldAction.RequireManualReview)
            {
                conflicts.Add(new ContractDedupe.ConflictDescriptor(
                    FieldName: "Tags",
                    ExistingValue: canonicalTags ?? "(none)",
                    CandidateValue: candidateTags ?? "(none)",
                    Reason: "Tag sets differ and similarity below auto-merge threshold"));
            }
        }

        var autoApply = assessment.Level == ContractDedupe.DuplicateAssessmentLevel.ExactDuplicate
            || IsAboveThreshold(assessment);

        var plan = new ContractDedupe.MergePlan(
            CandidateId: candidate.CandidateId,
            CanonicalEventId: TryParseGuid(canonical.CanonicalEventId),
            TitleAction: titleAction,
            LocationAction: locationAction,
            TimeAction: timeAction,
            TagsAction: tagsAction,
            Conflicts: conflicts.ToArray(),
            AutoApply: autoApply);

        _log.LogInformation(
            "Merge plan generated for Candidate {CandidateId} vs Event {EventId} - AutoApply={AutoApply}",
            candidate.CandidateId,
            canonical.CanonicalEventId,
            autoApply);

        return Task.FromResult(plan);
    }

    private static float GetScore(ContractDedupe.MatchScoreBreakdown[] scores, ContractDedupe.MatchDimension dimension)
        => scores.FirstOrDefault(s => s.Dimension == dimension)?.Score ?? 0f;

    private static bool IsAboveThreshold(ContractDedupe.DuplicateAssessment assessment)
        => assessment.Scores.Length > 0 && assessment.Scores.Average(s => s.Score) >= AutoMergeThreshold;

    private static ContractDedupe.MergeFieldAction ResolveAction(
        float similarity,
        string? candidateValue,
        string? existingValue,
        string fieldName,
        out ContractDedupe.ConflictDescriptor? conflict)
    {
        conflict = null;

        if (string.Equals(
            candidateValue?.Trim(),
            existingValue?.Trim(),
            StringComparison.OrdinalIgnoreCase))
        {
            return ContractDedupe.MergeFieldAction.KeepExisting;
        }

        if (similarity >= 0.90f)
        {
            return ContractDedupe.MergeFieldAction.ReplaceWithCandidate;
        }

        if (similarity >= 0.75f)
        {
            return ContractDedupe.MergeFieldAction.MergeValues;
        }

        conflict = new ContractDedupe.ConflictDescriptor(
            FieldName: fieldName,
            ExistingValue: existingValue ?? "(null)",
            CandidateValue: candidateValue ?? "(null)",
            Reason: $"Similarity {similarity:P0} below threshold for automatic merge.");

        return ContractDedupe.MergeFieldAction.RequireManualReview;
    }

    private static Guid TryParseGuid(string value)
        => Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty;
}
