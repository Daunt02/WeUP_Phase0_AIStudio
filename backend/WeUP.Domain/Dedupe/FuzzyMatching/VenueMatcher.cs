namespace WeUP.Domain.Dedupe.FuzzyMatching;

// ============================================================================
// M2-P07: VenueMatcher — Venue Name Fuzzy Similarity Service v1.0
//
// ALGORITHM
// ─────────────────────────────────────────────────────────────────────────────
// 1. Normalize both inputs via FuzzyNormalizationHelpers.NormalizeVenue().
//    This applies: NFC, lowercase, VenueAbbreviations expansion
//    (St→Street, Ave→Avenue, cardinal directions, suite designators),
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
//   0.85 – 1.00 = Same venue, variant spelling or abbreviation
//   0.65 – 0.84 = Likely same venue (e.g., "Skyline Club" vs "The Skyline Club")
//   0.40 – 0.64 = Partial name overlap; corroborate with address/geo signals
//   0.00 – 0.39 = No meaningful venue name similarity
//
// WHY SEPARATE FROM TitleMatcher?
// ─────────────────────────────────────────────────────────────────────────────
// Venue text contains street-type tokens (St, Ave, Blvd) and cardinal
// directions (N, S) that would be wrongly expanded in a title context.
// The normalization table differences are the primary reason these are
// two independent, interchangeable implementations of IFuzzyMatcher.
// ============================================================================

/// <summary>
/// Deterministic fuzzy matcher for venue name fields.
///
/// Thread-safe and stateless; safe for singleton registration in DI.
///
/// Normalization applied:
/// <list type="bullet">
///   <item>Unicode NFC canonicalization</item>
///   <item>Lowercase (invariant culture)</item>
///   <item>Venue abbreviation expansion (St, Ave, Blvd, N/S/E/W, Ste, etc.)</item>
///   <item>Punctuation → space</item>
///   <item>Whitespace collapse</item>
/// </list>
///
/// Algorithm: 60% normalized Levenshtein + 40% token Jaccard on normalized strings.
/// </summary>
public sealed class VenueMatcher : IFuzzyMatcher
{
    /// <inheritdoc />
    public FuzzyMatchDimension Dimension => FuzzyMatchDimension.Venue;

    /// <summary>
    /// Compute fuzzy venue similarity between two venue name strings.
    ///
    /// Null or empty inputs are penalized rather than silently matched.
    /// The Explanation field encodes all normalization outcomes and score
    /// components so that results are fully auditable.
    /// </summary>
    /// <param name="left">Candidate venue name (may be null).</param>
    /// <param name="right">Canonical/aggregate venue name (may be null).</param>
    /// <returns>
    /// A <see cref="FuzzyMatchResult"/> with Dimension = Venue.
    /// </returns>
    public FuzzyMatchResult Match(string? left, string? right)
    {
        var normalizedLeft = FuzzyNormalizationHelpers.NormalizeVenue(left);
        var normalizedRight = FuzzyNormalizationHelpers.NormalizeVenue(right);

        var leftIsEmpty = normalizedLeft.Length == 0;
        var rightIsEmpty = normalizedRight.Length == 0;

        // ── Null / empty guard ────────────────────────────────────────────────
        // A missing venue name must not silently produce a match.
        if (leftIsEmpty || rightIsEmpty)
        {
            var confidence = (leftIsEmpty && rightIsEmpty) ? 0.00 : 0.30;
            var reason = (leftIsEmpty && rightIsEmpty)
                ? "both_null_or_empty"
                : leftIsEmpty ? "left_null_or_empty" : "right_null_or_empty";

            return new FuzzyMatchResult(
                Dimension: FuzzyMatchDimension.Venue,
                Score: 0.0,
                Confidence: confidence,
                IsNullPenalized: true,
                Explanation: $"venue(score=0.0000, confidence={confidence:F2}, reason={reason})");
        }

        // ── Exact-match short-circuit ─────────────────────────────────────────
        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal))
        {
            return new FuzzyMatchResult(
                Dimension: FuzzyMatchDimension.Venue,
                Score: 1.0,
                Confidence: 1.0,
                IsNullPenalized: false,
                Explanation: $"venue(score=1.0000, confidence=1.00, method=exact_match_after_normalization, " +
                             $"normalized=\"{normalizedLeft}\")");
        }

        // ── Blended similarity computation ────────────────────────────────────
        var levenshtein = FuzzyNormalizationHelpers.NormalizedLevenshteinSimilarity(normalizedLeft, normalizedRight);
        var jaccard = FuzzyNormalizationHelpers.TokenJaccardSimilarity(normalizedLeft, normalizedRight);
        var blended = FuzzyNormalizationHelpers.Clamp01((0.6 * levenshtein) + (0.4 * jaccard));
        var score = FuzzyNormalizationHelpers.Round4(blended);

        return new FuzzyMatchResult(
            Dimension: FuzzyMatchDimension.Venue,
            Score: score,
            Confidence: 1.0,
            IsNullPenalized: false,
            Explanation: $"venue(score={score:F4}, confidence=1.00, method=0.6*levenshtein+0.4*jaccard, " +
                         $"levenshtein={levenshtein:F4}, jaccard={jaccard:F4}, " +
                         $"normalized_left=\"{normalizedLeft}\", normalized_right=\"{normalizedRight}\")");
    }
}
