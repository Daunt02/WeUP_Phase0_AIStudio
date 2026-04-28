// ------------------------------------------------------------
// File: WeUP.Application/Dedupe/TemporalMatcherService.cs
// M2-P07: Fuzzy Matching Across Title, Time, and Venue v1.0
// ------------------------------------------------------------
using WeUP.Contracts.Dedupe;

namespace WeUP.Application.Dedupe;

/// <summary>
/// Deterministic temporal similarity bucketed into minute-difference ranges.
/// </summary>
public sealed class TemporalMatcherService : ITemporalMatcher
{
    public Task<float> ComputeSimilarityAsync(DateTimeOffset? a, DateTimeOffset? b)
    {
        if (!a.HasValue || !b.HasValue)
            return Task.FromResult(0f);

        double diff = Math.Abs((a.Value - b.Value).TotalMinutes);

        // Bucketed similarity – fully deterministic.
        // ≤ 5 min → 1.0, ≤ 15 min → 0.9, ≤ 30 min → 0.7,
        // ≤ 60 min → 0.5, ≤ 120 min → 0.3, > 120 min → 0.
        float similarity = diff switch
        {
            <= 5   => 1.0f,
            <= 15  => 0.9f,
            <= 30  => 0.7f,
            <= 60  => 0.5f,
            <= 120 => 0.3f,
            _      => 0f,
        };

        return Task.FromResult(similarity);
    }
}
