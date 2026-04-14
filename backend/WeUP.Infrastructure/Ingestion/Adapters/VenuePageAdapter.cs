using System.Diagnostics;
using System.Text.Json;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;

namespace WeUP.Infrastructure.Ingestion.Adapters;

public sealed class VenuePageAdapter(IHttpClientFactory httpFactory) : IEventSourceAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AdapterCapabilityDescriptor Capability { get; } = new(
        "venue-page",
        "Venue Page Adapter",
        "p10.v1",
        [IngestionSourceKind.VenuePage],
        true,
        true,
        ["raw-payload", "venue-reference", "fetched-html", "extracted-metadata"],
        ["Captures venue-page provenance and returns a reviewable candidate seed for later page expansion."]);

    public bool CanHandle(IngestionSourceKind sourceKind) => sourceKind == IngestionSourceKind.VenuePage;

    public Task<CanonicalIngestionIssue[]> ValidateAsync(IngestionRequestEnvelope request, CancellationToken ct = default)
    {
        var issues = new List<CanonicalIngestionIssue>();
        var payload = Deserialize(request, issues);
        if (payload is null)
        {
            return Task.FromResult(issues.ToArray());
        }

        if (string.IsNullOrWhiteSpace(payload.VenueId))
        {
            issues.Add(BuildIssue("missing_venue_id", "venueId is required.", false, "venueId", IngestionIssueSeverity.Error));
        }

        if (string.IsNullOrWhiteSpace(payload.RequestedBy))
        {
            issues.Add(BuildIssue("missing_requested_by", "requestedBy is required.", false, "requestedBy", IngestionIssueSeverity.Error));
        }

        if (string.IsNullOrWhiteSpace(payload.PageUrl) || !Uri.TryCreate(payload.PageUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            issues.Add(BuildIssue("invalid_page_url", "pageUrl must be a valid absolute HTTP or HTTPS URL.", false, "pageUrl", IngestionIssueSeverity.Error));
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
                $"venue-reference-{request.RequestId}",
                "venue-reference",
                payload.VenueId,
                null,
                null,
                DateTimeOffset.UtcNow,
                1.0,
                new Dictionary<string, string?>
                {
                    ["pageUrl"] = payload.PageUrl,
                    ["venueName"] = payload.VenueName,
                })
        };

        string html;
        try
        {
            using var client = httpFactory.CreateClient("ingestion");
            using var response = await client.GetAsync(payload.PageUrl, ct);
            response.EnsureSuccessStatusCode();
            html = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            issues.Add(BuildIssue("venue_page_fetch_failed", ex.Message, true, "pageUrl", IngestionIssueSeverity.Error));
            return new AdapterExecutionResult(null, evidence.ToArray(), issues.ToArray(), BuildExecution(startedAt, stopwatch.ElapsedMilliseconds));
        }

        var pageTitle = ExtractTag(html, "title");
        var description = ExtractMetaContent(html, "description") ?? ExtractMetaContent(html, "og:description");
        var venueName = payload.VenueName ?? ExtractMetaContent(html, "og:site_name") ?? payload.VenueId;

        evidence.Add(new CanonicalSourceEvidence(
            $"venue-html-{request.RequestId}",
            "fetched-html",
            payload.PageUrl,
            "text/html",
            html.Length <= 1200 ? html : html[..1200],
            DateTimeOffset.UtcNow,
            0.75,
            new Dictionary<string, string?>
            {
                ["length"] = html.Length.ToString(),
            }));

        evidence.Add(new CanonicalSourceEvidence(
            $"venue-metadata-{request.RequestId}",
            "extracted-metadata",
            payload.PageUrl,
            "application/json",
            JsonSerializer.Serialize(new { pageTitle, description, venueName, payload.VenueId }, JsonOptions),
            DateTimeOffset.UtcNow,
            0.65,
            new Dictionary<string, string?>
            {
                ["heuristics"] = "title-tag,meta-description",
            }));

        issues.Add(BuildIssue(
            "venue_page_requires_expansion",
            "Venue-page ingestion produced a candidate seed that still requires downstream page expansion and review.",
            false,
            null,
            IngestionIssueSeverity.Warning));

        var candidate = new CanonicalEventCandidate(
            pageTitle,
            venueName,
            null,
            null,
            null,
            null,
            "venue-page",
            description,
            new[] { "venue-page", "requires-expansion" },
            "venue_page",
            payload.PageUrl,
            string.IsNullOrWhiteSpace(pageTitle) ? 0.20 : 0.48,
            0.0,
            0.0,
            evidence.Select(item => item.EvidenceId).ToArray(),
            payload.VenueId,
            new Dictionary<string, string?>
            {
                ["venueId"] = payload.VenueId,
                ["requestedBy"] = payload.RequestedBy,
            });

        return new AdapterExecutionResult(candidate, evidence.ToArray(), issues.ToArray(), BuildExecution(startedAt, stopwatch.ElapsedMilliseconds));
    }

    private VenuePageIngestionRequest? Deserialize(IngestionRequestEnvelope request, ICollection<CanonicalIngestionIssue> issues)
    {
        try
        {
            return JsonSerializer.Deserialize<VenuePageIngestionRequest>(request.RawPayloadJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            issues.Add(BuildIssue("invalid_venue_page_payload", ex.Message, false, null, IngestionIssueSeverity.Error));
            return null;
        }
    }

    private static CanonicalSourceEvidence BuildPayloadEvidence(IngestionRequestEnvelope request) =>
        new(
            $"venue-payload-{request.RequestId}",
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

    private static string? ExtractMetaContent(string html, string property)
    {
        var patterns = new[]
        {
            $"<meta property=\"{property}\" content=\"",
            $"<meta name=\"{property}\" content=\"",
        };

        foreach (var pattern in patterns)
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

    private static CanonicalIngestionIssue BuildIssue(string code, string message, bool retryable, string? field, IngestionIssueSeverity severity)
        => new(code, message, severity, retryable, field, new Dictionary<string, string?>());
}
