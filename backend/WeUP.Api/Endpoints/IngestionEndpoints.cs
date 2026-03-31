using Microsoft.AspNetCore.Mvc;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;

namespace WeUP.Api.Endpoints;

public static class IngestionEndpoints
{
    public static void MapIngestionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ingestion").WithTags("Ingestion");

        // POST /api/ingestion/manual
        group.MapPost("/manual", async (
            [FromBody] ManualIngestionRequest request,
            IIngestionDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var jobId = await dispatcher.DispatchManualAsync(request, ct);
            return Results.Accepted($"/api/ingestion/jobs/{jobId}", new { jobId });
        })
        .WithName("IngestManual")
        .Produces(202);

        // POST /api/ingestion/link
        group.MapPost("/link", async (
            [FromBody] LinkIngestionRequest request,
            IIngestionDispatcher dispatcher,
            CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(request.Url) || !Uri.TryCreate(request.Url, UriKind.Absolute, out _))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["url"] = ["A valid absolute URL is required."] });

            var jobId = await dispatcher.DispatchLinkAsync(request, ct);
            return Results.Accepted($"/api/ingestion/jobs/{jobId}", new { jobId });
        })
        .WithName("IngestLink")
        .Produces(202);

        // POST /api/ingestion/venue-page
        group.MapPost("/venue-page", async (
            [FromBody] VenuePageIngestionRequest request,
            IIngestionDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var jobId = await dispatcher.DispatchVenuePageAsync(request, ct);
            return Results.Accepted($"/api/ingestion/jobs/{jobId}", new { jobId });
        })
        .WithName("IngestVenuePage")
        .Produces(202);

        // GET /api/ingestion/jobs/{id}
        group.MapGet("/jobs/{id}", async (
            string id,
            IIngestionJobRepository jobs,
            CancellationToken ct) =>
        {
            var job = await jobs.GetJobAsync(id, ct);
            return job is null
                ? Results.NotFound(new ProblemDetails { Title = "Ingestion job not found", Status = 404 })
                : Results.Ok(job);
        })
        .WithName("GetIngestionJob")
        .Produces<IngestionJobResponse>()
        .ProducesProblem(404);
    }
}
