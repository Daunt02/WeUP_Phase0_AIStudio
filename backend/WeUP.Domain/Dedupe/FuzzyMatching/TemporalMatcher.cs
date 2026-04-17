namespace WeUP.Domain.Dedupe.FuzzyMatching;

// ============================================================================
// M2-P07: TemporalMatcher — Event Time Fuzzy Overlap Scoring v1.0
//
// ALGORITHM (applied in priority order)
// ─────────────────────────────────────────────────────────────────────────────
// 1. Both StartUtc null          → Score=0.00, Confidence=0.00 (no data at all)
// 2. Exactly one StartUtc null   → Score=0.00, Confidence=0.30 (partial data)
// 3. Exact start-time match      → Score=1.00, Confidence=1.00
// 4. Range overlap (both EndUtc) → Score=OverlapRatio (proportion of shared time)
//    with a floor of 0.75 on any positive overlap
// 5. Close-start step function (start delta in minutes):
//      ≤ 30   → Score=0.90   (same event, slight scheduling variance)
//      ≤ 120  → Score=0.70   (same event, different source reporting)
//      ≤ 360  → Score=0.40   (possibly same event, different slot)
//      ≤ 720  → Score=0.20   (same day, different event likely)
//      > 720  → Score=0.00   (different day; treat as distinct events)
//
// TIMEZONE CONTRACT
// ─────────────────────────────────────────────────────────────────────────────
// - TemporalInput.StartUtc and EndUtc MUST carry UTC offset (Offset == +00:00).
// - This matcher NEVER converts between timezones silently.
// - When both sides carry conflicting non-empty TimezoneIanaId values,
//   Confidence is reduced by TzConflictPenalty (0.15) with a floor of 0.30.
//   This is surfaced in Explanation so moderation queues can see the warning.
// - Identical TimezoneIanaId, or either side being null/empty = no penalty.
//
// NULL FIELD SEMANTICS
// ─────────────────────────────────────────────────────────────────────────────
// Null is never treated as "match". Missing start times explicitly reduce
// Confidence. Missing end times simply disable range-overlap scoring without
// penalizing the score further (end times are optional in event data).
//
// SCORE BAND INTERPRETATION (for consumers)
//   1.00        = Exact start-time identity
//   0.90        = ≤30-min start delta (scheduling variance threshold)
//   0.75 – 0.89 = Overlapping time ranges or very close start proximity
//   0.40 – 0.74 = 30–360 min delta; plausible if title/venue corroborate
//   0.20 – 0.39 = 360–720 min delta; requires strong corroborating signal
//   0.00        = >720 min apart, or null input — treat as non-overlapping
// ============================================================================

/// <summary>
/// Deterministic fuzzy matcher for event temporal data.
///
/// Thread-safe and stateless; safe for singleton registration in DI.
///
/// Inputs are structured <see cref="TemporalInput"/> records; callers are
/// responsible for UTC resolution before constructing inputs.
/// </summary>
public sealed class TemporalMatcher : ITemporalFuzzyMatcher
{
    // ──────────────────────────────────────────────────────────────────────────
    // Scoring constants — all named so they can be referenced in assertion tests
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Score when start-time delta is ≤ 30 minutes.
    /// Represents scheduling variance within a single event listing.
    /// </summary>
    public const double Score30Min = 0.90;

    /// <summary>
    /// Score when start-time delta is ≤ 120 minutes (2 hours).
    /// Represents possible same-event with source-dependent time reporting.
    /// </summary>
    public const double Score120Min = 0.70;

    /// <summary>
    /// Score when start-time delta is ≤ 360 minutes (6 hours).
    /// Possible duplicate; strong corroboration from title/venue required.
    /// </summary>
    public const double Score360Min = 0.40;

    /// <summary>
    /// Score when start-time delta is ≤ 720 minutes (12 hours).
    /// Low overlap — likely same-day different event.
    /// </summary>
    public const double Score720Min = 0.20;

    /// <summary>
    /// Hard boundary: delta &gt; 720 minutes yields Score=0.
    /// Events more than 12 hours apart are treated as temporally distinct.
    /// </summary>
    public const double HardBoundaryMinutes = 720.0;

    /// <summary>
    /// Confidence reduction when both sides carry a non-empty, conflicting
    /// TimezoneIanaId. Subtracted from the base confidence floor of 1.00.
    /// Does not affect Score.
    /// </summary>
    public const double TzConflictPenalty = 0.15;

    /// <summary>
    /// Minimum confidence value; never drops below this even with TZ conflict
    /// and partial-null scenarios combined.
    /// </summary>
    public const double ConfidenceFloor = 0.30;

    /// <inheritdoc />
    public FuzzyMatchDimension Dimension => FuzzyMatchDimension.Temporal;

    /// <summary>
    /// Compute temporal similarity between two event time windows.
    ///
    /// See the class-level documentation for the full algorithm and scoring
    /// band interpretation.
    /// </summary>
    /// <param name="left">Temporal data for the candidate event. Must not be null itself.</param>
    /// <param name="right">Temporal data for the canonical event. Must not be null itself.</param>
    public FuzzyMatchResult Match(TemporalInput left, TemporalInput right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var leftHasStart = left.StartUtc.HasValue;
        var rightHasStart = right.StartUtc.HasValue;

        // ── Case 1: Both starts null ──────────────────────────────────────────
        if (!leftHasStart && !rightHasStart)
        {
            return new FuzzyMatchResult(
                Dimension: FuzzyMatchDimension.Temporal,
                Score: 0.0,
                Confidence: 0.0,
                IsNullPenalized: true,
                Explanation: "temporal(score=0.0000, confidence=0.00, reason=both_start_utc_null)");
        }

        // ── Case 2: Exactly one start null ────────────────────────────────────
        if (!leftHasStart || !rightHasStart)
        {
            var missingSide = !leftHasStart ? "left" : "right";
            return new FuzzyMatchResult(
                Dimension: FuzzyMatchDimension.Temporal,
                Score: 0.0,
                Confidence: ConfidenceFloor,
                IsNullPenalized: true,
                Explanation: $"temporal(score=0.0000, confidence={ConfidenceFloor:F2}, " +
                             $"reason={missingSide}_start_utc_null)");
        }

        // ── Both starts are present ───────────────────────────────────────────
        var leftStart = left.StartUtc!.Value;
        var rightStart = right.StartUtc!.Value;

        // Compute base confidence; penalties may reduce it below.
        var confidence = 1.0;
        var tzNote = string.Empty;

        if (HasTimezoneConflict(left.TimezoneIanaId, right.TimezoneIanaId))
        {
            confidence = Math.Max(ConfidenceFloor, confidence - TzConflictPenalty);
            tzNote = $", tz_conflict=true(left={left.TimezoneIanaId}, right={right.TimezoneIanaId})" +
                     $", confidence_penalty={TzConflictPenalty:F2}";
        }

        // ── Case 3: Exact start-time identity ─────────────────────────────────
        if (leftStart == rightStart)
        {
            return new FuzzyMatchResult(
                Dimension: FuzzyMatchDimension.Temporal,
                Score: 1.0,
                Confidence: confidence,
                IsNullPenalized: false,
                Explanation: $"temporal(score=1.0000, confidence={confidence:F2}, " +
                             $"method=exact_start_match, start_utc={leftStart:O}{tzNote})");
        }

        // ── Case 4: Range overlap (both sides have EndUtc) ────────────────────
        if (left.EndUtc.HasValue && right.EndUtc.HasValue)
        {
            var overlapResult = ComputeRangeOverlapScore(leftStart, left.EndUtc.Value, rightStart, right.EndUtc.Value);
            if (overlapResult.HasOverlap)
            {
                var score = FuzzyNormalizationHelpers.Round4(overlapResult.Score);
                return new FuzzyMatchResult(
                    Dimension: FuzzyMatchDimension.Temporal,
                    Score: score,
                    Confidence: confidence,
                    IsNullPenalized: false,
                    Explanation: $"temporal(score={score:F4}, confidence={confidence:F2}, " +
                                 $"method=range_overlap, overlap_minutes={overlapResult.OverlapMinutes:F2}, " +
                                 $"left=[{leftStart:O},{left.EndUtc.Value:O}], " +
                                 $"right=[{rightStart:O},{right.EndUtc.Value:O}]{tzNote})");
            }
        }

        // ── Case 5: Start proximity step function ─────────────────────────────
        var deltaMinutes = Math.Abs((leftStart - rightStart).TotalMinutes);
        var proximityScore = ComputeStartProximityScore(deltaMinutes);
        var roundedScore = FuzzyNormalizationHelpers.Round4(proximityScore);

        return new FuzzyMatchResult(
            Dimension: FuzzyMatchDimension.Temporal,
            Score: roundedScore,
            Confidence: confidence,
            IsNullPenalized: false,
            Explanation: $"temporal(score={roundedScore:F4}, confidence={confidence:F2}, " +
                         $"method=start_proximity_step, delta_minutes={deltaMinutes:F2}, " +
                         $"breakpoints=[30=>{Score30Min},120=>{Score120Min}," +
                         $"360=>{Score360Min},720=>{Score720Min}]{tzNote})");
    }

    // =========================================================================
    // Internal scoring helpers — exposed internal for unit test access
    // =========================================================================

    /// <summary>
    /// Step-function score for the absolute delta between two start times.
    ///
    /// Breakpoints are intentionally named constants so test assertions can
    /// reference them without hard-coding magic numbers.
    /// </summary>
    /// <param name="deltaMinutes">|leftStart - rightStart| in minutes. Must be ≥ 0.</param>
    internal static double ComputeStartProximityScore(double deltaMinutes)
    {
        if (deltaMinutes <= 30.0)   return Score30Min;
        if (deltaMinutes <= 120.0)  return Score120Min;
        if (deltaMinutes <= 360.0)  return Score360Min;
        if (deltaMinutes <= HardBoundaryMinutes) return Score720Min;
        return 0.0;
    }

    /// <summary>
    /// Compute the time-range overlap between two intervals.
    ///
    /// Overlap score formula:
    ///   overlapMinutes = max(0, min(leftEnd, rightEnd) - max(leftStart, rightStart))
    ///   totalSpan      = max(leftEnd, rightEnd) - min(leftStart, rightStart)
    ///   rawScore       = overlapMinutes / totalSpan
    ///   finalScore     = max(0.75, rawScore)  — any positive overlap floors at 0.75
    ///
    /// The 0.75 floor reflects that overlapping time windows provide strong
    /// positive evidence even when the overlap is a small fraction of the total
    /// span (e.g., one event is a sub-event of a multi-hour festival).
    ///
    /// If there is no overlap (overlapMinutes == 0), HasOverlap = false and
    /// the caller falls through to start-proximity scoring.
    /// </summary>
    internal static (bool HasOverlap, double Score, double OverlapMinutes) ComputeRangeOverlapScore(
        DateTimeOffset leftStart,
        DateTimeOffset leftEnd,
        DateTimeOffset rightStart,
        DateTimeOffset rightEnd)
    {
        // Guard malformed ranges (end before start) — treat as having no end.
        if (leftEnd <= leftStart || rightEnd <= rightStart)
        {
            return (false, 0.0, 0.0);
        }

        var overlapStart = leftStart > rightStart ? leftStart : rightStart;
        var overlapEnd = leftEnd < rightEnd ? leftEnd : rightEnd;
        var overlapMinutes = (overlapEnd - overlapStart).TotalMinutes;

        if (overlapMinutes <= 0.0)
        {
            return (false, 0.0, 0.0);
        }

        var totalStart = leftStart < rightStart ? leftStart : rightStart;
        var totalEnd = leftEnd > rightEnd ? leftEnd : rightEnd;
        var totalMinutes = (totalEnd - totalStart).TotalMinutes;

        var rawRatio = totalMinutes > 0.0 ? overlapMinutes / totalMinutes : 0.0;

        // Floor at 0.75 so that any real overlap is a strong signal.
        var score = Math.Max(0.75, FuzzyNormalizationHelpers.Clamp01(rawRatio));

        return (true, score, overlapMinutes);
    }

    /// <summary>
    /// Returns true when both identifiers are non-empty and differ
    /// (case-insensitive). Two empty/null identifiers are not a conflict.
    /// </summary>
    internal static bool HasTimezoneConflict(string? leftTz, string? rightTz)
    {
        if (string.IsNullOrWhiteSpace(leftTz) || string.IsNullOrWhiteSpace(rightTz))
        {
            return false;
        }

        return !string.Equals(leftTz.Trim(), rightTz.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
