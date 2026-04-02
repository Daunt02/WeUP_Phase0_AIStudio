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
            var response = await repo.GetSavesAsync(userId, page, pageSize, ct);
            return Results.Ok(response);
        })
        .WithName("GetSavedEvents")
        .Produces<SavedEventsResponse>();

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
            var response = await repo.SaveEventAsync(userId, eventId, ct);
            return Results.Ok(response);
        })
        .WithName("SaveEvent")
        .Produces<SaveEventResponse>();

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
            var response = await repo.UnsaveEventAsync(userId, eventId, ct);
            return Results.Ok(response);
        })
        .WithName("UnsaveEvent")
        .Produces<SaveEventResponse>();
    }
}
