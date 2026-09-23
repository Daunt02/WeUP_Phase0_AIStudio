using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using WeUP.Application.Moderation;
using WeUP.Contracts.Ingestion;
using Xunit;

namespace WeUP.Tests.Moderation;

public sealed class RiskScoringServiceTests
{
    private readonly RiskScoringService _svc = new(NullLogger<RiskScoringService>.Instance);

    [Fact]
    public async Task MissingTitle_AddsMissingTitleFactor()
    {
        var candidate = new CandidateEvent(
            CandidateId: Guid.NewGuid(),
            RequestId: Guid.NewGuid(),
            Title: null,
            InferredStartUtc: DateTimeOffset.UtcNow,
            RawLocationText: "Some Park",
            OverallExtractionConfidence: 0.95f,
            RawFields: new System.Collections.Generic.Dictionary<string, string>());

        var result = await _svc.ComputeRiskAsync(candidate);

        Assert.Contains("MissingTitle", result.Factors.Keys);
        Assert.True(result.OverallScore >= 0f);
    }

    [Fact]
    public async Task LowOverallConfidence_AddsLowConfidencePenalty()
    {
        var candidate = new CandidateEvent(
            CandidateId: Guid.NewGuid(),
            RequestId: Guid.NewGuid(),
            Title: "Neighborhood Meeting",
            InferredStartUtc: DateTimeOffset.UtcNow,
            RawLocationText: "Community Center",
            OverallExtractionConfidence: 0.50f,
            RawFields: new System.Collections.Generic.Dictionary<string, string>());

        var result = await _svc.ComputeRiskAsync(candidate);

        Assert.Contains("LowConfidence", result.Factors.Keys);
        Assert.True(result.Factors["LowConfidence"] > 0f);
    }

    [Fact]
    public async Task SuspiciousKeyword_IncreasesScore()
    {
        var candidate = new CandidateEvent(
            CandidateId: Guid.NewGuid(),
            RequestId: Guid.NewGuid(),
            Title: "Free giveaway at the plaza",
            InferredStartUtc: DateTimeOffset.UtcNow,
            RawLocationText: "Downtown Plaza",
            OverallExtractionConfidence: 0.95f,
            RawFields: new System.Collections.Generic.Dictionary<string, string>());

        var result = await _svc.ComputeRiskAsync(candidate);

        Assert.Contains("Keyword:free", result.Factors.Keys);
        Assert.True(result.OverallScore > 0f);
    }
}
