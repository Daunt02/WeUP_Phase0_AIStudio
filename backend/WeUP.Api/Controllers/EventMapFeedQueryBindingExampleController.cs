using Microsoft.AspNetCore.Mvc;
using WeUP.Contracts.Events;

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
        [FromQuery] TimeWindowPreset preset,
        [FromQuery] string timezone,
        [FromQuery] DateTimeOffset? customStartUtc,
        [FromQuery] DateTimeOffset? customEndUtc,
        [FromQuery] string? district,
        [FromQuery] string[]? categories,
        [FromQuery] bool includeSavedOnly = false)
    {
        // Bind raw query params into the canonical contract once so downstream
        // services can apply one shared temporal policy.
        var query = new EventMapFeedQueryDto
        {
            Bbox = bbox,
            Preset = preset,
            Timezone = timezone,
            CustomStartUtc = customStartUtc,
            CustomEndUtc = customEndUtc,
            District = district,
            Categories = categories,
            IncludeSavedOnly = includeSavedOnly,
        };

        if (query.Preset == TimeWindowPreset.Custom)
        {
            if (query.CustomStartUtc is null || query.CustomEndUtc is null)
            {
                return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["custom"] = ["customStartUtc and customEndUtc are required when preset=custom."],
                }));
            }

            if (query.CustomStartUtc >= query.CustomEndUtc)
            {
                return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["custom"] = ["customStartUtc must be earlier than customEndUtc."],
                }));
            }
        }

        return Ok(query);
    }
}
