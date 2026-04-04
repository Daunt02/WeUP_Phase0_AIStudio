using Microsoft.AspNetCore.Mvc;
using WeUP.Contracts.Events;
using WeUP.Domain.Events;

namespace WeUP.Api.Endpoints;

public static class EventEndpoints
{
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
            IEventRepository repo,
            CancellationToken ct) =>
        {
            var response = await repo.GetEventDetailAsync(id, ct);
            return response.Event is null
                ? Results.NotFound(new ProblemDetails { Title = "Event not found", Status = 404 })
                : Results.Ok(response);
        })
        .WithName("GetEventDetail")
        .Produces<EventDetailResponse>()
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
}
