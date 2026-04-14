using System.Diagnostics;
using System.Text.Json;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;

namespace WeUP.Infrastructure.Ingestion.Adapters;

public sealed class ManualSubmissionAdapter : IEventSourceAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AdapterCapabilityDescriptor Capability { get; } = new(
        "manual-submission",
        "Manual Submission Adapter",
        "p10.v1",
        [IngestionSourceKind.ManualSubmission],
        true,
        false,
        ["raw-payload", "submitter-attribution"],
        ["Converts direct user submissions into canonical candidates without touching event aggregates."]);

    public bool CanHandle(IngestionSourceKind sourceKind) => sourceKind == IngestionSourceKind.ManualSubmission;

    public Task<CanonicalIngestionIssue[]> ValidateAsync(IngestionRequestEnvelope request, CancellationToken ct = default)
    {
        var issues = new List<CanonicalIngestionIssue>();
        var payload = Deserialize(request, issues);
        if (payload is null)
        {
            return Task.FromResult(issues.ToArray());
        }

        Require(payload.Title, "title", issues);
        Require(payload.VenueName, "venueName", issues);
        Require(payload.Address, "address", issues);
        Require(payload.StartDate, "startDate", issues);
        Require(payload.Timezone, "timezone", issues);
        Require(payload.Category, "category", issues);
        Require(payload.SubmitterId, "submitterId", issues);

        if (!string.IsNullOrWhiteSpace(payload.StartDate) && !DateTimeOffset.TryParse(payload.StartDate, out _))
        {
            issues.Add(BuildError("invalid_start_date", "StartDate must be a valid ISO 8601 timestamp.", false, "startDate"));
        }

        if (!string.IsNullOrWhiteSpace(payload.EndDate) && !DateTimeOffset.TryParse(payload.EndDate, out _))
        {
            issues.Add(BuildError("invalid_end_date", "EndDate must be a valid ISO 8601 timestamp when provided.", false, "endDate"));
        }

        return Task.FromResult(issues.ToArray());
    }

    public Task<AdapterExecutionResult> ExecuteAsync(IngestionRequestEnvelope request, CancellationToken ct = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var parseIssues = new List<CanonicalIngestionIssue>();
        var payload = Deserialize(request, parseIssues);

        if (payload is null)
        {
            return Task.FromResult(new AdapterExecutionResult(
                null,
                [BuildPayloadEvidence(request)],
                parseIssues.ToArray(),
                BuildExecution(startedAt, stopwatch.ElapsedMilliseconds)));
        }

        var payloadEvidence = BuildPayloadEvidence(request);
        var attributionEvidence = new CanonicalSourceEvidence(
            $"manual-attribution-{request.RequestId}",
            "submitter-attribution",
            payload.SubmitterId,
            null,
            null,
            DateTimeOffset.UtcNow,
            1.0,
            new Dictionary<string, string?>
            {
                ["submittedBy"] = payload.SubmitterId,
                ["sourceKind"] = request.SourceKind.ToString(),
            });

        var candidate = new CanonicalEventCandidate(
            payload.Title,
            payload.VenueName,
            payload.Address,
            payload.StartDate,
            payload.EndDate,
            payload.Timezone,
            payload.Category,
            payload.Description,
            payload.Tags,
            "manual_submission",
            request.SourceReference,
            0.98,
            0.72,
            0.94,
            [payloadEvidence.EvidenceId, attributionEvidence.EvidenceId],
            null,
            new Dictionary<string, string?>
            {
                ["requestId"] = request.RequestId,
                ["idempotencyKey"] = request.IdempotencyKey,
            });

        return Task.FromResult(new AdapterExecutionResult(
            candidate,
            [payloadEvidence, attributionEvidence],
            Array.Empty<CanonicalIngestionIssue>(),
            BuildExecution(startedAt, stopwatch.ElapsedMilliseconds)));
    }

    private ManualIngestionRequest? Deserialize(IngestionRequestEnvelope request, ICollection<CanonicalIngestionIssue> issues)
    {
        try
        {
            return JsonSerializer.Deserialize<ManualIngestionRequest>(request.RawPayloadJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            issues.Add(BuildError("invalid_manual_payload", ex.Message, false, null));
            return null;
        }
    }

    private static void Require(string? value, string field, ICollection<CanonicalIngestionIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        issues.Add(BuildError($"missing_{field}", $"{field} is required.", false, field));
    }

    private static CanonicalSourceEvidence BuildPayloadEvidence(IngestionRequestEnvelope request) =>
        new(
            $"manual-payload-{request.RequestId}",
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

    private static CanonicalIngestionIssue BuildError(string code, string message, bool retryable, string? field)
        => new(code, message, IngestionIssueSeverity.Error, retryable, field, new Dictionary<string, string?>());
}
