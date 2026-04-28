// ------------------------------------------------------------
// File: WeUP.Application/Ingestion/RegexNormalizationEngine.cs
// ------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Ocr;
using WeUP.Domain.Ingestion;

namespace WeUP.Application.Ingestion;

/// <summary>
/// Simple deterministic implementation based on regular expressions.
/// Designed for the MVP – can be replaced later with a more sophisticated NLP pipeline.
/// </summary>
public sealed class RegexNormalizationEngine : INormalizationEngine
{
    // Very permissive date patterns for demo purposes.
    private static readonly Regex[] DatePatterns = new[]
    {
        new Regex(@"\b\d{1,2}/\d{1,2}/\d{4}\b", RegexOptions.Compiled),          // 01/31/2025
        new Regex(@"\b\w{3,9}\s+\d{1,2},\s*\d{4}\b", RegexOptions.Compiled),   // Jan 31, 2025
        new Regex(@"\b\d{1,2}\s+\w{3,9}\s+\d{4}\b", RegexOptions.Compiled)    // 31 Jan 2025
    };

    // Simple venue detection – looks for common street suffixes.
    private static readonly Regex LocationPattern = new(@"(?i)\b\d+\s+[A-Za-z]+\s+(St|Ave|Blvd|Rd|Road|Ln|Lane|Pl|Place)\b",
                                                    RegexOptions.Compiled);

    public Task<CandidateEvent> NormalizeAsync(OcrResult ocrResult)
    {
        // 1️⃣ Gather raw fields from OCR blocks.
        var rawFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var block in ocrResult.Blocks)
        {
            // Very naive heuristics – treat each block as a candidate field.
            var text = block.Text.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            // Identify possible date, location or title.
            if (IsDate(text) && !rawFields.ContainsKey("Date"))
                rawFields["Date"] = text;
            else if (IsLocation(text) && !rawFields.ContainsKey("Location"))
                rawFields["Location"] = text;
            else if (!rawFields.ContainsKey("Title"))
                rawFields["Title"] = text;
        }

        // 2️⃣ Extract Title (first title-like block)
        rawFields.TryGetValue("Title", out var title);

        // 3️⃣ Parse Date → InferredStartUtc
        DateTimeOffset? start = null;
        if (rawFields.TryGetValue("Date", out var dateStr))
        {
            start = ParseDate(dateStr);
        }

        // 4️⃣ Location text (raw)
        rawFields.TryGetValue("Location", out var locationText);

        // 5️⃣ Confidence – average confidence of blocks that contributed to any field
        var usedBlocks = ocrResult.Blocks
                                  .Where(b => rawFields.Values.Any(v => b.Text.Contains(v, StringComparison.OrdinalIgnoreCase)))
                                  .Select(b => b.Confidence)
                                  .ToArray();

        var confidence = usedBlocks.Length > 0 ? usedBlocks.Average() : 0f;

        var candidate = new CandidateEvent(
            CandidateId: Guid.NewGuid(),
            RequestId: Guid.TryParse(ocrResult.ExtractionId, out var rid) ? rid : Guid.NewGuid(),
            Title: title,
            InferredStartUtc: start,
            RawLocationText: locationText,
            OverallExtractionConfidence: (float)confidence,
            RawFields: rawFields);

        return Task.FromResult(candidate);
    }

    private static bool IsDate(string text) => DatePatterns.Any(p => p.IsMatch(text));

    private static bool IsLocation(string text) => LocationPattern.IsMatch(text);

    private static DateTimeOffset? ParseDate(string text)
    {
        foreach (var pattern in DatePatterns)
        {
            var match = pattern.Match(text);
            if (!match.Success) continue;

            var candidate = match.Value;
            // Try several standard formats.
            var formats = new[]
            {
                "M/d/yyyy", "MM/dd/yyyy", "MMM d, yyyy", "d MMM yyyy", "yyyy-MM-dd"
            };
            if (DateTimeOffset.TryParseExact(candidate,
                                            formats,
                                            CultureInfo.InvariantCulture,
                                            DateTimeStyles.AssumeUniversal,
                                            out var parsed))
            {
                return parsed;
            }
        }
        return null;
    }
}
