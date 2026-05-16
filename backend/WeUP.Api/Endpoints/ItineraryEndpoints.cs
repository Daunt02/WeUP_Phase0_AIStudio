using WeUP.Contracts.Users;
using WeUP.Domain.Users;

namespace WeUP.Api.Endpoints;

public static class ItineraryEndpoints
{
    public static IEndpointRouteBuilder MapItineraryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users/me").WithTags("Itinerary & Preferences").RequireAuthorization();

        // GET /api/users/me/itinerary
        group.MapGet("/itinerary", async (
            IItineraryRepository repo,
            HttpContext ctx, CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx);
            if (userId is null) return Results.Unauthorized();

            var result = await repo.GetAsync(userId, ct);
            return Results.Ok(result);
        })
        .WithName("GetItinerary")
        .WithSummary("Get the user's current itinerary");

        // POST /api/users/me/itinerary
        group.MapPost("/itinerary", async (
            AddToItineraryRequest request,
            IItineraryRepository repo,
            HttpContext ctx, CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx);
            if (userId is null) return Results.Unauthorized();

            var result = await repo.AddAsync(userId, request, ct);
            return Results.Ok(result);
        })
        .WithName("AddToItinerary")
        .WithSummary("Add an event to the itinerary");

        // PATCH /api/users/me/itinerary/{itemId}
        group.MapPatch("/itinerary/{itemId}", async (
            string itemId, UpdateItineraryItemRequest request,
            IItineraryRepository repo,
            HttpContext ctx, CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx);
            if (userId is null) return Results.Unauthorized();

            var result = await repo.UpdateItemAsync(userId, itemId, request, ct);
            return result.Added ? Results.Ok(result) : Results.NotFound(result);
        })
        .WithName("UpdateItineraryItem")
        .WithSummary("Update note or position of an itinerary item");

        // DELETE /api/users/me/itinerary/{itemId}
        group.MapDelete("/itinerary/{itemId}", async (
            string itemId,
            IItineraryRepository repo,
            HttpContext ctx, CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx);
            if (userId is null) return Results.Unauthorized();

            var result = await repo.RemoveAsync(userId, itemId, ct);
            return Results.Ok(result);
        })
        .WithName("RemoveFromItinerary")
        .WithSummary("Remove an item from the itinerary");

        // GET /api/users/me/preferences
        group.MapGet("/preferences", async (
            IUserPreferencesRepository prefs,
            HttpContext ctx, CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx);
            if (userId is null) return Results.Unauthorized();

            var result = await prefs.GetAsync(userId, ct);
            return Results.Ok(result);
        })
        .WithName("GetPreferences")
        .WithSummary("Get discovery and notification preferences");

        // PATCH /api/users/me/preferences
        group.MapPatch("/preferences", async (
            UpdatePreferencesRequest request,
            IUserPreferencesRepository prefs,
            HttpContext ctx, CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx);
            if (userId is null) return Results.Unauthorized();

            var result = await prefs.UpsertAsync(userId, request, ct);
            return Results.Ok(result);
        })
        .WithName("UpdatePreferences")
        .WithSummary("Update discovery and notification preferences");

        return app;
    }
}
