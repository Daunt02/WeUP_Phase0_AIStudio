using Microsoft.AspNetCore.Mvc;
using WeUP.Contracts.Events;
using WeUP.Contracts.Spatial;
using WeUP.Application.Spatial;
using WeUP.Contracts.Temporal;

namespace WeUP.Api.Controllers;

/// <summary>
/// Example controller demonstrating canonical spatial query binding and validation.
/// Shows how to:
/// 1. Bind query parameters to EventMapFeedQueryV2Dto.
/// 2. Validate spatial queries using ISpatialQueryValidator.
/// 3. Handle validation errors explicitly (no hidden fallbacks).
/// 4. Extract normalized spatial semantics for downstream handlers.
/// 5. Ensure map, calendar, and saved discovery align to one spatial contract.
///
/// PRODUCTION PATTERN:
/// - All event discovery endpoints (map, calendar, saved) should bind to this same DTO.
/// - Validation happens once at the binding layer.
/// - Query handlers receive pre-validated, normalized SpatialQueryDto.
/// - No component-level spatial filtering logic (centralized at binding).
/// </summary>
[ApiController]
[Route("api/examples/events/spatial")]
public sealed class SpatialQueryBindingExampleController : ControllerBase
{
    private readonly ISpatialQueryValidator _spatialValidator;
    private readonly ILogger<SpatialQueryBindingExampleController> _logger;

    public SpatialQueryBindingExampleController(
        ISpatialQueryValidator spatialValidator,
        ILogger<SpatialQueryBindingExampleController> logger)
    {
        _spatialValidator = spatialValidator ?? throw new ArgumentNullException(nameof(spatialValidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Map feed with canonical spatial query binding.
    /// 
    /// EXAMPLE USAGE:
    /// 
    /// 1. BBOX-ONLY (freeform exploration):
    ///    GET /api/examples/events/spatial/map-feed?bbox=-95.7129,29.7589,-95.0681,30.2271&preset=now&timezone=America/Chicago
    ///    Response: Events in bounding box only (no taxonomy filter).
    ///
    /// 2. TAXONOMY-ONLY (curated by market/district):
    ///    GET /api/examples/events/spatial/map-feed?marketIds=houston&districtIds=downtown&preset=now&timezone=America/Chicago
    ///    Response: All events in downtown district, regardless of map bounds.
    ///
    /// 3. BBOX WITH TAXONOMY (refined search):
    ///    GET /api/examples/events/spatial/map-feed?bbox=-95.7129,29.7589,-95.0681,30.2271&districtIds=downtown&preset=now&timezone=America/Chicago
    ///    Response: Events in bbox AND downtown district (both filters apply).
    ///
    /// 4. SLUG-BASED (URL-friendly):
    ///    GET /api/examples/events/spatial/map-feed?marketSlugs=houston&districtSlugs=downtown&preset=now&timezone=America/Chicago
    ///    Backend resolves slugs to IDs, then proceeds as taxonomy-only.
    ///
    /// 5. NEIGHBORHOOD-SPECIFIC:
    ///    GET /api/examples/events/spatial/map-feed?neighborhoodIds=midtown,heights&preset=now&timezone=America/Chicago
    ///    Backend infers parent districts and markets automatically.
    ///
    /// ERROR HANDLING:
    /// - Bbox-only + taxonomy-only: 422 Unprocessable Entity (ambiguous combination).
    /// - Invalid slug: 422 with detailed error.
    /// - Missing timezone: 400 Bad Request.
    /// </summary>
    [HttpGet("map-feed")]
    [Produces("application/json")]
    public async Task<ActionResult> GetMapFeedWithSpatialQuery(
        // Spatial dimensions (query parameters)
        [FromQuery] string[]? marketIds,
        [FromQuery] string[]? marketSlugs,
        [FromQuery] string[]? districtIds,
        [FromQuery] string[]? districtSlugs,
        [FromQuery] string[]? neighborhoodIds,
        [FromQuery] string[]? neighborhoodSlugs,
        [FromQuery] string? bbox,
        [FromQuery] bool includeDescendants = true,
        [FromQuery] double minSpatialConfidence = 0.0,
        // Temporal dimensions
        [FromQuery] TimeWindowPreset preset = TimeWindowPreset.Now,
        [FromQuery] string? timezone,
        [FromQuery] string? marketTimezone,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] DateTimeOffset? referenceInstantUtc,
        [FromQuery] DateTimeOffset? customStartUtc,
        [FromQuery] DateTimeOffset? customEndUtc,
        // Content filters
        [FromQuery] string[]? categories,
        [FromQuery] bool includeSavedOnly = false)
    {
        // Bind all parameters into canonical DTO once
        var query = new EventMapFeedQueryV2Dto
        {
            // Spatial
            MarketIds = marketIds,
            MarketSlugs = marketSlugs,
            DistrictIds = districtIds,
            DistrictSlugs = districtSlugs,
            NeighborhoodIds = neighborhoodIds,
            NeighborhoodSlugs = neighborhoodSlugs,
            Bbox = bbox,
            IncludeDescendants = includeDescendants,
            MinSpatialConfidence = minSpatialConfidence,
            // Temporal
            Preset = preset,
            Timezone = timezone ?? string.Empty,
            MarketTimezone = marketTimezone,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            ReferenceInstantUtc = referenceInstantUtc,
            CustomStartUtc = customStartUtc,
            CustomEndUtc = customEndUtc,
            // Content
            Categories = categories,
            IncludeSavedOnly = includeSavedOnly
        };

        // Validate temporal dimension (timezone required)
        if (string.IsNullOrWhiteSpace(query.Timezone))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["timezone"] = ["timezone is required for all event queries."]
            }));
        }

        // Extract spatial query and validate
        var spatialQuery = query.ToSpatialQuery();
        var spatialValidation = await _spatialValidator.ValidateAsync(spatialQuery);

        if (!spatialValidation.IsValid())
        {
            _logger.LogInformation("Spatial query validation failed: {Errors}",
                string.Join("; ", spatialValidation.Errors));

            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Invalid spatial query.",
                Detail = string.Join("; ", spatialValidation.Errors),
                Type = "https://example.com/docs/spatial-query-errors"
            });
        }

        // At this point, spatialValidation.NormalizedQuery is guaranteed non-null
        var normalizedSpatial = spatialValidation.NormalizedQuery!;

        _logger.LogInformation("Spatial query validated: Mode={Mode}, Markets={Markets}, Districts={Districts}, Neighborhoods={Neighborhoods}",
            normalizedSpatial.ResolvedMode,
            string.Join(",", normalizedSpatial.MarketIds ?? Array.Empty<string>()),
            string.Join(",", normalizedSpatial.DistrictIds ?? Array.Empty<string>()),
            string.Join(",", normalizedSpatial.NeighborhoodIds ?? Array.Empty<string>()));

        // Return normalized query as example
        // Production handlers would pass normalizedSpatial to query service
        return Ok(new
        {
            spatialQuery = normalizedSpatial,
            compositionMode = normalizedSpatial.ResolvedMode,
            temporalQuery = new
            {
                preset = query.Preset,
                timezone = query.Timezone,
                fromUtc = query.FromUtc,
                toUtc = query.ToUtc
            },
            message = "Query validated successfully. Use normalized spatial query for execution."
        });
    }

    /// <summary>
    /// Calendar feed with spatial query semantics.
    /// Accepts same spatial dimensions as map feed.
    /// Ensures map and calendar project the same canonical event set.
    /// </summary>
    [HttpGet("calendar-feed")]
    [Produces("application/json")]
    public async Task<ActionResult> GetCalendarFeedWithSpatialQuery(
        [FromQuery] string[]? marketIds,
        [FromQuery] string[]? districtIds,
        [FromQuery] string[]? neighborhoodIds,
        [FromQuery] string? bbox,
        [FromQuery] bool includeDescendants = true,
        [FromQuery] TimeWindowPreset preset = TimeWindowPreset.Now,
        [FromQuery] string? timezone,
        [FromQuery] string[]? categories,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (string.IsNullOrWhiteSpace(timezone))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["timezone"] = ["timezone is required."]
            }));
        }

        var spatialQuery = new SpatialQueryDto
        {
            MarketIds = marketIds,
            DistrictIds = districtIds,
            NeighborhoodIds = neighborhoodIds,
            Bbox = bbox,
            IncludeDescendants = includeDescendants
        };

        var validation = await _spatialValidator.ValidateAsync(spatialQuery);
        if (!validation.IsValid())
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Invalid spatial query.",
                Detail = string.Join("; ", validation.Errors)
            });
        }

        return Ok(new
        {
            spatialQuery = validation.NormalizedQuery,
            pagination = new { page, pageSize },
            message = "Calendar feed query validated. Use normalized spatial query for execution."
        });
    }

    /// <summary>
    /// Saved discovery feed with spatial query semantics.
    /// Same validation as map/calendar; applies to user-saved events.
    /// </summary>
    [HttpGet("saved-feed")]
    [Produces("application/json")]
    public async Task<ActionResult> GetSavedFeedWithSpatialQuery(
        [FromQuery] string[]? marketIds,
        [FromQuery] string[]? districtIds,
        [FromQuery] string? bbox,
        [FromQuery] TimeWindowPreset preset = TimeWindowPreset.Now,
        [FromQuery] string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["timezone"] = ["timezone is required."]
            }));
        }

        var spatialQuery = new SpatialQueryDto
        {
            MarketIds = marketIds,
            DistrictIds = districtIds,
            Bbox = bbox
        };

        var validation = await _spatialValidator.ValidateAsync(spatialQuery);
        if (!validation.IsValid())
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Invalid spatial query.",
                Detail = string.Join("; ", validation.Errors)
            });
        }

        return Ok(new
        {
            spatialQuery = validation.NormalizedQuery,
            message = "Saved feed query validated. Use normalized spatial query for execution."
        });
    }
}
