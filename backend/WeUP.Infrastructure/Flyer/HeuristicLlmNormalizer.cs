using System.Text.RegularExpressions;
using WeUP.Domain.Flyer;

namespace WeUP.Infrastructure.Flyer;

/// <summary>
/// Phase 0 heuristic normalizer — no LLM dependency.
/// Extracts event fields from cleaned flyer text using regex heuristics
/// as defined in the WeUP TRD (pragmatic rule-based parsing for Phase 0).
///
/// Replace with a real LLM normalizer (Claude API call) when the backend
/// is connected to the Anthropic API. See docs/ingestion-llm-seam.md.
/// </summary>
public sealed partial class HeuristicLlmNormalizer : ILlmEventNormalizer
{
    private const string PROMPT_VERSION = "heuristic-v1.0";

    public Task<LlmNormalizationResult> NormalizeAsync(string cleanedText, string sourceRef, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cleanedText))
        {
            return Task.FromResult(new LlmNormalizationResult(
                null, null, null, null, null, null, null, null, null,
                0.0, 0.0, PROMPT_VERSION, false, "Empty OCR text"));
        }

        var title    = ExtractTitle(cleanedText);
        var date     = ExtractDate(cleanedText);
        var time     = ExtractTime(cleanedText);
        var address  = ExtractAddress(cleanedText);
        var venue    = ExtractVenueName(cleanedText);

        // Confidence based on what fields were found
        var foundFields = new[] { title, date, time, address }.Count(f => f is not null);
        var extractionConfidence = foundFields switch
        {
            4 => 0.80,
            3 => 0.65,
            2 => 0.45,
            1 => 0.25,
            _ => 0.10,
        };

        var temporalConfidence = (date is not null && time is not null) ? 0.75
                               : (date is not null) ? 0.50
                               : 0.10;

        var startDate = date is not null && time is not null ? $"{date}T{time}:00" : date;

        return Task.FromResult(new LlmNormalizationResult(
            Title: title,
            VenueName: venue,
            Address: address,
            StartDate: startDate,
            EndDate: null,
            Timezone: null,  // requires market context
            Category: InferCategory(cleanedText),
            Description: null,
            Tags: ExtractTags(cleanedText),
            ExtractionConfidence: extractionConfidence,
            TemporalConfidence: temporalConfidence,
            PromptVersion: PROMPT_VERSION,
            Success: foundFields >= 1,
            ErrorMessage: null));
    }

    private static string? ExtractTitle(string text)
    {
        // First non-empty line that looks like a title (all caps or title case, < 100 chars)
        var lines = text.Split('\n').Where(l => l.Trim().Length > 2);
        foreach (var line in lines.Take(5))
        {
            var t = line.Trim();
            if (t.Length is > 3 and < 100 && (IsAllCaps(t) || IsLikelyTitle(t)))
                return t;
        }
        return lines.FirstOrDefault()?.Trim();
    }

    private static string? ExtractDate(string text)
    {
        // Match common date patterns
        var m = DatePattern().Match(text);
        if (!m.Success) return null;

        // Normalize to YYYY-MM-DD best effort
        var raw = m.Value.Trim();
        // If already contains year, return as-is; otherwise add current year
        return raw.Contains("202") ? raw : raw;
    }

    private static string? ExtractTime(string text)
    {
        var m = TimePattern().Match(text);
        return m.Success ? m.Value.Trim() : null;
    }

    private static string? ExtractAddress(string text)
    {
        // Look for lines containing street indicators
        var streetWords = new[] { "St", "Ave", "Blvd", "Dr", "Rd", "Ln", "Pkwy", "Hwy", "Way" };
        foreach (var line in text.Split('\n'))
        {
            if (streetWords.Any(w => line.Contains(w, StringComparison.OrdinalIgnoreCase))
                && StreetNumber().IsMatch(line))
                return line.Trim();
        }
        return null;
    }

    private static string? ExtractVenueName(string text)
    {
        // Look for "at <Venue>" or "@<Venue>" patterns
        var m = VenuePattern().Match(text);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static string? InferCategory(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("dj") || lower.Contains("nightclub") || lower.Contains("dance")) return "nightlife";
        if (lower.Contains("concert") || lower.Contains("live music") || lower.Contains("band")) return "concert";
        if (lower.Contains("rooftop")) return "rooftop";
        if (lower.Contains("lounge")) return "lounge";
        if (lower.Contains("startup") || lower.Contains("tech")) return "startup";
        return null;
    }

    private static string[]? ExtractTags(string text)
    {
        var lower = text.ToLowerInvariant();
        var tags = new List<string>();
        if (lower.Contains("21+") || lower.Contains("21 and over")) tags.Add("21+");
        if (lower.Contains("free")) tags.Add("free");
        if (lower.Contains("rsvp")) tags.Add("rsvp");
        if (lower.Contains("vip")) tags.Add("vip");
        return tags.Count > 0 ? [.. tags] : null;
    }

    private static bool IsAllCaps(string s) => s == s.ToUpperInvariant() && s.Any(char.IsLetter);
    private static bool IsLikelyTitle(string s) => char.IsUpper(s[0]) && s.Any(char.IsLetter);

    [GeneratedRegex(@"(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]* \d{1,2}(,? \d{4})?|\d{1,2}/\d{1,2}(/\d{2,4})?")]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"\d{1,2}:\d{2}\s?(AM|PM|am|pm)|(\d{1,2}\s?(AM|PM|am|pm))")]
    private static partial Regex TimePattern();

    [GeneratedRegex(@"^\d+\s+\w")]
    private static partial Regex StreetNumber();

    [GeneratedRegex(@"(?:at|@)\s+([A-Z][^,\n]{2,40})", RegexOptions.IgnoreCase)]
    private static partial Regex VenuePattern();
}
