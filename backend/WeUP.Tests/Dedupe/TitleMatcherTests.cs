using WeUP.Domain.Dedupe.FuzzyMatching;
using Xunit;

namespace WeUP.Tests.Dedupe;

public sealed class TitleMatcherTests
{
    private readonly TitleMatcher _matcher = new();

    [Fact]
    public void Match_returns_exact_match_for_equivalent_titles_after_normalization()
    {
        var result = _matcher.Match("  Salsa Night feat. DJ Sol & Friends  ", "salsa night featuring dj sol and friends");

        Assert.Equal(FuzzyMatchDimension.Title, result.Dimension);
        Assert.Equal(1.0, result.Score);
        Assert.Equal(1.0, result.Confidence);
        Assert.False(result.IsNullPenalized);
        Assert.Contains("method=exact_match_after_normalization", result.Explanation);
    }

    [Fact]
    public void Match_penalizes_single_missing_title_explicitly()
    {
        var result = _matcher.Match(null, "warehouse session");

        Assert.Equal(0.0, result.Score);
        Assert.Equal(0.30, result.Confidence);
        Assert.True(result.IsNullPenalized);
        Assert.Contains("left_null_or_empty", result.Explanation);
    }

    [Fact]
    public void Match_penalizes_both_missing_titles_with_zero_confidence()
    {
        var result = _matcher.Match(" ", null);

        Assert.Equal(0.0, result.Score);
        Assert.Equal(0.0, result.Confidence);
        Assert.True(result.IsNullPenalized);
        Assert.Contains("both_null_or_empty", result.Explanation);
    }

    [Fact]
    public void Match_returns_partial_similarity_for_related_but_non_identical_titles()
    {
        var result = _matcher.Match("Summer rooftop dance party", "summer rooftop party");

        Assert.InRange(result.Score, 0.75, 0.95);
        Assert.Equal(1.0, result.Confidence);
        Assert.False(result.IsNullPenalized);
        Assert.Contains("levenshtein=", result.Explanation);
        Assert.Contains("jaccard=", result.Explanation);
    }

    [Fact]
    public void Match_is_deterministic_for_equal_inputs()
    {
        var first = _matcher.Match("Afrobeats vs. Amapiano", "afrobeats versus amapiano");
        var second = _matcher.Match("Afrobeats vs. Amapiano", "afrobeats versus amapiano");

        Assert.Equal(first, second);
    }
}