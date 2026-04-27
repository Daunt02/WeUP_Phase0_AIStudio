using WeUP.Contracts.Events;

namespace WeUP.Contracts.Temporal;

/// <summary>
/// Canonical temporal query contract shared by map, calendar, and saved-event retrieval surfaces.
///
/// Ownership boundary:
/// - Frontend chooses intent (preset or explicit range) and sends this DTO.
/// - Backend remains authoritative for resolving and validating effective windows.
/// - Frontend must not invent alternate preset semantics.
/// </summary>
public sealed record TemporalQueryDto
{
    /// <summary>Named temporal intent; resolved by backend into effective UTC window bounds.</summary>
    public TimeWindowPreset Preset { get; init; } = TimeWindowPreset.Now;

    /// <summary>
    /// Explicit UTC start bound. Required for custom-range requests.
    /// Optional for preset-based requests.
    /// </summary>
    public DateTimeOffset? FromUtc { get; init; }

    /// <summary>
    /// Explicit UTC end bound. Required for custom-range requests.
    /// Optional for preset-based requests.
    /// </summary>
    public DateTimeOffset? ToUtc { get; init; }

    /// <summary>
    /// Canonical market timezone identifier (IANA preferred). Always required.
    /// </summary>
    public string MarketTimezone { get; init; } = string.Empty;

    /// <summary>
    /// Optional deterministic reference instant for preset expansion.
    /// If omitted, backend uses current UTC instant.
    /// </summary>
    public DateTimeOffset? ReferenceInstantUtc { get; init; }

    /// <summary>
    /// Query is custom-range driven when preset is either explicit CustomRange or legacy Custom.
    /// </summary>
    public bool IsCustomRange =>
        Preset is TimeWindowPreset.CustomRange or TimeWindowPreset.Custom;
}
