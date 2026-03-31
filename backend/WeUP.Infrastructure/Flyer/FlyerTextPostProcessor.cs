using System.Text.RegularExpressions;
using WeUP.Domain.Flyer;

namespace WeUP.Infrastructure.Flyer;

/// <summary>
/// Cleans raw OCR text: strips excess whitespace, OCR artifacts,
/// normalizes Unicode, and removes common noise patterns.
/// </summary>
public sealed partial class FlyerTextPostProcessor : IFlyerTextPostProcessor
{
    public string Clean(string rawOcrText)
    {
        if (string.IsNullOrWhiteSpace(rawOcrText)) return string.Empty;

        var text = rawOcrText;

        // Normalize Unicode — replace common OCR misreads
        text = text.Replace("\u00b0", "°")
                   .Replace("\u2019", "'")
                   .Replace("\u201c", "\"")
                   .Replace("\u201d", "\"")
                   .Replace("\u2014", "-")
                   .Replace("\u00a0", " ");

        // Remove repeated punctuation artifacts (e.g., "......", "----")
        text = RepeatedPunctuation().Replace(text, m => new string(m.Value[0], 1));

        // Normalize line endings
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        // Collapse multiple blank lines
        text = MultipleBlankLines().Replace(text, "\n\n");

        // Trim each line
        var lines = text.Split('\n').Select(l => l.Trim());
        text = string.Join('\n', lines);

        // Remove very short noise-only lines (single character, just punctuation)
        lines = text.Split('\n').Where(l => l.Length > 1 || char.IsLetterOrDigit(l.FirstOrDefault()));
        text = string.Join('\n', lines);

        return text.Trim();
    }

    [GeneratedRegex(@"([.!?,\-_*|#@])\1{2,}")]
    private static partial Regex RepeatedPunctuation();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleBlankLines();
}
