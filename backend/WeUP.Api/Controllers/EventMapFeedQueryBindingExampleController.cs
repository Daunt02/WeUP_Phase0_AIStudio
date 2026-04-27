using Microsoft.AspNetCore.Mvc;
using WeUP.Contracts.Events;
using WeUP.Contracts.Temporal;

namespace WeUP.Api.Controllers;

/// <summary>
/// Example-only controller showing explicit query binding for EventMapFeedQueryDto.
/// This keeps map and calendar temporal semantics aligned by transporting preset + timezone
/// and never deriving date windows in the client.
///
/// NOTE: Program.cs currently uses Minimal APIs for production routes. This controller
/// is provided as a reference for teams preferring attribute-routing style handlers.
/// </summary>
[ApiController]
[Route("api/examples/events")]
public sealed class EventMapFeedQueryBindingExampleController : ControllerBase
{
    [HttpGet("map-feed-query-binding")]
    public ActionResult<EventMapFeedQueryDto> GetMapFeedQueryBindingExample(
        [FromQuery] string bbox,
        [FromQuery] TemporalQueryDto temporal,
        [FromQuery] string? district,
        [FromQuery] string[]? categories,
        [FromQuery] bool includeSavedOnly = false)
    {
        if (string.IsNullOrWhiteSpace(temporal.MarketTimezone))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["marketTimezone"] = ["marketTimezone is required."],
            }));
        }

        // Bind raw query params into the canonical contract once so downstream
        // services can apply one shared temporal policy.
        var query = new EventMapFeedQueryDto
        {
            Bbox = bbox,
            Preset = temporal.Preset,
            Timezone = temporal.MarketTimezone,
            CustomStartUtc = temporal.FromUtc,
            CustomEndUtc = temporal.ToUtc,
            District = district,
            Categories = categories,
            IncludeSavedOnly = includeSavedOnly,
        };

        if (temporal.IsCustomRange)
        {
            if (query.CustomStartUtc is null || query.CustomEndUtc is null)
            {
                return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["fromUtc"] = ["fromUtc and toUtc are required when preset is custom range."],
                }));
            }

            if (query.CustomStartUtc >= query.CustomEndUtc)
            {
                return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["fromUtc"] = ["fromUtc must be earlier than toUtc."],
                }));
            }
        }

        return Ok(query);
    }
}
