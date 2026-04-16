using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Ocr;
using WeUP.Infrastructure.Ocr;
using Xunit;

namespace WeUP.Tests.Integration;

public sealed class FlyerIngestionEndpointsTests : IClassFixture<FlyerIngestionEndpointsTests.FlyerIngestionApiFactory>
{
    private readonly HttpClient _client;

    public FlyerIngestionEndpointsTests(FlyerIngestionApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    [Fact]
    public async Task FlyerIngestion_SubmitAndFetchJobAndEvidence_ReturnsDeterministicArtifacts()
    {
        var assetId = await UploadFlyerAssetAsync("test-user-flyer-ingestion");

        var submitResponse = await _client.PostAsJsonAsync("/api/ingestion/flyers", new FlyerUploadIngestionRequest(
            AssetId: assetId,
            SubmittedBy: "test-user-flyer-ingestion",
            SubmissionId: "sub-001",
            SourceUrl: "https://example.com/flyer/1",
            PartnerProvider: null,
            SubmitterNote: "integration test",
            Reprocess: false));

        Assert.Equal(HttpStatusCode.Accepted, submitResponse.StatusCode);

        var accepted = await submitResponse.Content.ReadFromJsonAsync<IngestionAcceptedResponse>();
        Assert.NotNull(accepted);
        Assert.False(string.IsNullOrWhiteSpace(accepted!.JobId));
        Assert.Equal(IngestionJobStatus.REQUIRES_REVIEW, accepted.Status);

        var jobResponse = await _client.GetAsync($"/api/ingestion/flyers/{accepted.JobId}");
        jobResponse.EnsureSuccessStatusCode();

        var job = await jobResponse.Content.ReadFromJsonAsync<FlyerIngestionJobDetailResponse>();
        Assert.NotNull(job);
        Assert.Equal(accepted.JobId, job!.JobId);
        Assert.Equal(IngestionJobStatus.REQUIRES_REVIEW, job.Status);
        Assert.Equal(assetId, job.Asset.AssetId);
        Assert.NotNull(job.Ocr);
        Assert.True(job.Ocr!.Success);
        Assert.NotEmpty(job.Ocr.Blocks);
        Assert.NotNull(job.Candidate);
        Assert.True(job.RequiresManualReview);
        Assert.Contains(FlyerReviewTriggerReason.DedupePending, job.ReviewReasons);
        Assert.True(job.Confidence.Aggregate > 0.0);
        Assert.Contains(job.Lifecycle, x => x.Status == IngestionJobStatus.CANDIDATE_CREATED);

        var evidenceResponse = await _client.GetAsync($"/api/ingestion/flyers/{accepted.JobId}/evidence");
        evidenceResponse.EnsureSuccessStatusCode();

        var evidence = await evidenceResponse.Content.ReadFromJsonAsync<FlyerIngestionEvidenceResponse>();
        Assert.NotNull(evidence);
        Assert.Equal(accepted.JobId, evidence!.JobId);
        Assert.Equal(assetId, evidence.Asset.AssetId);
        Assert.False(string.IsNullOrWhiteSpace(evidence.RawOcrTextSnapshot));
        Assert.False(string.IsNullOrWhiteSpace(evidence.NormalizationRunId));
        Assert.False(string.IsNullOrWhiteSpace(evidence.NormalizationVersion));
        Assert.Contains("DedupePending", evidence.ReviewReasons);
    }

    [Fact]
    public async Task FlyerIngestion_MissingAsset_Returns422()
    {
        var response = await _client.PostAsJsonAsync("/api/ingestion/flyers", new FlyerUploadIngestionRequest(
            AssetId: "missing-asset",
            SubmittedBy: "test-user",
            Reprocess: false));

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Flyer asset 'missing-asset' was not found", body);
    }

    private async Task<string> UploadFlyerAssetAsync(string submitterId)
    {
        var flyerPath = ResolveFixtureFlyerPath();
        await using var fileStream = File.OpenRead(flyerPath);

        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "file", Path.GetFileName(flyerPath));

        var response = await _client.PostAsync($"/api/media/flyers?submitterId={submitterId}", form);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var assetId = payload.GetProperty("assetId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(assetId));
        return assetId!;
    }

    private static string ResolveFixtureFlyerPath()
    {
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(current, "flyers", "IMG_6661.PNG");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }

            current = parent.FullName;
        }

        throw new InvalidOperationException("Fixture flyer not found. Expected flyers/IMG_6661.PNG in repository root hierarchy.");
    }

    public class FlyerIngestionApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("SeedData:EnableOnStartup", "true");
            builder.UseSetting("SeedData:EnableResetEndpoint", "true");
            builder.UseSetting("SeedData:DatasetPath", "..\\..\\seed\\phase0-dataset.json");
            builder.ConfigureServices(services =>
            {
                var existing = services.SingleOrDefault(descriptor => descriptor.ServiceType == typeof(IOcrProvider));
                if (existing is not null)
                {
                    services.Remove(existing);
                }

                services.AddSingleton<IOcrProvider, DeterministicIntegrationOcrProvider>();
            });
        }
    }

    private sealed class DeterministicIntegrationOcrProvider : IOcrProvider
    {
        public string Name => "integration-ocr";
        public string Version => "v1";

        public Task<OcrResult> ExtractAsync(OcrProviderRequest request, CancellationToken ct = default)
        {
            OcrTextBlock[] blocks =
            [
                new OcrTextBlock(
                    Index: 0,
                    Text: "FRIDAY APR 24 8PM HOUSE NIGHT SKYLINE LOUNGE 1201 MAIN ST AUSTIN TX",
                    Confidence: 0.74,
                    X: 32,
                    Y: 40,
                    Width: 1024,
                    Height: 180,
                    Metadata: new Dictionary<string, string?>
                    {
                        ["source"] = "integration-test",
                        ["assetId"] = request.AssetId,
                    })
            ];

            return Task.FromResult(new OcrResult(
                ExtractionId: request.ExtractionId,
                JobId: request.JobId,
                AssetId: request.AssetId,
                Provider: Name,
                ProviderVersion: Version,
                Confidence: 0.74,
                Success: true,
                RawText: string.Join(Environment.NewLine, blocks.Select(block => block.Text)),
                Blocks: blocks,
                FailureReason: null,
                AttemptCount: request.AttemptCount,
                StartedAtUtc: request.StartedAtUtc,
                CompletedAtUtc: DateTimeOffset.UtcNow,
                Metadata: new Dictionary<string, string?>()));
        }
    }
}
