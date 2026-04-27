namespace WeUP.Domain.Temporal;

/// <summary>
/// Canonical temporal resolver for map-discovery presets.
///
/// Preset semantics:
/// - Tonight: local 18:00 -> local 03:00 next day. If reference is before 03:00,
///   uses the prior evening window to preserve late-night continuity.
/// - Tomorrow: next local calendar day [00:00, 00:00+1 day).
/// - ThisWeekend: Friday 18:00 -> Monday 00:00. If already inside this interval,
///   returns the containing weekend; otherwise returns the next weekend interval.
/// - Next24Hours / Next48Hours: exact rolling duration from reference instant.
///
/// DST policy:
/// - Rolling windows are computed in UTC using exact duration math.
/// - Local boundary windows are converted via explicit timezone rules.
/// - Invalid local boundary timestamps are advanced to the next valid local instant.
/// - Ambiguous local boundary timestamps choose the earliest UTC instant
///   (largest local offset) to keep behavior deterministic.
/// </summary>
public sealed class TimeWindowResolver : ITimeWindowResolver
{
    private const int NowWindowHours = 4;
    private const int Next24WindowHours = 24;
    private const int Next48WindowHours = 48;
    private const int CustomRangeMaxDays = 30;

    public bool TryResolve(
        TimeWindowPreset preset,
        string timezone,
        out ResolvedTimeWindow resolved,
        out string error,
        DateTimeOffset? referenceTime = null,
        DateTimeOffset? customStartUtc = null,
        DateTimeOffset? customEndUtc = null)
    {
        var resolutionInstant = (referenceTime ?? DateTimeOffset.UtcNow).ToUniversalTime();
        resolved = new ResolvedTimeWindow(
            resolutionInstant,
            resolutionInstant,
            "UTC",
            preset,
            string.Empty,
            resolutionInstant);

        if (!TryGetTimeWindow(
                preset,
                timezone,
                out var window,
                out error,
                referenceTime,
                customStartUtc,
                customEndUtc))
        {
            return false;
        }

        resolved = new ResolvedTimeWindow(
            window.StartUtc,
            window.EndUtc,
            window.Timezone,
            preset,
            GetPresetLabel(preset),
            resolutionInstant);

        return true;
    }

    public bool TryGetTimeWindow(
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

        if (preset == TimeWindowPreset.CustomRange || preset == TimeWindowPreset.Custom)
        {
            if (!customStartUtc.HasValue || !customEndUtc.HasValue)
            {
                error = "customStartUtc and customEndUtc are required when preset=CustomRange.";
                return false;
            }

            if (customStartUtc.Value >= customEndUtc.Value)
            {
                error = "customStartUtc must be earlier than customEndUtc.";
                return false;
            }

            if ((customEndUtc.Value - customStartUtc.Value).TotalDays > CustomRangeMaxDays)
            {
                error = $"CustomRange span must not exceed {CustomRangeMaxDays} days.";
                return false;
            }

            window = new TimeWindow(
                customStartUtc.Value.ToUniversalTime(),
                customEndUtc.Value.ToUniversalTime(),
                timezone.Trim());
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

        if (preset == TimeWindowPreset.Next24Hours)
        {
            window = new TimeWindow(utcReference, utcReference.AddHours(Next24WindowHours), contractTimezone);
            return true;
        }

        if (preset == TimeWindowPreset.Next48Hours)
        {
            window = new TimeWindow(utcReference, utcReference.AddHours(Next48WindowHours), contractTimezone);
            return true;
        }

        DateTime localStart;
        DateTime localEnd;

        switch (preset)
        {
            case TimeWindowPreset.Tonight:
                (localStart, localEnd) = ResolveTonightLocalRange(localReference);
                break;

            case TimeWindowPreset.Tomorrow:
                (localStart, localEnd) = ResolveTomorrowLocalRange(localReference);
                break;

            case TimeWindowPreset.ThisWeekend:
                (localStart, localEnd) = ResolveThisWeekendLocalRange(localReference);
                break;

            default:
                error = $"Unsupported preset '{preset}'.";
                return false;
        }

        var startUtc = ResolveLocalBoundaryToUtc(localStart, resolvedTimezone);
        var endUtc = ResolveLocalBoundaryToUtc(localEnd, resolvedTimezone);
        window = new TimeWindow(startUtc, endUtc, contractTimezone);
        return true;
    }

    public string GetPresetLabel(TimeWindowPreset preset) => GetPresetLabelStatic(preset);

    public static string GetPresetLabelStatic(TimeWindowPreset preset) => preset switch
    {
        TimeWindowPreset.Now => "Now",
        TimeWindowPreset.Tonight => "Tonight",
        TimeWindowPreset.Tomorrow => "Tomorrow",
        TimeWindowPreset.ThisWeekend => "This Weekend",
        TimeWindowPreset.Next24Hours => "Next 24 Hours",
        TimeWindowPreset.Next48Hours => "Next 48 Hours",
        TimeWindowPreset.Custom => "Custom",
        TimeWindowPreset.CustomRange => "Custom",
        _ => preset.ToString(),
    };

    /// <summary>
    /// Helper for unit tests and deterministic edge-case assertions.
    /// </summary>
    public static (DateTime startLocal, DateTime endLocal) ResolveTonightLocalRange(DateTimeOffset localReference)
    {
        var todayAtThreeAm = localReference.Date.AddHours(3);

        if (localReference.DateTime < todayAtThreeAm)
        {
            var priorEveningStart = localReference.Date.AddDays(-1).AddHours(18);
            var currentDayThreeAm = localReference.Date.AddHours(3);
            return (priorEveningStart, currentDayThreeAm);
        }

        var todayAtSixPm = localReference.Date.AddHours(18);
        return (todayAtSixPm, todayAtSixPm.AddHours(9));
    }

    /// <summary>
    /// Helper for unit tests and deterministic edge-case assertions.
    /// </summary>
    public static (DateTime startLocal, DateTime endLocal) ResolveTomorrowLocalRange(DateTimeOffset localReference)
    {
        var start = localReference.Date.AddDays(1);
        return (start, start.AddDays(1));
    }

    /// <summary>
    /// Helper for unit tests and deterministic edge-case assertions.
    /// </summary>
    public static (DateTime startLocal, DateTime endLocal) ResolveThisWeekendLocalRange(DateTimeOffset localReference)
    {
        var daysSinceFriday = ((int)localReference.DayOfWeek - (int)DayOfWeek.Friday + 7) % 7;
        var currentWeekFridayStart = localReference.Date.AddDays(-daysSinceFriday).AddHours(18);
        var currentWeekendEnd = currentWeekFridayStart.AddHours(54);

        if (localReference.DateTime < currentWeekendEnd)
        {
            return (currentWeekFridayStart, currentWeekendEnd);
        }

        var nextWeekFridayStart = currentWeekFridayStart.AddDays(7);
        return (nextWeekFridayStart, nextWeekFridayStart.AddHours(54));
    }

    /// <summary>
    /// Converts a market-local boundary to UTC while handling DST anomalies explicitly.
    /// </summary>
    public static DateTimeOffset ResolveLocalBoundaryToUtc(DateTime localBoundary, TimeZoneInfo timezone)
    {
        var local = DateTime.SpecifyKind(localBoundary, DateTimeKind.Unspecified);

        while (timezone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        TimeSpan offset;
        if (timezone.IsAmbiguousTime(local))
        {
            var offsets = timezone.GetAmbiguousTimeOffsets(local);
            offset = offsets.Max();
        }
        else
        {
            offset = timezone.GetUtcOffset(local);
        }

        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    /// <summary>
    /// Resolves IANA/Windows timezone identifiers without falling back to server-local timezone.
    /// </summary>
    public static bool TryResolveTimezone(
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
                "america/denver" => "Mountain Standard Time",
                "america/phoenix" => "US Mountain Standard Time",
                "europe/london" => "GMT Standard Time",
                "europe/paris" => "Romance Standard Time",
                "asia/tokyo" => "Tokyo Standard Time",
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
