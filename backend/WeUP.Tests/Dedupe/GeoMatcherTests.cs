using Microsoft.Extensions.Logging.Abstractions;
using WeUP.Application.Dedupe;
using WeUP.Contracts.Dedupe;
using Xunit;

namespace WeUP.Tests.Dedupe;

public sealed class GeoMatcherTests
{
    private readonly GeoMatcher _matcher = new(NullLogger<GeoMatcher>.Instance);

    [Fact]
    public async Task ComputeSimilarityAsync_returns_identical_points_as_exact_match()
    {
        var point = new GeoPoint(29.76040, -95.36980);

        var result = await _matcher.ComputeSimilarityAsync(point, point);

        Assert.Equal(1.0f, result.Similarity);
        Assert.Equal(0d, result.DistanceMeters, 6);
    }

    [Fact]
    public async Task ComputeSimilarityAsync_returns_point_pairs_about_50_meters_apart_as_0_9()
    {
        var left = new GeoPoint(29.76040, -95.36980);
        var right = new GeoPoint(29.760849, -95.36980);

        var result = await _matcher.ComputeSimilarityAsync(left, right);

        Assert.Equal(0.9f, result.Similarity);
        Assert.InRange(result.DistanceMeters, 45d, 60d);
    }

    [Fact]
    public async Task ComputeSimilarityAsync_returns_point_pairs_about_1500_meters_apart_as_0_2()
    {
        var left = new GeoPoint(29.76040, -95.36980);
        var right = new GeoPoint(29.773874, -95.36980);

        var result = await _matcher.ComputeSimilarityAsync(left, right);

        Assert.Equal(0.2f, result.Similarity);
        Assert.InRange(result.DistanceMeters, 1450d, 1550d);
    }

    [Fact]
    public async Task ComputeSimilarityAsync_returns_zero_for_null_input()
    {
        var right = new GeoPoint(29.76040, -95.36980);

        var result = await _matcher.ComputeSimilarityAsync(null, right);

        Assert.Equal(0f, result.Similarity);
        Assert.Equal(double.MaxValue, result.DistanceMeters);
    }
}