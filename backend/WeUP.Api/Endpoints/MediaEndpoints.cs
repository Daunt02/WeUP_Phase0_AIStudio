using WeUP.Domain.Media;
using WeUP.Infrastructure.Media;
using WeUP.Domain.Users;

namespace WeUP.Api.Endpoints;

/// <summary>
/// P25/P26: Flyer Media Intake Endpoints
/// Upload, retrieve, and manage flyer assets with lifecycle and validation.
/// </summary>
public static class MediaEndpoints
{
    public static void MapMediaEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/media")
            .WithOpenApi()
            .WithName("Media");

        group.MapPost("/uploads", InitializeUpload)
            .WithName("CreateMediaUpload")
            .WithDescription("Create a durable media asset/upload record and stream file content.");

        group.MapPost("/uploads/{uploadId}/complete", CompleteUpload)
            .WithName("CompleteMediaUpload")
            .WithDescription("Finalize upload processing lifecycle and queue review when required.");

        group.MapGet("/uploads/{uploadId}", GetUpload)
            .WithName("GetMediaUpload")
            .WithDescription("Get media upload lifecycle record.");

        group.MapGet("/assets/{assetId}", GetAsset)
            .WithName("GetMediaAsset")
            .WithDescription("Get durable media asset record with owner and storage refs.");

        group.MapPost("/flyers", UploadFlyer)
            .WithName("UploadFlyer")
            .WithDescription("Upload a flyer image (JPEG/PNG)");

        group.MapGet("/flyers/{assetId}", GetFlyer)
            .WithName("GetFlyer")
            .WithDescription("Get flyer asset metadata by ID");

        group.MapGet("/flyers/user/{submitterId}", ListUserFlyers)
            .WithName("ListUserFlyers")
            .WithDescription("List all flyers uploaded by a user");

        group.MapDelete("/flyers/{assetId}", DeleteFlyer)
            .WithName("DeleteFlyer")
            .WithDescription("Delete a flyer asset");

        // P27: Unified intake with provenance + evidence
        group.MapPost("/flyers/intake", IntakeFlyer)
            .WithName("IntakeFlyer")
            .WithDescription("P27: Unified flyer intake — validates, stores, records provenance, creates evidence stub");

        group.MapPost("/flyers/{evidenceId}/link-event", LinkFlyerToEvent)
            .WithName("LinkFlyerToEvent")
            .WithDescription("P27: Link an evidence record to a published event ID");

        group.MapGet("/flyers/evidence/pending", ListPendingEvidence)
            .WithName("ListPendingEvidence")
            .WithDescription("P27: List all pending (unlinked) flyer evidence records for moderation");
    }

    private static async Task<IResult> InitializeUpload(
        HttpRequest request,
        HttpContext httpContext,
        IMediaIntakeService mediaService,
        ITokenService tokens,
        CancellationToken ct)
    {
        if (!request.HasFormContentType)
        {
            return Results.Problem(
                title: "Invalid request",
                detail: "Request must be multipart/form-data.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var form = await request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null)
        {
            return Results.Problem(
                title: "Missing file",
                detail: "Expected multipart field 'file'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var uploaderUserId = AuthEndpoints.ResolveUserId(httpContext, tokens) ?? form["uploaderUserId"].ToString().NullIfEmpty();
        var ownerTypeRaw = form["ownerType"].ToString().NullIfEmpty() ?? MediaOwnerType.User.ToString();
        if (!Enum.TryParse<MediaOwnerType>(ownerTypeRaw, true, out var ownerType))
        {
            return Results.Problem(
                title: "Invalid ownerType",
                detail: $"ownerType '{ownerTypeRaw}' is not supported.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var ingestionWorkflowSource = form["ingestionWorkflowSource"].ToString().NullIfEmpty();
        var requiresAuth = ownerType != MediaOwnerType.SystemWorkflow;
        if (requiresAuth && string.IsNullOrWhiteSpace(uploaderUserId))
        {
            return Results.Problem(
                title: "Unauthorized",
                detail: "Authenticated uploader is required for this media owner type.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!Enum.TryParse<MediaAssetType>(form["assetType"].ToString().NullIfEmpty() ?? string.Empty, true, out var assetType))
        {
            return Results.Problem(
                title: "Invalid assetType",
                detail: "assetType is required and must be one of FlyerImage, VenueImage, PromotionalPoster, EventMedia.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var owner = new MediaOwnerRef(
            ownerType,
            form["ownerId"].ToString().NullIfEmpty() ?? uploaderUserId,
            form["venueId"].ToString().NullIfEmpty(),
            ingestionWorkflowSource);

        try
        {
            await using var stream = file.OpenReadStream();
            var upload = await mediaService.CreateUploadAsync(
                new CreateMediaUploadCommand(
                    assetType,
                    file.ContentType,
                    file.FileName,
                    file.Length,
                    uploaderUserId,
                    owner,
                    form["submissionId"].ToString().NullIfEmpty(),
                    form["metadataJson"].ToString().NullIfEmpty()),
                stream,
                ct);

            return Results.Created($"/api/media/uploads/{upload.UploadId}", new CreateMediaUploadResponse(upload.UploadId, upload.AssetId, upload.Status.ToString()));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(
                title: "Upload validation failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<IResult> CompleteUpload(
        string uploadId,
        CompleteMediaUploadRequest request,
        IMediaIntakeService mediaService,
        CancellationToken ct)
    {
        var upload = await mediaService.CompleteUploadAsync(
            uploadId,
            new CompleteMediaUploadCommand(
                request.ProcessingSucceeded,
                request.QueueForReview,
                request.FailureReason,
                request.MetadataJson),
            ct);

        if (upload is null)
        {
            return Results.NotFound(new { error = $"Upload {uploadId} not found" });
        }

        return Results.Ok(new MediaUploadDto(upload.UploadId, upload.AssetId, upload.Status.ToString(), upload.InitializedAt, upload.CompletedAt, upload.RequestedByUserId, upload.FailureReason));
    }

    private static async Task<IResult> GetUpload(
        string uploadId,
        IMediaIntakeService mediaService,
        CancellationToken ct)
    {
        var upload = await mediaService.GetUploadAsync(uploadId, ct);
        if (upload is null)
        {
            return Results.NotFound(new { error = $"Upload {uploadId} not found" });
        }

        return Results.Ok(new MediaUploadDto(upload.UploadId, upload.AssetId, upload.Status.ToString(), upload.InitializedAt, upload.CompletedAt, upload.RequestedByUserId, upload.FailureReason));
    }

    private static async Task<IResult> GetAsset(
        string assetId,
        IMediaIntakeService mediaService,
        CancellationToken ct)
    {
        var asset = await mediaService.GetAssetAsync(assetId, ct);
        if (asset is null)
        {
            return Results.NotFound(new { error = $"Asset {assetId} not found" });
        }

        return Results.Ok(new MediaAssetDto(
            asset.AssetId,
            asset.AssetType.ToString(),
            asset.Status.ToString(),
            asset.ContentType,
            asset.FileSizeBytes,
            asset.ChecksumSha256,
            asset.OriginalFilename,
            asset.UploadedAt,
            asset.UploaderUserId,
            new MediaOwnerRefDto(asset.Owner.OwnerType.ToString(), asset.Owner.OwnerId, asset.Owner.VenueId, asset.Owner.IngestionWorkflowSource),
            new MediaStorageRefDto(asset.Storage.Provider.ToString(), asset.Storage.Container, asset.Storage.ObjectKey, asset.Storage.Uri, asset.Storage.ETag, asset.Storage.VersionId),
            asset.SubmissionId,
            asset.MetadataJson));
    }

    /// <summary>
    /// POST /api/media/flyers
    /// Upload a flyer image.
    /// </summary>
    private static async Task<IResult> UploadFlyer(
        HttpRequest request,
        IFlyerUploadService flyerService,
        CancellationToken ct)
    {
                var sourceReference = request.Query["sourceReference"].ToString().NullIfEmpty();
                var allowSameSourceReupload = bool.TryParse(request.Query["allowSameSourceReupload"], out var allowSameSource)
                    ? allowSameSource
                    : false;

        // Extract submitter ID from query param or header
        var submitterId = request.Query["submitterId"].ToString();
        if (string.IsNullOrEmpty(submitterId))
            return Results.BadRequest(new { error = "submitterId query parameter required" });

        // Validate form has file
        if (!request.HasFormContentType)
            return Results.BadRequest(new { error = "Request must be multipart/form-data" });

        var form = await request.ReadFormAsync(ct);
        var file = form.Files.FirstOrDefault();
        if (file == null)
            return Results.BadRequest(new { error = "No file provided" });

        var normalizedMime = file.ContentType.Split(';')[0].Trim().ToLowerInvariant();
        if (!FlyerPolicy.AllowedMimeTypes.Contains(normalizedMime))
            return Results.BadRequest(new { error = $"Only {string.Join(", ", FlyerPolicy.AllowedMimeTypes)} images allowed" });

        // Upload flyer — validation and lifecycle managed inside the service
        try
        {
            using var stream = file.OpenReadStream();
            var assetId = await flyerService.UploadFlyerAsync(
                stream,
                file.FileName,
                file.ContentType,
                submitterId,
                sourceReference,
                allowSameSourceReupload,
                ct);
            return Results.Created($"/api/media/flyers/{assetId}", new UploadFlyerResponse(AssetId: assetId));
        }
        catch (FlyerUploadException ex) when (ex.ErrorCode == FlyerUploadErrorCode.Duplicate)
        {
            return Results.Conflict(new { error = ex.Message, duplicateAssetId = ex.DuplicateAssetId, code = "DUPLICATE" });
        }
        catch (FlyerUploadException ex)
        {
            return Results.UnprocessableEntity(new { error = ex.Message, code = "VALIDATION_FAILED" });
        }
    }

    /// <summary>
    /// GET /api/media/flyers/{assetId}
    /// Get flyer asset metadata.
    /// </summary>
    private static async Task<IResult> GetFlyer(
        string assetId,
        IFlyerUploadService flyerService,
        CancellationToken ct)
    {
        var asset = await flyerService.GetFlyerAsync(assetId, ct);
        if (asset == null)
            return Results.NotFound(new { error = $"Flyer {assetId} not found" });

        return Results.Ok(new FlyerAssetDto(
            AssetId: asset.AssetId,
            OriginalFilename: asset.OriginalFilename,
            FileSizeBytes: asset.FileSizeBytes,
            ContentType: asset.ContentType,
            SubmitterId: asset.SubmitterId,
            UploadedAt: asset.UploadedAt,
            Status: asset.Status,
            ContentHash: asset.ContentHash,
            StorageKey: asset.StorageKey,
            Revision: asset.Revision,
            ValidationFailureReason: asset.ValidationFailureReason,
            WidthPx: asset.WidthPx,
            HeightPx: asset.HeightPx,
            IsAnimated: asset.IsAnimated,
            S3Url: asset.S3Url));
    }

    /// <summary>
    /// GET /api/media/flyers/user/{submitterId}
    /// List all flyers uploaded by a user.
    /// </summary>
    private static async Task<IResult> ListUserFlyers(
        string submitterId,
        IFlyerUploadService flyerService,
        CancellationToken ct)
    {
        var assets = await flyerService.ListUserFlyersAsync(submitterId, ct);
        var dtos = assets.Select(a => new FlyerAssetDto(
            AssetId: a.AssetId,
            OriginalFilename: a.OriginalFilename,
            FileSizeBytes: a.FileSizeBytes,
            ContentType: a.ContentType,
            SubmitterId: a.SubmitterId,
            UploadedAt: a.UploadedAt,
            Status: a.Status,
            ContentHash: a.ContentHash,
            StorageKey: a.StorageKey,
            Revision: a.Revision,
            ValidationFailureReason: a.ValidationFailureReason,
            WidthPx: a.WidthPx,
            HeightPx: a.HeightPx,
            IsAnimated: a.IsAnimated,
            S3Url: a.S3Url)).ToArray();

        return Results.Ok(new ListUserFlyersResponse(Flyers: dtos));
    }

    /// <summary>
    /// DELETE /api/media/flyers/{assetId}
    /// Delete a flyer asset.
    /// </summary>
    private static async Task<IResult> DeleteFlyer(
        string assetId,
        IFlyerUploadService flyerService,
        CancellationToken ct)
    {
        await flyerService.DeleteFlyerAsync(assetId, ct);
        return Results.NoContent();
    }

    /// <summary>
    /// POST /api/media/flyers/intake
    /// P27: Unified flyer intake with provenance and evidence creation.
    /// </summary>
    private static async Task<IResult> IntakeFlyer(
        HttpRequest request,
        IFlyerIntakeService intakeService,
        CancellationToken ct)
    {
        var submitterId = request.Query["submitterId"].ToString();
        if (string.IsNullOrEmpty(submitterId))
            return Results.BadRequest(new { error = "submitterId query parameter required" });

        var sourceTierStr = request.Query["sourceTier"].ToString();
        var sourceTier = sourceTierStr switch
        {
            "T1" => SourceTier.T1_Verified,
            "T2" => SourceTier.T2_Approved,
            _    => SourceTier.T3_Unverified,
        };

        var submissionId  = request.Query["submissionId"].ToString().NullIfEmpty();
        var sourceUrl     = request.Query["sourceUrl"].ToString().NullIfEmpty();
        var submitterNote = request.Query["note"].ToString().NullIfEmpty();

        if (!request.HasFormContentType)
            return Results.BadRequest(new { error = "Request must be multipart/form-data" });

        var form = await request.ReadFormAsync(ct);
        var file = form.Files.FirstOrDefault();
        if (file == null)
            return Results.BadRequest(new { error = "No file provided" });

        try
        {
            using var stream = file.OpenReadStream();
            var result = await intakeService.IntakeAsync(
                stream, file.FileName, file.ContentType,
                submitterId, sourceTier, submissionId, sourceUrl, submitterNote, ct);

            return Results.Created($"/api/media/flyers/{result.AssetId}",
                new FlyerIntakeResponse(
                    AssetId:           result.AssetId,
                    ProvenanceId:      result.ProvenanceId,
                    EvidenceId:        result.EvidenceId,
                    BaselineAuthority: result.BaselineAuthority,
                    AssetStatus:       result.AssetStatus.ToString()));
        }
        catch (FlyerUploadException ex) when (ex.ErrorCode == FlyerUploadErrorCode.Duplicate)
        {
            return Results.Conflict(new { error = ex.Message, duplicateAssetId = ex.DuplicateAssetId, code = "DUPLICATE" });
        }
        catch (FlyerUploadException ex)
        {
            return Results.UnprocessableEntity(new { error = ex.Message, code = "VALIDATION_FAILED" });
        }
    }

    /// <summary>
    /// POST /api/media/flyers/{evidenceId}/link-event
    /// P27: Link an evidence record to a published event.
    /// </summary>
    private static async Task<IResult> LinkFlyerToEvent(
        string evidenceId,
        HttpRequest request,
        IFlyerIntakeService intakeService,
        CancellationToken ct)
    {
        var eventId = request.Query["eventId"].ToString();
        if (string.IsNullOrEmpty(eventId))
            return Results.BadRequest(new { error = "eventId query parameter required" });

        try
        {
            await intakeService.LinkToEventAsync(evidenceId, eventId, ct);
            return Results.Ok(new { evidenceId, eventId, status = "Linked" });
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/media/flyers/evidence/pending
    /// P27: List pending evidence records for moderation.
    /// </summary>
    private static async Task<IResult> ListPendingEvidence(
        IFlyerEvidenceRepository evidenceRepo,
        CancellationToken ct)
    {
        var records = await evidenceRepo.ListPendingAsync(ct);
        return Results.Ok(new PendingEvidenceResponse(
            Records: records.Select(r => new FlyerEvidenceDto(
                r.EvidenceId, r.AssetId, r.ProvenanceId,
                r.FlyerType.ToString(), r.Status.ToString(),
                r.OcrText, r.ConfidenceScore, r.CreatedAt,
                r.EventId, r.SubmissionId)).ToArray(),
            Count: records.Length));
    }
}

// ── P27 DTOs ─────────────────────────────────────────────────────────────────

file static class StringExtensions
{
    public static string? NullIfEmpty(this string s) => string.IsNullOrEmpty(s) ? null : s;
}

/// <summary>
/// Response after unified flyer intake.
/// </summary>
public record FlyerIntakeResponse(
    string AssetId,
    string ProvenanceId,
    string EvidenceId,
    double BaselineAuthority,
    string AssetStatus);

/// <summary>
/// Flyer evidence data transfer object.
/// </summary>
public record FlyerEvidenceDto(
    string EvidenceId,
    string AssetId,
    string ProvenanceId,
    string FlyerType,
    string Status,
    string? OcrText,
    double? ConfidenceScore,
    DateTimeOffset CreatedAt,
    string? EventId,
    string? SubmissionId);

/// <summary>
/// Response listing pending evidence records.
/// </summary>
public record PendingEvidenceResponse(FlyerEvidenceDto[] Records, int Count);

/// <summary>
/// Response after uploading a flyer.
/// </summary>
public record UploadFlyerResponse(string AssetId);

/// <summary>
/// Flyer asset data transfer object.
/// </summary>
public record FlyerAssetDto(
    string AssetId,
    string OriginalFilename,
    long FileSizeBytes,
    string ContentType,
    string SubmitterId,
    DateTimeOffset UploadedAt,
    string Status,
    string ContentHash,
    string StorageKey,
    int Revision,
    string? ValidationFailureReason,
    int? WidthPx,
    int? HeightPx,
    bool IsAnimated,
    string? S3Url);

/// <summary>
/// Response listing user's flyers.
/// </summary>
public record ListUserFlyersResponse(FlyerAssetDto[] Flyers);

public record CreateMediaUploadResponse(
    string UploadId,
    string AssetId,
    string Status);

public record CompleteMediaUploadRequest(
    bool ProcessingSucceeded = true,
    bool QueueForReview = true,
    string? FailureReason = null,
    string? MetadataJson = null);

public record MediaUploadDto(
    string UploadId,
    string AssetId,
    string Status,
    DateTimeOffset InitializedAt,
    DateTimeOffset? CompletedAt,
    string? RequestedByUserId,
    string? FailureReason);

public record MediaOwnerRefDto(
    string OwnerType,
    string? OwnerId,
    string? VenueId,
    string? IngestionWorkflowSource);

public record MediaStorageRefDto(
    string Provider,
    string Container,
    string ObjectKey,
    string? Uri,
    string? ETag,
    string? VersionId);

public record MediaAssetDto(
    string AssetId,
    string AssetType,
    string Status,
    string ContentType,
    long FileSizeBytes,
    string ChecksumSha256,
    string OriginalFilename,
    DateTimeOffset UploadedAt,
    string? UploaderUserId,
    MediaOwnerRefDto Owner,
    MediaStorageRefDto Storage,
    string? SubmissionId,
    string? MetadataJson);
