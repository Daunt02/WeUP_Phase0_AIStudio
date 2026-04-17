namespace WeUP.Domain.Dedupe.FuzzyMatching;

/// <summary>
/// Canonical geo matching outcome bucket used by deduplication orchestration.
/// </summary>
public enum GeoMatchOutcome
{
    /// <summary>
    /// Coordinates are effectively colocated and venue/address coherence confirms
    /// this is likely the same place.
    /// </summary>
    SameVenueSameCoordinates,

    /// <summary>
    /// Nearby coordinates with enough venue/address coherence to be a likely
    /// duplicate signal, but not deterministic identity.
    /// </summary>
    NearbyLikelyDuplicate,

    /// <summary>
    /// Nearby coordinates but venue/address disagreement indicates likely
    /// different events at nearby places.
    /// </summary>
    NearbyDistinctVenue,

    /// <summary>
    /// Missing/invalid coordinates or distance too large to provide meaningful
    /// duplicate evidence.
    /// </summary>
    NoMeaningfulGeoMatch,
}

/// <summary>
/// Input projection for one side of a geo comparison.
/// </summary>
public sealed record GeoMatchInput(
    double? Latitude,
    double? Longitude,
    string? VenueName,
    string? Address,
    double? GeocodeConfidence = null);

/// <summary>
/// Result of one deterministic geo matching pass.
///
/// Score is bounded [0,1] and should be consumed as one signal in a weighted
/// deduplication strategy. Geographic closeness alone must not force duplicate
/// classification.
/// </summary>
public sealed record GeoMatchResult(
    GeoMatchOutcome Outcome,
    double Score,
    double Confidence,
    double? DistanceMeters,
    bool IsCoordinateMissing,
    bool VenueAgreement,
    bool AddressAgreement,
    bool VenueConflict,
    bool AddressConflict,
    string Explanation);

/// <summary>
/// Threshold and band-score configuration.
///
/// Distance breakpoints are in meters:
/// - ExactProximityMeters: colocated points
/// - NearProximityMeters: same area block-level proximity
/// - WeakProximityMeters: nearby district-level proximity
/// - NoMatchMeters: beyond this, geo contributes no positive evidence
/// </summary>
public sealed record GeoMatcherThresholds(
    double ExactProximityMeters = 25.0,
    double NearProximityMeters = 250.0,
    double WeakProximityMeters = 1_000.0,
    double NoMatchMeters = 5_000.0,
    double ExactBandScore = 0.92,
    double NearBandScore = 0.72,
    double WeakBandScore = 0.38,
    double NoMatchBandScore = 0.10)
{
    public static GeoMatcherThresholds Default { get; } = new();
}