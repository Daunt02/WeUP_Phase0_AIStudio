using WeUP.Domain.Flyer;

namespace WeUP.Api.Endpoints;

public static class FlyerEndpoints
{
    public static void MapFlyerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ingestion/flyers").WithTags("Ingestion");

        // POST /api/ingestion/flyers
        // Accepts multipart/form-data with a single "file" field.
        group.MapPost("/", async (
            IFormFile file,
            IFlyerIngestionPipeline pipeline,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["file"] = ["A non-empty file is required."] });

            var userId = http.User.Identity?.Name ?? "anonymous";

            await using var stream = file.OpenReadStream();
            var request = new FlyerUploadRequest(stream, file.FileName, file.ContentType, userId);

            var result = await pipeline.ProcessAsync(request, ct);

            if (!result.Success)
                return Results.Problem(
                    title: "Flyer ingestion failed",
                    detail: result.ErrorMessage,
                    statusCode: 422);

            return Results.Accepted(
                $"/api/ingestion/jobs/{result.JobId}",
                new { result.JobId, result.AssetId, result.RequiresReview, result.ReviewBlockers });
        })
        .WithName("IngestFlyer")
        .DisableAntiforgery()
        .Produces(202)
        .ProducesProblem(422);
    }
}
