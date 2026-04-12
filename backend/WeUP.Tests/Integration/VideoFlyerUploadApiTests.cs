using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using WeUP.Contracts.Auth;
using Xunit;

namespace WeUP.Tests.Integration;

/// <summary>
/// P28: Integration tests for the video flyer intake endpoints.
///
/// Covers:
///  - Valid video upload creates durable asset and upload records
///  - Unsupported content type returns 422
///  - Oversized file returns 422
///  - Unauthenticated request returns 401
///  - Completion signal creates a processing job
///  - Job lookup returns Queued status
///  - Failure signal rejects asset; no job created
/// </summary>
public sealed class VideoFlyerUploadApiTests : IClassFixture<VideoFlyerUploadApiTests.VideoTestApiFactory>
{
    private readonly HttpClient _client;

    public VideoFlyerUploadApiTests(VideoTestApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    /// <summary>
    /// Factory that lowers VideoIntake:MaxFileSizeBytes to 2 MB for test isolation.
    /// Standalone factory that mirrors Phase0ApiFactory settings and lowers
    /// VideoIntake:MaxFileSizeBytes to 2 MB for test isolation.
    /// This allows the oversized-file test to send a 3 MB file instead of 501 MB.
    /// </summary>
    public sealed class VideoTestApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("SeedData:EnableOnStartup", "true");
            builder.UseSetting("SeedData:EnableResetEndpoint", "true");
            builder.UseSetting("SeedData:DatasetPath", "..\\..\\seed\\phase0-dataset.json");
            builder.UseSetting("VideoIntake:MaxFileSizeBytes", (2 * 1024 * 1024).ToString()); // 2 MB for tests
        }
    }

    // =========================================================================
    // Upload initiation
    // =========================================================================

    [Fact]
    public async Task VideoUpload_ValidMp4_ReturnsDurableIds()
    {
        await AuthenticateAsync();

        var response = await PostVideoAsync("flyer-clip.mp4", "video/mp4", sizeBytes: 2048);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("uploadId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("assetId").GetString()));
        Assert.Equal("Uploaded", payload.GetProperty("status").GetString());
    }

    [Fact]
    public async Task VideoUpload_ValidWebm_Succeeds()
    {
        await AuthenticateAsync();

        var response = await PostVideoAsync("clip.webm", "video/webm", sizeBytes: 1024);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task VideoUpload_UnsupportedContentType_Returns422()
    {
        await AuthenticateAsync();

        var response = await PostVideoAsync("note.txt", "text/plain", sizeBytes: 512);

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("unsupported video content type", body);
    }

    [Fact]
    public async Task VideoUpload_ImageMimeType_Returns422()
    {
        await AuthenticateAsync();

        // image/jpeg is valid for image uploads but must be rejected by the video endpoint
        var response = await PostVideoAsync("photo.jpg", "image/jpeg", sizeBytes: 512);

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("unsupported video content type", body);
    }

    [Fact]
    public async Task VideoUpload_OversizedFile_Returns422()
    {
        await AuthenticateAsync();

        // VideoTestApiFactory sets MaxFileSizeBytes to 2 MB; send 3 MB to trigger the limit.
        var sizeBytes = 3 * 1024 * 1024;
        var response = await PostVideoAsync("medium.mp4", "video/mp4", sizeBytes);

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("exceeds the maximum", body);
    }

    [Fact]
    public async Task VideoUpload_WithoutAuth_Returns401()
    {
        // No authentication header set
        using var form = BuildVideoForm("flyer.mp4", "video/mp4", 1024);
        var response = await _client.PostAsync("/api/media/video-uploads", form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // Completion signal → job creation
    // =========================================================================

    [Fact]
    public async Task CompleteUpload_Success_CreatesProcessingJob()
    {
        await AuthenticateAsync();

        var createResponse = await PostVideoAsync("event-promo.mp4", "video/mp4", 4096);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();

        var uploadId = created.GetProperty("uploadId").GetString()!;
        var assetId = created.GetProperty("assetId").GetString()!;

        // Signal completion with client-reported hints
        var completeResponse = await _client.PostAsJsonAsync(
            $"/api/media/video-uploads/{uploadId}/complete",
            new
            {
                success = true,
                detectedCodec = "h264",
                clientDurationSeconds = 30,
                clientWidthPx = 1920,
                clientHeightPx = 1080,
            });

        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var completion = await completeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ProcessingPending", completion.GetProperty("status").GetString());
        var jobId = completion.GetProperty("jobId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(jobId));

        // Asset must be in ProcessingPending
        var assetResponse = await _client.GetAsync($"/api/media/video-assets/{assetId}");
        Assert.Equal(HttpStatusCode.OK, assetResponse.StatusCode);
        var asset = await assetResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ProcessingPending", asset.GetProperty("status").GetString());
        Assert.Equal(jobId, asset.GetProperty("processingJobId").GetString());
        Assert.Equal("h264", asset.GetProperty("detectedCodec").GetString());
        Assert.Equal(30, asset.GetProperty("durationSeconds").GetInt32());

        // Job must be Queued
        var jobResponse = await _client.GetAsync($"/api/media/video-jobs/{jobId}");
        Assert.Equal(HttpStatusCode.OK, jobResponse.StatusCode);
        var job = await jobResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Queued", job.GetProperty("status").GetString());
        Assert.Equal(assetId, job.GetProperty("assetId").GetString());
        Assert.Equal(0, job.GetProperty("stageHistory").GetArrayLength());
    }

    [Fact]
    public async Task CompleteUpload_WithFailure_RejectsAsset_NoJobCreated()
    {
        await AuthenticateAsync();

        var createResponse = await PostVideoAsync("bad-upload.mp4", "video/mp4", 1024);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var uploadId = created.GetProperty("uploadId").GetString()!;
        var assetId = created.GetProperty("assetId").GetString()!;

        var completeResponse = await _client.PostAsJsonAsync(
            $"/api/media/video-uploads/{uploadId}/complete",
            new { success = false, failureReason = "network interrupted" });

        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var completion = await completeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Rejected", completion.GetProperty("status").GetString());

        // jobId must be null for failure path
        var jobIdElement = completion.GetProperty("jobId");
        Assert.True(jobIdElement.ValueKind == JsonValueKind.Null);

        // Asset must reflect Rejected status
        var assetResponse = await _client.GetAsync($"/api/media/video-assets/{assetId}");
        assetResponse.EnsureSuccessStatusCode();
        var asset = await assetResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Rejected", asset.GetProperty("status").GetString());
    }

    [Fact]
    public async Task CompleteUpload_UnknownUploadId_Returns404()
    {
        await AuthenticateAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/media/video-uploads/nonexistent-id/complete",
            new { success = true });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetVideoAsset_UnknownId_Returns404()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync("/api/media/video-assets/no-such-asset");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetVideoJob_UnknownId_Returns404()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync("/api/media/video-jobs/no-such-job");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // =========================================================================
    // Provenance linkage
    // =========================================================================

    [Fact]
    public async Task VideoUpload_WithProvenance_LinksSubmissionAndVenue()
    {
        await AuthenticateAsync();

        using var form = BuildVideoForm("promo.mp4", "video/mp4", 2048);
        form.Add(new StringContent("sub-abc123"), "submissionId");
        form.Add(new StringContent("venue-xyz"), "venueId");

        var createResponse = await _client.PostAsync("/api/media/video-uploads", form);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var assetId = created.GetProperty("assetId").GetString()!;

        var assetResponse = await _client.GetAsync($"/api/media/video-assets/{assetId}");
        assetResponse.EnsureSuccessStatusCode();
        var asset = await assetResponse.Content.ReadFromJsonAsync<JsonElement>();

        var provenance = asset.GetProperty("provenance");
        Assert.Equal("sub-abc123", provenance.GetProperty("submissionId").GetString());
        Assert.Equal("venue-xyz", provenance.GetProperty("venueId").GetString());
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task<HttpResponseMessage> PostVideoAsync(string filename, string contentType, int sizeBytes)
    {
        using var form = BuildVideoForm(filename, contentType, sizeBytes);
        return await _client.PostAsync("/api/media/video-uploads", form);
    }

    private static MultipartFormDataContent BuildVideoForm(string filename, string contentType, int sizeBytes)
    {
        var form = new MultipartFormDataContent();
        var bytes = MakeVideoBytes(sizeBytes);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", filename);
        return form;
    }

    private async Task AuthenticateAsync()
    {
        var login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("camille+phase0@weup.test"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
    }

    /// <summary>
    /// Builds a synthetic byte array that looks like an MP4 file header (ftyp box).
    /// The server does no deep container inspection in Phase 0; a plausible MIME type suffices.
    /// </summary>
    private static byte[] MakeVideoBytes(int size)
    {
        var bytes = new byte[Math.Max(size, 16)];
        // ftyp box: size (4 bytes BE) + "ftyp" + "mp42"
        bytes[0] = 0x00; bytes[1] = 0x00; bytes[2] = 0x00; bytes[3] = 0x1C;
        bytes[4] = (byte)'f'; bytes[5] = (byte)'t'; bytes[6] = (byte)'y'; bytes[7] = (byte)'p';
        bytes[8] = (byte)'m'; bytes[9] = (byte)'p'; bytes[10] = (byte)'4'; bytes[11] = (byte)'2';
        return bytes;
    }
}
