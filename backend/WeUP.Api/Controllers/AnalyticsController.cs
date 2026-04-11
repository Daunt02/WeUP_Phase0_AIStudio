using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using WeUP.Api.Observability;

namespace WeUP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IOperationalTelemetry _telemetry;

        public AnalyticsController(IOperationalTelemetry telemetry)
        {
            _telemetry = telemetry;
        }

        public class AnalyticsEventDto
        {
            public string? Name { get; set; }
            public Dictionary<string, string>? Props { get; set; }
            public string? UserIdHash { get; set; }
            public DateTime? Timestamp { get; set; }
        }

        [HttpPost]
        public IActionResult Post([FromBody] AnalyticsEventDto evt)
        {
            if (evt == null || string.IsNullOrWhiteSpace(evt.Name)) return BadRequest();

            try
            {
                var props = new Dictionary<string, string>();
                if (evt.Props != null)
                {
                    foreach (var kv in evt.Props)
                    {
                        // Phase 0: drop anything that looks like PII keys
                        if (System.Text.RegularExpressions.Regex.IsMatch(kv.Key, "email|phone|name|address|ssn|dob", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                            continue;
                        props[kv.Key] = kv.Value;
                    }
                }

                props["userIdHash"] = evt.UserIdHash ?? "";
                props["timestamp"] = evt.Timestamp?.ToString("o") ?? DateTime.UtcNow.ToString("o");

                if (evt.Name.StartsWith("frontend_exception", StringComparison.OrdinalIgnoreCase))
                {
                    _telemetry.TrackException(new Exception(evt.Props != null && evt.Props.ContainsKey("message") ? evt.Props["message"] : "frontend_exception"), props);
                }
                else
                {
                    _telemetry.TrackEvent(evt.Name, props);
                }

                return Accepted();
            }
            catch (Exception ex)
            {
                _telemetry.TrackException(ex, new Dictionary<string, string>{{"stage","analytics-controller"}});
                return StatusCode(500);
            }
        }
    }
}
