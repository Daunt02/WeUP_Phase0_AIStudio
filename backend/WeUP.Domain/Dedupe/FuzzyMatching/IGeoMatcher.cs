namespace WeUP.Domain.Dedupe.FuzzyMatching;

/// <summary>
/// Contract for geospatial duplicate-signal scoring.
///
/// Implementations must be deterministic and side-effect free:
/// - No external I/O
/// - No mutable state
/// - Missing coordinates never become positive evidence
/// - Venue/address conflict must reduce confidence
/// </summary>
public interface IGeoMatcher
{
    GeoMatchResult Match(GeoMatchInput left, GeoMatchInput right);
}