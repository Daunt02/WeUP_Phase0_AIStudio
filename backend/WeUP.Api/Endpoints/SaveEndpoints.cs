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
            HttpContext ctx,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default) =>
        {
            var userId = ResolveUserId(ctx);
            var response = await repo.GetSavesAsync(userId, page, pageSize, ct);
            return Results.Ok(response);
        })
        .WithName("GetSavedEvents")
        .Produces<SavedEventsResponse>();

        // POST /api/users/me/saves/{eventId}
        group.MapPost("/{eventId}", async (
            string eventId,
            ISaveRepository repo,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ResolveUserId(ctx);
            var response = await repo.SaveEventAsync(userId, eventId, ct);
            return Results.Ok(response);
        })
        .WithName("SaveEvent")
        .Produces<SaveEventResponse>();

        // DELETE /api/users/me/saves/{eventId}
        group.MapDelete("/{eventId}", async (
            string eventId,
            ISaveRepository repo,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ResolveUserId(ctx);
            var response = await repo.UnsaveEventAsync(userId, eventId, ct);
            return Results.Ok(response);
        })
        .WithName("UnsaveEvent")
        .Produces<SaveEventResponse>();
    }

    /// <summary>
    /// Auth seam — resolves user identity from the session/token.
    /// Full implementation in P16 (auth backbone).
    /// </summary>
    private static string ResolveUserId(HttpContext ctx)
        => ctx.User?.FindFirst("sub")?.Value ?? "anonymous";
}
