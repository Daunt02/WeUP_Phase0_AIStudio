using WeUP.Domain.Dedupe.FuzzyMatching;
using Xunit;

namespace WeUP.Tests.Dedupe;

public sealed class VenueMatcherTests
{
    private readonly VenueMatcher _matcher = new();

    [Fact]
    public void Match_returns_exact_match_for_equivalent_venue_strings_after_normalization()
    {
        var result = _matcher.Match("Warehouse 22, 101 N Main St.", "warehouse 22 101 north main street");

        Assert.Equal(FuzzyMatchDimension.Venue, result.Dimension);
        Assert.Equal(1.0, result.Score);
        Assert.Equal(1.0, result.Confidence);
        Assert.False(result.IsNullPenalized);
        Assert.Contains("method=exact_match_after_normalization", result.Explanation);
    }

    [Fact]
    public void Match_penalizes_missing_venue_explicitly()
    {
        var result = _matcher.Match("The Grand Hall", null);

        Assert.Equal(0.0, result.Score);
        Assert.Equal(0.30, result.Confidence);
        Assert.True(result.IsNullPenalized);
        Assert.Contains("right_null_or_empty", result.Explanation);
    }

    [Fact]
    public void Match_scores_related_venue_names_as_partial_similarity()
    {
        var result = _matcher.Match("The Skyline Club", "Skyline Club");

        Assert.InRange(result.Score, 0.65, 0.95);
        Assert.Equal(1.0, result.Confidence);
        Assert.False(result.IsNullPenalized);
    }

    [Fact]
    public void Match_does_not_apply_title_specific_abbreviation_rules()
    {
        var result = _matcher.Match("Club ft. Worth", "club featuring worth");

        Assert.True(result.Score < 1.0);
        Assert.DoesNotContain("exact_match_after_normalization", result.Explanation);
    }
}