using System.Text.Json;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;

namespace WeUP.Application.Ingestion;

public sealed class EventSourceAdapterResolver(IEnumerable<IEventSourceAdapter> adapters) : IEventSourceAdapterResolver
{
    private readonly IReadOnlyDictionary<IngestionSourceKind, IEventSourceAdapter> _adapters =
        adapters
            .SelectMany(adapter => adapter.Capability.SupportedSourceKinds.Select(kind => (kind, adapter)))
            .ToDictionary(pair => pair.kind, pair => pair.adapter);

    public IEventSourceAdapter Resolve(IngestionSourceKind sourceKind)
    {
        if (_adapters.TryGetValue(sourceKind, out var adapter))
        {
            return adapter;
        }

        throw new InvalidOperationException($"No ingestion adapter registered for source kind '{sourceKind}'.");
    }
}

public sealed class IngestionCoordinator(
    IIngestionJobRepository jobs,
    IIngestionAuditWriter audit,
    IEventSourceAdapterResolver resolver) : IIngestionCoordinator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<IngestionResult> SubmitManualAsync(ManualIngestionRequest request, CancellationToken ct = default)
    {
        var envelope = BuildEnvelope(
            IngestionSourceKind.ManualSubmission,
            request.SubmitterId,
            request.SubmitterId,
            request,
            new Dictionary<string, string?>
            {
                ["inputKind"] = "manual-submission",
                ["title"] = request.Title,
                ["venueName"] = request.VenueName,
            });

        return ExecuteAsync(envelope, ct);
    }

    public Task<IngestionResult> SubmitUrlAsync(UrlIngestionRequest request, CancellationToken ct = default)
    {
        var envelope = BuildEnvelope(
            IngestionSourceKind.PastedUrl,
            request.Url,
            request.SubmitterId,
            request,
            new Dictionary<string, string?>
            {
                ["inputKind"] = "pasted-url",
                ["sourceLabel"] = request.SourceLabel,
            });

        return ExecuteAsync(envelope, ct);
    }

    public Task<IngestionResult> SubmitVenuePageAsync(VenuePageIngestionRequest request, CancellationToken ct = default)
    {
        var envelope = BuildEnvelope(
            IngestionSourceKind.VenuePage,
            request.PageUrl,
            request.RequestedBy,
            request,
            new Dictionary<string, string?>
            {
                ["inputKind"] = "venue-page",
                ["venueId"] = request.VenueId,
                ["venueName"] = request.VenueName,
            });

        return ExecuteAsync(envelope, ct);
    }

    public Task<IngestionResult?> GetJobAsync(string jobId, CancellationToken ct = default)
        => jobs.GetAsync(jobId, ct);

    private async Task<IngestionResult> ExecuteAsync(IngestionRequestEnvelope envelope, CancellationToken ct)
    {
        var job = await jobs.CreateAsync(envelope, ct);
        await audit.WriteAsync(job.JobId, IngestionJobStatus.RECEIVED.ToString(), $"sourceKind={envelope.SourceKind}", ct);

        IEventSourceAdapter adapter;
        try
        {
            adapter = resolver.Resolve(envelope.SourceKind);
        }
        catch (InvalidOperationException ex)
        {
            var failed = Transition(
                job,
                IngestionJobStatus.FAILED,
                ex.Message,
                issues:
                [
                    new CanonicalIngestionIssue(
                        "adapter_not_registered",
                        ex.Message,
                        IngestionIssueSeverity.Error,
                        false,
                        null,
                        EmptyMetadata())
                ]);

            await jobs.SaveAsync(failed, ct);
            await audit.WriteAsync(failed.JobId, IngestionJobStatus.FAILED.ToString(), ex.Message, ct);
            return failed;
        }

        job = Transition(job, IngestionJobStatus.VALIDATING, $"adapter={adapter.Capability.AdapterKey}");
        await jobs.SaveAsync(job, ct);
        await audit.WriteAsync(job.JobId, IngestionJobStatus.VALIDATING.ToString(), $"adapter={adapter.Capability.AdapterKey}", ct);

        var validationIssues = await adapter.ValidateAsync(envelope, ct);
        var blockingValidationIssues = validationIssues.Where(issue => issue.Severity == IngestionIssueSeverity.Error).ToArray();
        if (blockingValidationIssues.Length > 0)
        {
            var terminalStatus = blockingValidationIssues.Any(issue => issue.IsRetryable)
                ? IngestionJobStatus.RETRYABLE_FAILURE
                : IngestionJobStatus.FAILED;
            var detail = string.Join(" | ", blockingValidationIssues.Select(issue => issue.Message));

            job = Transition(job, terminalStatus, detail, issues: blockingValidationIssues);
            await jobs.SaveAsync(job, ct);
            await audit.WriteAsync(job.JobId, terminalStatus.ToString(), detail, ct);
            return job;
        }

        job = Transition(job, IngestionJobStatus.NORMALIZING, $"adapter={adapter.Capability.AdapterKey}", issues: validationIssues);
        await jobs.SaveAsync(job, ct);
        await audit.WriteAsync(job.JobId, IngestionJobStatus.NORMALIZING.ToString(), $"adapter={adapter.Capability.AdapterKey}", ct);

        var execution = await adapter.ExecuteAsync(envelope, ct);
        var mergedIssues = validationIssues.Concat(execution.Issues).ToArray();

        if (execution.Candidate is null)
        {
            var terminalStatus = mergedIssues.Any(issue => issue.IsRetryable && issue.Severity == IngestionIssueSeverity.Error)
                ? IngestionJobStatus.RETRYABLE_FAILURE
                : IngestionJobStatus.FAILED;
            var detail = string.Join(" | ", mergedIssues.Where(issue => issue.Severity == IngestionIssueSeverity.Error).Select(issue => issue.Message));

            job = Transition(
                job,
                terminalStatus,
                string.IsNullOrWhiteSpace(detail) ? "Adapter did not produce a canonical candidate." : detail,
                candidate: null,
                evidence: execution.Evidence,
                issues: mergedIssues,
                executionMetadata: execution.Execution);

            await jobs.SaveAsync(job, ct);
            await audit.WriteAsync(job.JobId, terminalStatus.ToString(), job.FailureReason, ct);
            return job;
        }

        job = Transition(
            job,
            IngestionJobStatus.CANDIDATE_CREATED,
            $"candidateSource={execution.Candidate.SourceRef}",
            candidate: execution.Candidate,
            evidence: execution.Evidence,
            issues: mergedIssues,
            executionMetadata: execution.Execution);
        await jobs.SaveAsync(job, ct);
        await audit.WriteAsync(job.JobId, IngestionJobStatus.CANDIDATE_CREATED.ToString(), $"candidateSource={execution.Candidate.SourceRef}", ct);

        var reviewDetail = DetermineReviewDetail(mergedIssues);
        job = Transition(
            job,
            IngestionJobStatus.REQUIRES_REVIEW,
            reviewDetail,
            candidate: execution.Candidate,
            evidence: execution.Evidence,
            issues: mergedIssues,
            executionMetadata: execution.Execution);
        await jobs.SaveAsync(job, ct);
        await audit.WriteAsync(job.JobId, IngestionJobStatus.REQUIRES_REVIEW.ToString(), reviewDetail, ct);

        return job;
    }

    private static IngestionRequestEnvelope BuildEnvelope<TPayload>(
        IngestionSourceKind sourceKind,
        string sourceReference,
        string submittedBy,
        TPayload payload,
        IReadOnlyDictionary<string, string?> metadata)
    {
        var rawPayloadJson = JsonSerializer.Serialize(payload, JsonOptions);

        return new IngestionRequestEnvelope(
            RequestId: Guid.NewGuid().ToString("N"),
            SourceKind: sourceKind,
            SourceReference: sourceReference,
            SubmittedBy: submittedBy,
            RawPayloadJson: rawPayloadJson,
            IdempotencyKey: BuildIdempotencyKey(sourceKind, sourceReference, rawPayloadJson),
            ReceivedAtUtc: DateTimeOffset.UtcNow,
            Metadata: metadata);
    }

    private static string BuildIdempotencyKey(IngestionSourceKind sourceKind, string sourceReference, string rawPayloadJson)
    {
        var payload = System.Text.Encoding.UTF8.GetBytes($"{sourceKind}|{sourceReference}|{rawPayloadJson}");
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant();
    }

    private static IngestionResult Transition(
        IngestionResult current,
        IngestionJobStatus status,
        string? detail,
        CanonicalEventCandidate? candidate = null,
        CanonicalSourceEvidence[]? evidence = null,
        CanonicalIngestionIssue[]? issues = null,
        AdapterExecutionMetadata? executionMetadata = null)
    {
        var timestamp = DateTimeOffset.UtcNow;

        return current with
        {
            Status = status,
            Candidate = candidate ?? current.Candidate,
            Evidence = evidence ?? current.Evidence,
            Issues = issues ?? current.Issues,
            Execution = executionMetadata ?? current.Execution,
            Lifecycle = [.. current.Lifecycle, new IngestionStatusRecord(status, timestamp, detail)],
            UpdatedAtUtc = timestamp,
        };
    }

    private static string DetermineReviewDetail(IEnumerable<CanonicalIngestionIssue> issues)
    {
        var warnings = issues.Count(issue => issue.Severity == IngestionIssueSeverity.Warning);
        return warnings == 0
            ? "Canonical candidate is awaiting review before aggregate promotion."
            : $"Canonical candidate is awaiting review with {warnings} warning(s).";
    }

    private static IReadOnlyDictionary<string, string?> EmptyMetadata()
        => new Dictionary<string, string?>();
}
