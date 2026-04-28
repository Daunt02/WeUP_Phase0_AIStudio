// ------------------------------------------------------------
// File: WeUP.Application/Dedupe/LevenshteinStringMatcher.cs
// M2-P07: Fuzzy Matching Across Title, Time, and Venue v1.0
// ------------------------------------------------------------
using System.Text.RegularExpressions;
using WeUP.Contracts.Dedupe;

namespace WeUP.Application.Dedupe;

/// <summary>
/// Deterministic Levenshtein-based string similarity.
/// Normalises whitespace, case and a small set of common abbreviations.
/// </summary>
public sealed class LevenshteinStringMatcher : IFuzzyStringMatcher
{
    private static readonly (string Abbr, string Full)[] AbbreviationMap =
    [
        ("st", "street"),
        ("ave", "avenue"),
        ("blvd", "boulevard"),
        ("rd", "road"),
    ];

    public Task<float> ComputeSimilarityAsync(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return Task.FromResult(0f);

        var normA = Normalise(a);
        var normB = Normalise(b);

        if (normA.Equals(normB, StringComparison.Ordinal))
            return Task.FromResult(1f);

        int distance = LevenshteinDistance(normA, normB);
        int max = Math.Max(normA.Length, normB.Length);
        float similarity = max == 0 ? 1f : 1f - ((float)distance / max);
        return Task.FromResult(Math.Max(0f, similarity));
    }

    private static string Normalise(string input)
    {
        var lowered = input.Trim().ToLowerInvariant();
        var collapsed = Regex.Replace(lowered, @"\s+", " ");

        // Replace only whole-word occurrences to avoid expanding abbreviations
        // that appear as substrings inside other words (e.g. "st" inside "street").
        foreach (var (abbr, full) in AbbreviationMap)
        {
            collapsed = Regex.Replace(collapsed, $@"\b{Regex.Escape(abbr)}\b", full);
        }

        return collapsed;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var costs = new int[b.Length + 1];

        for (int j = 0; j <= b.Length; j++) costs[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            costs[0] = i;
            int corner = i - 1;

            for (int j = 1; j <= b.Length; j++)
            {
                int upper = costs[j];
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                costs[j] = Math.Min(Math.Min(costs[j - 1] + 1, costs[j] + 1), corner + cost);
                corner = upper;
            }
        }

        return costs[b.Length];
    }
}
