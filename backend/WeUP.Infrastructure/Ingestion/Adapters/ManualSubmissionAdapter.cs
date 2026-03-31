using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;

namespace WeUP.Infrastructure.Ingestion.Adapters;

/// <summary>
/// Adapter for manual event submissions. High-trust source — user provided all fields directly.
/// Confidence is high by default; geocode confidence depends on address quality.
/// </summary>
public sealed class ManualSubmissionAdapter : IIngestionAdapter
{
    public IngestionSourceKind SourceKind => IngestionSourceKind.ManualSubmission;

    public Task<NormalizedEventCandidate> ExtractAsync(object request, CancellationToken ct = default)
    {
        if (request is not ManualIngestionRequest req)
            throw new ArgumentException($"Expected {nameof(ManualIngestionRequest)}", nameof(request));

        var candidate = new NormalizedEventCandidate(
            Title: req.Title,
            VenueName: req.VenueName,
            Address: req.Address,
            StartUtc: req.StartDate,
            EndUtc: req.EndDate,
            Timezone: req.Timezone,
            Category: req.Category,
            Description: req.Description,
            Tags: req.Tags,
            SourceKind: "manual_submission",
            SourceRef: req.SubmitterId,
            ExtractionConfidence: 0.95, // user-provided — high extraction confidence
            GeocodeConfidence: 0.70,    // address still needs geocoding verification
            TemporalConfidence: 0.90,   // user typed the date — high but not perfect
            EvidenceRefs: null);

        return Task.FromResult(candidate);
    }
}
