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
        Today = 0,
        Tonight = 1,
        Weekend = 2,
        Next7Days = 3
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
