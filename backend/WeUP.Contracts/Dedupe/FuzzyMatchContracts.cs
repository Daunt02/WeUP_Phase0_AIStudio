// ------------------------------------------------------------
// File: WeUP.Contracts/Dedupe/FuzzyMatchContracts.cs
// M2-P07: Fuzzy Matching Across Title, Time, and Venue v1.0
// ------------------------------------------------------------
namespace WeUP.Contracts.Dedupe;

/// <summary>
/// Generic fuzzy matcher for two strings.
/// </summary>
public interface IFuzzyStringMatcher
{
    /// <summary>
    /// Returns a similarity value between 0.0 (completely different) and 1.0 (identical).
    /// </summary>
    Task<float> ComputeSimilarityAsync(string? a, string? b);
}

/// <summary>
/// Matcher for event start times.
/// </summary>
public interface ITemporalMatcher
{
    /// <summary>
    /// Returns a similarity value based on the absolute difference between the two timestamps.
    /// </summary>
    Task<float> ComputeSimilarityAsync(DateTimeOffset? a, DateTimeOffset? b);
}
