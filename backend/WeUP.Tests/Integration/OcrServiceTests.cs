using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using WeUP.Contracts.Ocr;
using WeUP.Domain.Ocr;
using WeUP.Infrastructure.Ocr;
using Xunit;

namespace WeUP.Tests.Integration;

public sealed class OcrServiceTests
{
    [Fact]
    public async Task ExtractAsync_RetriesOnceAndReturnsProviderOutput()
    {
        var provider = new FlakyProvider();
        var service = new ProviderBackedOcrService(provider);
        var imagePath = CreateTempPng();

        try
        {
            var result = await service.ExtractAsync(new OcrRequest(
                JobId: "job-1",
                AssetId: "asset-1",
                StorageKey: "flyers/job-1.png",
                ContentType: "image/png",
                OriginalFilename: "job-1.png",
                LocalPath: imagePath,
                Metadata: new Dictionary<string, string?>()));

            Assert.True(result.Success);
            Assert.Equal(2, result.AttemptCount);
            Assert.Equal(0.82, result.Confidence);
            Assert.Equal("APR 24", result.RawText);
            Assert.Single(result.Blocks);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task ExtractAsync_WhenProviderFails_ReturnsLowConfidenceFailure()
    {
        var service = new ProviderBackedOcrService(new AlwaysFailProvider());
        var imagePath = CreateTempPng();

        try
        {
            var result = await service.ExtractAsync(new OcrRequest(
                JobId: "job-2",
                AssetId: "asset-2",
                StorageKey: "flyers/job-2.png",
                ContentType: "image/png",
                OriginalFilename: "job-2.png",
                LocalPath: imagePath,
                Metadata: new Dictionary<string, string?>()));

            Assert.False(result.Success);
            Assert.Equal(0.0, result.Confidence);
            Assert.Equal(string.Empty, result.RawText);
            Assert.Empty(result.Blocks);
            Assert.Equal(2, result.AttemptCount);
            Assert.Contains("provider boom", result.FailureReason, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task ExtractAsync_EmptyProviderResult_PropagatesWithoutFabrication()
    {
        var service = new ProviderBackedOcrService(new EmptyProvider());
        var imagePath = CreateTempPng();

        try
        {
            var result = await service.ExtractAsync(new OcrRequest(
                JobId: "job-3",
                AssetId: "asset-3",
                StorageKey: "flyers/job-3.png",
                ContentType: "image/png",
                OriginalFilename: "job-3.png",
                LocalPath: imagePath,
                Metadata: new Dictionary<string, string?>()));

            Assert.True(result.Success);
            Assert.Equal(0.0, result.Confidence);
            Assert.Equal(string.Empty, result.RawText);
            Assert.Empty(result.Blocks);
            Assert.Null(result.FailureReason);
            Assert.Equal(1, result.AttemptCount);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task FlyerIngestion_CanUseDeterministicTestProvider()
    {
        await using var factory = new FlyerIngestionEndpointsTests.FlyerIngestionApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var flyerPath = ResolveFixtureFlyerPath();
        await using var fileStream = File.OpenRead(flyerPath);

        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "file", Path.GetFileName(flyerPath));

        var uploadResponse = await client.PostAsync("/api/media/flyers?submitterId=test-user-ocr", form);
        uploadResponse.EnsureSuccessStatusCode();
    }

    private static string CreateTempPng()
    {
        var path = Path.Combine(Path.GetTempPath(), $"weup-ocr-{Guid.NewGuid():N}.png");
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+Xl4QAAAAASUVORK5CYII=");
        File.WriteAllBytes(path, bytes);
        return path;
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

    private sealed class FlakyProvider : IOcrProvider
    {
        private int _calls;

        public string Name => "test-provider";
        public string Version => "v1";

        public Task<OcrResult> ExtractAsync(OcrProviderRequest request, CancellationToken ct = default)
        {
            _calls++;
            if (_calls == 1)
            {
                throw new InvalidOperationException("transient fail");
            }

            return Task.FromResult(new OcrResult(
                ExtractionId: request.ExtractionId,
                JobId: request.JobId,
                AssetId: request.AssetId,
                Provider: Name,
                ProviderVersion: Version,
                Confidence: 0.82,
                Success: true,
                RawText: "APR 24",
                Blocks:
                [
                    new OcrTextBlock(0, "APR 24", 0.82, 1, 2, 30, 12, new Dictionary<string, string?>())
                ],
                FailureReason: null,
                AttemptCount: request.AttemptCount,
                StartedAtUtc: request.StartedAtUtc,
                CompletedAtUtc: DateTimeOffset.UtcNow,
                Metadata: new Dictionary<string, string?>()));
        }
    }

    private sealed class AlwaysFailProvider : IOcrProvider
    {
        public string Name => "failing-provider";
        public string Version => "v1";

        public Task<OcrResult> ExtractAsync(OcrProviderRequest request, CancellationToken ct = default)
            => throw new InvalidOperationException("provider boom");
    }

    private sealed class EmptyProvider : IOcrProvider
    {
        public string Name => "empty-provider";
        public string Version => "v1";

        public Task<OcrResult> ExtractAsync(OcrProviderRequest request, CancellationToken ct = default)
            => Task.FromResult(new OcrResult(
                ExtractionId: request.ExtractionId,
                JobId: request.JobId,
                AssetId: request.AssetId,
                Provider: Name,
                ProviderVersion: Version,
                Confidence: 0.0,
                Success: true,
                RawText: string.Empty,
                Blocks: [],
                FailureReason: null,
                AttemptCount: request.AttemptCount,
                StartedAtUtc: request.StartedAtUtc,
                CompletedAtUtc: DateTimeOffset.UtcNow,
                Metadata: new Dictionary<string, string?>()));
    }
}