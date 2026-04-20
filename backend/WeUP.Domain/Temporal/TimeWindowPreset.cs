namespace WeUP.Domain.Temporal;

/// <summary>
/// Canonical map-discovery time windows.
/// These values are shared with the frontend and must remain semantically aligned.
/// </summary>
public enum TimeWindowPreset
{
    Now = 0,
    Tonight = 1,
    Tomorrow = 2,
    ThisWeekend = 3,
    Custom = 4,
}

/// <summary>
/// Computes concrete time windows for canonical map discovery presets.
/// The frontend never expands presets on its own; the backend owns the semantics.
/// </summary>
public static class TimeWindowPresetMapper
{
    private const int NowWindowHours = 4;

    public static bool TryGetTimeWindow(
        TimeWindowPreset preset,
        string timezone,
        out TimeWindow window,
        out string error,
        DateTimeOffset? referenceTime = null,
        DateTimeOffset? customStartUtc = null,
        DateTimeOffset? customEndUtc = null)
    {
        error = string.Empty;
        window = new TimeWindow(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "UTC");

        if (string.IsNullOrWhiteSpace(timezone))
        {
            error = "timezone is required and must be an IANA or Windows timezone identifier.";
            return false;
        }

        if (preset == TimeWindowPreset.Custom)
        {
            if (!customStartUtc.HasValue || !customEndUtc.HasValue)
            {
                error = "customStartUtc and customEndUtc are required when preset=custom.";
                return false;
            }

            if (customStartUtc.Value >= customEndUtc.Value)
            {
                error = "customStartUtc must be earlier than customEndUtc.";
                return false;
            }

            window = new TimeWindow(customStartUtc.Value, customEndUtc.Value, timezone.Trim());
            return true;
        }

        if (!TryResolveTimezone(timezone, out var resolvedTimezone, out var contractTimezone))
        {
            error = $"Unsupported timezone '{timezone}'. Use a supported IANA or Windows timezone identifier.";
            return false;
        }

        var utcReference = (referenceTime ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var localReference = TimeZoneInfo.ConvertTime(utcReference, resolvedTimezone);

        if (preset == TimeWindowPreset.Now)
        {
            window = new TimeWindow(utcReference, utcReference.AddHours(NowWindowHours), contractTimezone);
            return true;
        }

        DateTime localStart;
        DateTime localEnd;

        switch (preset)
        {
            case TimeWindowPreset.Tonight:
                (localStart, localEnd) = GetTonightRange(localReference);
                break;

            case TimeWindowPreset.Tomorrow:
                localStart = localReference.Date.AddDays(1);
                localEnd = localStart.AddDays(1);
                break;

            case TimeWindowPreset.ThisWeekend:
                (localStart, localEnd) = GetThisWeekendRange(localReference);
                break;

            default:
                error = $"Unsupported preset '{preset}'.";
                return false;
        }

        var startOffset = new DateTimeOffset(localStart, resolvedTimezone.GetUtcOffset(localStart));
        var endOffset = new DateTimeOffset(localEnd, resolvedTimezone.GetUtcOffset(localEnd));
        window = new TimeWindow(startOffset.ToUniversalTime(), endOffset.ToUniversalTime(), contractTimezone);
        return true;
    }

    private static (DateTime startLocal, DateTime endLocal) GetTonightRange(DateTimeOffset localReference)
    {
        var todayAtSixPm = localReference.Date.AddHours(18);
        var todayAtThreeAm = localReference.Date.AddHours(3);

        if (localReference.DateTime < todayAtThreeAm)
        {
            var priorEvening = localReference.Date.AddDays(-1).AddHours(18);
            return (priorEvening, localReference.Date.AddHours(3));
        }

        if (localReference.DateTime >= todayAtSixPm)
        {
            return (todayAtSixPm, todayAtSixPm.AddHours(9));
        }

        return (todayAtSixPm, todayAtSixPm.AddHours(9));
    }

    private static (DateTime startLocal, DateTime endLocal) GetThisWeekendRange(DateTimeOffset localReference)
    {
        var daysSinceFriday = ((int)localReference.DayOfWeek - (int)DayOfWeek.Friday + 7) % 7;
        var currentWeekFriday = localReference.Date.AddDays(-daysSinceFriday).AddHours(18);
        var currentWeekMondayBoundary = currentWeekFriday.AddHours(54);

        if (localReference.DateTime < currentWeekFriday)
        {
            return (currentWeekFriday, currentWeekMondayBoundary);
        }

        if (localReference.DateTime < currentWeekMondayBoundary)
        {
            return (currentWeekFriday, currentWeekMondayBoundary);
        }

        var nextWeekFriday = currentWeekFriday.AddDays(7);
        return (nextWeekFriday, nextWeekFriday.AddHours(54));
    }

    private static bool TryResolveTimezone(
        string timezone,
        out TimeZoneInfo resolvedTimezone,
        out string contractTimezone)
    {
        resolvedTimezone = TimeZoneInfo.Utc;
        contractTimezone = timezone.Trim();

        if (string.IsNullOrWhiteSpace(contractTimezone))
        {
            return false;
        }

        try
        {
            resolvedTimezone = TimeZoneInfo.FindSystemTimeZoneById(contractTimezone);
            return true;
        }
        catch
        {
            var windowsId = contractTimezone.ToLowerInvariant() switch
            {
                "america/los_angeles" => "Pacific Standard Time",
                "america/new_york" => "Eastern Standard Time",
                "america/chicago" => "Central Standard Time",
                "europe/london" => "GMT Standard Time",
                _ => null,
            };

            if (windowsId is null)
            {
                return false;
            }

            try
            {
                resolvedTimezone = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}