using WeUP.Contracts.Events;
using WeUP.Domain.Events;
using WeUP.Domain.Users;

namespace WeUP.Api.Endpoints;

public static class SubmissionEndpoints
{
    public static IEndpointRouteBuilder MapSubmissionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/events/submissions").WithTags("Submissions");

        // POST /api/events/submissions — create draft
        group.MapPost("/", async (
            DraftSubmissionRequest request,
            IEventSubmissionService svc, ITokenService tokens,
            HttpContext ctx, CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx, tokens);
            if (userId is null) return Results.Unauthorized();

            var dto = await svc.CreateDraftAsync(userId, request, ct);
            return Results.Created($"/api/events/submissions/{dto.SubmissionId}", dto);
        })
        .WithName("CreateDraft")
        .WithSummary("Create a new event draft");

        // GET /api/events/submissions — list user's own submissions
        group.MapGet("/", async (
            IEventSubmissionService svc, ITokenService tokens,
            HttpContext ctx, int page = 1, int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx, tokens);
            if (userId is null) return Results.Unauthorized();

            var result = await svc.ListByUserAsync(userId, page, pageSize, ct);
            return Results.Ok(result);
        })
        .WithName("ListMySubmissions")
        .WithSummary("List the authenticated user's submissions");

        // GET /api/events/submissions/{id}
        group.MapGet("/{id}", async (
            string id, IEventSubmissionService svc,
            ITokenService tokens, HttpContext ctx, CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx, tokens);
            if (userId is null) return Results.Unauthorized();

            var dto = await svc.GetAsync(id, ct);
            if (dto is null) return Results.NotFound();
            if (dto.SubmittedByUserId != userId) return Results.Forbid();
            return Results.Ok(dto);
        })
        .WithName("GetSubmission")
        .WithSummary("Get a submission by ID");

        // PATCH /api/events/submissions/{id} — update draft
        group.MapPatch("/{id}", async (
            string id, DraftSubmissionRequest request,
            IEventSubmissionService svc, ITokenService tokens,
            HttpContext ctx, CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx, tokens);
            if (userId is null) return Results.Unauthorized();

            var dto = await svc.UpdateDraftAsync(id, userId, request, ct);
            return dto is null
                ? Results.Problem("Submission not found, not owned by user, or not in editable state.", statusCode: 409)
                : Results.Ok(dto);
        })
        .WithName("UpdateDraft")
        .WithSummary("Update a draft or changes-requested submission");

        // POST /api/events/submissions/{id}/submit — submit for review
        group.MapPost("/{id}/submit", async (
            string id, IEventSubmissionService svc,
            ITokenService tokens, HttpContext ctx, CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx, tokens);
            if (userId is null) return Results.Unauthorized();

            var result = await svc.SubmitForReviewAsync(id, userId, ct);
            return result.Status == SubmissionStatus.SubmittedForReview
                ? Results.Accepted($"/api/events/submissions/{id}", result)
                : Results.UnprocessableEntity(result);
        })
        .WithName("SubmitForReview")
        .WithSummary("Submit a draft for moderation review");

        // GET /api/events/submissions/{id}/status
        group.MapGet("/{id}/status", async (
            string id, IEventSubmissionService svc, CancellationToken ct) =>
        {
            var dto = await svc.GetAsync(id, ct);
            return dto is null
                ? Results.NotFound()
                : Results.Ok(new { submissionId = id, status = dto.Status.ToString() });
        })
        .WithName("GetSubmissionStatus")
        .WithSummary("Get the status of a submission (public)");

        return app;
    }
}
