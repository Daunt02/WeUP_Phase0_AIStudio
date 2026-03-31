using System.Net.Http;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;

namespace WeUP.Infrastructure.Ingestion.Adapters;

/// <summary>
/// Adapter for pasted event links (Eventbrite, RA, venue sites, etc.).
/// Fetches the page, extracts structured metadata (Open Graph / JSON-LD / heuristics),
/// and produces a normalized candidate.
///
/// Phase 0: heuristic extraction only. LLM-assisted normalization added in P11.
/// </summary>
public sealed class LinkAdapter(IHttpClientFactory httpFactory) : IIngestionAdapter
{
    public IngestionSourceKind SourceKind => IngestionSourceKind.PastedUrl;

    public async Task<NormalizedEventCandidate> ExtractAsync(object request, CancellationToken ct = default)
    {
        if (request is not LinkIngestionRequest req)
            throw new ArgumentException($"Expected {nameof(LinkIngestionRequest)}", nameof(request));

        string? rawHtml = null;
        var evidenceRefs = new List<string>();

        try
        {
            using var client = httpFactory.CreateClient("ingestion");
            using var response = await client.GetAsync(req.Url, ct);
            response.EnsureSuccessStatusCode();
            rawHtml = await response.Content.ReadAsStringAsync(ct);
            evidenceRefs.Add($"url:{req.Url}");
        }
        catch (Exception ex)
        {
            // Return low-confidence candidate with failure evidence
            return new NormalizedEventCandidate(
                Title: null, VenueName: null, Address: null,
                StartUtc: null, EndUtc: null, Timezone: null,
                Category: null, Description: null, Tags: null,
                SourceKind: "pasted_url",
                SourceRef: req.Url,
                ExtractionConfidence: 0.0,
                GeocodeConfidence: 0.0,
                TemporalConfidence: 0.0,
                EvidenceRefs: [$"fetch_error:{ex.Message}"]);
        }

        // Heuristic extraction — extract Open Graph / JSON-LD metadata
        var title = ExtractMetaContent(rawHtml, "og:title")
                 ?? ExtractMetaContent(rawHtml, "twitter:title");
        var description = ExtractMetaContent(rawHtml, "og:description");

        return new NormalizedEventCandidate(
            Title: title,
            VenueName: null,
            Address: null,
            StartUtc: null,    // requires LLM normalization (P11) or structured data parsing
            EndUtc: null,
            Timezone: null,
            Category: null,
            Description: description,
            Tags: null,
            SourceKind: "pasted_url",
            SourceRef: req.Url,
            ExtractionConfidence: title is not null ? 0.55 : 0.20,
            GeocodeConfidence: 0.0,   // no address yet
            TemporalConfidence: 0.0,  // no time parsed yet
            EvidenceRefs: [.. evidenceRefs]);
    }

    private static string? ExtractMetaContent(string html, string property)
    {
        // Minimal meta tag extraction — property or name attribute
        var patterns = new[]
        {
            $"<meta property=\"{property}\" content=\"",
            $"<meta name=\"{property}\" content=\"",
        };
        foreach (var pattern in patterns)
        {
            var idx = html.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;
            var start = idx + pattern.Length;
            var end = html.IndexOf('"', start);
            if (end > start) return html[start..end];
        }
        return null;
    }
}
