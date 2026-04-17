namespace WeUP.Domain.Dedupe.FuzzyMatching;

// ============================================================================
// M2-P07: IFuzzyMatcher — Single-Dimension Fuzzy Matching Interface v1.0
//
// INTERFACE INVARIANTS
// ─────────────────────────────────────────────────────────────────────────────
// 1. All implementations must be pure, stateless, and thread-safe.
// 2. Inputs are not mutated; normalization produces new strings.
// 3. Null inputs are penalized explicitly — never silently promoted to matches.
// 4. Equal inputs always produce identical FuzzyMatchResult (determinism).
// 5. The Dimension property must match the FuzzyMatchDimension returned in results.
// ============================================================================

/// <summary>
/// Contract for a single-dimension string fuzzy matcher.
///
/// Implementations score the similarity between two string inputs and return
/// a fully-explained <see cref="FuzzyMatchResult"/>.
///
/// Null inputs do not cause exceptions; instead they produce:
///   - Score  = 0.0
///   - IsNullPenalized = true
///   - Confidence reduced proportionally (see FuzzyMatchResult confidence semantics)
///
/// Implementations must NOT:
///   - Call external services
///   - Perform I/O
///   - Cache mutable state
///   - Guess missing fields into valid match signals
/// </summary>
public interface IFuzzyMatcher
{
    /// <summary>
    /// The matching dimension this instance is responsible for.
    /// Must equal the Dimension field in every returned <see cref="FuzzyMatchResult"/>.
    /// </summary>
    FuzzyMatchDimension Dimension { get; }

    /// <summary>
    /// Compute the fuzzy similarity between two string inputs.
    ///
    /// Both inputs are normalized internally before comparison.
    /// Null or empty strings are treated as absent data (IsNullPenalized = true)
    /// and explicitly reduce Confidence without increasing Score.
    /// </summary>
    /// <param name="left">First string value (e.g., candidate field).</param>
    /// <param name="right">Second string value (e.g., canonical/aggregate field).</param>
    /// <returns>
    /// A <see cref="FuzzyMatchResult"/> with Score, Confidence, and Explanation.
    /// Never returns null.
    /// </returns>
    FuzzyMatchResult Match(string? left, string? right);
}

/// <summary>
/// Contract for temporal fuzzy matching, handling null times, overlapping
/// ranges, and close-start proximity.
///
/// Unlike <see cref="IFuzzyMatcher"/>, temporal inputs are structured
/// <see cref="TemporalInput"/> records, not raw strings, because time
/// parsing must happen at the boundary and not be repeated inside scoring.
///
/// Timezone handling contract:
///   - UTC offset MUST be resolved by the caller before constructing TemporalInput.
///   - This interface NEVER silently converts between timezones.
///   - Conflicting TimezoneIanaId values reduce Confidence and are surfaced
///     in the Explanation string.
/// </summary>
public interface ITemporalFuzzyMatcher
{
    /// <summary>Always returns <see cref="FuzzyMatchDimension.Temporal"/>.</summary>
    FuzzyMatchDimension Dimension { get; }

    /// <summary>
    /// Compute temporal similarity between two event time windows.
    ///
    /// Scoring logic (in priority order):
    ///   1. Both null → Score=0.00, Confidence=0.00, IsNullPenalized=true
    ///   2. One null  → Score=0.00, Confidence=0.30, IsNullPenalized=true
    ///   3. Exact start-time match → Score=1.00, Confidence=1.00
    ///   4. Full range overlap (both have EndUtc) → Score based on overlap ratio
    ///   5. Start proximity step-function:
    ///        ≤ 30 min  → Score=0.90
    ///        ≤ 120 min → Score=0.70
    ///        ≤ 360 min → Score=0.40
    ///        ≤ 720 min → Score=0.20
    ///        &gt; 720 min → Score=0.00
    ///   6. Conflicting timezones reduce Confidence by 0.15 (floor 0.30)
    /// </summary>
    /// <param name="left">Temporal data for the candidate event.</param>
    /// <param name="right">Temporal data for the canonical/aggregate event.</param>
    /// <returns>A <see cref="FuzzyMatchResult"/> for the Temporal dimension.</returns>
    FuzzyMatchResult Match(TemporalInput left, TemporalInput right);
}
