using WeUP.Domain.Dedupe.FuzzyMatching;
using Xunit;

namespace WeUP.Tests.Dedupe;

public sealed class TemporalMatcherTests
{
    private readonly TemporalMatcher _matcher = new();

    [Fact]
    public void Match_returns_zero_score_and_confidence_when_both_starts_are_unknown()
    {
        var result = _matcher.Match(new TemporalInput(null, null), new TemporalInput(null, null));

        Assert.Equal(FuzzyMatchDimension.Temporal, result.Dimension);
        Assert.Equal(0.0, result.Score);
        Assert.Equal(0.0, result.Confidence);
        Assert.True(result.IsNullPenalized);
        Assert.Contains("both_start_utc_null", result.Explanation);
    }

    [Fact]
    public void Match_returns_zero_score_and_low_confidence_when_only_one_start_is_known()
    {
        var rightStart = new DateTimeOffset(2026, 4, 18, 2, 0, 0, TimeSpan.Zero);
        var result = _matcher.Match(new TemporalInput(null, null), new TemporalInput(rightStart, null));

        Assert.Equal(0.0, result.Score);
        Assert.Equal(TemporalMatcher.ConfidenceFloor, result.Confidence);
        Assert.True(result.IsNullPenalized);
        Assert.Contains("left_start_utc_null", result.Explanation);
    }

    [Fact]
    public void Match_returns_exact_score_for_identical_start_times()
    {
        var start = new DateTimeOffset(2026, 4, 18, 2, 0, 0, TimeSpan.Zero);

        var result = _matcher.Match(
            new TemporalInput(start, start.AddHours(2), "America/Chicago"),
            new TemporalInput(start, start.AddHours(2), "America/Chicago"));

        Assert.Equal(1.0, result.Score);
        Assert.Equal(1.0, result.Confidence);
        Assert.False(result.IsNullPenalized);
        Assert.Contains("method=exact_start_match", result.Explanation);
    }

    [Fact]
    public void Match_scores_positive_range_overlap_with_floor()
    {
        var leftStart = new DateTimeOffset(2026, 4, 18, 2, 0, 0, TimeSpan.Zero);
        var rightStart = leftStart.AddMinutes(90);

        var result = _matcher.Match(
            new TemporalInput(leftStart, leftStart.AddHours(4)),
            new TemporalInput(rightStart, rightStart.AddHours(2)));

        Assert.Equal(0.75, result.Score);
        Assert.Equal(1.0, result.Confidence);
        Assert.Contains("method=range_overlap", result.Explanation);
    }

    [Theory]
    [InlineData(30, TemporalMatcher.Score30Min)]
    [InlineData(31, TemporalMatcher.Score120Min)]
    [InlineData(180, TemporalMatcher.Score360Min)]
    [InlineData(720, TemporalMatcher.Score720Min)]
    [InlineData(721, 0.0)]
    public void Match_uses_start_proximity_step_function(double deltaMinutes, double expectedScore)
    {
        var start = new DateTimeOffset(2026, 4, 18, 2, 0, 0, TimeSpan.Zero);
        var shifted = start.AddMinutes(deltaMinutes);

        var result = _matcher.Match(new TemporalInput(start, null), new TemporalInput(shifted, null));

        Assert.Equal(expectedScore, result.Score);
        Assert.Equal(1.0, result.Confidence);
        Assert.Contains("method=start_proximity_step", result.Explanation);
    }

    [Fact]
    public void Match_applies_timezone_conflict_penalty_without_changing_overlap_score()
    {
        var start = new DateTimeOffset(2026, 4, 18, 2, 0, 0, TimeSpan.Zero);

        var result = _matcher.Match(
            new TemporalInput(start, start.AddHours(3), "America/Chicago"),
            new TemporalInput(start.AddMinutes(30), start.AddHours(2), "America/New_York"));

        Assert.Equal(0.75, result.Score);
        Assert.Equal(0.85, result.Confidence);
        Assert.False(result.IsNullPenalized);
        Assert.Contains("tz_conflict=true", result.Explanation);
    }

    [Theory]
    [InlineData(0, TemporalMatcher.Score30Min)]
    [InlineData(120, TemporalMatcher.Score120Min)]
    [InlineData(360, TemporalMatcher.Score360Min)]
    [InlineData(1000, 0.0)]
    public void ComputeStartProximityScore_returns_expected_band(double deltaMinutes, double expected)
    {
        Assert.Equal(expected, TemporalMatcher.ComputeStartProximityScore(deltaMinutes));
    }

    [Fact]
    public void ComputeRangeOverlapScore_reports_overlap_minutes_and_floor_score()
    {
        var start = new DateTimeOffset(2026, 4, 18, 2, 0, 0, TimeSpan.Zero);
        var result = TemporalMatcher.ComputeRangeOverlapScore(
            start,
            start.AddHours(5),
            start.AddHours(4),
            start.AddHours(6));

        Assert.True(result.HasOverlap);
        Assert.Equal(60.0, result.OverlapMinutes);
        Assert.Equal(0.75, result.Score);
    }

    [Fact]
    public void HasTimezoneConflict_requires_both_values_and_compares_case_insensitively()
    {
        Assert.False(TemporalMatcher.HasTimezoneConflict(null, "America/Chicago"));
        Assert.False(TemporalMatcher.HasTimezoneConflict("America/Chicago", "america/chicago"));
        Assert.True(TemporalMatcher.HasTimezoneConflict("America/Chicago", "America/New_York"));
    }
}