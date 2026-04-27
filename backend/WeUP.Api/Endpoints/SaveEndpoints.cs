using WeUP.Contracts.Saves;
using WeUP.Contracts.Users;
using WeUP.Domain.Users;

namespace WeUP.Api.Endpoints;

public static class SaveEndpoints
{
    public static void MapSaveEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users/me/saves").WithTags("Saves");

        // GET /api/users/me/saves
        // Retrieval invariant:
        // - Authenticated callers receive canonical normalized event projections.
        // - Missing/deleted saves remain explicit rows so saved count and panel state do not drift silently.
        group.MapGet("/", async (
            ISaveRepository repo,
            ITokenService tokens,
            HttpContext ctx,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx, tokens);
            if (userId is null) return Results.Unauthorized();
            var response = await repo.GetSavesAsync(userId, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);
            return Results.Ok(response);
        })
        .WithName("GetSavedEvents")
        .Produces<SavedEventsResponse>();

        // POST /api/users/me/saves/migrate-anonymous-state
        // One-way migration: anonymous local state -> authenticated ownership.
        // This operation is deterministic and idempotent.
        group.MapPost("/migrate-anonymous-state", async (
            SaveStateMigrationRequestDto request,
            ISaveRepository repo,
            IUserPreferencesRepository preferences,
            ITokenService tokens,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx, tokens);
            if (userId is null) return Results.Unauthorized();

            var rawLocalIds = request.LocalSavedEventIds ?? [];
            var normalizedLocalIds = rawLocalIds
                .Select(static id => id?.Trim() ?? string.Empty)
                .ToArray();

            var missingLocalIdCount = normalizedLocalIds.Count(static id => id.Length == 0);
            var distinctLocalIds = normalizedLocalIds
                .Where(static id => id.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static id => id, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var duplicateCollapsedCount = Math.Max(0, normalizedLocalIds.Length - missingLocalIdCount - distinctLocalIds.Length);

            var existingSaved = await ResolveSavedEventIdsAsync(userId, repo, ct);
            var itemResults = new List<SaveStateMigrationItemResultDto>(distinctLocalIds.Length);
            var retainedLocalIds = new List<string>();

            foreach (var eventId in distinctLocalIds)
            {
                if (existingSaved.Contains(eventId))
                {
                    itemResults.Add(new SaveStateMigrationItemResultDto(
                        EventId: eventId,
                        Outcome: "already-saved",
                        Message: "Event already exists in authenticated saves."));
                    continue;
                }

                var saveResult = await repo.SaveEventAsync(userId, eventId, ct);
                if (saveResult.Saved)
                {
                    existingSaved.Add(eventId);
                    itemResults.Add(new SaveStateMigrationItemResultDto(
                        EventId: eventId,
                        Outcome: "migrated",
                        Message: "Event migrated to authenticated saves."));
                    continue;
                }

                // Invalid IDs are explicitly retained on local side for client-visible follow-up;
                // they are never persisted into authenticated ownership.
                retainedLocalIds.Add(eventId);
                itemResults.Add(new SaveStateMigrationItemResultDto(
                    EventId: eventId,
                    Outcome: "invalid-local-event-id",
                    Message: saveResult.Message));
            }

            if (missingLocalIdCount > 0)
            {
                itemResults.Add(new SaveStateMigrationItemResultDto(
                    EventId: "",
                    Outcome: "missing-local-event-id",
                    Message: $"{missingLocalIdCount} local save ids were blank and skipped."));
            }

            var migratedCount = itemResults.Count(static result => result.Outcome == "migrated");
            var alreadySavedCount = itemResults.Count(static result => result.Outcome == "already-saved");
            var invalidLocalIdCount = itemResults.Count(static result => result.Outcome == "invalid-local-event-id");

            var discoveryContext = await MigrateDiscoveryContextAsync(
                userId,
                request.LocalDiscoveryContext,
                preferences,
                ct);

            var status = invalidLocalIdCount > 0 || missingLocalIdCount > 0
                ? "completed-with-issues"
                : "completed";

            var response = new SaveStateMigrationResultDto(
                Status: status,
                MigrationDirection: "anonymous-to-authenticated",
                Ownership: "authenticated-user",
                ClientMigrationKey: request.ClientMigrationKey,
                ProcessedAtUtc: DateTimeOffset.UtcNow,
                Counts: new SaveStateMigrationCountsDto(
                    ReceivedLocalSavedCount: rawLocalIds.Length,
                    DistinctLocalSavedCount: distinctLocalIds.Length,
                    DuplicateCollapsedCount: duplicateCollapsedCount,
                    MigratedCount: migratedCount,
                    AlreadySavedCount: alreadySavedCount,
                    InvalidLocalIdCount: invalidLocalIdCount,
                    MissingLocalIdCount: missingLocalIdCount),
                ItemResults: [.. itemResults],
                RetainedLocalSavedEventIds: [.. retainedLocalIds],
                DiscoveryContext: discoveryContext);

            return Results.Ok(response);
        })
        .WithName("MigrateAnonymousSaveState")
        .Produces<SaveStateMigrationResultDto>()
        .Produces(StatusCodes.Status401Unauthorized);

        // GET /api/users/me/saves/{eventId}/state
        // Response (SavedStateDto):
        // {
        //   "eventId": "evt-sf-midnight-groove",
        //   "saved": true,
        //   "sessionKind": "authenticated",
        //   "persistenceSource": "backend"
        // }
        group.MapGet("/{eventId}/state", async (
            string eventId,
            ISaveRepository repo,
            ITokenService tokens,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx, tokens);
            if (userId is null) return Results.Unauthorized();

            var normalizedEventId = eventId.Trim();
            if (normalizedEventId.Length == 0)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["eventId"] = ["Canonical eventId is required."],
                    },
                    statusCode: 422);
            }

            var saved = await repo.IsEventSavedAsync(userId, normalizedEventId, ct);
            return Results.Ok(new SavedStateDto(
                normalizedEventId,
                saved,
                SessionKind: "authenticated",
                PersistenceSource: "backend"));
        })
        .WithName("GetSavedStateByEventId")
        .Produces<SavedStateDto>()
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized);

        // POST /api/users/me/saves
        // Request (SaveEventRequestDto):
        // { "eventId": "evt-sf-midnight-groove" }
        // Response (SaveEventResponseDto):
        // { "eventId": "evt-sf-midnight-groove", "saved": true, "message": "Saved" }
        group.MapPost("/", async (
            SaveEventRequestDto request,
            ISaveRepository repo,
            ITokenService tokens,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx, tokens);
            if (userId is null) return Results.Unauthorized();

            var normalizedEventId = request.EventId.Trim();
            if (normalizedEventId.Length == 0)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["eventId"] = ["Canonical eventId is required."],
                    },
                    statusCode: 422);
            }

            var response = await repo.SaveEventAsync(userId, normalizedEventId, ct);
            return Results.Ok(response);
        })
        .WithName("SaveEventV1")
        .Produces<SaveEventResponseDto>()
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized);

        // POST /api/users/me/saves/unsave
        // Request (UnsaveEventRequestDto):
        // { "eventId": "evt-sf-midnight-groove" }
        // Response (SaveEventResponseDto):
        // { "eventId": "evt-sf-midnight-groove", "saved": false, "message": "Unsaved" }
        group.MapPost("/unsave", async (
            UnsaveEventRequestDto request,
            ISaveRepository repo,
            ITokenService tokens,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx, tokens);
            if (userId is null) return Results.Unauthorized();

            var normalizedEventId = request.EventId.Trim();
            if (normalizedEventId.Length == 0)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["eventId"] = ["Canonical eventId is required."],
                    },
                    statusCode: 422);
            }

            var response = await repo.UnsaveEventAsync(userId, normalizedEventId, ct);
            return Results.Ok(response);
        })
        .WithName("UnsaveEventV1")
        .Produces<SaveEventResponseDto>()
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized);

        // POST /api/users/me/saves/{eventId}
        group.MapPost("/{eventId}", async (
            string eventId,
            ISaveRepository repo,
            ITokenService tokens,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx, tokens);
            if (userId is null) return Results.Unauthorized();

            var normalizedEventId = eventId.Trim();
            if (normalizedEventId.Length == 0)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["eventId"] = ["Canonical eventId is required."],
                    },
                    statusCode: 422);
            }

            var response = await repo.SaveEventAsync(userId, normalizedEventId, ct);
            return Results.Ok(response);
        })
        .WithName("SaveEvent")
        .Produces<SaveEventResponseDto>()
        .ProducesValidationProblem();

        // DELETE /api/users/me/saves/{eventId}
        group.MapDelete("/{eventId}", async (
            string eventId,
            ISaveRepository repo,
            ITokenService tokens,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = AuthEndpoints.ResolveUserId(ctx, tokens);
            if (userId is null) return Results.Unauthorized();

            var normalizedEventId = eventId.Trim();
            if (normalizedEventId.Length == 0)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["eventId"] = ["Canonical eventId is required."],
                    },
                    statusCode: 422);
            }

            var response = await repo.UnsaveEventAsync(userId, normalizedEventId, ct);
            return Results.Ok(response);
        })
        .WithName("UnsaveEvent")
        .Produces<SaveEventResponseDto>()
        .ProducesValidationProblem();
    }

    private static async Task<HashSet<string>> ResolveSavedEventIdsAsync(
        string userId,
        ISaveRepository repo,
        CancellationToken ct)
    {
        var saved = await repo.GetSavesAsync(userId, page: 1, pageSize: 5000, ct);
        return saved.Items
            .Select(static item => item.EventId)
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(static id => id.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<SaveStateDiscoveryContextMigrationResultDto> MigrateDiscoveryContextAsync(
        string userId,
        SaveStateLocalDiscoveryContextDto? localContext,
        IUserPreferencesRepository preferences,
        CancellationToken ct)
    {
        if (localContext is null)
        {
            return new SaveStateDiscoveryContextMigrationResultDto(
                Status: "absent",
                Applied: false,
                Message: "No local discovery context provided.",
                ResolvedPreferredTimezone: null);
        }

        var timezone = localContext.LastTemporalFilter?.Timezone?.Trim();
        if (string.IsNullOrWhiteSpace(timezone))
        {
            return new SaveStateDiscoveryContextMigrationResultDto(
                Status: "received-not-applied",
                Applied: false,
                Message: "No supported discovery-context fields were eligible for migration.",
                ResolvedPreferredTimezone: null);
        }

        var current = await preferences.GetAsync(userId, ct);
        if (!string.IsNullOrWhiteSpace(current.PreferredTimeZone))
        {
            return new SaveStateDiscoveryContextMigrationResultDto(
                Status: "received-not-applied",
                Applied: false,
                Message: "Authenticated preferredTimeZone already exists and was preserved.",
                ResolvedPreferredTimezone: current.PreferredTimeZone);
        }

        var updated = await preferences.UpsertAsync(
            userId,
            new UpdatePreferencesRequest(
                PreferredCategories: null,
                HomeRadiusMeters: null,
                NotifyOnNewEvents: null,
                NotifyOnSaveReminders: null,
                PreferredTimeZone: timezone,
                LastKnownMapCenterLat: null,
                LastKnownMapCenterLng: null),
            ct);

        return new SaveStateDiscoveryContextMigrationResultDto(
            Status: "applied",
            Applied: true,
            Message: "Discovery context timezone migrated into authenticated preferences.",
            ResolvedPreferredTimezone: updated.PreferredTimeZone);
    }
}
