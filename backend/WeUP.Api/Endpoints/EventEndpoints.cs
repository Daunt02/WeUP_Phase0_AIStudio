using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System.Globalization;
using WeUP.Application.Events;
using WeUP.Contracts.Events;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Events;
using WeUP.Domain.Moderation;
using WeUP.Domain.Temporal;
using WeUP.Domain.Users;

namespace WeUP.Api.Endpoints;

public static class EventEndpoints
{
    private const int V1ClusterActivationVisibleEventCountThreshold = 24;
    private const double V1ClusterActivationMaxZoomInclusive = 13.5;
    private const int V1ClusterRadiusPixels = 56;
    private const int V1ClusterMaxZoomInclusive = 15;
    private const int V1ClusterQuantizationPrecision = 3;
    private const string V1ClusterStrategy = "client_v1";
    private const string V1ClusterExpansionBehavior = "zoom_or_expand";

    public static void MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/events").WithTags("Events");

        // POST /api/events/map
        group.MapPost("/map", async (
            [FromBody] MapFeedRequest request,
            IEventRepository repo,
            CancellationToken ct) =>
        {
            var validation = ValidateBoundingBox(request.Bounds);
            if (validation is not null) return validation;

            var response = await repo.GetMapFeedAsync(request, ct);
            return Results.Ok(response);
        })
        .WithName("GetEventMapFeed")
        .Produces<MapFeedResponse>()
        .ProducesValidationProblem();

        // GET /api/events/map-feed/v1?bbox=minLng,minLat,maxLng,maxLat&preset=tomorrow&timezone=America/Chicago
        group.MapGet("/map-feed/v1", async (
            HttpContext ctx,
            IEventRepository repo,
            ITimeWindowResolver timeWindowResolver,
            ITokenService tokens,
            ISaveRepository saves,
            string bbox,
            string preset,
            string? timezone,
            DateTimeOffset? customStartUtc,
            DateTimeOffset? customEndUtc,
            string? district,
            string[]? categories,
            bool? includeSavedOnly,
            CancellationToken ct) =>
        {
            if (!Enum.TryParse<WeUP.Contracts.Events.TimeWindowPreset>(preset, ignoreCase: true, out var boundPreset))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["preset"] = ["Unsupported preset. Allowed values: now, tonight, tomorrow, thisWeekend, custom."] },
                    statusCode: 422);
            }

            // Backend query binding example: bind raw query parameters once, then hydrate the
            // canonical DTO so map feed logic and downstream calendar overlays share one shape.
            var query = new EventMapFeedQueryDto
            {
                Bbox = bbox,
                Preset = boundPreset,
                Timezone = timezone ?? string.Empty,
                CustomStartUtc = customStartUtc,
                CustomEndUtc = customEndUtc,
                District = district,
                Categories = categories,
                IncludeSavedOnly = includeSavedOnly ?? false,
            };

            if (!TryParseBbox(query.Bbox, out var bounds, out var bboxError))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["bbox"] = [bboxError] },
                    statusCode: 422);
            }

            var bboxValidation = ValidateBoundingBox(bounds);
            if (bboxValidation is not null)
            {
                return bboxValidation;
            }

            if (!timeWindowResolver.TryGetTimeWindow(
                (WeUP.Domain.Temporal.TimeWindowPreset)query.Preset,
                query.Timezone,
                out var resolvedWindow,
                out var windowError,
                customStartUtc: query.CustomStartUtc,
                customEndUtc: query.CustomEndUtc))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["timeWindow"] = [windowError] },
                    statusCode: 422);
            }

            var window = new TimeWindowRequest(
                resolvedWindow.StartUtc,
                resolvedWindow.EndUtc,
                resolvedWindow.Timezone);

            var userId = AuthEndpoints.ResolveUserId(ctx, tokens);
            if (query.IncludeSavedOnly && string.IsNullOrWhiteSpace(userId))
            {
                return Results.Ok(BuildV1MapFeedResponse(Array.Empty<EventMapItemDto>()));
            }

            var mapRequest = new MapFeedRequest(
                Bounds: bounds,
                Window: window,
                Categories: query.Categories,
                DistrictCode: query.District,
                MinConfidence: 0.0,
                Sort: "start_time_asc");

            var mapResponse = await repo.GetMapFeedAsync(mapRequest, ct);
            var savedEventIds = await ResolveSavedEventIdsAsync(userId, saves, ct);

            // Invariant: low-confidence markers are never rendered in the public map surface.
            var visibleCards = mapResponse.Events
                .Where(card => ResolveMarkerState(card, savedEventIds) != "low-confidence-hidden")
                .ToArray();

            var detailSnapshots = await Task.WhenAll(
                visibleCards.Select(async card => new
                {
                    card.Id,
                    Detail = (await repo.GetEventDetailAsync(card.Id, ct)).Event
                }));

            var detailsById = detailSnapshots
                .Where(item => item.Detail is not null)
                .ToDictionary(item => item.Id, item => item.Detail!, StringComparer.OrdinalIgnoreCase);

            var visible = visibleCards
                .Where(card => detailsById.ContainsKey(card.Id))
                .Select(card => ToV1MapItem(card, detailsById[card.Id], savedEventIds))
                .ToArray();

            if (query.IncludeSavedOnly)
            {
                visible = visible
                    .Where(item => item.SavedByCurrentUser)
                    .ToArray();
            }

            return Results.Ok(BuildV1MapFeedResponse(visible));
        })
        .WithName("GetEventMapFeedV1")
        .Produces<EventMapFeedV1ResponseDto>()
        .ProducesValidationProblem()
        .WithOpenApi(operation =>
        {
            var bboxParameter = operation.Parameters.FirstOrDefault(p => p.Name == "bbox");
            if (bboxParameter is not null)
            {
                bboxParameter.Description = "Bounding box as minLng,minLat,maxLng,maxLat.";
                bboxParameter.Example = new OpenApiString("-122.52,37.70,-122.37,37.85");
            }

            var presetParameter = operation.Parameters.FirstOrDefault(p => p.Name == "preset");
            if (presetParameter is not null)
            {
                presetParameter.Description = "Canonical time window preset. Allowed: now, tonight, tomorrow, thisWeekend, custom.";
                presetParameter.Example = new OpenApiString("thisWeekend");
            }

            var timezoneParameter = operation.Parameters.FirstOrDefault(p => p.Name == "timezone");
            if (timezoneParameter is not null)
            {
                timezoneParameter.Description = "Required IANA or Windows timezone identifier used to resolve preset boundaries.";
                timezoneParameter.Example = new OpenApiString("America/Chicago");
            }

            var customStartUtcParameter = operation.Parameters.FirstOrDefault(p => p.Name == "customStartUtc");
            if (customStartUtcParameter is not null)
            {
                customStartUtcParameter.Example = new OpenApiString("2026-04-11T00:00:00Z");
            }

            var customEndUtcParameter = operation.Parameters.FirstOrDefault(p => p.Name == "customEndUtc");
            if (customEndUtcParameter is not null)
            {
                customEndUtcParameter.Example = new OpenApiString("2026-04-13T00:00:00Z");
            }

            var includeSavedOnlyParameter = operation.Parameters.FirstOrDefault(p => p.Name == "includeSavedOnly");
            if (includeSavedOnlyParameter is not null)
            {
                includeSavedOnlyParameter.Example = new OpenApiBoolean(false);
            }

            operation.Summary = "Get canonical map marker feed (v1).";
            operation.Description =
                "Returns public map markers using canonical eventIds and contract-bound marker states. " +
                "Temporal presets resolve on the server using the supplied timezone so map and calendar overlays share one contract. " +
                "Low-confidence-hidden markers are excluded from this public response. " +
                "Cluster metadata is additive and does not replace canonical event identity.";

            return operation;
        });

        // POST /api/events/calendar
        group.MapPost("/calendar", async (
            [FromBody] CalendarFeedRequest request,
            IEventRepository repo,
            CancellationToken ct) =>
        {
            var response = await repo.GetCalendarFeedAsync(request, ct);
            return Results.Ok(response);
        })
        .WithName("GetCalendarFeed")
        .Produces<CalendarFeedResponse>();

        // GET /api/events/{id}
        group.MapGet("/{id}", async (
            string id,
            HttpContext ctx,
            IEventRepository repo,
            ITokenService tokens,
            ISaveRepository saves,
            CancellationToken ct) =>
        {
            var response = await repo.GetEventDetailAsync(id, ct);
            var userId = AuthEndpoints.ResolveUserId(ctx, tokens);

            if (response.Event is not null && !string.IsNullOrWhiteSpace(userId))
            {
                var savedByCurrentUser = await saves.IsEventSavedAsync(userId, id, ct);
                response = new EventDetailResponse(response.Event with
                {
                    SavedByCurrentUser = savedByCurrentUser,
                });
            }

            return response.Event is null
                ? Results.NotFound(new ProblemDetails { Title = "Event not found", Status = 404 })
                : Results.Ok(response);
        })
        .WithName("GetEventDetail")
        .Produces<EventDetailResponse>()
        .ProducesProblem(404);

        // M4-P17: Publish eligibility gate — OPERATIONAL (requires moderator auth in Phase 1)
        // GET /api/events/{id}/publish-eligibility
        group.MapGet("/{id}/publish-eligibility", async (
            string id,
            IEventRepository repo,
            IPublishEligibilityService eligibility,
            CancellationToken ct) =>
        {
            var aggregate = await repo.GetAggregateAsync(id, ct);
            if (aggregate is null)
                return Results.NotFound(new ProblemDetails { Title = "Event not found", Status = 404 });

            var candidate = new NormalizedEventCandidate(
                Title: aggregate.Title,
                VenueName: aggregate.VenueName,
                Address: EventDtoMapper.FormatAddress(aggregate.Address),
                StartUtc: aggregate.StartUtc.ToString("O"),
                EndUtc: aggregate.EndUtc?.ToString("O"),
                Timezone: aggregate.TimeZone,
                Category: aggregate.Category,
                Description: aggregate.Description,
                Tags: aggregate.Tags,
                SourceKind: aggregate.Provenance.PrimarySourceKind ?? "unknown",
                SourceRef: aggregate.CanonicalEventId,
                ExtractionConfidence: aggregate.ConfidenceScore,
                GeocodeConfidence: aggregate.Latitude != 0 ? 1.0 : 0.0,
                TemporalConfidence: aggregate.StartUtc != default ? 1.0 : 0.0,
                EvidenceRefs: null);

            var result = eligibility.Evaluate(candidate);
            return Results.Ok(EventDtoMapper.ToPublishEligibility(aggregate, result));
        })
        .WithName("GetEventPublishEligibility")
        .Produces<EventPublishEligibilityDto>()
        .ProducesProblem(404);

        // Submission routes moved to SubmissionEndpoints (P18) — /api/events/submissions/*
    }

    private static IResult? ValidateBoundingBox(GeoBoundingBox bb)
    {
        var errors = new List<string>();
        if (bb.MinLat >= bb.MaxLat) errors.Add("minLat must be less than maxLat");
        if (bb.MinLng >= bb.MaxLng) errors.Add("minLng must be less than maxLng");
        if (bb.MinLat < -90 || bb.MaxLat > 90) errors.Add("Latitude out of range [-90, 90]");
        if (bb.MinLng < -180 || bb.MaxLng > 180) errors.Add("Longitude out of range [-180, 180]");
        if (bb.MaxLat - bb.MinLat > 10 || bb.MaxLng - bb.MinLng > 10)
            errors.Add("Bounding box span exceeds 10 degrees");

        if (errors.Count == 0) return null;
        return Results.ValidationProblem(
            errors.ToDictionary(e => "bounds", e => new[] { e }),
            statusCode: 422);
    }

    private static bool TryParseBbox(string raw, out GeoBoundingBox bounds, out string error)
    {
        bounds = new GeoBoundingBox(0, 0, 0, 0);
        error = "";

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "bbox is required and must use format 'minLng,minLat,maxLng,maxLat'.";
            return false;
        }

        var tokens = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length != 4)
        {
            error = "bbox must include exactly 4 comma-separated numeric values: minLng,minLat,maxLng,maxLat.";
            return false;
        }

        if (!double.TryParse(tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var minLng) ||
            !double.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var minLat) ||
            !double.TryParse(tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var maxLng) ||
            !double.TryParse(tokens[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var maxLat))
        {
            error = "bbox values must be valid numbers in format minLng,minLat,maxLng,maxLat.";
            return false;
        }

        bounds = new GeoBoundingBox(minLat, maxLat, minLng, maxLng);
        return true;
    }

    private static async Task<HashSet<string>> ResolveSavedEventIdsAsync(
        string? userId,
        ISaveRepository saves,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var saved = await saves.GetSavesAsync(userId, page: 1, pageSize: 5000, ct);
        return saved.Items
            .Select(item => item.EventId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static EventMapItemDto ToV1MapItem(
        EventMapCardDto card,
        EventDetailDto detail,
        HashSet<string> savedEventIds)
    {
        var isSaved = savedEventIds.Contains(card.Id);
        return new EventMapItemDto(
            EventId: card.Id,
            Title: card.Title,
            StartUtc: detail.StartUtc,
            EndUtc: detail.EndUtc,
            Latitude: card.Lat,
            Longitude: card.Lng,
            VenueName: card.VenueName,
            District: null,
            PrimaryCategory: detail.Category,
            SavedByCurrentUser: isSaved,
            MarkerState: ResolveMarkerState(card, savedEventIds));
    }

    private static string ResolveMarkerState(EventMapCardDto card, HashSet<string> savedEventIds)
    {
        if (card.Confidence < 0.35)
        {
            return "low-confidence-hidden";
        }

        if (savedEventIds.Contains(card.Id))
        {
            return "saved";
        }

        return "default";
    }

    private static EventMapFeedV1ResponseDto BuildV1MapFeedResponse(EventMapItemDto[] visible)
    {
        return new EventMapFeedV1ResponseDto(
            Events: visible,
            TotalCount: visible.Length,
            Clusters: BuildV1Clusters(visible),
            DensityControl: BuildV1DensityControl(visible.Length),
            ClusterStrategy: V1ClusterStrategy);
    }

    private static EventMapFeedClusterDto[] BuildV1Clusters(EventMapItemDto[] visible)
    {
        if (visible.Length == 0)
        {
            return Array.Empty<EventMapFeedClusterDto>();
        }

        // Density invariant: cluster grouping is deterministic for a stable filtered payload.
        return visible
            .GroupBy(item =>
                $"{Math.Round(item.Latitude, V1ClusterQuantizationPrecision):F3}:" +
                $"{Math.Round(item.Longitude, V1ClusterQuantizationPrecision):F3}")
            .Select(group => new EventMapFeedClusterDto(
                ClusterId: group.Key,
                CenterLat: group.Average(item => item.Latitude),
                CenterLng: group.Average(item => item.Longitude),
                Count: group.Count(),
                EventIds: group.Select(item => item.EventId).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                SavedCount: group.Count(item => item.SavedByCurrentUser)))
            .Where(cluster => cluster.Count > 1)
            .OrderByDescending(cluster => cluster.Count)
            .ThenBy(cluster => cluster.ClusterId, StringComparer.Ordinal)
            .ToArray();
    }

    private static EventMapDensityControlDto BuildV1DensityControl(int visibleEventCount)
    {
        return new EventMapDensityControlDto(
            ClusteringEnabled: visibleEventCount >= V1ClusterActivationVisibleEventCountThreshold,
            ActivationVisibleEventCountThreshold: V1ClusterActivationVisibleEventCountThreshold,
            ActivationMaxZoomInclusive: V1ClusterActivationMaxZoomInclusive,
            ClusterRadiusPixels: V1ClusterRadiusPixels,
            ClusterMaxZoomInclusive: V1ClusterMaxZoomInclusive,
            SelectedMarkerBypassEnabled: true,
            ExpansionBehavior: V1ClusterExpansionBehavior);
    }
}
