using WeUP.Domain.Media;
using WeUP.Infrastructure.Media;

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

    /// <summary>
    /// POST /api/media/flyers
    /// Upload a flyer image.
    /// </summary>
    private static async Task<IResult> UploadFlyer(
        HttpRequest request,
        IFlyerUploadService flyerService,
        CancellationToken ct)
    {
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

        // Validate content type
        if (!file.ContentType.StartsWith("image/jpeg") && !file.ContentType.StartsWith("image/png"))
            return Results.BadRequest(new { error = "Only JPEG and PNG images allowed" });

        // Upload flyer — validation and lifecycle managed inside the service
        try
        {
            using var stream = file.OpenReadStream();
            var assetId = await flyerService.UploadFlyerAsync(stream, file.FileName, file.ContentType, submitterId, ct);
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
            S3Url: asset.S3Url,
            LocalPath: asset.LocalPath));
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
            S3Url: a.S3Url,
            LocalPath: a.LocalPath)).ToArray();

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
    string? S3Url,
    string? LocalPath);

/// <summary>
/// Response listing user's flyers.
/// </summary>
public record ListUserFlyersResponse(FlyerAssetDto[] Flyers);
