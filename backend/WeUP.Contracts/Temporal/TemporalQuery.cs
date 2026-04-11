using System;

namespace WeUP.Contracts.Temporal
{
    public enum TemporalQueryMode
    {
        Preset,
        AbsoluteWindow,
        CalendarDate
    }

    public enum TemporalPresetDto
    {
        NOW = 0,
        Evening6PM = 1,
        Evening9PM = 2,
        Midnight = 3,
        EarlyMorning3AM = 4,
        Friday = 5,
        Saturday = 6,
        Sunday = 7
    }

    public record TimeWindowDto(DateTimeOffset StartUtc, DateTimeOffset EndUtc, string Timezone);

    public record DateWindowDto(DateOnly Date, string Timezone);

    public record TemporalQuery(
        TemporalQueryMode Mode,
        TemporalPresetDto? Preset = null,
        TimeWindowDto? Window = null,
        DateWindowDto? Date = null,
        string? MarketTimezone = null,
        DateTimeOffset? ReferenceTimeUtc = null
    );
}
