using WeUP.Contracts.Ocr;
using WeUP.Infrastructure.Flyer;
using Xunit;

namespace WeUP.Tests.Integration;

public sealed class NormalizationEngineTests
{
    [Fact]
    public void Normalize_WhenTimezoneIsMissing_DoesNotFabricateUtc()
    {
        var engine = new NormalizationEngine();
        var ocr = BuildOcrResult(
            rawText: "Spring Community Meetup\nApr 24, 2026 8:00 PM\nCity Hall\n1201 Main St, Austin, TX",
            blocks:
            [
                new OcrTextBlock(0, "Spring Community Meetup", 0.95, 20, 12, 260, 30, new Dictionary<string, string?>()),
                new OcrTextBlock(1, "Apr 24, 2026 8:00 PM", 0.90, 20, 50, 260, 24, new Dictionary<string, string?>()),
                new OcrTextBlock(2, "City Hall", 0.88, 20, 80, 180, 24, new Dictionary<string, string?>()),
                new OcrTextBlock(3, "1201 Main St, Austin, TX", 0.90, 20, 110, 280, 24, new Dictionary<string, string?>()),
            ]);

        var candidate = engine.Normalize(ocr);

        Assert.Equal("Spring Community Meetup", candidate.Title);
        Assert.Null(candidate.StartUtc);
        Assert.Null(candidate.EndUtc);
        Assert.Equal("1201 Main St, Austin, TX", candidate.Address);
        Assert.Contains("rawText", candidate.RawFields.Keys);
        Assert.Contains("title", candidate.FieldScores.Keys);
    }

    [Fact]
    public void Normalize_WithExplicitIsoRange_ParsesStartAndEndUtc()
    {
        var engine = new NormalizationEngine();
        var ocr = BuildOcrResult(
            rawText: "Downtown House Night\n2026-04-24T20:00:00-05:00 to 2026-04-24T23:30:00-05:00\nVenue: Skyline Room\n#house #dj",
            blocks:
            [
                new OcrTextBlock(0, "Downtown House Night", 0.93, 30, 10, 300, 30, new Dictionary<string, string?>()),
                new OcrTextBlock(1, "2026-04-24T20:00:00-05:00 to 2026-04-24T23:30:00-05:00", 0.91, 30, 45, 420, 24, new Dictionary<string, string?>()),
                new OcrTextBlock(2, "Venue: Skyline Room", 0.87, 30, 75, 250, 24, new Dictionary<string, string?>()),
                new OcrTextBlock(3, "#house #dj", 0.86, 30, 102, 200, 24, new Dictionary<string, string?>()),
            ]);

        var candidate = engine.Normalize(ocr);

        Assert.Equal(new DateTimeOffset(2026, 4, 25, 1, 0, 0, TimeSpan.Zero), candidate.StartUtc);
        Assert.Equal(new DateTimeOffset(2026, 4, 25, 4, 30, 0, TimeSpan.Zero), candidate.EndUtc);
        Assert.Equal("Skyline Room", candidate.Venue);
        Assert.Contains("house", candidate.Tags, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("music", candidate.Tags, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Normalize_PreservesAllRawBlockFields()
    {
        var engine = new NormalizationEngine();
        var ocr = BuildOcrResult(
            rawText: "Neighborhood Market\nLocation: Eastside Plaza\n1450 River Rd, Austin, TX",
            blocks:
            [
                new OcrTextBlock(0, "Neighborhood Market", 0.92, 12, 10, 280, 28, new Dictionary<string, string?> { ["font"] = "bold" }),
                new OcrTextBlock(1, "Location: Eastside Plaza", 0.84, 12, 44, 300, 24, new Dictionary<string, string?>()),
                new OcrTextBlock(2, "1450 River Rd, Austin, TX", 0.82, 12, 78, 320, 24, new Dictionary<string, string?>()),
            ]);

        var candidate = engine.Normalize(ocr);

        Assert.Equal("Neighborhood Market", candidate.Title);
        Assert.Equal("1450 River Rd, Austin, TX", candidate.Address);
        Assert.Equal("bold", candidate.RawFields["blocks:0:metadata:font"]);
        Assert.Equal("Neighborhood Market", candidate.RawFields["blocks:0:text"]);
        Assert.Equal("true", candidate.RawFields["success"]);
    }

    private static OcrResult BuildOcrResult(string rawText, OcrTextBlock[] blocks)
    {
        return new OcrResult(
            ExtractionId: "extract-1",
            JobId: "job-1",
            AssetId: "asset-1",
            Provider: "test-provider",
            ProviderVersion: "v1",
            Confidence: 0.89,
            Success: true,
            RawText: rawText,
            Blocks: blocks,
            FailureReason: null,
            AttemptCount: 1,
            StartedAtUtc: new DateTimeOffset(2026, 4, 15, 10, 0, 0, TimeSpan.Zero),
            CompletedAtUtc: new DateTimeOffset(2026, 4, 15, 10, 0, 2, TimeSpan.Zero),
            Metadata: new Dictionary<string, string?>
            {
                ["source"] = "unit-test",
            });
    }
}
