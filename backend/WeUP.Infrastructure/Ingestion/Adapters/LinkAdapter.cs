using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;

namespace WeUP.Infrastructure.Ingestion.Adapters;

public sealed partial class LinkAdapter(IHttpClientFactory httpFactory) : IEventSourceAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AdapterCapabilityDescriptor Capability { get; } = new(
        "pasted-url",
        "Pasted URL Adapter",
        "p10.v1",
        [IngestionSourceKind.PastedUrl],
        true,
        true,
        ["raw-payload", "url-reference", "fetched-html", "extracted-metadata"],
        ["Uses deterministic HTML/OpenGraph/JSON-LD heuristics only in P10."]);

    public bool CanHandle(IngestionSourceKind sourceKind) => sourceKind == IngestionSourceKind.PastedUrl;

    public Task<CanonicalIngestionIssue[]> ValidateAsync(IngestionRequestEnvelope request, CancellationToken ct = default)
    {
        var issues = new List<CanonicalIngestionIssue>();
        var payload = Deserialize(request, issues);
        if (payload is null)
        {
            return Task.FromResult(issues.ToArray());
        }

        if (string.IsNullOrWhiteSpace(payload.SubmitterId))
        {
            issues.Add(BuildIssue("missing_submitter", "submitterId is required.", false, "submitterId", IngestionIssueSeverity.Error));
        }

        if (string.IsNullOrWhiteSpace(payload.Url) || !Uri.TryCreate(payload.Url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            issues.Add(BuildIssue("invalid_url", "Url must be a valid absolute HTTP or HTTPS URL.", false, "url", IngestionIssueSeverity.Error));
        }

        return Task.FromResult(issues.ToArray());
    }

    public async Task<AdapterExecutionResult> ExecuteAsync(IngestionRequestEnvelope request, CancellationToken ct = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var issues = new List<CanonicalIngestionIssue>();
        var payload = Deserialize(request, issues);
        if (payload is null)
        {
            return new AdapterExecutionResult(null, [BuildPayloadEvidence(request)], issues.ToArray(), BuildExecution(startedAt, stopwatch.ElapsedMilliseconds));
        }

        var evidence = new List<CanonicalSourceEvidence>
        {
            BuildPayloadEvidence(request),
            new(
                $"url-reference-{request.RequestId}",
                "url-reference",
                payload.Url,
                null,
                null,
                DateTimeOffset.UtcNow,
                1.0,
                new Dictionary<string, string?>
                {
                    ["sourceLabel"] = payload.SourceLabel,
                    ["submittedBy"] = payload.SubmitterId,
                })
        };

        string html;
        try
        {
            using var client = httpFactory.CreateClient("ingestion");
            using var response = await client.GetAsync(payload.Url, ct);
            response.EnsureSuccessStatusCode();
            html = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            issues.Add(BuildIssue("url_fetch_failed", ex.Message, true, "url", IngestionIssueSeverity.Error));
            return new AdapterExecutionResult(null, evidence.ToArray(), issues.ToArray(), BuildExecution(startedAt, stopwatch.ElapsedMilliseconds));
        }

        evidence.Add(new CanonicalSourceEvidence(
            $"url-html-{request.RequestId}",
            "fetched-html",
            payload.Url,
            "text/html",
            Clip(html, 1200),
            DateTimeOffset.UtcNow,
            0.85,
            new Dictionary<string, string?>
            {
                ["length"] = html.Length.ToString(),
            }));

        var title = ExtractMetaContent(html, "og:title")
            ?? ExtractMetaContent(html, "twitter:title")
            ?? ExtractJsonLdValue(html, "name")
            ?? ExtractTag(html, "title");
        var description = ExtractMetaContent(html, "og:description")
            ?? ExtractMetaContent(html, "description")
            ?? ExtractJsonLdValue(html, "description");
        var venueName = ExtractMetaContent(html, "og:site_name")
            ?? ExtractJsonLdValue(html, "location.name")
            ?? TryGetHost(payload.Url);
        var startDate = ExtractJsonLdValue(html, "startDate");
        var address = ExtractJsonLdValue(html, "streetAddress");

        evidence.Add(new CanonicalSourceEvidence(
            $"url-metadata-{request.RequestId}",
            "extracted-metadata",
            payload.Url,
            "application/json",
            JsonSerializer.Serialize(new { title, description, venueName, startDate, address }, JsonOptions),
            DateTimeOffset.UtcNow,
            0.7,
            new Dictionary<string, string?>
            {
                ["heuristics"] = "opengraph,jsonld,title-tag",
            }));

        if (string.IsNullOrWhiteSpace(title))
        {
            issues.Add(BuildIssue("missing_title", "No title could be extracted from the pasted URL.", false, "title", IngestionIssueSeverity.Warning));
        }

        if (string.IsNullOrWhiteSpace(startDate))
        {
            issues.Add(BuildIssue("missing_start_date", "No explicit start date was found in the pasted URL payload.", false, "startDate", IngestionIssueSeverity.Warning));
        }

        var candidate = new CanonicalEventCandidate(
            title,
            venueName,
            address,
            startDate,
            null,
            null,
            "event-link",
            description,
            null,
            "pasted_url",
            payload.Url,
            title is null ? 0.25 : 0.63,
            string.IsNullOrWhiteSpace(address) ? 0.0 : 0.30,
            string.IsNullOrWhiteSpace(startDate) ? 0.0 : 0.55,
            evidence.Select(item => item.EvidenceId).ToArray(),
            payload.Url,
            new Dictionary<string, string?>
            {
                ["requestId"] = request.RequestId,
                ["sourceLabel"] = payload.SourceLabel,
            });

        return new AdapterExecutionResult(candidate, evidence.ToArray(), issues.ToArray(), BuildExecution(startedAt, stopwatch.ElapsedMilliseconds));
    }

    private UrlIngestionRequest? Deserialize(IngestionRequestEnvelope request, ICollection<CanonicalIngestionIssue> issues)
    {
        try
        {
            return JsonSerializer.Deserialize<UrlIngestionRequest>(request.RawPayloadJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            issues.Add(BuildIssue("invalid_url_payload", ex.Message, false, null, IngestionIssueSeverity.Error));
            return null;
        }
    }

    private static CanonicalSourceEvidence BuildPayloadEvidence(IngestionRequestEnvelope request) =>
        new(
            $"url-payload-{request.RequestId}",
            "raw-payload",
            request.SourceReference,
            "application/json",
            request.RawPayloadJson,
            request.ReceivedAtUtc,
            1.0,
            request.Metadata);

    private AdapterExecutionMetadata BuildExecution(DateTimeOffset startedAtUtc, long durationMs)
        => new(
            Capability.AdapterKey,
            Capability.Version,
            Capability.IsRetrySafe,
            1,
            startedAtUtc,
            DateTimeOffset.UtcNow,
            durationMs,
            Capability);

    private static CanonicalIngestionIssue BuildIssue(string code, string message, bool retryable, string? field, IngestionIssueSeverity severity)
        => new(code, message, severity, retryable, field, new Dictionary<string, string?>());

    private static string? ExtractMetaContent(string html, string property)
    {
        var patterns = new[]
        {
            $"<meta property=\"{property}\" content=\"",
            $"<meta name=\"{property}\" content=\"",
            $"<meta content=\"",
        };

        foreach (var pattern in patterns.Take(2))
        {
            var idx = html.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                continue;
            }

            var start = idx + pattern.Length;
            var end = html.IndexOf('"', start);
            if (end > start)
            {
                return html[start..end].Trim();
            }
        }

        return null;
    }

    private static string? ExtractTag(string html, string tag)
    {
        var open = $"<{tag}>";
        var close = $"</{tag}>";
        var start = html.IndexOf(open, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += open.Length;
        var end = html.IndexOf(close, start, StringComparison.OrdinalIgnoreCase);
        return end <= start ? null : html[start..end].Trim();
    }

    private static string? ExtractJsonLdValue(string html, string propertyName)
    {
        var sanitizedProperty = Regex.Escape(propertyName.Replace(".", "\\s*.*?"));
        var match = JsonLdRegex().Match(html.Replace("\r", string.Empty).Replace("\n", string.Empty));
        if (!match.Success)
        {
            return null;
        }

        var payload = match.Groups[1].Value;
        var propertyMatch = Regex.Match(payload, $"\"{sanitizedProperty}\"\\s*:\\s*\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase);
        return propertyMatch.Success ? propertyMatch.Groups["value"].Value.Trim() : null;
    }

    private static string Clip(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static string? TryGetHost(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;

    [GeneratedRegex("<script[^>]*type=\"application/ld\\+json\"[^>]*>(.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex JsonLdRegex();
}
