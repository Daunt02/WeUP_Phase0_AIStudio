// ------------------------------------------------------------
// File: WeUP.Contracts/Dedupe/GeoMatchContracts.cs
// M2-P08: Geo-Spatial Proximity Matching v1.0
// ------------------------------------------------------------
using System.Threading.Tasks;

namespace WeUP.Contracts.Dedupe;

/// <summary>
/// Simple value object that stores latitude/longitude in decimal degrees.
/// </summary>
public readonly record struct GeoPoint(double Latitude, double Longitude);

/// <summary>
/// Result returned by the geo matcher.
/// </summary>
public sealed record GeoMatchResult(
    float Similarity,
    double DistanceMeters);

/// <summary>
/// Service that computes a similarity score between two geographic points.
/// </summary>
public interface IGeoMatcher
{
    /// <summary>
    /// Compute similarity between two points. Returns 0 if either point is null.
    /// </summary>
    Task<GeoMatchResult> ComputeSimilarityAsync(GeoPoint? a, GeoPoint? b);
}