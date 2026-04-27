namespace WeUP.Domain.Temporal;

/// <summary>
/// M8-P38: Timezone Normalization Policy v1.0.
///
/// Single policy across ingestion, canonical storage, query resolution, and display:
/// 1) Canonical event storage:
///    - StartUtc/EndUtc are authoritative and MUST be UTC (+00:00).
///    - MarketTimezone carries projection context for display and preset resolution.
/// 2) Event querying and time-window resolution:
///    - Backend resolves all presets and relative windows.
///    - Resolved bounds are always UTC and deterministic for (preset, timezone, referenceInstant).
/// 3) Event display:
///    - Clients format stored UTC instants using MarketTimezone/EventTimezone context.
///    - Clients must never reinterpret already-normalized UTC fields as local wall-clock inputs.
/// 4) Ingestion ambiguity:
///    - Unknown or ambiguous timezone/local-time evidence must remain explicit.
///    - Do not manufacture false precision by silently assigning exact UTC instants.
///
/// Constraints:
/// - No mixed storage strategy (UTC + non-UTC in canonical aggregate).
/// - Browser locale/timezone is not source-of-truth for event semantics.
/// </summary>
public static class TimezoneNormalizationPolicy
{
    /// <summary>
    /// Canonical storage timezone identifier for persisted event instants.
    /// </summary>
    public const string CanonicalStorageTimezone = "UTC";

    /// <summary>
    /// Launch-market default timezone when market context is unavailable.
    /// This is only a fallback projection context and does not alter stored UTC values.
    /// </summary>
    public const string DefaultMarketTimezone = "America/Chicago";
}

/// <summary>
/// Certainty level for how ingestion obtained temporal information.
///
/// High-certainty paths produce exact UTC instants.
/// Lower-certainty paths remain explicit and reviewable to prevent false precision.
/// </summary>
public enum IngestionTemporalCertainty
{
    /// <summary>
    /// Exact UTC instant derived from explicit timezone/offset evidence
    /// (e.g., ISO timestamp with offset or trusted source metadata).
    /// </summary>
    ExactUtcFromExplicitTimezone = 0,

    /// <summary>
    /// UTC instant projected from local datetime plus trusted market timezone context.
    /// Deterministic but inferred from contextual timezone rather than explicit source timezone.
    /// </summary>
    ProjectedFromMarketTimezone = 1,

    /// <summary>
    /// Date-level certainty only (time-of-day missing or unresolved).
    /// Keep ambiguity explicit and route through review/guardrails.
    /// </summary>
    DateOnlyWithTimezoneContext = 2,

    /// <summary>
    /// Input is incomplete or conflicting; exact UTC instant is not trustworthy.
    /// Canonical StartUtc/EndUtc should remain unset until resolved.
    /// </summary>
    AmbiguousOrIncomplete = 3,
}

/// <summary>
/// Normalized temporal projection created during ingestion before canonical persistence.
///
/// This record allows ingestion to preserve what is known, what is inferred, and what
/// remains unresolved. Ambiguity is represented explicitly instead of being hidden.
/// </summary>
public sealed record IngestionTemporalNormalization(
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    string? SourceTimezone,
    string MarketTimezone,
    IngestionTemporalCertainty Certainty,
    string[] UnresolvedAmbiguities,
    string[] EvidenceRefs)
{
    /// <summary>
    /// True when temporal values may be persisted as canonical event time.
    /// Canonical persistence still requires UTC normalization and invariant validation.
    /// </summary>
    public bool CanPersistCanonicalUtc =>
        Certainty is IngestionTemporalCertainty.ExactUtcFromExplicitTimezone
            or IngestionTemporalCertainty.ProjectedFromMarketTimezone;
}

/// <summary>
/// Canonical display context used by backend DTO projection and frontend formatting.
///
/// Stored UTC instants remain unchanged; only the projection timezone changes.
/// </summary>
public sealed record EventDisplayTimezoneContext(
    string MarketTimezone,
    string? EventTimezone = null,
    string? TimezoneSource = null);
