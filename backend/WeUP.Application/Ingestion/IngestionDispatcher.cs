using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;

namespace WeUP.Application.Ingestion;

/// <summary>
/// Routes ingestion requests to the correct adapter and orchestrates the pipeline.
/// Accepts IEnumerable&lt;IIngestionAdapter&gt; — new adapters are registered in DI
/// without changing this class.
/// </summary>
public sealed class IngestionDispatcher(
    IIngestionJobRepository jobs,
    IIngestionAuditWriter audit,
    IEnumerable<IIngestionAdapter> adapters) : IIngestionDispatcher
{
    private readonly Dictionary<IngestionSourceKind, IIngestionAdapter> _adapters =
        adapters.ToDictionary(a => a.SourceKind);

    public async Task<string> DispatchManualAsync(ManualIngestionRequest request, CancellationToken ct = default)
    {
        var jobId = await jobs.CreateJobAsync(IngestionSourceKind.ManualSubmission, request.SubmitterId, ct);
        await RunPipelineAsync(jobId, GetAdapter(IngestionSourceKind.ManualSubmission), request, ct);
        return jobId;
    }

    public async Task<string> DispatchLinkAsync(LinkIngestionRequest request, CancellationToken ct = default)
    {
        var jobId = await jobs.CreateJobAsync(IngestionSourceKind.PastedUrl, request.Url, ct);
        await RunPipelineAsync(jobId, GetAdapter(IngestionSourceKind.PastedUrl), request, ct);
        return jobId;
    }

    public async Task<string> DispatchVenuePageAsync(VenuePageIngestionRequest request, CancellationToken ct = default)
    {
        var jobId = await jobs.CreateJobAsync(IngestionSourceKind.VenuePage, request.PageUrl, ct);
        await RunPipelineAsync(jobId, GetAdapter(IngestionSourceKind.VenuePage), request, ct);
        return jobId;
    }

    private IIngestionAdapter GetAdapter(IngestionSourceKind kind)
    {
        if (_adapters.TryGetValue(kind, out var adapter)) return adapter;
        throw new InvalidOperationException($"No adapter registered for source kind: {kind}");
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

            // All candidates route to ReviewPending in Phase 0
            // Full auto-approve logic implemented in P14
            await jobs.UpdateStatusAsync(jobId, IngestionJobStatus.ReviewPending, ct: ct);
            await audit.WriteAsync(jobId, "ReviewPending", null, ct);
        }
        catch (Exception ex)
        {
            await jobs.UpdateStatusAsync(jobId, IngestionJobStatus.Failed, ex.Message, ct);
            await audit.WriteAsync(jobId, "Failed", ex.Message, ct);
        }
    }
}
