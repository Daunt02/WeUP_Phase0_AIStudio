using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Moderation;
using WeUP.Domain.Moderation;

namespace WeUP.Application.Moderation;

/// <summary>
/// Simple rule-based risk scorer used for the MVP.  All thresholds are hard-coded to keep the
/// implementation deterministic; they can be moved to configuration later.
/// </summary>
public sealed class RiskScoringService : IRiskScoringService
{
    private readonly ILogger<RiskScoringService> _log;

    // Hard-coded thresholds (feel free to move to IOptions<> later)
    private const float MissingFieldPenalty   = 0.30f;
    private const float LowConfidencePenalty = 0.25f;
    private const float ConflictPenalty       = 0.20f;
    private const float KeywordPenalty       = 0.15f;

    // Keywords that raise suspicion (case-insensitive)
    private static readonly string[] SuspiciousKeywords =
    {
        "free", "giveaway", "lottery", "spam", "adult", "drugs"
    };

    public RiskScoringService(ILogger<RiskScoringService> log) => _log = log;

    public Task<EventRiskScore> ComputeRiskAsync(CandidateEvent candidate)
    {
        var factors = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        float score = 0f;
        var explanations = new List<string>();

        // ----- 1️⃣ Missing critical fields -----
        if (string.IsNullOrWhiteSpace(candidate.Title))
        {
            score += MissingFieldPenalty;
            factors["MissingTitle"] = MissingFieldPenalty;
            explanations.Add("Title is missing.");
        }

        if (!candidate.InferredStartUtc.HasValue)
        {
            score += MissingFieldPenalty;
            factors["MissingStartTime"] = MissingFieldPenalty;
            explanations.Add("Start time is missing.");
        }

        if (string.IsNullOrWhiteSpace(candidate.RawLocationText))
        {
            score += MissingFieldPenalty;
            factors["MissingLocation"] = MissingFieldPenalty;
            explanations.Add("Location text is missing.");
        }

        // ----- 2️⃣ Low confidence from extraction -----
        if (candidate.OverallExtractionConfidence < 0.70f)
        {
            var penalty = LowConfidencePenalty * (1f - candidate.OverallExtractionConfidence);
            score += penalty;
            factors["LowConfidence"] = penalty;
            explanations.Add($"Overall confidence low ({candidate.OverallExtractionConfidence:P0}).");
        }

        // ----- 3️⃣ Keyword heuristics -----
        foreach (var kw in SuspiciousKeywords)
        {
            if ((candidate.Title?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (candidate.RawLocationText?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                score += KeywordPenalty;
                factors[$"Keyword:{kw}"] = KeywordPenalty;
                explanations.Add($"Suspicious keyword detected: \"{kw}\".");
            }
        }

        // ----- 4️⃣ Simple conflict detection (placeholder) -----
        // For MVP we just flag when the raw location looks like an address but the title contains a phone number.
        bool looksLikeAddress = candidate.RawLocationText?.Contains("St", StringComparison.OrdinalIgnoreCase) ?? false;
        bool titleHasPhone = candidate.Title?.Any(char.IsDigit) ?? false; // crude check
        if (looksLikeAddress && titleHasPhone)
        {
            score += ConflictPenalty;
            factors["Location/PhoneConflict"] = ConflictPenalty;
            explanations.Add("Location appears to be an address while title contains a phone number – possible spam.");
        }

        // Clamp score to 0-1 range.
        score = Math.Min(1f, Math.Max(0f, score));

        // Derive risk level from score.
        var level = score switch
        {
            >= 0.80f => EventRiskLevel.Restricted,
            >= 0.60f => EventRiskLevel.High,
            >= 0.40f => EventRiskLevel.Medium,
            _        => EventRiskLevel.Low
        };

        var result = new EventRiskScore(
            CandidateId: candidate.CandidateId,
            Level: level,
            OverallScore: score,
            Factors: factors,
            Explanation: string.Join(" ", explanations));

        _log.LogInformation(
            "RiskScore computed for Candidate {CandidateId}: Level={Level}, Score={Score:P2}",
            candidate.CandidateId, level, score);

        return Task.FromResult(result);
    }
}
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