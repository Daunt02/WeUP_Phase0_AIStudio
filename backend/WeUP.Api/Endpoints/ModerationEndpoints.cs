using Microsoft.AspNetCore.Mvc;
using WeUP.Application.Events;
using WeUP.Application.Moderation;
using WeUP.Api.Observability;
using WeUP.Contracts.Events;
using WeUP.Contracts.Moderation;
using WeUP.Domain.Events;
using WeUP.Domain.Moderation;
using WeUP.Domain.Users;
using ContractQueueItem = WeUP.Contracts.Moderation.ModerationQueueItem;

namespace WeUP.Api.Endpoints;

public static class ModerationEndpoints
{
    public static void MapModerationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/moderation")
            .WithTags("Moderation")
            .AddEndpointFilter<ModeratorAuthorizationFilter>();

        group.MapGet("/queue", async (
            [FromQuery] ModerationItemStatus? status,
            [FromQuery] ModerationReviewStatus? reviewStatus,
            [FromQuery] ModerationItemKind? kind,
            [FromQuery] string? sourceKind,
            [FromQuery] double? minConfidence,
            [FromQuery] double? maxConfidence,
            [FromQuery] ConfidenceBucket? confidenceBucket,
            [FromQuery] DuplicateSeverity? minDuplicateSeverity,
            [FromQuery] string? assignedReviewerId,
            [FromQuery] string? ingestionJobId,
            [FromQuery] string? afterUtc,
            [FromQuery] string? beforeUtc,
            [FromQuery] int pageSize,
            [FromQuery] string? cursor,
            IModerationQueueService queue,
            IOperationalTelemetry telemetry,
            CancellationToken ct) =>
        {
            var filter = new ModerationQueueFilter(
                Status: status,
                ReviewStatus: reviewStatus,
                Kind: kind,
                SourceKind: sourceKind,
                MinConfidence: minConfidence,
                MaxConfidence: maxConfidence,
                ReviewReason: null,
                ConfidenceBucket: confidenceBucket,
                MinDuplicateSeverity: minDuplicateSeverity,
                AssignedReviewerId: assignedReviewerId,
                IngestionJobId: ingestionJobId,
                AfterUtc: afterUtc,
                BeforeUtc: beforeUtc,
                PageSize: Math.Clamp(pageSize == 0 ? 25 : pageSize, 1, 100),
                Cursor: cursor);

            var response = await queue.GetQueueAsync(filter, ct);
            telemetry.TrackEvent("moderation.queue.read", new Dictionary<string, string>
            {
                ["count"] = response.Items.Count().ToString(),
                ["cursor"] = cursor ?? string.Empty,
            });
            return Results.Ok(response);
        })
        .WithName("GetModerationQueue")
        .Produces<ModerationQueueResponse>();

        group.MapGet("/queue/{id}", async (
            string id,
            IModerationQueueService queue,
            CancellationToken ct) =>
        {
            var item = await queue.GetItemAsync(id, ct);
            return item is null
                ? Results.NotFound(new ProblemDetails { Title = "Moderation item not found", Status = StatusCodes.Status404NotFound })
                : Results.Ok(item);
        })
        .WithName("GetModerationQueueItem")
        .Produces<ContractQueueItem>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/queue/{id}/evidence", async (
            string id,
            IModerationEvidenceService evidence,
            CancellationToken ct) =>
        {
            var bundle = await evidence.GetEvidenceBundleAsync(id, ct);
            return bundle is null
                ? Results.NotFound(new ProblemDetails { Title = "Moderation evidence not found", Status = StatusCodes.Status404NotFound })
                : Results.Ok(bundle);
        })
        .WithName("GetModerationEvidence")
        .Produces<ModerationEvidenceBundle>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/reviews/{id}/approve", async (
            string id,
            [FromBody] ReviewDecisionRequest request,
            ITokenService tokens,
            HttpContext ctx,
            IOperationalTelemetry telemetry,
            IReviewDecisionService reviews,
            CancellationToken ct) =>
            await SubmitDecisionAsync(
                id,
                request with
                {
                    ActorId = AuthEndpoints.ResolveUserId(ctx, tokens) ?? string.Empty,
                    Decision = ReviewDecisionKind.Approve,
                },
                telemetry,
                reviews,
                ct))
        .WithName("ApproveModerationReview")
        .Produces<ReviewDecisionResponse>()
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/reviews/{id}/reject", async (
            string id,
            [FromBody] ReviewDecisionRequest request,
            ITokenService tokens,
            HttpContext ctx,
            IOperationalTelemetry telemetry,
            IReviewDecisionService reviews,
            CancellationToken ct) =>
            await SubmitDecisionAsync(
                id,
                request with
                {
                    ActorId = AuthEndpoints.ResolveUserId(ctx, tokens) ?? string.Empty,
                    Decision = ReviewDecisionKind.Reject,
                },
                telemetry,
                reviews,
                ct))
        .WithName("RejectModerationReview")
        .Produces<ReviewDecisionResponse>()
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/reviews/{id}/request-changes", async (
            string id,
            [FromBody] ReviewDecisionRequest request,
            ITokenService tokens,
            HttpContext ctx,
            IOperationalTelemetry telemetry,
            IReviewDecisionService reviews,
            CancellationToken ct) =>
            await SubmitDecisionAsync(
                id,
                request with
                {
                    ActorId = AuthEndpoints.ResolveUserId(ctx, tokens) ?? string.Empty,
                    Decision = ReviewDecisionKind.RequestChanges,
                },
                telemetry,
                reviews,
                ct))
        .WithName("RequestChangesModerationReview")
        .Produces<ReviewDecisionResponse>()
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/reviews/{id}/history", async (
            string id,
            [FromQuery] int pageSize,
            [FromQuery] string? cursor,
            IReviewDecisionService reviews,
            CancellationToken ct) =>
        {
            var size = Math.Clamp(pageSize == 0 ? 50 : pageSize, 1, 200);
            var history = await reviews.GetReviewHistoryAsync(id, size, cursor, ct);
            return Results.Ok(history);
        })
        .WithName("GetModerationReviewHistory")
        .Produces<IReadOnlyList<ReviewAuditRecord>>();

        // M4-P17: Moderation event view — returns EventModerationDto (OPERATIONAL)
        // GET /api/moderation/events/{id}
        group.MapGet("/events/{id}", async (
            string id,
            IEventRepository repo,
            CancellationToken ct) =>
        {
            var aggregate = await repo.GetAggregateAsync(id, ct);
            return aggregate is null
                ? Results.NotFound(new ProblemDetails { Title = "Event not found", Status = 404 })
                : Results.Ok(EventDtoMapper.ToModeration(aggregate));
        })
        .WithName("GetModerationEventView")
        .Produces<EventModerationDto>()
        .ProducesProblem(404);

        group.MapPost("/events/{eventId}/rollback", async (            string eventId,
            [FromBody] RollbackRequest request,
            WeUP.Domain.Moderation.IRollbackService rollback,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.RollbackReason))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["rollbackReason"] = ["rollbackReason is required."],
                });
            }

            var result = await rollback.RollbackEventAsync(eventId, request, ct);
            return result.Success
                ? Results.Ok(result)
                : Results.UnprocessableEntity(new ProblemDetails
                {
                    Title = "Rollback could not be applied",
                    Detail = result.ErrorMessage,
                    Status = StatusCodes.Status422UnprocessableEntity,
                });
        })
        .WithName("RollbackModeratedEvent")
        .Produces<ReviewActionResponse>()
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // Backward compatible route aliases
        group.MapPost("/queue/{id}/approve", async (string id, [FromBody] ApproveRequest req, ITokenService tokens, HttpContext ctx, IOperationalTelemetry telemetry, IReviewDecisionService reviews, CancellationToken ct) =>
            await SubmitDecisionAsync(id, new ReviewDecisionRequest(AuthEndpoints.ResolveUserId(ctx, tokens) ?? string.Empty, ReviewDecisionKind.Approve, req.Note), telemetry, reviews, ct));

        group.MapPost("/queue/{id}/reject", async (string id, [FromBody] RejectRequest req, ITokenService tokens, HttpContext ctx, IOperationalTelemetry telemetry, IReviewDecisionService reviews, CancellationToken ct) =>
            await SubmitDecisionAsync(id, new ReviewDecisionRequest(AuthEndpoints.ResolveUserId(ctx, tokens) ?? string.Empty, ReviewDecisionKind.Reject, req.Note, [req.RejectionReason]), telemetry, reviews, ct));

        group.MapPost("/queue/{id}/request-changes", async (string id, [FromBody] RequestChangesRequest req, ITokenService tokens, HttpContext ctx, IOperationalTelemetry telemetry, IReviewDecisionService reviews, CancellationToken ct) =>
            await SubmitDecisionAsync(id, new ReviewDecisionRequest(AuthEndpoints.ResolveUserId(ctx, tokens) ?? string.Empty, ReviewDecisionKind.RequestChanges, req.Note, [req.CorrectionInstructions]), telemetry, reviews, ct));
    }

    private static async Task<IResult> SubmitDecisionAsync(
        string id,
        ReviewDecisionRequest request,
        IOperationalTelemetry telemetry,
        IReviewDecisionService reviews,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ActorId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["actorId"] = ["actorId is required."],
            });
        }

        try
        {
            var result = await reviews.SubmitDecisionAsync(id, request, ct);
            if (!result.Accepted)
            {
                telemetry.TrackEvent("moderation.decision.rejected", new Dictionary<string, string>
                {
                    ["itemId"] = id,
                    ["decision"] = request.Decision.ToString(),
                    ["actorId"] = request.ActorId,
                });

                return Results.UnprocessableEntity(new ProblemDetails
                {
                    Title = "Decision could not be applied",
                    Detail = result.ErrorMessage,
                    Status = StatusCodes.Status422UnprocessableEntity,
                });
            }

            telemetry.TrackEvent("moderation.decision.accepted", new Dictionary<string, string>
            {
                ["itemId"] = id,
                ["decision"] = request.Decision.ToString(),
                ["actorId"] = request.ActorId,
            });
            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            telemetry.TrackException(ex, new Dictionary<string, string>
            {
                ["route"] = "/api/moderation/reviews",
                ["itemId"] = id,
                ["decision"] = request.Decision.ToString(),
            });
            throw;
        }
    }
}
