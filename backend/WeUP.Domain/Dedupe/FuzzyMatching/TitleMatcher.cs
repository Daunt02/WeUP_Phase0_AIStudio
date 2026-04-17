namespace WeUP.Domain.Dedupe.FuzzyMatching;

// ============================================================================
// M2-P07: TitleMatcher — Event Title Fuzzy Similarity Service v1.0
//
// ALGORITHM
// ─────────────────────────────────────────────────────────────────────────────
// 1. Normalize both inputs via FuzzyNormalizationHelpers.NormalizeTitle().
//    This applies: NFC, lowercase, TitleAbbreviations expansion,
//    punctuation→space, whitespace collapse.
// 2. Null/empty guard: reduces Confidence, forces Score=0.0.
// 3. Exact-match short-circuit: Score=1.0, Confidence=1.0.
// 4. Blended similarity: 60% normalized Levenshtein + 40% token Jaccard.
//
// CONFIDENCE RULES
// ─────────────────────────────────────────────────────────────────────────────
//   Both populated  → Confidence = 1.00
//   One null/empty  → Confidence = 0.30, Score = 0.00, IsNullPenalized = true
//   Both null/empty → Confidence = 0.00, Score = 0.00, IsNullPenalized = true
//
// SCORE BAND INTERPRETATION (for consumers)
//   1.00        = Identical after normalization — deterministic match
//   0.85 – 1.00 = Title variant (subtitle added, punctuation differs)
//   0.65 – 0.84 = Likely same event with different marketing copy
//   0.40 – 0.64 = Partial word overlap; needs corroborating signals
//   0.00 – 0.39 = No meaningful title similarity
// ============================================================================

/// <summary>
/// Deterministic fuzzy matcher for event title fields.
///
/// Thread-safe and stateless; safe for singleton registration in DI.
///
/// Normalization applied:
/// <list type="bullet">
///   <item>Unicode NFC canonicalization</item>
///   <item>Lowercase (invariant culture)</item>
///   <item>Title abbreviation expansion (feat., ft., &amp;, @, vs., w/, pres.)</item>
///   <item>Punctuation → space</item>
///   <item>Whitespace collapse</item>
/// </list>
///
/// Algorithm: 60% normalized Levenshtein + 40% token Jaccard on normalized strings.
/// See <see cref="FuzzyNormalizationHelpers.BlendedSimilarity"/> for the shared primitive.
/// </summary>
public sealed class TitleMatcher : IFuzzyMatcher
{
    /// <inheritdoc />
    public FuzzyMatchDimension Dimension => FuzzyMatchDimension.Title;

    /// <summary>
    /// Compute fuzzy title similarity between two event title strings.
    ///
    /// Null or empty inputs are penalized rather than silently matched.
    /// The Explanation field encodes all normalization outcomes and score
    /// components so that results are fully auditable.
    /// </summary>
    /// <param name="left">Candidate event title (may be null).</param>
    /// <param name="right">Canonical/aggregate event title (may be null).</param>
    /// <returns>
    /// A <see cref="FuzzyMatchResult"/> with Dimension = Title.
    /// </returns>
    public FuzzyMatchResult Match(string? left, string? right)
    {
        var normalizedLeft = FuzzyNormalizationHelpers.NormalizeTitle(left);
        var normalizedRight = FuzzyNormalizationHelpers.NormalizeTitle(right);

        var leftIsEmpty = normalizedLeft.Length == 0;
        var rightIsEmpty = normalizedRight.Length == 0;

        // ── Null / empty guard ────────────────────────────────────────────────
        // A missing title is not an implicit match. We emit low confidence
        // and zero score so this gap is visible to the composite scorer.
        if (leftIsEmpty || rightIsEmpty)
        {
            var confidence = (leftIsEmpty && rightIsEmpty) ? 0.00 : 0.30;
            var reason = (leftIsEmpty && rightIsEmpty)
                ? "both_null_or_empty"
                : leftIsEmpty ? "left_null_or_empty" : "right_null_or_empty";

            return new FuzzyMatchResult(
                Dimension: FuzzyMatchDimension.Title,
                Score: 0.0,
                Confidence: confidence,
                IsNullPenalized: true,
                Explanation: $"title(score=0.0000, confidence={confidence:F2}, reason={reason})");
        }

        // ── Exact-match short-circuit ─────────────────────────────────────────
        // Identical normalized strings skip the DP matrix entirely.
        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal))
        {
            return new FuzzyMatchResult(
                Dimension: FuzzyMatchDimension.Title,
                Score: 1.0,
                Confidence: 1.0,
                IsNullPenalized: false,
                Explanation: $"title(score=1.0000, confidence=1.00, method=exact_match_after_normalization, " +
                             $"normalized=\"{normalizedLeft}\")");
        }

        // ── Blended similarity computation ────────────────────────────────────
        var levenshtein = FuzzyNormalizationHelpers.NormalizedLevenshteinSimilarity(normalizedLeft, normalizedRight);
        var jaccard = FuzzyNormalizationHelpers.TokenJaccardSimilarity(normalizedLeft, normalizedRight);
        var blended = FuzzyNormalizationHelpers.Clamp01((0.6 * levenshtein) + (0.4 * jaccard));
        var score = FuzzyNormalizationHelpers.Round4(blended);

        return new FuzzyMatchResult(
            Dimension: FuzzyMatchDimension.Title,
            Score: score,
            Confidence: 1.0,
            IsNullPenalized: false,
            Explanation: $"title(score={score:F4}, confidence=1.00, method=0.6*levenshtein+0.4*jaccard, " +
                         $"levenshtein={levenshtein:F4}, jaccard={jaccard:F4}, " +
                         $"normalized_left=\"{normalizedLeft}\", normalized_right=\"{normalizedRight}\")");
    }
}
