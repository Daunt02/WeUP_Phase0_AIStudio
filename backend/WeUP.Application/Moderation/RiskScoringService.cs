using System.Globalization;
using System.Text;
using WeUP.Domain.Dedupe;
using WeUP.Domain.Flyer;
using WeUP.Domain.Moderation;

namespace WeUP.Application.Moderation;

/// <summary>
/// Deterministic, explainable moderation risk scoring based on ingestion quality and dedupe ambiguity.
/// </summary>
public sealed class RiskScoringService : IRiskScoringService
{
    // Missing critical data must materially raise risk.
    private const int MissingTitleWeight = 18;
    private const int MissingStartUtcWeight = 20;
    private const int MissingVenueWeight = 12;
    private const int MissingAddressWeight = 10;

    // Low confidence signals indicate extraction uncertainty.
    private const int VeryLowOverallConfidenceWeight = 20;
    private const int LowOverallConfidenceWeight = 12;
    private const int LowCriticalFieldConfidenceWeight = 8;

    // Conflicts must be explicit and auditable.
    private const int TemporalConflictWeight = 18;
    private const int FieldConflictWeight = 10;

    // Unknown venue/address quality indicates validation and geocoding risk.
    private const int UnknownVenueWeight = 10;
    private const int UnknownAddressWeight = 8;

    // Basic rule-based lexical safety checks.
    private const int SuspiciousKeywordWeight = 15;

    // Duplicate ambiguity from dedup engine.
    private const int PossibleDuplicateWeight = 10;
    private const int ProbableDuplicateWeight = 22;
    private const int ExactDuplicateWeight = 26;
    private const int DuplicateSafetyBlockerWeight = 8;

    public EventRiskScore Score(EventCandidateV2 candidate, DuplicateAssessment duplicateAssessment)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(duplicateAssessment);

        var factors = new List<EventRiskFactor>(12);
        AddMissingCriticalFieldFactors(candidate, factors);
        AddConfidenceFactors(candidate, factors);
        AddConflictFactors(candidate, duplicateAssessment, factors);
        AddUnknownLocationFactors(candidate, factors);
        AddSuspiciousKeywordFactors(candidate, factors);
        AddDuplicateAmbiguityFactors(duplicateAssessment, factors);

        var total = factors.Sum(static f => f.Weight);
        var score = Math.Clamp(total, 0, 100);
        var level = ClassifyLevel(score);
        var explanation = BuildExplanation(score, level, factors);

        return new EventRiskScore(
            OverallScore: score,
            Level: level,
            ContributingFactors: factors.AsReadOnly(),
            Explanation: explanation);
    }

    private static void AddMissingCriticalFieldFactors(EventCandidateV2 candidate, List<EventRiskFactor> factors)
    {
        if (IsMissing(candidate.Title))
        {
            factors.Add(new EventRiskFactor(
                Code: "missing_title",
                Weight: MissingTitleWeight,
                Explanation: "Critical field 'title' is missing."));
        }

        if (candidate.StartUtc is null)
        {
            factors.Add(new EventRiskFactor(
                Code: "missing_start_utc",
                Weight: MissingStartUtcWeight,
                Explanation: "Critical field 'startUtc' is missing."));
        }

        if (IsMissing(candidate.Venue))
        {
            factors.Add(new EventRiskFactor(
                Code: "missing_venue",
                Weight: MissingVenueWeight,
                Explanation: "Critical field 'venue' is missing."));
        }

        if (IsMissing(candidate.Address))
        {
            factors.Add(new EventRiskFactor(
                Code: "missing_address",
                Weight: MissingAddressWeight,
                Explanation: "Critical field 'address' is missing."));
        }
    }

    private static void AddConfidenceFactors(EventCandidateV2 candidate, List<EventRiskFactor> factors)
    {
        if (candidate.OverallConfidence < 0.35)
        {
            factors.Add(new EventRiskFactor(
                Code: "very_low_overall_confidence",
                Weight: VeryLowOverallConfidenceWeight,
                Explanation: $"Overall confidence is very low ({candidate.OverallConfidence:F2})."));
        }
        else if (candidate.OverallConfidence < 0.55)
        {
            factors.Add(new EventRiskFactor(
                Code: "low_overall_confidence",
                Weight: LowOverallConfidenceWeight,
                Explanation: $"Overall confidence is below expected threshold ({candidate.OverallConfidence:F2})."));
        }

        AddLowFieldConfidenceFactor("title", candidate.Title?.Confidence, factors);
        AddLowFieldConfidenceFactor("startUtc", candidate.StartUtc?.Confidence, factors);
        AddLowFieldConfidenceFactor("venue", candidate.Venue?.Confidence, factors);
        AddLowFieldConfidenceFactor("address", candidate.Address?.Confidence, factors);
    }

    private static void AddLowFieldConfidenceFactor(string fieldName, double? confidence, List<EventRiskFactor> factors)
    {
        if (!confidence.HasValue || confidence.Value >= 0.50)
        {
            return;
        }

        factors.Add(new EventRiskFactor(
            Code: $"low_{fieldName}_confidence",
            Weight: LowCriticalFieldConfidenceWeight,
            Explanation: $"Field '{fieldName}' confidence is low ({confidence.Value:F2})."));
    }

    private static void AddConflictFactors(
        EventCandidateV2 candidate,
        DuplicateAssessment duplicateAssessment,
        List<EventRiskFactor> factors)
    {
        if (candidate.StartUtc is not null && candidate.EndUtc is not null && candidate.EndUtc.Value < candidate.StartUtc.Value)
        {
            factors.Add(new EventRiskFactor(
                Code: "temporal_conflict",
                Weight: TemporalConflictWeight,
                Explanation: "Temporal conflict detected: endUtc is earlier than startUtc."));
        }

        var evidenceOfConflict = duplicateAssessment.Breakdown.ScoringNotes
            .Any(static note => note.Contains("conflict", StringComparison.OrdinalIgnoreCase));
        if (evidenceOfConflict)
        {
            factors.Add(new EventRiskFactor(
                Code: "dedupe_conflict_signal",
                Weight: FieldConflictWeight,
                Explanation: "Dedup assessment contains conflict indicators in scoring notes."));
        }
    }

    private static void AddUnknownLocationFactors(EventCandidateV2 candidate, List<EventRiskFactor> factors)
    {
        if (IsUnknown(candidate.Venue?.Value))
        {
            factors.Add(new EventRiskFactor(
                Code: "unknown_venue",
                Weight: UnknownVenueWeight,
                Explanation: "Venue value is unknown or placeholder."));
        }

        if (IsUnknown(candidate.Address?.Value))
        {
            factors.Add(new EventRiskFactor(
                Code: "unknown_address",
                Weight: UnknownAddressWeight,
                Explanation: "Address value is unknown or placeholder."));
        }
    }

    private static void AddSuspiciousKeywordFactors(EventCandidateV2 candidate, List<EventRiskFactor> factors)
    {
        var fields = new[]
        {
            candidate.Title?.Value,
            candidate.Description?.Value,
            candidate.Category?.Value,
        };

        var combined = string.Join(' ', fields.Where(static text => !string.IsNullOrWhiteSpace(text)));
        if (string.IsNullOrWhiteSpace(combined))
        {
            return;
        }

        var matched = SuspiciousKeywords.Where(keyword =>
                combined.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static keyword => keyword, StringComparer.Ordinal)
            .ToArray();

        if (matched.Length == 0)
        {
            return;
        }

        factors.Add(new EventRiskFactor(
            Code: "suspicious_keywords",
            Weight: SuspiciousKeywordWeight,
            Explanation: $"Suspicious keyword(s) detected: {string.Join(", ", matched)}."));
    }

    private static void AddDuplicateAmbiguityFactors(DuplicateAssessment assessment, List<EventRiskFactor> factors)
    {
        var weight = assessment.Level switch
        {
            DuplicateAssessmentLevel.PossibleDuplicate => PossibleDuplicateWeight,
            DuplicateAssessmentLevel.ProbableDuplicate => ProbableDuplicateWeight,
            DuplicateAssessmentLevel.ExactDuplicate => ExactDuplicateWeight,
            _ => 0,
        };

        if (weight > 0)
        {
            factors.Add(new EventRiskFactor(
                Code: "duplicate_ambiguity",
                Weight: weight,
                Explanation: $"Dedup assessment level '{assessment.Level}' indicates duplicate ambiguity."));
        }

        if (!assessment.AutoMergeAllowed && assessment.Level != DuplicateAssessmentLevel.Distinct)
        {
            factors.Add(new EventRiskFactor(
                Code: "duplicate_safety_blocker",
                Weight: DuplicateSafetyBlockerWeight,
                Explanation: "Duplicate assessment has active merge blockers; human review required."));
        }
    }

    private static string BuildExplanation(int score, EventRiskLevel level, IReadOnlyList<EventRiskFactor> factors)
    {
        if (factors.Count == 0)
        {
            return "Risk score 0 (Low): no risk factors were triggered.";
        }

        var builder = new StringBuilder();
        builder.Append("Risk score ");
        builder.Append(score.ToString(CultureInfo.InvariantCulture));
        builder.Append(" (");
        builder.Append(level);
        builder.Append("): ");

        for (var i = 0; i < factors.Count; i++)
        {
            var factor = factors[i];
            if (i > 0)
            {
                builder.Append("; ");
            }

            builder.Append(factor.Code);
            builder.Append("(+");
            builder.Append(factor.Weight.ToString(CultureInfo.InvariantCulture));
            builder.Append(")=");
            builder.Append(factor.Explanation);
        }

        return builder.ToString();
    }

    private static EventRiskLevel ClassifyLevel(int score)
    {
        if (score >= 75)
        {
            return EventRiskLevel.Restricted;
        }

        if (score >= 45)
        {
            return EventRiskLevel.High;
        }

        if (score >= 20)
        {
            return EventRiskLevel.Medium;
        }

        return EventRiskLevel.Low;
    }

    private static bool IsMissing(FieldConfidence<string>? field)
        => field is null || string.IsNullOrWhiteSpace(field.Value);

    private static bool IsUnknown(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "unknown" or "tbd" or "n/a" or "none" or "to be announced";
    }

    private static readonly string[] SuspiciousKeywords =
    [
        "afterparty",
        "dm for address",
        "private location",
        "secret location",
        "undisclosed",
        "unverified",
    ];
}