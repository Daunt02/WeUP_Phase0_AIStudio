using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using WeUP.Contracts.Auth;
using Xunit;

namespace WeUP.Tests.Integration;

public sealed class MediaUploadApiTests : IClassFixture<Phase0ReleaseApiTests.Phase0ApiFactory>
{
    private readonly HttpClient _client;

    public MediaUploadApiTests(Phase0ReleaseApiTests.Phase0ApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    [Fact]
    public async Task UploadFlyer_Succeeds_AndReturnsDurableIds()
    {
        await AuthenticateAsync();

        using var form = new MultipartFormDataContent();
        var bytes = MakeJpegBytes(2048);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", "flyer.jpg");
        form.Add(new StringContent("FlyerImage"), "assetType");
        form.Add(new StringContent("User"), "ownerType");

        var response = await _client.PostAsync("/api/media/uploads", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CreateUploadPayload>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.UploadId));
        Assert.False(string.IsNullOrWhiteSpace(payload.AssetId));
        Assert.Equal("Uploaded", payload.Status);
    }

    [Fact]
    public async Task UploadRejects_OversizedFile()
    {
        await AuthenticateAsync();

        using var form = new MultipartFormDataContent();
        var bytes = MakeJpegBytes((12 * 1024 * 1024) + 1);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", "oversized.jpg");
        form.Add(new StringContent("FlyerImage"), "assetType");

        var response = await _client.PostAsync("/api/media/uploads", form);

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("exceeds maximum", body);
    }

    [Fact]
    public async Task UploadRejects_UnsupportedContentType()
    {
        await AuthenticateAsync();

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("plain text"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(fileContent, "file", "note.txt");
        form.Add(new StringContent("FlyerImage"), "assetType");

        var response = await _client.PostAsync("/api/media/uploads", form);

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("unsupported content type", body);
    }

    [Fact]
    public async Task UploadWithoutAuth_WhenUserOwnerRequired_ReturnsUnauthorized()
    {
        using var form = new MultipartFormDataContent();
        var bytes = MakeJpegBytes(512);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", "anonymous.jpg");
        form.Add(new StringContent("FlyerImage"), "assetType");
        form.Add(new StringContent("User"), "ownerType");

        var response = await _client.PostAsync("/api/media/uploads", form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CompleteUpload_ThenRetrieveUploadRecord()
    {
        await AuthenticateAsync();

        using var form = new MultipartFormDataContent();
        var bytes = MakeJpegBytes(1024);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", "complete.jpg");
        form.Add(new StringContent("VenueImage"), "assetType");
        form.Add(new StringContent("Venue"), "ownerType");
        form.Add(new StringContent("venue-123"), "ownerId");
        form.Add(new StringContent("venue-123"), "venueId");

        var createResponse = await _client.PostAsync("/api/media/uploads", form);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateUploadPayload>();
        Assert.NotNull(created);

        var completeResponse = await _client.PostAsJsonAsync($"/api/media/uploads/{created!.UploadId}/complete", new
        {
            processingSucceeded = true,
            queueForReview = true,
        });
        completeResponse.EnsureSuccessStatusCode();

        var getUpload = await _client.GetAsync($"/api/media/uploads/{created.UploadId}");
        getUpload.EnsureSuccessStatusCode();
        var uploadJson = await getUpload.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(created.AssetId, uploadJson.GetProperty("assetId").GetString());
        Assert.Equal("ReviewPending", uploadJson.GetProperty("status").GetString());

        var getAsset = await _client.GetAsync($"/api/media/assets/{created.AssetId}");
        getAsset.EnsureSuccessStatusCode();
        var assetJson = await getAsset.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("VenueImage", assetJson.GetProperty("assetType").GetString());
        Assert.Equal("ReviewPending", assetJson.GetProperty("status").GetString());
        Assert.Equal("venue-123", assetJson.GetProperty("owner").GetProperty("ownerId").GetString());
    }

    private async Task AuthenticateAsync()
    {
        var login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("camille+phase0@weup.test"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
    }

    private static byte[] MakeJpegBytes(int size)
    {
        var bytes = new byte[size];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        for (var i = 3; i < size; i++)
        {
            bytes[i] = (byte)(i % 251);
        }

        return bytes;
    }

    private sealed record CreateUploadPayload(string UploadId, string AssetId, string Status);
}
