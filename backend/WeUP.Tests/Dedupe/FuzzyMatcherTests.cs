// ------------------------------------------------------------
// File: WeUP.Tests/Dedupe/FuzzyMatcherTests.cs
// M2-P07: Unit tests for Application-layer fuzzy matchers
// ------------------------------------------------------------
using WeUP.Application.Dedupe;
using Xunit;

namespace WeUP.Tests.Dedupe;

public sealed class LevenshteinStringMatcherTests
{
    private readonly LevenshteinStringMatcher _matcher = new();

    [Fact]
    public async Task ComputeSimilarityAsync_returns_one_for_identical_strings()
    {
        var result = await _matcher.ComputeSimilarityAsync("Salsa Night", "Salsa Night");

        Assert.Equal(1f, result);
    }

    [Fact]
    public async Task ComputeSimilarityAsync_returns_one_for_case_insensitive_identical_strings()
    {
        var result = await _matcher.ComputeSimilarityAsync("SALSA NIGHT", "salsa night");

        Assert.Equal(1f, result);
    }

    [Fact]
    public async Task ComputeSimilarityAsync_expands_abbreviation_St_to_Street()
    {
        // After normalisation "St" expands to "Street" on both sides so they become equivalent.
        var result = await _matcher.ComputeSimilarityAsync("123 Main St", "123 Main Street");

        Assert.True(result > 0.9f, $"Expected > 0.9 but got {result}");
    }

    [Fact]
    public async Task ComputeSimilarityAsync_returns_zero_for_null_input()
    {
        var result = await _matcher.ComputeSimilarityAsync(null, "warehouse session");

        Assert.Equal(0f, result);
    }

    [Fact]
    public async Task ComputeSimilarityAsync_returns_zero_for_whitespace_input()
    {
        var result = await _matcher.ComputeSimilarityAsync("  ", "warehouse session");

        Assert.Equal(0f, result);
    }

    [Fact]
    public async Task ComputeSimilarityAsync_is_deterministic_for_same_inputs()
    {
        const string a = "Summer Rooftop Party";
        const string b = "summer rooftop party";

        var first  = await _matcher.ComputeSimilarityAsync(a, b);
        var second = await _matcher.ComputeSimilarityAsync(a, b);

        Assert.Equal(first, second);
    }
}

public sealed class TemporalMatcherServiceTests
{
    private readonly TemporalMatcherService _matcher = new();

    [Fact]
    public async Task ComputeSimilarityAsync_returns_one_for_three_minute_difference()
    {
        var a = new DateTimeOffset(2026, 4, 18, 20, 0, 0, TimeSpan.Zero);
        var b = a.AddMinutes(3);

        var result = await _matcher.ComputeSimilarityAsync(a, b);

        Assert.Equal(1.0f, result);
    }

    [Fact]
    public async Task ComputeSimilarityAsync_returns_half_for_45_minute_difference()
    {
        var a = new DateTimeOffset(2026, 4, 18, 20, 0, 0, TimeSpan.Zero);
        var b = a.AddMinutes(45);

        var result = await _matcher.ComputeSimilarityAsync(a, b);

        Assert.Equal(0.5f, result);
    }

    [Fact]
    public async Task ComputeSimilarityAsync_returns_zero_for_more_than_two_hour_difference()
    {
        var a = new DateTimeOffset(2026, 4, 18, 20, 0, 0, TimeSpan.Zero);
        var b = a.AddMinutes(121);

        var result = await _matcher.ComputeSimilarityAsync(a, b);

        Assert.Equal(0f, result);
    }

    [Fact]
    public async Task ComputeSimilarityAsync_returns_zero_when_either_input_is_null()
    {
        var result = await _matcher.ComputeSimilarityAsync(null, DateTimeOffset.UtcNow);

        Assert.Equal(0f, result);
    }

    [Fact]
    public async Task ComputeSimilarityAsync_is_deterministic_for_same_inputs()
    {
        var a = new DateTimeOffset(2026, 4, 18, 20, 0, 0, TimeSpan.Zero);
        var b = a.AddMinutes(10);

        var first  = await _matcher.ComputeSimilarityAsync(a, b);
        var second = await _matcher.ComputeSimilarityAsync(a, b);

        Assert.Equal(first, second);
    }
}
