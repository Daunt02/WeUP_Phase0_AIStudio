using WeUP.Contracts.Saves;
using WeUP.Domain.Users;

namespace WeUP.Api.Endpoints;

public static class SaveEndpoints
{
    public static void MapSaveEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users/me/saves").WithTags("Saves");

        // GET /api/users/me/saves
        group.MapGet("/", async (
            ISaveRepository repo,
            ITokenService tokens,
            HttpContext ctx,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx, tokens);
            if (userId is null) return Results.Unauthorized();
            var response = await repo.GetSavesAsync(userId, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);
            return Results.Ok(response);
        })
        .WithName("GetSavedEvents")
        .Produces<SavedEventsResponse>();

        // GET /api/users/me/saves/{eventId}/state
        // Response (SavedStateDto):
        // {
        //   "eventId": "evt-sf-midnight-groove",
        //   "saved": true,
        //   "sessionKind": "authenticated",
        //   "persistenceSource": "backend"
        // }
        group.MapGet("/{eventId}/state", async (
            string eventId,
            ISaveRepository repo,
            ITokenService tokens,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx, tokens);
            if (userId is null) return Results.Unauthorized();

            var normalizedEventId = eventId.Trim();
            if (normalizedEventId.Length == 0)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["eventId"] = ["Canonical eventId is required."],
                    },
                    statusCode: 422);
            }

            var saved = await repo.IsEventSavedAsync(userId, normalizedEventId, ct);
            return Results.Ok(new SavedStateDto(
                normalizedEventId,
                saved,
                SessionKind: "authenticated",
                PersistenceSource: "backend"));
        })
        .WithName("GetSavedStateByEventId")
        .Produces<SavedStateDto>()
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized);

        // POST /api/users/me/saves
        // Request (SaveEventRequestDto):
        // { "eventId": "evt-sf-midnight-groove" }
        // Response (SaveEventResponseDto):
        // { "eventId": "evt-sf-midnight-groove", "saved": true, "message": "Saved" }
        group.MapPost("/", async (
            SaveEventRequestDto request,
            ISaveRepository repo,
            ITokenService tokens,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx, tokens);
            if (userId is null) return Results.Unauthorized();

            var normalizedEventId = request.EventId.Trim();
            if (normalizedEventId.Length == 0)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["eventId"] = ["Canonical eventId is required."],
                    },
                    statusCode: 422);
            }

            var response = await repo.SaveEventAsync(userId, normalizedEventId, ct);
            return Results.Ok(response);
        })
        .WithName("SaveEventV1")
        .Produces<SaveEventResponseDto>()
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized);

        // POST /api/users/me/saves/unsave
        // Request (UnsaveEventRequestDto):
        // { "eventId": "evt-sf-midnight-groove" }
        // Response (SaveEventResponseDto):
        // { "eventId": "evt-sf-midnight-groove", "saved": false, "message": "Unsaved" }
        group.MapPost("/unsave", async (
            UnsaveEventRequestDto request,
            ISaveRepository repo,
            ITokenService tokens,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx, tokens);
            if (userId is null) return Results.Unauthorized();

            var normalizedEventId = request.EventId.Trim();
            if (normalizedEventId.Length == 0)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["eventId"] = ["Canonical eventId is required."],
                    },
                    statusCode: 422);
            }

            var response = await repo.UnsaveEventAsync(userId, normalizedEventId, ct);
            return Results.Ok(response);
        })
        .WithName("UnsaveEventV1")
        .Produces<SaveEventResponseDto>()
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized);

        // POST /api/users/me/saves/{eventId}
        group.MapPost("/{eventId}", async (
            string eventId,
            ISaveRepository repo,
            ITokenService tokens,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx, tokens);
            if (userId is null) return Results.Unauthorized();

            var normalizedEventId = eventId.Trim();
            if (normalizedEventId.Length == 0)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["eventId"] = ["Canonical eventId is required."],
                    },
                    statusCode: 422);
            }

            var response = await repo.SaveEventAsync(userId, normalizedEventId, ct);
            return Results.Ok(response);
        })
        .WithName("SaveEvent")
        .Produces<SaveEventResponseDto>()
        .ProducesValidationProblem();

        // DELETE /api/users/me/saves/{eventId}
        group.MapDelete("/{eventId}", async (
            string eventId,
            ISaveRepository repo,
            ITokenService tokens,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx, tokens);
            if (userId is null) return Results.Unauthorized();

            var normalizedEventId = eventId.Trim();
            if (normalizedEventId.Length == 0)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["eventId"] = ["Canonical eventId is required."],
                    },
                    statusCode: 422);
            }

            var response = await repo.UnsaveEventAsync(userId, normalizedEventId, ct);
            return Results.Ok(response);
        })
        .WithName("UnsaveEvent")
        .Produces<SaveEventResponseDto>()
        .ProducesValidationProblem();
    }
}
