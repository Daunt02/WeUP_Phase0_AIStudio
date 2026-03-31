using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;
using WeUP.Infrastructure.Ingestion.Adapters;

namespace WeUP.Application.Ingestion;

/// <summary>
/// Routes ingestion requests to the correct adapter and orchestrates the pipeline:
/// 1. Create job record
/// 2. Run adapter → raw candidate
/// 3. Persist candidate
/// 4. Route to review or auto-approve
/// </summary>
public sealed class IngestionDispatcher(
    IIngestionJobRepository jobs,
    IIngestionAuditWriter audit,
    ManualSubmissionAdapter manualAdapter,
    LinkAdapter linkAdapter,
    VenuePageAdapter venueAdapter) : IIngestionDispatcher
{
    public async Task<string> DispatchManualAsync(ManualIngestionRequest request, CancellationToken ct = default)
    {
        var jobId = await jobs.CreateJobAsync(IngestionSourceKind.ManualSubmission, request.SubmitterId, ct);
        await RunPipelineAsync(jobId, manualAdapter, request, ct);
        return jobId;
    }

    public async Task<string> DispatchLinkAsync(LinkIngestionRequest request, CancellationToken ct = default)
    {
        var jobId = await jobs.CreateJobAsync(IngestionSourceKind.PastedUrl, request.Url, ct);
        await RunPipelineAsync(jobId, linkAdapter, request, ct);
        return jobId;
    }

    public async Task<string> DispatchVenuePageAsync(VenuePageIngestionRequest request, CancellationToken ct = default)
    {
        var jobId = await jobs.CreateJobAsync(IngestionSourceKind.VenuePage, request.PageUrl, ct);
        await RunPipelineAsync(jobId, venueAdapter, request, ct);
        return jobId;
    }

    private async Task RunPipelineAsync(string jobId, IIngestionAdapter adapter, object request, CancellationToken ct)
    {
        try
        {
            await jobs.UpdateStatusAsync(jobId, IngestionJobStatus.Fetching, ct: ct);
            await audit.WriteAsync(jobId, "Fetching", null, ct);

            await jobs.UpdateStatusAsync(jobId, IngestionJobStatus.Extracting, ct: ct);
            var candidate = await adapter.ExtractAsync(request, ct);
            await audit.WriteAsync(jobId, "Extracted", $"confidence={candidate.ExtractionConfidence:F2}", ct);

            await jobs.UpdateStatusAsync(jobId, IngestionJobStatus.Normalizing, ct: ct);
            await audit.WriteAsync(jobId, "Normalizing", null, ct);

            // Route: if extraction confidence is high enough → auto-route to ReviewPending
            // Full publish eligibility implemented in P14
            var nextStatus = candidate.ExtractionConfidence >= 0.85
                ? IngestionJobStatus.ReviewPending
                : IngestionJobStatus.ReviewPending; // all go to review in Phase 0

            await jobs.UpdateStatusAsync(jobId, nextStatus, ct: ct);
            await audit.WriteAsync(jobId, nextStatus.ToString(), null, ct);
        }
        catch (Exception ex)
        {
            await jobs.UpdateStatusAsync(jobId, IngestionJobStatus.Failed, ex.Message, ct);
            await audit.WriteAsync(jobId, "Failed", ex.Message, ct);
        }
    }
}
