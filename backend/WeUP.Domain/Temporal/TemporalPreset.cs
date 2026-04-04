namespace WeUP.Domain.Temporal;

/// <summary>
/// Temporal presets for quick time window selection.
/// Maps UI labels (NOW, 6PM, MIDNIGHT, etc.) to canonical time windows.
/// P21: Convert Timeline and Calendar UX into Real Temporal Query Logic
/// </summary>
public enum TemporalPreset
{
    /// <summary>Events happening right now.</summary>
    NOW = 0,

    /// <summary>Events in the 6 PM hour.</summary>
    Evening6PM = 1,

    /// <summary>Events in the 9 PM hour.</summary>
    Evening9PM = 2,

    /// <summary>Events at midnight and early morning.</summary>
    Midnight = 3,

    /// <summary>Events in the 3 AM hour (early morning).</summary>
    EarlyMorning3AM = 4,

    /// <summary>Events on Friday.</summary>
    Friday = 5,

    /// <summary>Events on Saturday.</summary>
    Saturday = 6,

    /// <summary>Events on Sunday.</summary>
    Sunday = 7,
}

/// <summary>
/// Time window with timezone awareness.
/// Used for querying events within a specific time range.
/// </summary>
public record TimeWindow(
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Timezone)
{
    /// <summary>
    /// Validates that start is before end.
    /// </summary>
    public void Validate()
    {
        if (StartUtc >= EndUtc)
            throw new InvalidOperationException($"StartUtc ({StartUtc}) must be before EndUtc ({EndUtc})");
    }

    /// <summary>
    /// Checks if a given time falls within this window.
    /// </summary>
    public bool Contains(DateTimeOffset time)
    {
        return time >= StartUtc && time < EndUtc;
    }
}

/// <summary>
/// Service for computing time windows from presets.
/// </summary>
public static class TemporalPresetMapper
{
    /// <summary>
    /// Convert a temporal preset to a TimeWindow.
    /// Presets are relative to the current moment in the market's local timezone.
    /// </summary>
    public static TimeWindow GetTimeWindow(
        TemporalPreset preset,
        DateTimeOffset? referenceTime = null,
        string? marketTimezone = null)
    {
        marketTimezone ??= "America/Los_Angeles"; // Default to SF
        var refTime = referenceTime ?? DateTimeOffset.UtcNow;

        // For this MVP, use a simplified approach:
        // Real implementation would:
        // 1. Convert refTime to market-local time
        // 2. Compute window boundaries in market-local time
        // 3. Convert back to UTC

        var now = refTime;
        return preset switch
        {
            TemporalPreset.NOW =>
                new TimeWindow(now, now.AddHours(1), marketTimezone),

            TemporalPreset.Evening6PM =>
                new TimeWindow(now.Date.AddHours(18), now.Date.AddHours(19), marketTimezone),

            TemporalPreset.Evening9PM =>
                new TimeWindow(now.Date.AddHours(21), now.Date.AddHours(22), marketTimezone),

            TemporalPreset.Midnight =>
                // Cross-midnight: 11 PM to 2 AM
                new TimeWindow(now.Date.AddHours(23), now.Date.AddDays(1).AddHours(2), marketTimezone),

            TemporalPreset.EarlyMorning3AM =>
                new TimeWindow(now.Date.AddHours(3), now.Date.AddHours(4), marketTimezone),

            TemporalPreset.Friday =>
            {
                // Get next Friday midnight to Saturday midnight
                var daysUntilFriday = ((int)DayOfWeek.Friday - (int)now.DayOfWeek + 7) % 7;
                if (daysUntilFriday == 0) daysUntilFriday = 7; // If today is Friday, get next Friday
                var friday = now.Date.AddDays(daysUntilFriday);
                return new TimeWindow(friday, friday.AddDays(1), marketTimezone);
            },

            TemporalPreset.Saturday =>
            {
                var daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)now.DayOfWeek + 7) % 7;
                if (daysUntilSaturday == 0) daysUntilSaturday = 7;
                var saturday = now.Date.AddDays(daysUntilSaturday);
                return new TimeWindow(saturday, saturday.AddDays(1), marketTimezone);
            },

            TemporalPreset.Sunday =>
            {
                var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)now.DayOfWeek + 7) % 7;
                if (daysUntilSunday == 0) daysUntilSunday = 7;
                var sunday = now.Date.AddDays(daysUntilSunday);
                return new TimeWindow(sunday, sunday.AddDays(1), marketTimezone);
            },

            _ => throw new ArgumentException($"Unknown preset: {preset}"),
        };
    }

    /// <summary>
    /// Get the preset label for UI display.
    /// </summary>
    public static string GetPresetLabel(TemporalPreset preset) => preset switch
    {
        TemporalPreset.NOW => "NOW",
        TemporalPreset.Evening6PM => "6PM",
        TemporalPreset.Evening9PM => "9PM",
        TemporalPreset.Midnight => "MIDNIGHT",
        TemporalPreset.EarlyMorning3AM => "3AM",
        TemporalPreset.Friday => "FRI",
        TemporalPreset.Saturday => "SAT",
        TemporalPreset.Sunday => "SUN",
        _ => "UNKNOWN",
    };
}
