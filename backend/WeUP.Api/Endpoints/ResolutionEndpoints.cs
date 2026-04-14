using Microsoft.AspNetCore.Mvc;
using WeUP.Contracts.Resolution;
using WeUP.Domain.Resolution;

namespace WeUP.Api.Endpoints;

public static class ResolutionEndpoints
{
    public static void MapResolutionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/resolution").WithTags("Resolution");

        group.MapPost("/evaluate", async (
            [FromBody] EvaluateResolutionRequest request,
            IEntityResolutionService service,
            CancellationToken ct) =>
        {
            if (request.Candidate is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["candidate"] = ["Candidate payload is required."],
                });
            }

            if (string.IsNullOrWhiteSpace(request.Candidate.SourceRef))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["candidate.sourceRef"] = ["Candidate sourceRef is required."],
                });
            }

            var result = await service.EvaluateAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("EvaluateEntityResolution")
        .Produces<EntityResolutionResult>()
        .ProducesValidationProblem();

        group.MapPost("/merge", async (
            [FromBody] MergeResolutionRequest request,
            IEntityResolutionService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.ResolutionId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["resolutionId"] = ["resolutionId is required."],
                });
            }

            var result = await service.MergeAsync(request, ct);
            if (result.Merged)
            {
                return Results.Ok(result);
            }

            return result.RequiresManualReview
                ? Results.Conflict(result)
                : Results.NotFound(result);
        })
        .WithName("CommitEntityResolutionMerge")
        .Produces<MergeResolutionResponse>()
        .Produces<MergeResolutionResponse>(404)
        .Produces<MergeResolutionResponse>(409)
        .ProducesValidationProblem();

        group.MapGet("/{id}", async (
            string id,
            IEntityResolutionService service,
            CancellationToken ct) =>
        {
            var result = await service.GetAsync(id, ct);
            return result is null
                ? Results.NotFound(new ProblemDetails
                {
                    Title = "Resolution not found",
                    Detail = $"Resolution '{id}' does not exist.",
                    Status = 404,
                })
                : Results.Ok(result);
        })
        .WithName("GetEntityResolution")
        .Produces<EntityResolutionResult>()
        .ProducesProblem(404);
    }
}
