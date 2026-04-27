namespace WeUP.Domain.Temporal;

// ─────────────────────────────────────────────────────────────────────────────
// M8-P40: Temporal Edge-Case Validation
//
// VALIDATION RULES
//   1. Timezone blank              → "timezone is required..."
//   2. Timezone unsupported        → "Unsupported timezone '{tz}'..."
//   3. CustomRange: missing bounds → "customStartUtc and customEndUtc are required..."
//   4. CustomRange: start == end   → "customStartUtc must be earlier than customEndUtc."
//   5. CustomRange: end < start    → "customStartUtc must be earlier than customEndUtc."
//   6. CustomRange: span > 30 days → "CustomRange span must not exceed 30 days."
//   7. Non-Custom with bounds      → "Custom bounds must not be supplied with a non-Custom preset."
//   8. No preset + no explicit range → "A preset or explicit custom range must be supplied."
//
// BOUNDARY SEMANTICS (enforced by TimeWindow.Contains)
//   • startUtc is INCLUSIVE: an event at exactly startUtc is included.
//   • endUtc is EXCLUSIVE: an event at exactly endUtc is excluded.
//   • Cross-day events are included if their start instant falls within [startUtc, endUtc).
//
// DST POLICY (enforced by TimeWindowResolver.ResolveLocalBoundaryToUtc)
//   • Invalid local times (spring-forward gap): advanced minute-by-minute to next valid instant.
//   • Ambiguous local times (fall-back repeat): resolved to earliest UTC instant (offset = Max).
//   • Rolling windows (Now, Next24Hours, Next48Hours): pure UTC duration math; DST is irrelevant.
//   • Tonight that crosses spring-forward: window is 8 UTC hours instead of the nominal 9.
//   • Tonight that crosses fall-back: window is 10 UTC hours instead of the nominal 9.
//   • Today on spring-forward date: window is 23 UTC hours.
//   • Today on fall-back date: window is 25 UTC hours.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Validates temporal range requests before they reach the resolver.
/// Callers should check this first; the resolver performs the same core checks
/// but this service allows early rejection with a structured result.
///
/// All validation is explicit and deterministic.  No silent self-correction is
/// performed: invalid inputs always produce a failed result with a descriptive
/// message.
/// </summary>
public static class TemporalRangeValidator
{
    private const int CustomRangeMaxDays = 30;

    // ── Structured result ─────────────────────────────────────────────────────

    /// <summary>
    /// Result of a temporal validation check.
    /// </summary>
    public readonly record struct ValidationResult(bool IsValid, string? Error)
    {
        public static ValidationResult Ok() => new(true, null);
        public static ValidationResult Fail(string error) => new(false, error);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates that <paramref name="timezone"/> is a non-blank, recognised IANA
    /// or Windows timezone identifier.
    ///
    /// PASS: "America/Chicago", "Central Standard Time"
    /// FAIL: null, "  ", "Unknown/Timezone"
    /// </summary>
    public static ValidationResult ValidateTimezone(string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
            return ValidationResult.Fail(
                "timezone is required and must be an IANA or Windows timezone identifier.");

        if (!TimeWindowResolver.TryResolveTimezone(timezone, out _, out _))
            return ValidationResult.Fail(
                $"Unsupported timezone '{timezone}'. Use a supported IANA or Windows timezone identifier.");

        return ValidationResult.Ok();
    }

    /// <summary>
    /// Validates the bounds for a custom range.
    ///
    /// Rules:
    ///   • Both bounds must be present.
    ///   • start must be strictly earlier than end.
    ///   • span must not exceed 30 days.
    ///
    /// PASS: start=2026-05-01Z, end=2026-05-03Z
    /// FAIL: start=end, end&lt;start, span>30d, either null
    /// </summary>
    public static ValidationResult ValidateCustomRange(
        DateTimeOffset? customStartUtc,
        DateTimeOffset? customEndUtc)
    {
        if (!customStartUtc.HasValue || !customEndUtc.HasValue)
            return ValidationResult.Fail(
                "customStartUtc and customEndUtc are required when preset=CustomRange.");

        if (customStartUtc.Value >= customEndUtc.Value)
            return ValidationResult.Fail(
                "customStartUtc must be earlier than customEndUtc.");

        if ((customEndUtc.Value - customStartUtc.Value).TotalDays > CustomRangeMaxDays)
            return ValidationResult.Fail(
                $"CustomRange span must not exceed {CustomRangeMaxDays} days.");

        return ValidationResult.Ok();
    }

    /// <summary>
    /// Validates that a non-Custom preset is not paired with explicit custom bounds.
    ///
    /// Supplying custom bounds alongside a non-Custom preset is ambiguous intent
    /// and is rejected explicitly.  Callers must send either (a) a non-Custom preset
    /// with no bounds, or (b) CustomRange/Custom with both bounds.
    ///
    /// PASS: preset=Tonight, no bounds
    /// PASS: preset=CustomRange, both bounds present
    /// FAIL: preset=Tonight, bounds also supplied → ambiguous, rejected
    /// </summary>
    public static ValidationResult ValidatePresetConsistency(
        TimeWindowPreset preset,
        DateTimeOffset? customStartUtc,
        DateTimeOffset? customEndUtc)
    {
        bool isCustomPreset = preset == TimeWindowPreset.CustomRange || preset == TimeWindowPreset.Custom;
        bool hasCustomBounds = customStartUtc.HasValue || customEndUtc.HasValue;

        if (!isCustomPreset && hasCustomBounds)
            return ValidationResult.Fail(
                $"Custom bounds must not be supplied with a non-Custom preset (preset={preset}). " +
                "Use preset=CustomRange to supply explicit bounds.");

        return ValidationResult.Ok();
    }

    /// <summary>
    /// Full request validation combining timezone, preset consistency, and range checks.
    ///
    /// Validates in order:
    ///   1. Timezone
    ///   2. Preset/bounds consistency
    ///   3. Custom range bounds (only when preset is Custom/CustomRange)
    ///
    /// Returns the first failure encountered so callers get the most actionable error.
    /// </summary>
    public static ValidationResult ValidateRequest(
        string? timezone,
        TimeWindowPreset preset,
        DateTimeOffset? customStartUtc = null,
        DateTimeOffset? customEndUtc = null)
    {
        var tzResult = ValidateTimezone(timezone);
        if (!tzResult.IsValid) return tzResult;

        var consistencyResult = ValidatePresetConsistency(preset, customStartUtc, customEndUtc);
        if (!consistencyResult.IsValid) return consistencyResult;

        bool isCustomPreset = preset == TimeWindowPreset.CustomRange || preset == TimeWindowPreset.Custom;
        if (isCustomPreset)
        {
            var rangeResult = ValidateCustomRange(customStartUtc, customEndUtc);
            if (!rangeResult.IsValid) return rangeResult;
        }

        return ValidationResult.Ok();
    }
}
