using WeUP.Domain.Dedupe.FuzzyMatching;
using Xunit;

namespace WeUP.Tests.Dedupe;

public sealed class GeoMatcherTests
{
    private readonly GeoMatcher _matcher = new();

    [Fact]
    public void Match_returns_same_venue_same_coordinates_for_exact_colocated_consistent_records()
    {
        var left = new GeoMatchInput(29.76040, -95.36980, "The Skyline Club", "101 Main St", 0.95);
        var right = new GeoMatchInput(29.76041, -95.36979, "Skyline Club", "101 Main Street", 0.93);

        var result = _matcher.Match(left, right);

        Assert.Equal(GeoMatchOutcome.SameVenueSameCoordinates, result.Outcome);
        Assert.True(result.Score >= 0.75);
        Assert.True(result.Confidence >= 0.75);
        Assert.NotNull(result.DistanceMeters);
        Assert.False(result.IsCoordinateMissing);
    }

    [Fact]
    public void Match_returns_nearby_likely_duplicate_for_near_points_with_coherent_venue_or_address()
    {
        var left = new GeoMatchInput(29.76040, -95.36980, "Warehouse Live", "813 Saint Emanuel Street", 0.90);
        var right = new GeoMatchInput(29.76120, -95.36950, "Warehouse Live", "813 St Emanuel St", 0.88);

        var result = _matcher.Match(left, right);

        Assert.Equal(GeoMatchOutcome.NearbyLikelyDuplicate, result.Outcome);
        Assert.True(result.Score >= 0.45);
        Assert.True(result.Confidence >= 0.60);
        Assert.Contains("band=", result.Explanation);
    }

    [Fact]
    public void Match_returns_nearby_distinct_venue_when_proximity_conflicts_with_venue_and_address()
    {
        var left = new GeoMatchInput(29.76040, -95.36980, "Skyline Club", "101 Main St", 0.95);
        var right = new GeoMatchInput(29.76055, -95.36970, "Bayou Jazz Hall", "500 Travis St", 0.95);

        var result = _matcher.Match(left, right);

        Assert.Equal(GeoMatchOutcome.NearbyDistinctVenue, result.Outcome);
        Assert.True(result.VenueConflict || result.AddressConflict);
        Assert.True(result.Score < 0.60);
        Assert.True(result.Confidence < 0.85);
    }

    [Fact]
    public void Match_does_not_treat_missing_coordinates_as_positive_signal()
    {
        var left = new GeoMatchInput(null, null, "Skyline Club", "101 Main St", 0.90);
        var right = new GeoMatchInput(29.76055, -95.36970, "Skyline Club", "101 Main St", 0.90);

        var result = _matcher.Match(left, right);

        Assert.Equal(GeoMatchOutcome.NoMeaningfulGeoMatch, result.Outcome);
        Assert.Equal(0.0, result.Score);
        Assert.True(result.IsCoordinateMissing);
        Assert.Null(result.DistanceMeters);
        Assert.True(result.Confidence <= 0.30);
    }

    [Fact]
    public void Match_returns_no_meaningful_geo_match_when_points_are_far_apart()
    {
        var left = new GeoMatchInput(29.76040, -95.36980, "Skyline Club", "101 Main St", 0.95);
        var right = new GeoMatchInput(30.26720, -97.74310, "Skyline Club", "101 Main St", 0.95);

        var result = _matcher.Match(left, right);

        Assert.Equal(GeoMatchOutcome.NoMeaningfulGeoMatch, result.Outcome);
        Assert.True(result.Score <= 0.10);
        Assert.NotNull(result.DistanceMeters);
        Assert.True(result.DistanceMeters > 5_000.0);
    }

    [Fact]
    public void Match_proximity_alone_does_not_force_duplicate_bucket_when_conflict_is_strong()
    {
        var left = new GeoMatchInput(29.76040, -95.36980, "Venue A", "100 Alpha St", 0.95);
        var right = new GeoMatchInput(29.76042, -95.36981, "Venue B", "900 Omega Blvd", 0.95);

        var result = _matcher.Match(left, right);

        Assert.Equal(GeoMatchOutcome.NearbyDistinctVenue, result.Outcome);
        Assert.True(result.Score < 0.60);
        Assert.True(result.Confidence < 0.85);
    }
}