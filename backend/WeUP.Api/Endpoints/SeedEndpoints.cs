using WeUP.Infrastructure.Seed;

namespace WeUP.Api.Endpoints;

public static class SeedEndpoints
{
    public static void MapSeedEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/seed").WithTags("Seed");

        group.MapGet("/status", (Phase0SeedService seeder) =>
        {
            return seeder.CurrentSnapshot is null
                ? Results.NotFound(new { error = "Seed data has not been loaded yet." })
                : Results.Ok(seeder.CurrentSnapshot);
        })
        .WithName("GetSeedStatus");

        group.MapPost("/reset", async (Phase0SeedService seeder, CancellationToken ct) =>
        {
            var snapshot = await seeder.ResetAsync(ct);
            return Results.Ok(snapshot);
        })
        .WithName("ResetSeedData");
    }
}