using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using WeUP.Contracts.Ingestion;

namespace WeUP.Tests.Integration;

public sealed class IngestionEndpointsTests : IClassFixture<IngestionEndpointsTests.IngestionApiFactory>
{
    private readonly HttpClient _client;

    public IngestionEndpointsTests(IngestionApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    [Fact]
    public async Task ManualIngestion_CreatesReviewableCanonicalCandidate()
    {
        var request = new ManualIngestionRequest(
            "Sunset Rooftop Session",
            "The Beacon",
            "123 Mission St, San Francisco, CA",
            "2026-06-20T19:00:00-07:00",
            "2026-06-20T23:00:00-07:00",
            "America/Los_Angeles",
            "music",
            "Deep house on the roof.",
            ["house", "sunset"],
            "user-manual-1");

        var submitResponse = await _client.PostAsJsonAsync("/api/ingestion/manual", request);
        Assert.Equal(HttpStatusCode.Accepted, submitResponse.StatusCode);

        var accepted = await submitResponse.Content.ReadFromJsonAsync<IngestionAcceptedResponse>();
        Assert.NotNull(accepted);

        var jobResponse = await _client.GetAsync(accepted!.StatusUrl);
        jobResponse.EnsureSuccessStatusCode();

        var job = await jobResponse.Content.ReadFromJsonAsync<IngestionResult>();
        Assert.NotNull(job);
        Assert.Equal(IngestionJobStatus.REQUIRES_REVIEW, job!.Status);
        Assert.Equal(IngestionSourceKind.ManualSubmission, job.Request.SourceKind);
        Assert.NotNull(job.Candidate);
        Assert.Equal("Sunset Rooftop Session", job.Candidate!.Title);
        Assert.Contains(job.Evidence, evidence => evidence.EvidenceKind == "raw-payload");
        Assert.Contains(job.Lifecycle, step => step.Status == IngestionJobStatus.RECEIVED);
        Assert.Contains(job.Lifecycle, step => step.Status == IngestionJobStatus.CANDIDATE_CREATED);
        Assert.Contains(job.Lifecycle, step => step.Status == IngestionJobStatus.REQUIRES_REVIEW);
    }

    [Fact]
    public async Task UrlIngestion_InvalidAbsoluteUrl_ReturnsValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/api/ingestion/url", new UrlIngestionRequest("not-a-url", "user-link-1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("A valid absolute URL is required.", payload);
    }

    [Fact]
    public async Task VenuePageIngestion_InvalidPayload_ProducesFailedJob()
    {
        var request = new VenuePageIngestionRequest(
            "venue-123",
            "notaurl",
            "operator-1",
            "Venue 123");

        var submitResponse = await _client.PostAsJsonAsync("/api/ingestion/venue-page", request);
        Assert.Equal(HttpStatusCode.Accepted, submitResponse.StatusCode);

        var accepted = await submitResponse.Content.ReadFromJsonAsync<IngestionAcceptedResponse>();
        Assert.NotNull(accepted);

        var job = await _client.GetFromJsonAsync<IngestionResult>(accepted!.StatusUrl);
        Assert.NotNull(job);
        Assert.Equal(IngestionJobStatus.FAILED, job!.Status);
        Assert.Equal(IngestionSourceKind.VenuePage, job.Request.SourceKind);
        Assert.Contains(job.Issues, issue => issue.Code == "invalid_page_url");
        Assert.Contains(job.Lifecycle, step => step.Status == IngestionJobStatus.VALIDATING);
        Assert.Contains(job.Lifecycle, step => step.Status == IngestionJobStatus.FAILED);
    }

    public sealed class IngestionApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("SeedData:EnableOnStartup", "true");
            builder.UseSetting("SeedData:EnableResetEndpoint", "true");
            builder.UseSetting("SeedData:DatasetPath", "..\\..\\seed\\phase0-dataset.json");
        }
    }
}