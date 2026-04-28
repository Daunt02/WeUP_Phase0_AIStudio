using System.Globalization;
using System.Text.RegularExpressions;
using WeUP.Contracts.Ocr;
using WeUP.Domain.Flyer;

namespace WeUP.Infrastructure.Flyer;

public sealed class NormalizationEngine(IOcrNormalizationTelemetry? telemetry = null) : INormalizationEngine
{
    private static readonly Regex HashtagRegex = new("#(?<tag>[a-z0-9_]{2,30})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IsoDateTimeRegex = new(
        "(?<start>\\d{4}-\\d{2}-\\d{2}[t\\s]\\d{2}:\\d{2}(?::\\d{2})?(?:z|[+-]\\d{2}:?\\d{2}))\\s*(?:to|\\-|until)?\\s*(?<end>\\d{4}-\\d{2}-\\d{2}[t\\s]\\d{2}:\\d{2}(?::\\d{2})?(?:z|[+-]\\d{2}:?\\d{2}))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MonthDateTimeRegex = new(
        "(?<month>jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:t(?:ember)?)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\\s+(?<day>\\d{1,2})(?:,\\s*|\\s+)(?<year>\\d{4}).{0,24}?(?<startTime>\\d{1,2}(?::\\d{2})?\\s*(?:am|pm))(?:\\s*(?:to|\\-|until)\\s*(?<endTime>\\d{1,2}(?::\\d{2})?\\s*(?:am|pm)))?.{0,12}?(?<tz>utc|gmt|est|edt|cst|cdt|mst|mdt|pst|pdt|[+-]\\d{2}:?\\d{2})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AddressRegex = new(
        "\\b\\d{1,6}\\s+[a-z0-9.'#\\-\\s]{2,}\\s(?:street|st|avenue|ave|road|rd|boulevard|blvd|drive|dr|lane|ln|way|court|ct|place|pl|terrace|ter|highway|hwy)\\b(?:,\\s*[a-z.'\\-\\s]+)?(?:,\\s*[a-z]{2})?(?:\\s+\\d{5}(?:-\\d{4})?)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex VenueKeywordRegex = new(
        "\\b(?:venue|location|hosted at|located at|at)\\s*[:\\-]?\\s*(?<venue>[a-z0-9&'().\\-\\s]{3,80})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DateOrTimeSignalRegex = new(
        "\\b(?:[01]?\\d:[0-5]\\d\\s*(?:am|pm)|(?:jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)\\b|\\d{4}|utc|gmt|[+-]\\d{2}:?\\d{2})\\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<string, string> TagKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dj"] = "music",
        ["concert"] = "music",
        ["live"] = "live-music",
        ["house"] = "house-music",
        ["festival"] = "festival",
        ["comedy"] = "comedy",
        ["workshop"] = "workshop",
        ["networking"] = "networking",
        ["community"] = "community",
        ["family"] = "family",
        ["food"] = "food",
        ["market"] = "market",
        ["dance"] = "dance",
        ["party"] = "party",
    };

    private readonly IOcrNormalizationTelemetry _telemetry = telemetry ?? new NoopOcrNormalizationTelemetry();

    public EventCandidate Normalize(OcrResult ocr)
    {
        ArgumentNullException.ThrowIfNull(ocr);

        var (title, titleScore) = ExtractTitle(ocr);
        var (startUtc, endUtc, startScore, endScore) = ExtractDateTime(ocr);
        var (venue, venueScore) = ExtractVenue(ocr);
        var (address, addressScore) = ExtractAddress(ocr);
        var (tags, tagsScore) = ExtractTags(ocr);

        var rawFields = BuildRawFields(ocr);
        var scores = new Dictionary<string, FieldHeuristicScore>(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = titleScore,
            ["startUtc"] = startScore,
            ["endUtc"] = endScore,
            ["venue"] = venueScore,
            ["address"] = addressScore,
            ["tags"] = tagsScore,
        };

        var candidate = new EventCandidate(
            Title: title,
            StartUtc: startUtc,
            EndUtc: endUtc,
            Venue: venue,
            Address: address,
            Tags: tags,
            RawFields: rawFields,
            FieldScores: scores);

        _telemetry.TrackNormalization(candidate);
        return candidate;
    }

    public (string? Value, FieldHeuristicScore Score) ExtractTitle(OcrResult ocr)
    {
        var lines = GetOrderedLines(ocr);
        if (lines.Count == 0)
        {
            return (null, new FieldHeuristicScore(0.0, "no OCR text available"));
        }

        var scanWindow = Math.Min(6, lines.Count);
        var bestValue = (string?)null;
        var bestScore = 0.0;

        for (var i = 0; i < scanWindow; i++)
        {
            var line = lines[i];
            var lower = line.Text.ToLowerInvariant();
            if (line.Text.Length < 4
                || LooksLikeAddress(line.Text)
                || LooksLikeDateOrTime(line.Text)
                || line.Text.Contains('#')
                || lower.StartsWith("location:", StringComparison.Ordinal)
                || lower.StartsWith("venue:", StringComparison.Ordinal)
                || lower.StartsWith("address:", StringComparison.Ordinal))
            {
                continue;
            }

            var positionWeight = 1.0 - (i / (double)scanWindow);
            var letterDensity = line.Text.Count(char.IsLetter) / (double)Math.Max(1, line.Text.Length);
            var score = 0.25 + (line.Confidence * 0.35) + (positionWeight * 0.25) + (letterDensity * 0.15);

            if (score > bestScore)
            {
                bestScore = score;
                bestValue = line.Text;
            }
        }

        if (bestScore < 0.45)
        {
            return (null, new FieldHeuristicScore(bestScore, "no reliable title signal"));
        }

        return (bestValue, new FieldHeuristicScore(Math.Clamp(bestScore, 0.0, 1.0), "top-position text with non-date lexical profile"));
    }

    public (DateTimeOffset? StartUtc, DateTimeOffset? EndUtc, FieldHeuristicScore StartScore, FieldHeuristicScore EndScore) ExtractDateTime(OcrResult ocr)
    {
        var text = ocr.RawText ?? string.Empty;

        var isoMatch = IsoDateTimeRegex.Match(text);
        if (isoMatch.Success)
        {
            var startRaw = isoMatch.Groups["start"].Value;
            var endRaw = isoMatch.Groups["end"].Value;
            if (TryParseExplicitDateTime(startRaw, out var startUtc))
            {
                DateTimeOffset? endUtc = null;
                var endScore = new FieldHeuristicScore(0.0, "no explicit end time found");
                if (!string.IsNullOrWhiteSpace(endRaw) && TryParseExplicitDateTime(endRaw, out var parsedEndUtc))
                {
                    endUtc = parsedEndUtc;
                    endScore = new FieldHeuristicScore(0.95, "ISO-8601 end datetime with explicit offset");
                }

                return (
                    startUtc,
                    endUtc,
                    new FieldHeuristicScore(0.98, "ISO-8601 datetime with explicit offset"),
                    endScore);
            }
        }

        var monthMatch = MonthDateTimeRegex.Match(text);
        if (!monthMatch.Success)
        {
            return (
                null,
                null,
                new FieldHeuristicScore(0.0, "no datetime pattern with explicit timezone found"),
                new FieldHeuristicScore(0.0, "no datetime pattern with explicit timezone found"));
        }

        var monthToken = monthMatch.Groups["month"].Value;
        var dayToken = monthMatch.Groups["day"].Value;
        var yearToken = monthMatch.Groups["year"].Value;
        var startTimeToken = monthMatch.Groups["startTime"].Value;
        var endTimeToken = monthMatch.Groups["endTime"].Value;
        var timezoneToken = monthMatch.Groups["tz"].Value;

        if (!TryResolveOffset(timezoneToken, out var offset))
        {
            return (
                null,
                null,
                new FieldHeuristicScore(0.0, "timezone token was not explicit or not recognized"),
                new FieldHeuristicScore(0.0, "timezone token was not explicit or not recognized"));
        }

        if (!TryBuildUtc(monthToken, dayToken, yearToken, startTimeToken, offset, out var startUtcValue))
        {
            return (
                null,
                null,
                new FieldHeuristicScore(0.0, "insufficient date/time parts for start datetime"),
                new FieldHeuristicScore(0.0, "insufficient date/time parts for end datetime"));
        }

        DateTimeOffset? endUtcValue = null;
        var endFieldScore = new FieldHeuristicScore(0.0, "explicit end time was not found");
        if (!string.IsNullOrWhiteSpace(endTimeToken) && TryBuildUtc(monthToken, dayToken, yearToken, endTimeToken, offset, out var monthParsedEndUtc))
        {
            endUtcValue = monthParsedEndUtc;
            endFieldScore = new FieldHeuristicScore(0.87, "date + end time + timezone abbreviation");
        }

        return (
            startUtcValue,
            endUtcValue,
            new FieldHeuristicScore(0.90, "date + start time + timezone abbreviation"),
            endFieldScore);
    }

    public (string? Value, FieldHeuristicScore Score) ExtractVenue(OcrResult ocr)
    {
        var lines = GetOrderedLines(ocr);
        if (lines.Count == 0)
        {
            return (null, new FieldHeuristicScore(0.0, "no OCR text available"));
        }

        var candidates = new List<(string Value, double Score)>();

        foreach (var line in lines)
        {
            var keywordMatch = VenueKeywordRegex.Match(line.Text.Trim());
            if (keywordMatch.Success)
            {
                var extracted = keywordMatch.Groups["venue"].Value.Trim(' ', '-', ':', ',');
                if (!string.IsNullOrWhiteSpace(extracted) && !LooksLikeAddress(extracted))
                {
                    candidates.Add((extracted, 0.55 + (line.Confidence * 0.30)));
                }
            }

            if (!LooksLikeAddress(line.Text) && !LooksLikeDateOrTime(line.Text) && line.Text.Length is >= 4 and <= 70)
            {
                var capitalizedWords = line.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Count(token => token.Length > 1 && char.IsUpper(token[0]));
                if (capitalizedWords >= 2)
                {
                    candidates.Add((line.Text, 0.35 + (line.Confidence * 0.20)));
                }
            }
        }

        if (candidates.Count == 0)
        {
            return (null, new FieldHeuristicScore(0.0, "no venue pattern found"));
        }

        var top = candidates.OrderByDescending(c => c.Score).First();
        var second = candidates
            .Where(c => !string.Equals(c.Value, top.Value, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.Score)
            .FirstOrDefault();

        if (second.Value is not null && Math.Abs(top.Score - second.Score) < 0.05)
        {
            return (null, new FieldHeuristicScore(Math.Clamp(top.Score, 0.0, 1.0), "ambiguous venue candidates"));
        }

        return (top.Value, new FieldHeuristicScore(Math.Clamp(top.Score, 0.0, 1.0), "venue keyword and lexical scoring"));
    }

    public (string? Value, FieldHeuristicScore Score) ExtractAddress(OcrResult ocr)
    {
        var lines = GetOrderedLines(ocr);

        foreach (var line in lines)
        {
            var addressMatch = AddressRegex.Match(line.Text);
            if (addressMatch.Success)
            {
                var value = NormalizeWhitespace(addressMatch.Value);
                var confidence = Math.Clamp(0.65 + (line.Confidence * 0.30), 0.0, 1.0);
                return (value, new FieldHeuristicScore(confidence, "street address regex match"));
            }
        }

        var rawMatch = AddressRegex.Match(ocr.RawText ?? string.Empty);
        if (rawMatch.Success)
        {
            var value = NormalizeWhitespace(rawMatch.Value);
            return (value, new FieldHeuristicScore(0.62, "street address regex match in raw OCR text"));
        }

        return (null, new FieldHeuristicScore(0.0, "no address-like pattern found"));
    }

    public (string[] Values, FieldHeuristicScore Score) ExtractTags(OcrResult ocr)
    {
        var text = (ocr.RawText ?? string.Empty).ToLowerInvariant();
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in HashtagRegex.Matches(text))
        {
            var tag = match.Groups["tag"].Value.Trim();
            if (tag.Length > 1)
            {
                tags.Add(tag);
            }
        }

        foreach (var keyword in TagKeywords)
        {
            if (text.Contains(keyword.Key, StringComparison.OrdinalIgnoreCase))
            {
                tags.Add(keyword.Value);
            }
        }

        var ordered = tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToArray();
        if (ordered.Length == 0)
        {
            return (ordered, new FieldHeuristicScore(0.0, "no hashtag or keyword tag signals found"));
        }

        var score = Math.Clamp(0.30 + (ordered.Length * 0.12), 0.0, 1.0);
        return (ordered, new FieldHeuristicScore(score, "hashtags and keyword dictionary"));
    }

    private static IReadOnlyDictionary<string, string?> BuildRawFields(OcrResult ocr)
    {
        var raw = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["extractionId"] = ocr.ExtractionId,
            ["jobId"] = ocr.JobId,
            ["assetId"] = ocr.AssetId,
            ["provider"] = ocr.Provider,
            ["providerVersion"] = ocr.ProviderVersion,
            ["success"] = ocr.Success ? "true" : "false",
            ["confidence"] = ocr.Confidence.ToString(CultureInfo.InvariantCulture),
            ["rawText"] = ocr.RawText,
            ["failureReason"] = ocr.FailureReason,
            ["attemptCount"] = ocr.AttemptCount.ToString(CultureInfo.InvariantCulture),
            ["startedAtUtc"] = ocr.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            ["completedAtUtc"] = ocr.CompletedAtUtc.ToString("O", CultureInfo.InvariantCulture),
        };

        foreach (var metadata in ocr.Metadata)
        {
            raw[$"metadata:{metadata.Key}"] = metadata.Value;
        }

        for (var i = 0; i < ocr.Blocks.Length; i++)
        {
            var block = ocr.Blocks[i];
            raw[$"blocks:{i}:index"] = block.Index.ToString(CultureInfo.InvariantCulture);
            raw[$"blocks:{i}:text"] = block.Text;
            raw[$"blocks:{i}:confidence"] = block.Confidence.ToString(CultureInfo.InvariantCulture);
            raw[$"blocks:{i}:x"] = block.X.ToString(CultureInfo.InvariantCulture);
            raw[$"blocks:{i}:y"] = block.Y.ToString(CultureInfo.InvariantCulture);
            raw[$"blocks:{i}:width"] = block.Width.ToString(CultureInfo.InvariantCulture);
            raw[$"blocks:{i}:height"] = block.Height.ToString(CultureInfo.InvariantCulture);

            foreach (var metadata in block.Metadata)
            {
                raw[$"blocks:{i}:metadata:{metadata.Key}"] = metadata.Value;
            }
        }

        return raw;
    }

    private static IReadOnlyList<OcrLine> GetOrderedLines(OcrResult ocr)
    {
        if (ocr.Blocks.Length > 0)
        {
            return ocr.Blocks
                .Where(b => !string.IsNullOrWhiteSpace(b.Text))
                .OrderBy(b => b.Y)
                .ThenBy(b => b.X)
                .Select((b, idx) => new OcrLine(NormalizeWhitespace(b.Text), Math.Clamp(b.Confidence, 0.0, 1.0), idx))
                .Where(line => line.Text.Length > 0)
                .ToArray();
        }

        return (ocr.RawText ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((line, idx) => new OcrLine(NormalizeWhitespace(line), 0.5, idx))
            .Where(line => line.Text.Length > 0)
            .ToArray();
    }

    private static bool TryParseExplicitDateTime(string raw, out DateTimeOffset utc)
    {
        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
        {
            utc = parsed.ToUniversalTime();
            return true;
        }

        utc = default;
        return false;
    }

    private static bool TryBuildUtc(string month, string day, string year, string time, TimeSpan offset, out DateTimeOffset utc)
    {
        utc = default;

        if (!TryParseMonth(month, out var monthValue)
            || !int.TryParse(day, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dayValue)
            || !int.TryParse(year, NumberStyles.Integer, CultureInfo.InvariantCulture, out var yearValue)
            || !DateTime.TryParseExact(time.Trim(), ["h:mm tt", "h tt", "hh:mm tt", "hh tt"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var timeValue))
        {
            return false;
        }

        try
        {
            var local = new DateTimeOffset(yearValue, monthValue, dayValue, timeValue.Hour, timeValue.Minute, 0, offset);
            utc = local.ToUniversalTime();
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool TryResolveOffset(string timezoneToken, out TimeSpan offset)
    {
        offset = default;
        var token = timezoneToken.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (token == "UTC" || token == "GMT")
        {
            offset = TimeSpan.Zero;
            return true;
        }

        if (Regex.IsMatch(token, "^[+-]\\d{2}:?\\d{2}$"))
        {
            var normalized = token.Length == 5
                ? token.Insert(3, ":")
                : token;

            return TimeSpan.TryParseExact(normalized, "hh\\:mm", CultureInfo.InvariantCulture, out offset)
                || TimeSpan.TryParse(normalized, CultureInfo.InvariantCulture, out offset);
        }

        switch (token)
        {
            case "EST":
                offset = TimeSpan.FromHours(-5);
                return true;
            case "EDT":
                offset = TimeSpan.FromHours(-4);
                return true;
            case "CST":
                offset = TimeSpan.FromHours(-6);
                return true;
            case "CDT":
                offset = TimeSpan.FromHours(-5);
                return true;
            case "MST":
                offset = TimeSpan.FromHours(-7);
                return true;
            case "MDT":
                offset = TimeSpan.FromHours(-6);
                return true;
            case "PST":
                offset = TimeSpan.FromHours(-8);
                return true;
            case "PDT":
                offset = TimeSpan.FromHours(-7);
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseMonth(string month, out int value)
    {
        value = month.Trim().ToLowerInvariant() switch
        {
            "jan" or "january" => 1,
            "feb" or "february" => 2,
            "mar" or "march" => 3,
            "apr" or "april" => 4,
            "may" => 5,
            "jun" or "june" => 6,
            "jul" or "july" => 7,
            "aug" or "august" => 8,
            "sep" or "sept" or "september" => 9,
            "oct" or "october" => 10,
            "nov" or "november" => 11,
            "dec" or "december" => 12,
            _ => 0,
        };

        return value > 0;
    }

    private static bool LooksLikeDateOrTime(string text)
    {
        return DateOrTimeSignalRegex.IsMatch(text);
    }

    private static bool LooksLikeAddress(string text)
        => AddressRegex.IsMatch(text);

    private static string NormalizeWhitespace(string value)
        => Regex.Replace(value.Trim(), "\\s+", " ");

    private sealed class NoopOcrNormalizationTelemetry : IOcrNormalizationTelemetry
    {
        public void TrackOcrExtraction(string provider, string providerVersion, bool success, double confidence)
        {
        }

        public void TrackNormalization(EventCandidate candidate)
        {
        }
    }

    private sealed record OcrLine(string Text, double Confidence, int Position);
}
