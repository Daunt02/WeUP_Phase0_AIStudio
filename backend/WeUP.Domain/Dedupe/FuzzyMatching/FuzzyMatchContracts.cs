namespace WeUP.Domain.Dedupe.FuzzyMatching;

// ============================================================================
// M2-P07: Fuzzy Matching Subsystem — Type Contracts v1.0
//
// CONTRACT INVARIANTS
// ─────────────────────────────────────────────────────────────────────────────
// 1. Score (0.0–1.0) reflects computed similarity; 1.0 is a perfect match.
// 2. Confidence (0.0–1.0) reflects data availability for the comparison.
//    Low confidence does NOT imply low similarity — they are independent axes.
// 3. IsNullPenalized = true whenever one or both inputs were null/empty.
//    A null input MUST reduce Confidence. It MUST NOT silently produce a 1.0 Score.
// 4. Explanation is a deterministic string encoding all inputs and algorithm
//    parameters that produced the result. Equal inputs → equal explanation.
// 5. No timezone coercion is ever performed silently. Timezone ambiguity is
//    surfaced in Explanation and reduces Confidence.
// ============================================================================

// ---------------------------------------------------------------------------
// FuzzyMatchDimension
// ---------------------------------------------------------------------------

/// <summary>
/// Identifies which dimension a <see cref="FuzzyMatchResult"/> was produced for.
/// </summary>
public enum FuzzyMatchDimension
{
    /// <summary>
    /// Event title similarity. Scored using blended Levenshtein + Jaccard
    /// after punctuation stripping, lowercasing, abbreviation expansion,
    /// and whitespace collapse.
    /// </summary>
    Title,

    /// <summary>
    /// Venue name similarity. Scored using the same blended algorithm as
    /// Title with a venue-specific abbreviation expansion table
    /// (e.g., St → Street, Ave → Avenue).
    /// </summary>
    Venue,

    /// <summary>
    /// Temporal proximity/overlap score. Accounts for exact start-time match,
    /// overlapping time ranges, and close-start proximity.
    /// Both null values produce Score=0, Confidence=0.
    /// </summary>
    Temporal,
}

// ---------------------------------------------------------------------------
// FuzzyMatchResult
// ---------------------------------------------------------------------------

/// <summary>
/// Result of a single fuzzy matching dimension evaluation.
///
/// Score semantics (0.0 – 1.0):
///   1.00        = Deterministically identical after normalization
///   0.75 – 1.00 = Strong similarity; likely the same entity
///   0.50 – 0.75 = Partial overlap; possible match — requires composite review
///   0.25 – 0.50 = Weak similarity; low evidence of shared identity
///   0.00 – 0.25 = No meaningful evidence of match
///
/// Confidence semantics (0.0 – 1.0):
///   1.00 = Both sides had non-null, parseable, unambiguous values
///   0.70 = Data present but includes ambiguous elements (e.g., conflicting timezone)
///   0.30 = Exactly one side was null/empty — result is speculative
///   0.00 = Both sides were null/empty — no basis for comparison
///
/// Score and Confidence are independent. A high Score with low Confidence
/// must NOT be promoted to a strong match signal without acknowledging the gap.
/// Consumers should multiply Score × Confidence to produce an effective weight,
/// or preserve them separately for explainability in moderation queues.
/// </summary>
public sealed record FuzzyMatchResult(
    /// <summary>Which matching dimension produced this result.</summary>
    FuzzyMatchDimension Dimension,

    /// <summary>
    /// Similarity score in [0.0, 1.0]. Deterministic for equal inputs.
    /// When IsNullPenalized = true, this value is always 0.0.
    /// </summary>
    double Score,

    /// <summary>
    /// Data availability confidence in [0.0, 1.0].
    /// Reduced when any input was null, empty, or timezone-ambiguous.
    /// Never inflated: missing data cannot improve certainty.
    /// </summary>
    double Confidence,

    /// <summary>
    /// True when one or both input values were null or empty.
    /// A null input explicitly reduces Confidence; it does not produce a match.
    /// </summary>
    bool IsNullPenalized,

    /// <summary>
    /// Human-readable, deterministic justification string.
    /// Encodes input hashes, algorithm, and all breakpoints used.
    /// Suitable for moderation queue display and audit trail storage.
    /// Format: "dimension(method=..., left_tokens=..., right_tokens=..., raw_score=..., confidence=...)"
    /// </summary>
    string Explanation);

// ---------------------------------------------------------------------------
// TemporalInput
// ---------------------------------------------------------------------------

/// <summary>
/// Temporal information for one event side, used as input to <see cref="ITemporalFuzzyMatcher"/>.
///
/// INVARIANT: StartUtc and EndUtc, when non-null, MUST carry UTC offset (Offset = +00:00).
/// Callers are responsible for timezone conversion BEFORE constructing this record.
/// The matcher NEVER performs silent timezone coercion.
///
/// TimezoneIanaId is informational only. It is surfaced in the Explanation
/// and can trigger a confidence penalty when both sides carry conflicting
/// non-UTC timezone identifiers, but it does NOT alter the computed score.
/// </summary>
public sealed record TemporalInput(
    /// <summary>
    /// Event start in UTC. Null means the start time is unknown.
    /// When null, temporal score is 0 and confidence is reduced.
    /// </summary>
    DateTimeOffset? StartUtc,

    /// <summary>
    /// Event end in UTC. Null means the end time is unknown or unbounded.
    /// Used for range-overlap scoring when both sides have an end time.
    /// </summary>
    DateTimeOffset? EndUtc,

    /// <summary>
    /// IANA timezone identifier (e.g., "America/Chicago") used for display.
    /// Does NOT affect score computation. Conflicting IDs reduce Confidence.
    /// </summary>
    string? TimezoneIanaId = null);
