using Microsoft.AspNetCore.Mvc;
using WeUP.Api.Observability;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;

namespace WeUP.Api.Endpoints;

public static class IngestionEndpoints
{
    public static void MapIngestionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ingestion").WithTags("Ingestion");

        group.MapPost("/manual", async (
            [FromBody] ManualIngestionRequest request,
            IIngestionCoordinator coordinator,
            IOperationalTelemetry telemetry,
            CancellationToken ct) =>
        {
            try
            {
                var result = await coordinator.SubmitManualAsync(request, ct);
                telemetry.TrackEvent("ingestion.manual.accepted", new Dictionary<string, string>
                {
                    ["jobId"] = result.JobId,
                    ["status"] = result.Status.ToString(),
                });

                return Results.Accepted(
                    $"/api/ingestion/jobs/{result.JobId}",
                    new IngestionAcceptedResponse(result.JobId, result.Status, $"/api/ingestion/jobs/{result.JobId}"));
            }
            catch (Exception ex)
            {
                telemetry.TrackException(ex, new Dictionary<string, string>
                {
                    ["route"] = "/api/ingestion/manual",
                });
                throw;
            }
        })
        .WithName("IngestManual")
        .Produces<IngestionAcceptedResponse>(202);

        group.MapPost("/url", async (
            [FromBody] UrlIngestionRequest request,
            IIngestionCoordinator coordinator,
            IOperationalTelemetry telemetry,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Url) || !Uri.TryCreate(request.Url, UriKind.Absolute, out _))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["url"] = ["A valid absolute URL is required."] });
            }

            try
            {
                var result = await coordinator.SubmitUrlAsync(request, ct);
                telemetry.TrackEvent("ingestion.url.accepted", new Dictionary<string, string>
                {
                    ["jobId"] = result.JobId,
                    ["status"] = result.Status.ToString(),
                });

                return Results.Accepted(
                    $"/api/ingestion/jobs/{result.JobId}",
                    new IngestionAcceptedResponse(result.JobId, result.Status, $"/api/ingestion/jobs/{result.JobId}"));
            }
            catch (Exception ex)
            {
                telemetry.TrackException(ex, new Dictionary<string, string>
                {
                    ["route"] = "/api/ingestion/url",
                });
                throw;
            }
        })
        .WithName("IngestUrl")
        .Produces<IngestionAcceptedResponse>(202)
        .ProducesValidationProblem();

        group.MapPost("/link", async (
            [FromBody] LinkIngestionRequest request,
            IIngestionCoordinator coordinator,
            CancellationToken ct) =>
        {
            var result = await coordinator.SubmitUrlAsync(new UrlIngestionRequest(request.Url, request.SubmitterId), ct);
            return Results.Accepted(
                $"/api/ingestion/jobs/{result.JobId}",
                new IngestionAcceptedResponse(result.JobId, result.Status, $"/api/ingestion/jobs/{result.JobId}"));
        })
        .WithName("IngestLinkCompat")
        .ExcludeFromDescription();

        group.MapPost("/venue-page", async (
            [FromBody] VenuePageIngestionRequest request,
            IIngestionCoordinator coordinator,
            IOperationalTelemetry telemetry,
            CancellationToken ct) =>
        {
            try
            {
                var result = await coordinator.SubmitVenuePageAsync(request, ct);
                telemetry.TrackEvent("ingestion.venue-page.accepted", new Dictionary<string, string>
                {
                    ["jobId"] = result.JobId,
                    ["status"] = result.Status.ToString(),
                });

                return Results.Accepted(
                    $"/api/ingestion/jobs/{result.JobId}",
                    new IngestionAcceptedResponse(result.JobId, result.Status, $"/api/ingestion/jobs/{result.JobId}"));
            }
            catch (Exception ex)
            {
                telemetry.TrackException(ex, new Dictionary<string, string>
                {
                    ["route"] = "/api/ingestion/venue-page",
                });
                throw;
            }
        })
        .WithName("IngestVenuePage")
        .Produces<IngestionAcceptedResponse>(202);

        group.MapGet("/jobs/{id}", async (
            string id,
            IIngestionCoordinator coordinator,
            IOperationalTelemetry telemetry,
            CancellationToken ct) =>
        {
            var job = await coordinator.GetJobAsync(id, ct);
            telemetry.TrackEvent("ingestion.job.lookup", new Dictionary<string, string>
            {
                ["jobId"] = id,
                ["found"] = (job is not null).ToString(),
            });

            return job is null
                ? Results.NotFound(new ProblemDetails { Title = "Ingestion job not found", Status = 404 })
                : Results.Ok(job);
        })
        .WithName("GetIngestionJob")
        .Produces<IngestionResult>()
        .ProducesProblem(404);
    }
}
