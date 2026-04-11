using System;
using Xunit;
using WeUP.Domain.Temporal;

namespace WeUP.Tests.Temporal
{
    public class TemporalPresetTests
    {
        [Fact]
        public void Midnight_preset_spans_midnight_for_market()
        {
            // Reference time: 2026-04-10T10:00:00Z (UTC) — arbitrary
            var refTime = DateTimeOffset.Parse("2026-04-10T10:00:00Z");
            var tz = "America/Los_Angeles";

            var win = TemporalPresetMapper.GetTimeWindow(TemporalPreset.Midnight, refTime, tz);

            Assert.True(win.StartUtc < win.EndUtc);
            // Duration should be 3 hours as per mapping (23:00 -> 02:00)
            Assert.Equal(TimeSpan.FromHours(3), win.EndUtc - win.StartUtc);
        }

        [Fact]
        public void Now_preset_is_one_hour()
        {
            var refTime = DateTimeOffset.Parse("2026-04-10T20:30:00Z");
            var win = TemporalPresetMapper.GetTimeWindow(TemporalPreset.NOW, refTime, "America/Los_Angeles");
            Assert.Equal(TimeSpan.FromHours(1), win.EndUtc - win.StartUtc);
        }

        [Fact]
        public void Friday_preset_is_next_friday_full_day()
        {
            var refTime = DateTimeOffset.Parse("2026-04-08T12:00:00Z"); // Wednesday
            var win = TemporalPresetMapper.GetTimeWindow(TemporalPreset.Friday, refTime, "America/Los_Angeles");
            Assert.Equal(TimeSpan.FromDays(1), win.EndUtc - win.StartUtc);
        }
    }
}
