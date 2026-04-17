using WeUP.Domain.Dedupe.FuzzyMatching;
using Xunit;

namespace WeUP.Tests.Dedupe;

public sealed class FuzzyNormalizationHelpersTests
{
    [Fact]
    public void NormalizeTitle_expands_common_abbreviations_and_collapses_whitespace()
    {
        var normalized = FuzzyNormalizationHelpers.NormalizeTitle("  DJ Kool feat.  Lil Mo  & Friends @ Main Room  ");

        Assert.Equal("dj kool featuring lil mo and friends at main room", normalized);
    }

    [Fact]
    public void NormalizeVenue_expands_address_abbreviations_without_title_specific_tokens()
    {
        var normalized = FuzzyNormalizationHelpers.NormalizeVenue("Warehouse 22, 101 N Main St Ste. 400");

        Assert.Equal("warehouse 22 101 north main street suite 400", normalized);
    }

    [Fact]
    public void ReplacePunctuationWithSpace_preserves_letters_digits_and_whitespace()
    {
        var replaced = FuzzyNormalizationHelpers.ReplacePunctuationWithSpace("A/B-C, D!");

        Assert.Equal("A B C  D ", replaced);
    }

    [Fact]
    public void CollapseWhitespace_trims_and_reduces_runs_to_single_spaces()
    {
        var collapsed = FuzzyNormalizationHelpers.CollapseWhitespace("  one\t\ttwo   three  ");

        Assert.Equal("one two three", collapsed);
    }

    [Fact]
    public void BlendedSimilarity_returns_zero_when_either_input_is_empty()
    {
        var score = FuzzyNormalizationHelpers.BlendedSimilarity(string.Empty, "value");

        Assert.Equal(0.0, score);
    }

    [Fact]
    public void TokenJaccardSimilarity_uses_set_semantics()
    {
        var score = FuzzyNormalizationHelpers.TokenJaccardSimilarity("skyline skyline club", "skyline club downtown");

        Assert.Equal(0.6666666666666666, score, 12);
    }
}