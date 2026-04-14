using WeUP.Contracts.Ingestion;
using WeUP.Domain.Flyer;

namespace WeUP.Api.Endpoints;

public static class FlyerEndpoints
{
    public static void MapFlyerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ingestion/flyers").WithTags("Ingestion");

        group.MapPost("", async (
            FlyerUploadIngestionRequest request,
            IFlyerIngestionPipeline pipeline,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.AssetId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["assetId"] = ["assetId is required."],
                });
            }

            if (string.IsNullOrWhiteSpace(request.SubmittedBy))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["submittedBy"] = ["submittedBy is required."],
                });
            }

            try
            {
                var result = await pipeline.SubmitAsync(request, ct);
                var statusUrl = $"/api/ingestion/flyers/{result.JobId}";
                return Results.Accepted(statusUrl, new IngestionAcceptedResponse(result.JobId, result.Status, statusUrl));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    title: "Flyer ingestion request rejected",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }
        })
        .WithName("IngestFlyer")
        .Produces<IngestionAcceptedResponse>(202)
        .ProducesProblem(422)
        .ProducesValidationProblem();

        group.MapGet("/{jobId}", async (
            string jobId,
            IFlyerIngestionPipeline pipeline,
            CancellationToken ct) =>
        {
            var detail = await pipeline.GetJobAsync(jobId, ct);
            if (detail is null)
            {
                return Results.NotFound(new ProblemDetails
                {
                    Title = "Flyer ingestion job not found",
                    Status = StatusCodes.Status404NotFound,
                });
            }

            return Results.Ok(detail);
        })
        .WithName("GetFlyerIngestionJob")
        .Produces<FlyerIngestionJobDetailResponse>(200)
        .ProducesProblem(404);

        group.MapGet("/{jobId}/evidence", async (
            string jobId,
            IFlyerIngestionPipeline pipeline,
            CancellationToken ct) =>
        {
            var evidence = await pipeline.GetEvidenceAsync(jobId, ct);
            if (evidence is null)
            {
                return Results.NotFound(new ProblemDetails
                {
                    Title = "Flyer ingestion evidence not found",
                    Status = StatusCodes.Status404NotFound,
                });
            }

            return Results.Ok(evidence);
        })
        .WithName("GetFlyerIngestionEvidence")
        .Produces<FlyerIngestionEvidenceResponse>(200)
        .ProducesProblem(404);

    }
}
