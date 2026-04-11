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
        marketTimezone ??= "America/Los_Angeles"; // default market id (IANA)
        var refUtc = (referenceTime ?? DateTimeOffset.UtcNow).ToUniversalTime();

        // Resolve a TimeZoneInfo for the market. Accept both common IANA ids and Windows ids.
        var tz = ResolveTimeZone(marketTimezone);

        // Convert reference to market-local time for canonical window computation.
        var localRef = TimeZoneInfo.ConvertTime(refUtc, TimeZoneInfo.Utc, tz);

        DateTime localStart;
        DateTime localEnd;

        switch (preset)
        {
            case TemporalPreset.NOW:
                localStart = localRef.Date.Add(localRef.TimeOfDay);
                localEnd = localStart.AddHours(1);
                break;

            case TemporalPreset.Evening6PM:
                localStart = localRef.Date.AddHours(18);
                localEnd = localStart.AddHours(1);
                break;

            case TemporalPreset.Evening9PM:
                localStart = localRef.Date.AddHours(21);
                localEnd = localStart.AddHours(1);
                break;

            case TemporalPreset.Midnight:
                // Cross-midnight nightlife window: 23:00 -> 02:00 (next day)
                localStart = localRef.Date.AddHours(23);
                localEnd = localStart.AddHours(3);
                break;

            case TemporalPreset.EarlyMorning3AM:
                localStart = localRef.Date.AddHours(3);
                localEnd = localStart.AddHours(1);
                break;

            case TemporalPreset.Friday:
                (localStart, localEnd) = GetNextDayRange(localRef, DayOfWeek.Friday);
                break;

            case TemporalPreset.Saturday:
                (localStart, localEnd) = GetNextDayRange(localRef, DayOfWeek.Saturday);
                break;

            case TemporalPreset.Sunday:
                (localStart, localEnd) = GetNextDayRange(localRef, DayOfWeek.Sunday);
                break;

            default:
                throw new ArgumentException($"Unknown preset: {preset}");
        }

        // Convert local start/end back to UTC offsets
        var startOffset = new DateTimeOffset(localStart, tz.GetUtcOffset(localStart)).ToUniversalTime();
        var endOffset = new DateTimeOffset(localEnd, tz.GetUtcOffset(localEnd)).ToUniversalTime();

        return new TimeWindow(startOffset, endOffset, tz.Id);
    }

    /// <summary>
    /// Helper to compute next occurrence of a day of week (midnight to midnight).
    /// </summary>
    private static (DateTime startLocal, DateTime endLocal) GetNextDayRange(DateTimeOffset localRef, DayOfWeek targetDay)
    {
        var daysUntil = ((int)targetDay - (int)localRef.DayOfWeek + 7) % 7;
        if (daysUntil == 0) daysUntil = 7; // prefer next week's instance if same-day
        var dayStart = localRef.Date.AddDays(daysUntil);
        return (dayStart, dayStart.AddDays(1));
    }

    /// <summary>
    /// Resolve a TimeZoneInfo from a market identifier. Accept a few common IANA ids
    /// and map them to Windows ids when necessary. Extend this mapping as markets are added.
    /// </summary>
    private static TimeZoneInfo ResolveTimeZone(string marketTimezone)
    {
        try
        {
            // Try as provided (works for Windows timezone ids on Windows)
            return TimeZoneInfo.FindSystemTimeZoneById(marketTimezone);
        }
        catch
        {
            // Try a tiny IANA -> Windows mapping for common markets (expand as needed)
            var iana = marketTimezone;
            var mapping = iana.ToLowerInvariant() switch
            {
                "america/los_angeles" => "Pacific Standard Time",
                "america/new_york" => "Eastern Standard Time",
                "america/chicago" => "Central Standard Time",
                "europe/london" => "GMT Standard Time",
                _ => null
            };

            if (mapping != null)
                return TimeZoneInfo.FindSystemTimeZoneById(mapping);

            // Fall back to UTC
            return TimeZoneInfo.Utc;
        }
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
