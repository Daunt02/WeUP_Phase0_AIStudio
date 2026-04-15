namespace WeUP.Domain.Temporal;

/// <summary>
/// Temporal presets for quick time window selection.
/// Maps UI labels (NOW, 6PM, MIDNIGHT, etc.) to canonical time windows.
/// P21: Convert Timeline and Calendar UX into Real Temporal Query Logic
/// </summary>
public enum TemporalPreset
{
    /// <summary>Events in today's local market day window.</summary>
    Today = 0,

    /// <summary>Local nightlife window from 18:00 to 03:00 next day.</summary>
    Tonight = 1,

    /// <summary>Friday 18:00 through Monday 00:00 in market local time.</summary>
    Weekend = 2,

    /// <summary>Rolling next seven days from reference time.</summary>
    Next7Days = 3,
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
        marketTimezone ??= "America/Chicago"; // Houston Phase 0 default market timezone
        var refUtc = (referenceTime ?? DateTimeOffset.UtcNow).ToUniversalTime();

        // Resolve a TimeZoneInfo for the market. Accept both common IANA ids and Windows ids.
        var resolved = ResolveTimeZone(marketTimezone);
        var tz = resolved.Timezone;

        // Convert reference to market-local time for canonical window computation.
        var localRef = TimeZoneInfo.ConvertTime(refUtc, tz);

        DateTime localStart;
        DateTime localEnd;

        switch (preset)
        {
            case TemporalPreset.Today:
                localStart = localRef.Date;
                localEnd = localStart.AddDays(1);
                break;

            case TemporalPreset.Tonight:
                // Cross-midnight nightlife window: 18:00 -> 03:00 (next day)
                localStart = localRef.Date.AddHours(18);
                localEnd = localStart.AddHours(9);
                break;

            case TemporalPreset.Weekend:
                (localStart, localEnd) = GetWeekendRange(localRef);
                break;

            case TemporalPreset.Next7Days:
                localStart = localRef.DateTime;
                localEnd = localStart.AddDays(7);
                break;

            default:
                throw new ArgumentException($"Unknown preset: {preset}");
        }

        // Preserve the market-local offset on the returned window. Existing callers and tests
        // interpret these boundaries in market-local time even though the DTO field name says Utc.
        var startOffset = new DateTimeOffset(localStart, tz.GetUtcOffset(localStart));
        var endOffset = new DateTimeOffset(localEnd, tz.GetUtcOffset(localEnd));

        var timezoneForContract = resolved.ContractTimezone;
        return new TimeWindow(startOffset, endOffset, timezoneForContract);
    }

    private static (DateTime startLocal, DateTime endLocal) GetWeekendRange(DateTimeOffset localRef)
    {
        // Friday 18:00 through Monday 00:00 in local time.
        const int friday = (int)DayOfWeek.Friday;
        var today = (int)localRef.DayOfWeek;
        var daysUntilFriday = (friday - today + 7) % 7;
        var fridayStart = localRef.Date.AddDays(daysUntilFriday).AddHours(18);
        return (fridayStart, fridayStart.AddHours(54));
    }

    /// <summary>
    /// Resolve a TimeZoneInfo from a market identifier. Accept a few common IANA ids
    /// and map them to Windows ids when necessary. Extend this mapping as markets are added.
    /// </summary>
    private static (TimeZoneInfo Timezone, string ContractTimezone) ResolveTimeZone(string marketTimezone)
    {
        try
        {
            // Try as provided (works for Windows timezone ids on Windows)
            return (TimeZoneInfo.FindSystemTimeZoneById(marketTimezone), marketTimezone);
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
                return (TimeZoneInfo.FindSystemTimeZoneById(mapping), marketTimezone);

            // Fall back to UTC
            return (TimeZoneInfo.Utc, "UTC");
        }
    }

    /// <summary>
    /// Get the preset label for UI display.
    /// </summary>
    public static string GetPresetLabel(TemporalPreset preset) => preset switch
    {
        TemporalPreset.Today => "TODAY",
        TemporalPreset.Tonight => "TONIGHT",
        TemporalPreset.Weekend => "WEEKEND",
        TemporalPreset.Next7Days => "NEXT 7 DAYS",
        _ => "UNKNOWN",
    };
}
