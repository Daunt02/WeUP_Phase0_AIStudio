using System;
using Xunit;
using WeUP.Domain.Temporal;

namespace WeUP.Tests.Temporal
{
    public class TemporalPresetTests
    {
        [Fact]
        public void Tonight_preset_spans_nine_hours_for_market()
        {
            var refTime = DateTimeOffset.Parse("2026-04-10T10:00:00Z");
            var tz = "America/Chicago";

            var win = TemporalPresetMapper.GetTimeWindow(TemporalPreset.Tonight, refTime, tz);

            Assert.True(win.StartUtc < win.EndUtc);
            Assert.Equal(TimeSpan.FromHours(9), win.EndUtc - win.StartUtc);
        }

        [Fact]
        public void Today_preset_is_one_day()
        {
            var refTime = DateTimeOffset.Parse("2026-04-10T20:30:00Z");
            var win = TemporalPresetMapper.GetTimeWindow(TemporalPreset.Today, refTime, "America/Chicago");
            Assert.Equal(TimeSpan.FromDays(1), win.EndUtc - win.StartUtc);
        }

        [Fact]
        public void Weekend_preset_is_friday_evening_through_monday_start()
        {
            var refTime = DateTimeOffset.Parse("2026-04-08T12:00:00Z"); // Wednesday
            var win = TemporalPresetMapper.GetTimeWindow(TemporalPreset.Weekend, refTime, "America/Chicago");
            Assert.Equal(TimeSpan.FromHours(54), win.EndUtc - win.StartUtc);
        }
    }
}
