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