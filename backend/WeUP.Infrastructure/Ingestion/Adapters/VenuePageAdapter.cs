using System.Net.Http;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;

namespace WeUP.Infrastructure.Ingestion.Adapters;

/// <summary>
/// Adapter for scraping venue calendar pages.
/// Phase 0: fetches raw HTML, extracts meta and structured data.
/// Full calendar parsing added in a future sprint with venue-specific adapters.
/// </summary>
public sealed class VenuePageAdapter(IHttpClientFactory httpFactory) : IIngestionAdapter
{
    public IngestionSourceKind SourceKind => IngestionSourceKind.VenuePage;

    public async Task<NormalizedEventCandidate> ExtractAsync(object request, CancellationToken ct = default)
    {
        if (request is not VenuePageIngestionRequest req)
            throw new ArgumentException($"Expected {nameof(VenuePageIngestionRequest)}", nameof(request));

        string? rawHtml = null;
        try
        {
            using var client = httpFactory.CreateClient("ingestion");
            using var response = await client.GetAsync(req.PageUrl, ct);
            response.EnsureSuccessStatusCode();
            rawHtml = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            return new NormalizedEventCandidate(
                Title: null, VenueName: null, Address: null,
                StartUtc: null, EndUtc: null, Timezone: null,
                Category: null, Description: null, Tags: null,
                SourceKind: "scraped_venue_page",
                SourceRef: req.PageUrl,
                ExtractionConfidence: 0.0,
                GeocodeConfidence: 0.0,
                TemporalConfidence: 0.0,
                EvidenceRefs: [$"fetch_error:{ex.Message}"]);
        }

        // Phase 0 heuristic — page name as candidate title seed
        var pageTitle = ExtractTag(rawHtml, "title");

        return new NormalizedEventCandidate(
            Title: pageTitle,
            VenueName: null,          // venue id from req.VenueId — resolved by service layer
            Address: null,
            StartUtc: null,
            EndUtc: null,
            Timezone: null,
            Category: "nightlife",
            Description: null,
            Tags: null,
            SourceKind: "scraped_venue_page",
            SourceRef: req.PageUrl,
            ExtractionConfidence: pageTitle is not null ? 0.40 : 0.10,
            GeocodeConfidence: 0.0,
            TemporalConfidence: 0.0,
            EvidenceRefs: [$"url:{req.PageUrl}", $"venueId:{req.VenueId}"]);
    }

    private static string? ExtractTag(string html, string tag)
    {
        var open = $"<{tag}>";
        var close = $"</{tag}>";
        var start = html.IndexOf(open, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        start += open.Length;
        var end = html.IndexOf(close, start, StringComparison.OrdinalIgnoreCase);
        if (end <= start) return null;
        return html[start..end].Trim();
    }
}
