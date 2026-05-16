using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using WeUP.Domain.Media;
using WeUP.Domain.Users;
using WeUP.Infrastructure.Media;

namespace WeUP.Api.Endpoints;

/// <summary>
/// P28: Video Flyer Intake Endpoints
///
/// Two-phase upload lifecycle:
///   Phase 1 — POST /api/media/video-uploads          (multipart with file, Phase 0.15)
///   Phase 2 — POST /api/media/video-uploads/{id}/complete  (signals completion, creates processing job)
///
/// Read:
///   GET /api/media/video-assets/{assetId}  — full asset projection
///   GET /api/media/video-jobs/{jobId}      — processing job status and stage history
/// </summary>
public static class VideoFlyerEndpoints
{
    public static void MapVideoFlyerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/media")
            .WithOpenApi()
            .WithName("VideoFlyer");

        group.MapPost("/video-uploads", InitiateVideoUpload)
            .WithName("InitiateVideoUpload")
            .WithDescription(
                "P28: Register and store a video flyer upload (Phase 0.15 direct multipart). " +
                "Returns uploadId and assetId. Call /complete once the file is received.");

        group.MapPost("/video-uploads/{uploadId}/complete", CompleteVideoUpload)
            .WithName("CompleteVideoUpload")
            .WithDescription(
                "P28: Signal video upload completion. Transitions asset to ProcessingPending " +
                "and creates a VideoProcessingJob in Queued status.");

        group.MapGet("/video-assets/{assetId}", GetVideoAsset)
            .WithName("GetVideoAsset")
            .WithDescription(
                "P28: Retrieve a video flyer asset with provenance links, storage ref, and processing metadata.");

        group.MapGet("/video-jobs/{jobId}", GetVideoJob)
            .WithName("GetVideoJob")
            .WithDescription(
                "P28: Retrieve a VideoProcessingJob record including stage history and result artifacts.");

        group.MapGet("/video-assets/{assetId}/poster", GetDerivedPoster)
            .WithName("GetDerivedPoster")
            .WithDescription("P29: Retrieve the selected poster derived asset metadata for a source video asset.");

        group.MapGet("/video-assets/{assetId}/frames", GetDerivedFrames)
            .WithName("ListDerivedFrames")
            .WithDescription("P29: Retrieve extracted frame metadata for a source video asset.");

        group.MapGet("/video-assets/{assetId}/processing-summary", GetProcessingSummary)
            .WithName("GetVideoProcessingSummary")
            .WithDescription("P29: Retrieve processing summary with derived frame count and failure diagnostics.");
    }

    // -------------------------------------------------------------------------
    // POST /api/media/video-uploads
    // -------------------------------------------------------------------------

    private static async Task<IResult> InitiateVideoUpload(
        HttpRequest request,
        HttpContext httpContext,
        IVideoFlyerUploadService videoService,
        IOptions<VideoIntakeOptions> videoOptions,
        CancellationToken ct)
    {
        if (!request.HasFormContentType)
        {
            return Results.Problem(
                title: "Invalid content type",
                detail: "Request must be multipart/form-data.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Raise ASP.NET Core's multipart body limit to match the configured video max size
        // so that oversized requests are rejected by our validation rather than the framework.
        var bodySizeFeature = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySizeFeature is not null && !bodySizeFeature.IsReadOnly)
        {
            bodySizeFeature.MaxRequestBodySize = null; // Disable Kestrel hard cap; let VideoIntakeValidation enforce it
        }
        var formOptions = new FormOptions
        {
            MultipartBodyLengthLimit = videoOptions.Value.MaxFileSizeBytes + (10 * 1024 * 1024) // +10 MB head-room
        };
        httpContext.Features.Set<IFormFeature>(new FormFeature(request, formOptions));
        var form = await request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null)
        {
            return Results.Problem(
                title: "Missing file",
                detail: "Expected a multipart field named 'file' containing the video.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var uploaderUserId = AuthEndpoints.ResolveUserId(httpContext)
            ?? form["uploaderUserId"].ToString().NullIfEmpty();

        if (string.IsNullOrWhiteSpace(uploaderUserId))
        {
            return Results.Problem(
                title: "Unauthorized",
                detail: "An authenticated uploader is required to register a video flyer.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var provenance = new VideoProvenanceLinks(
            SubmissionId: form["submissionId"].ToString().NullIfEmpty(),
            VenueId: form["venueId"].ToString().NullIfEmpty(),
            ModerationItemId: form["moderationItemId"].ToString().NullIfEmpty(),
            IngestionJobId: form["ingestionJobId"].ToString().NullIfEmpty());

        var command = new InitiateVideoUploadCommand(
            OriginalFilename: file.FileName,
            ContentType: file.ContentType,
            FileSizeBytes: file.Length,
            UploaderUserId: uploaderUserId,
            Provenance: provenance);

        try
        {
            await using var stream = file.OpenReadStream();
            var upload = await videoService.InitiateUploadAsync(command, stream, ct);

            return Results.Created(
                $"/api/media/video-uploads/{upload.UploadId}",
                new InitiateVideoUploadResponse(
                    upload.UploadId,
                    upload.AssetId,
                    upload.Status.ToString(),
                    upload.UploadUrl,
                    upload.InitializedAt));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(
                title: "Video upload validation failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    // -------------------------------------------------------------------------
    // POST /api/media/video-uploads/{uploadId}/complete
    // -------------------------------------------------------------------------

    private static async Task<IResult> CompleteVideoUpload(
        string uploadId,
        CompleteVideoUploadRequest request,
        IVideoFlyerUploadService videoService,
        CancellationToken ct)
    {
        try
        {
            var result = await videoService.CompleteUploadAsync(
                uploadId,
                new CompleteVideoUploadCommand(
                    Success: request.Success,
                    FailureReason: request.FailureReason,
                    DetectedCodec: request.DetectedCodec,
                    ClientDurationSeconds: request.ClientDurationSeconds,
                    ClientWidthPx: request.ClientWidthPx,
                    ClientHeightPx: request.ClientHeightPx),
                ct);

            if (result is null)
                return Results.NotFound(new { error = $"Video upload '{uploadId}' not found." });

            return Results.Ok(new CompleteVideoUploadResponse(
                result.Upload.UploadId,
                result.Upload.AssetId,
                result.Job?.JobId,
                result.Upload.Status.ToString(),
                result.Upload.CompletedAt));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(
                title: "Video upload completion failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    // -------------------------------------------------------------------------
    // GET /api/media/video-assets/{assetId}
    // -------------------------------------------------------------------------

    private static async Task<IResult> GetVideoAsset(
        string assetId,
        IVideoFlyerUploadService videoService,
        CancellationToken ct)
    {
        var asset = await videoService.GetAssetAsync(assetId, ct);
        if (asset is null)
            return Results.NotFound(new { error = $"Video asset '{assetId}' not found." });

        return Results.Ok(new VideoAssetResponse(
            asset.AssetId,
            asset.Status.ToString(),
            asset.ContentType,
            asset.FileSizeBytes,
            asset.OriginalFilename,
            asset.UploadedAt,
            asset.UploaderUserId,
            new VideoProvenanceLinksDto(
                asset.Provenance.SubmissionId,
                asset.Provenance.VenueId,
                asset.Provenance.ModerationItemId,
                asset.Provenance.IngestionJobId),
            new VideoStorageRefDto(
                asset.Storage.Provider,
                asset.Storage.Container,
                asset.Storage.ObjectKey),
            asset.DurationSeconds,
            asset.WidthPx,
            asset.HeightPx,
            asset.DetectedCodec,
            asset.BitrateKbps,
            asset.ProcessingJobId,
            asset.PosterAssetId,
            asset.CreatedAt,
            asset.UpdatedAt));
    }

    // -------------------------------------------------------------------------
    // GET /api/media/video-jobs/{jobId}
    // -------------------------------------------------------------------------

    private static async Task<IResult> GetVideoJob(
        string jobId,
        IVideoFlyerUploadService videoService,
        CancellationToken ct)
    {
        var job = await videoService.GetJobAsync(jobId, ct);
        if (job is null)
            return Results.NotFound(new { error = $"Video processing job '{jobId}' not found." });

        return Results.Ok(new VideoJobResponse(
            job.JobId,
            job.AssetId,
            job.Status.ToString(),
            job.QueuedAt,
            job.StartedAt,
            job.CompletedAt,
            job.FailureReason,
            job.CurrentStage?.ToString(),
            job.StageHistory.Select(s => new VideoJobStageDto(
                s.Stage.ToString(),
                s.Status.ToString(),
                s.StartedAt,
                s.CompletedAt,
                s.ErrorDetail)),
            job.Result is null ? null : new VideoJobResultDto(
                job.Result.DurationSeconds,
                job.Result.WidthPx,
                job.Result.HeightPx,
                job.Result.DetectedCodec,
                job.Result.BitrateKbps,
                job.Result.PosterAssetId,
                job.Result.FrameAssetIds)));
    }

    private static async Task<IResult> GetDerivedPoster(
        string assetId,
        IVideoDerivedAssetQueryService queryService,
        CancellationToken ct)
    {
        var poster = await queryService.GetPosterAsync(assetId, ct);
        if (poster is null)
        {
            return Results.NotFound(new { error = $"No poster found for source video asset '{assetId}'." });
        }

        return Results.Ok(ToDto(poster));
    }

    private static async Task<IResult> GetDerivedFrames(
        string assetId,
        IVideoDerivedAssetQueryService queryService,
        CancellationToken ct)
    {
        var frames = await queryService.ListFramesAsync(assetId, ct);
        return Results.Ok(frames.Select(ToDto));
    }

    private static async Task<IResult> GetProcessingSummary(
        string assetId,
        IVideoDerivedAssetQueryService queryService,
        CancellationToken ct)
    {
        var summary = await queryService.GetProcessingSummaryAsync(assetId, ct);
        if (summary is null)
        {
            return Results.NotFound(new { error = $"Video asset '{assetId}' not found." });
        }

        return Results.Ok(summary);
    }

    private static VideoDerivedFrameDto ToDto(VideoDerivedFrameAsset frame) => new(
        frame.SourceVideoAssetId,
        frame.DerivedAssetId,
        frame.ProcessingJobId,
        frame.UploaderUserId,
        frame.SubmissionId,
        frame.VenueId,
        frame.ModerationItemId,
        frame.TimestampOffsetMs,
        frame.FrameType.ToString(),
        frame.WidthPx,
        frame.HeightPx,
        frame.Storage.Provider.ToString(),
        frame.Storage.Container,
        frame.Storage.ObjectKey,
        frame.Storage.Uri,
        frame.ExtractionStage,
        frame.ExtractionVersion,
        frame.IsPosterSelected,
        frame.PosterSelectionReason,
        frame.CreatedAt);

    // =========================================================================
    // Request / Response contracts (internal to this file)
    // =========================================================================

    private sealed record InitiateVideoUploadResponse(
        string UploadId,
        string AssetId,
        string Status,
        string? UploadUrl,
        DateTimeOffset InitializedAt);

    private sealed record CompleteVideoUploadRequest(
        bool Success,
        string? FailureReason = null,
        string? DetectedCodec = null,
        int? ClientDurationSeconds = null,
        int? ClientWidthPx = null,
        int? ClientHeightPx = null);

    private sealed record CompleteVideoUploadResponse(
        string UploadId,
        string AssetId,
        string? JobId,
        string Status,
        DateTimeOffset? CompletedAt);

    private sealed record VideoAssetResponse(
        string AssetId,
        string Status,
        string ContentType,
        long FileSizeBytes,
        string OriginalFilename,
        DateTimeOffset UploadedAt,
        string? UploaderUserId,
        VideoProvenanceLinksDto Provenance,
        VideoStorageRefDto Storage,
        int? DurationSeconds,
        int? WidthPx,
        int? HeightPx,
        string? DetectedCodec,
        int? BitrateKbps,
        string? ProcessingJobId,
        string? PosterAssetId,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record VideoProvenanceLinksDto(
        string? SubmissionId,
        string? VenueId,
        string? ModerationItemId,
        string? IngestionJobId);

    private sealed record VideoStorageRefDto(
        string Provider,
        string Container,
        string ObjectKey);

    private sealed record VideoJobResponse(
        string JobId,
        string AssetId,
        string Status,
        DateTimeOffset QueuedAt,
        DateTimeOffset? StartedAt,
        DateTimeOffset? CompletedAt,
        string? FailureReason,
        string? CurrentStage,
        IEnumerable<VideoJobStageDto> StageHistory,
        VideoJobResultDto? Result);

    private sealed record VideoJobStageDto(
        string Stage,
        string Status,
        DateTimeOffset StartedAt,
        DateTimeOffset? CompletedAt,
        string? ErrorDetail);

    private sealed record VideoJobResultDto(
        int? DurationSeconds,
        int? WidthPx,
        int? HeightPx,
        string? DetectedCodec,
        int? BitrateKbps,
        string? PosterAssetId,
        string[]? FrameAssetIds);

    private sealed record VideoDerivedFrameDto(
        string SourceVideoAssetId,
        string DerivedAssetId,
        string ProcessingJobId,
        string? UploaderUserId,
        string? SubmissionId,
        string? VenueId,
        string? ModerationItemId,
        long TimestampOffsetMs,
        string FrameType,
        int WidthPx,
        int HeightPx,
        string StorageProvider,
        string StorageContainer,
        string StorageObjectKey,
        string? StorageUri,
        string ExtractionStage,
        string ExtractionVersion,
        bool IsPosterSelected,
        string? PosterSelectionReason,
        DateTimeOffset CreatedAt);
}

// File-scoped extension — mirrors the one in MediaEndpoints.cs
file static class VideoStringExtensions
{
    public static string? NullIfEmpty(this string s) => string.IsNullOrEmpty(s) ? null : s;
}
