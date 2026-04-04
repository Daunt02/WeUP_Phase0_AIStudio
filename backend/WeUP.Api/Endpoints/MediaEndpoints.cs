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
}

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
