using System;
using Microsoft.AspNetCore.Mvc;
using WeUP.Contracts.Temporal;
using WeUP.Domain.Temporal;

namespace WeUP.Api.Controllers
{
    [ApiController]
    [Route("api/temporal")]
    public class TemporalController : ControllerBase
    {
        [HttpPost("window")]
        public ActionResult<TimeWindowDto> Window([FromBody] TemporalQuery query)
        {
            if (query == null) return BadRequest("query is required");

            var market = query.MarketTimezone ?? "America/Los_Angeles";

            if (query.Mode == TemporalQueryMode.Preset && query.Preset.HasValue)
            {
                // Convert contract preset to domain preset
                if (!Enum.TryParse<TemporalPreset>(query.Preset.Value.ToString(), out var domainPreset))
                    return BadRequest("Unknown preset");

                var window = TemporalPresetMapper.GetTimeWindow(domainPreset, query.ReferenceTimeUtc, market);
                return Ok(new TimeWindowDto(window.StartUtc, window.EndUtc, window.Timezone));
            }

            if (query.Mode == TemporalQueryMode.AbsoluteWindow && query.Window != null)
            {
                return Ok(query.Window);
            }

            if (query.Mode == TemporalQueryMode.CalendarDate && query.Date != null)
            {
                // Compute calendar date window in market-local timezone then return UTC bounds
                var resolved = ResolveTimeZone(market);
                var tz = resolved.Timezone;
                var localStart = new DateTime(query.Date.Date.Year, query.Date.Date.Month, query.Date.Date.Day, 0, 0, 0);
                var localEnd = localStart.AddDays(1);
                var startUtc = new DateTimeOffset(localStart, tz.GetUtcOffset(localStart)).ToUniversalTime();
                var endUtc = new DateTimeOffset(localEnd, tz.GetUtcOffset(localEnd)).ToUniversalTime();
                return Ok(new TimeWindowDto(startUtc, endUtc, resolved.ContractTimezone));
            }

            return BadRequest("Unsupported temporal query");
        }

        private static (TimeZoneInfo Timezone, string ContractTimezone) ResolveTimeZone(string marketTimezone)
        {
            try
            {
                return (TimeZoneInfo.FindSystemTimeZoneById(marketTimezone), marketTimezone);
            }
            catch
            {
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

                return (TimeZoneInfo.Utc, "UTC");
            }
        }
    }
}
