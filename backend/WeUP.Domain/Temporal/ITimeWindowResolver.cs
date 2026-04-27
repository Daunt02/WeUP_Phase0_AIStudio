namespace WeUP.Domain.Temporal;

/// <summary>
/// Resolves canonical map-discovery presets into explicit UTC time windows.
/// Implementations must be deterministic for a given (preset, timezone, referenceTime).
/// </summary>
public interface ITimeWindowResolver
{
    bool TryResolve(
        TimeWindowPreset preset,
        string timezone,
        out ResolvedTimeWindow resolved,
        out string error,
        DateTimeOffset? referenceTime = null,
        DateTimeOffset? customStartUtc = null,
        DateTimeOffset? customEndUtc = null);

    bool TryGetTimeWindow(
        TimeWindowPreset preset,
        string timezone,
        out TimeWindow window,
        out string error,
        DateTimeOffset? referenceTime = null,
        DateTimeOffset? customStartUtc = null,
        DateTimeOffset? customEndUtc = null);

    string GetPresetLabel(TimeWindowPreset preset);
}
