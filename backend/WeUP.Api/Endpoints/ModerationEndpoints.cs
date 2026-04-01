using Microsoft.AspNetCore.Mvc;
using WeUP.Application.Moderation;
using WeUP.Contracts.Moderation;
using WeUP.Domain.Moderation;

namespace WeUP.Api.Endpoints;

/// <summary>
/// Moderation queue and review dashboard API.
/// All routes are under /api/moderation. Authorization seam added — full RBAC in P16.
/// </summary>
public static class ModerationEndpoints
{
    public static void MapModerationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/moderation").WithTags("Moderation");
        // Auth seam: .RequireAuthorization("ModerationPolicy") enabled after P16

        // -----------------------------------------------------------------
        // P13 — Queue and dashboard read endpoints
        // -----------------------------------------------------------------

        // GET /api/moderation/queue
        group.MapGet("/queue", async (
            [FromQuery] ModerationItemStatus? status,
            [FromQuery] ModerationItemKind? kind,
            [FromQuery] string? sourceKind,
            [FromQuery] ConfidenceBucket? confidenceBucket,
            [FromQuery] DuplicateSeverity? minDuplicateSeverity,
            [FromQuery] string? assignedReviewerId,
            [FromQuery] string? ingestionJobId,
            [FromQuery] string? afterUtc,
            [FromQuery] string? beforeUtc,
            [FromQuery] int pageSize,
            [FromQuery] string? cursor,
            IModerationQueueService service,
            CancellationToken ct) =>
        {
            pageSize = Math.Clamp(pageSize == 0 ? 25 : pageSize, 1, 100);
            var query = new ModerationQueueQuery(
                status, kind, sourceKind, null, confidenceBucket,
                minDuplicateSeverity, assignedReviewerId, ingestionJobId,
                afterUtc, beforeUtc, pageSize, cursor);

            var result = await service.GetQueueAsync(query, ct);
            return Results.Ok(result);
        })
        .WithName("GetModerationQueue")
        .Produces<ModerationQueueResponse>();

        // GET /api/moderation/queue/{itemId}
        group.MapGet("/queue/{itemId}", async (
            string itemId,
            IModerationQueueService service,
            CancellationToken ct) =>
        {
            var item = await service.GetItemAsync(itemId, ct);
            return item is null
                ? Results.NotFound(new ProblemDetails { Title = "Moderation item not found", Status = 404 })
                : Results.Ok(item);
        })
        .WithName("GetModerationItem")
        .Produces<ModerationQueueItemDto>()
        .ProducesProblem(404);

        // GET /api/moderation/stats
        group.MapGet("/stats", async (
            IModerationQueueService service,
            CancellationToken ct) =>
        {
            var stats = await service.GetStatsAsync(ct);
            return Results.Ok(stats);
        })
        .WithName("GetModerationStats")
        .Produces<ModerationStatsDto>();

        // GET /api/moderation/review-history
        group.MapGet("/review-history", async (
            [FromQuery] int pageSize,
            [FromQuery] string? cursor,
            IModerationQueueService service,
            CancellationToken ct) =>
        {
            pageSize = Math.Clamp(pageSize == 0 ? 50 : pageSize, 1, 200);
            var result = await service.GetReviewHistoryAsync(pageSize, cursor, ct);
            return Results.Ok(result);
        })
        .WithName("GetReviewHistory")
        .Produces<ReviewHistoryResponse>();

        // -----------------------------------------------------------------
        // P15 — Review action endpoints
        // -----------------------------------------------------------------

        // POST /api/moderation/queue/{itemId}/approve
        group.MapPost("/queue/{itemId}/approve", async (
            string itemId,
            [FromBody] ApproveRequest request,
            IReviewActionService actions,
            CancellationToken ct) =>
        {
            var result = await actions.ApproveAsync(itemId, request, ct);
            return result.Success ? Results.Ok(result)
                : Results.UnprocessableEntity(ProblemFrom(result));
        })
        .WithName("ApproveQueueItem")
        .Produces<ReviewActionResponse>()
        .ProducesValidationProblem(422);

        // POST /api/moderation/queue/{itemId}/reject
        group.MapPost("/queue/{itemId}/reject", async (
            string itemId,
            [FromBody] RejectRequest request,
            IReviewActionService actions,
            CancellationToken ct) =>
        {
            var result = await actions.RejectAsync(itemId, request, ct);
            return result.Success ? Results.Ok(result)
                : Results.UnprocessableEntity(ProblemFrom(result));
        })
        .WithName("RejectQueueItem")
        .Produces<ReviewActionResponse>();

        // POST /api/moderation/queue/{itemId}/request-changes
        group.MapPost("/queue/{itemId}/request-changes", async (
            string itemId,
            [FromBody] RequestChangesRequest request,
            IReviewActionService actions,
            CancellationToken ct) =>
        {
            var result = await actions.RequestChangesAsync(itemId, request, ct);
            return result.Success ? Results.Ok(result)
                : Results.UnprocessableEntity(ProblemFrom(result));
        })
        .WithName("RequestChangesOnQueueItem")
        .Produces<ReviewActionResponse>();

        // POST /api/moderation/queue/{itemId}/mark-duplicate
        group.MapPost("/queue/{itemId}/mark-duplicate", async (
            string itemId,
            [FromBody] MarkDuplicateRequest request,
            IReviewActionService actions,
            CancellationToken ct) =>
        {
            var result = await actions.MarkDuplicateAsync(itemId, request, ct);
            return result.Success ? Results.Ok(result)
                : Results.UnprocessableEntity(ProblemFrom(result));
        })
        .WithName("MarkDuplicate")
        .Produces<ReviewActionResponse>();

        // POST /api/moderation/queue/{itemId}/merge
        group.MapPost("/queue/{itemId}/merge", async (
            string itemId,
            [FromBody] MergeRequest request,
            IReviewActionService actions,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.TargetEventId))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["targetEventId"] = ["Target event ID is required for merge."] });

            var result = await actions.MergeAsync(itemId, request, ct);
            return result.Success ? Results.Ok(result)
                : Results.UnprocessableEntity(ProblemFrom(result));
        })
        .WithName("MergeQueueItem")
        .Produces<ReviewActionResponse>();

        // POST /api/moderation/queue/{itemId}/archive
        group.MapPost("/queue/{itemId}/archive", async (
            string itemId,
            [FromBody] ArchiveRequest request,
            IReviewActionService actions,
            CancellationToken ct) =>
        {
            var result = await actions.ArchiveAsync(itemId, request, ct);
            return result.Success ? Results.Ok(result)
                : Results.UnprocessableEntity(ProblemFrom(result));
        })
        .WithName("ArchiveQueueItem")
        .Produces<ReviewActionResponse>();

        // POST /api/moderation/queue/{itemId}/reopen
        group.MapPost("/queue/{itemId}/reopen", async (
            string itemId,
            [FromBody] ReopenRequest request,
            IReviewActionService actions,
            CancellationToken ct) =>
        {
            var result = await actions.ReopenAsync(itemId, request, ct);
            return result.Success ? Results.Ok(result)
                : Results.UnprocessableEntity(ProblemFrom(result));
        })
        .WithName("ReopenQueueItem")
        .Produces<ReviewActionResponse>();

        // POST /api/moderation/events/{eventId}/rollback
        group.MapPost("/events/{eventId}/rollback", async (
            string eventId,
            [FromBody] RollbackRequest request,
            IRollbackService rollback,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.RollbackReason))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["rollbackReason"] = ["A rollback reason is required."] });

            var result = await rollback.RollbackEventAsync(eventId, request, ct);
            return result.Success ? Results.Ok(result)
                : Results.UnprocessableEntity(ProblemFrom(result));
        })
        .WithName("RollbackEvent")
        .Produces<ReviewActionResponse>();
    }

    private static ProblemDetails ProblemFrom(ReviewActionResponse r) =>
        new() { Title = "Action not permitted", Detail = r.ErrorMessage, Status = 422 };
}
