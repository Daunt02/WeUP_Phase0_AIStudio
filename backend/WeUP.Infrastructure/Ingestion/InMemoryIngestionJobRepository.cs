using System.Collections.Concurrent;
using System.Text.Json;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;
using WeUP.Infrastructure.Seed;

namespace WeUP.Infrastructure.Ingestion;

/// <summary>
/// In-memory ingestion job repository for Phase 0 development.
/// Replace with EF-backed implementation when persistence is required.
/// </summary>
public sealed class InMemoryIngestionJobRepository : IIngestionJobRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, IngestionResult> _jobs = new();
    private int _sequence;

    public void Reset(Phase0SeedDataset dataset)
    {
        _jobs.Clear();
        foreach (var job in dataset.IngestionJobs)
        {
            var createdAt = DateTimeOffset.UtcNow;
            var status = MapSeedStatus(job.Status);
            var request = new IngestionRequestEnvelope(
                RequestId: $"seed-{job.JobId}",
                SourceKind: Enum.TryParse<IngestionSourceKind>(job.SourceKind, true, out var kind) ? kind : IngestionSourceKind.ExternalFeed,
                SourceReference: job.SourceRef,
                SubmittedBy: "seed",
                RawPayloadJson: JsonSerializer.Serialize(new { job.SourceKind, job.SourceRef }, JsonOptions),
                IdempotencyKey: $"seed-{job.JobId}",
                ReceivedAtUtc: createdAt,
                Metadata: new Dictionary<string, string?>
                {
                    ["seeded"] = "true",
                });

            var issues = string.IsNullOrWhiteSpace(job.FailureReason)
                ? Array.Empty<CanonicalIngestionIssue>()
                :
                [
                    new CanonicalIngestionIssue(
                        "seed_failure",
                        job.FailureReason,
                        IngestionIssueSeverity.Error,
                        false,
                        null,
                        new Dictionary<string, string?>())
                ];

            _jobs[job.JobId] = new IngestionResult(
                job.JobId,
                request,
                status,
                null,
                Array.Empty<CanonicalSourceEvidence>(),
                issues,
                null,
                [new IngestionStatusRecord(status, createdAt, job.FailureReason)],
                createdAt,
                createdAt);
        }

        _sequence = dataset.IngestionJobs.Length;
    }

    public Task<IngestionResult> CreateAsync(IngestionRequestEnvelope request, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var jobId = $"ing-runtime-{Interlocked.Increment(ref _sequence):000}";
        var result = new IngestionResult(
            jobId,
            request,
            IngestionJobStatus.RECEIVED,
            null,
            Array.Empty<CanonicalSourceEvidence>(),
            Array.Empty<CanonicalIngestionIssue>(),
            null,
            [new IngestionStatusRecord(IngestionJobStatus.RECEIVED, now, "Ingestion request accepted.")],
            now,
            now);

        _jobs[jobId] = result;
        return Task.FromResult(result);
    }

    public Task SaveAsync(IngestionResult result, CancellationToken ct = default)
    {
        _jobs[result.JobId] = result;
        return Task.CompletedTask;
    }

    public Task<IngestionResult?> GetAsync(string jobId, CancellationToken ct = default)
    {
        _jobs.TryGetValue(jobId, out var result);
        return Task.FromResult(result);
    }

    public IReadOnlyList<IngestionResult> SnapshotJobs()
        => _jobs.Values.OrderBy(v => v.CreatedAtUtc).ToArray();

    private static IngestionJobStatus MapSeedStatus(string status)
        => status switch
        {
            "ReviewPending" => IngestionJobStatus.REQUIRES_REVIEW,
            "Failed" => IngestionJobStatus.FAILED,
            "Queued" => IngestionJobStatus.RECEIVED,
            "Normalizing" => IngestionJobStatus.NORMALIZING,
            _ when Enum.TryParse<IngestionJobStatus>(status, true, out var parsed) => parsed,
            _ => IngestionJobStatus.REQUIRES_REVIEW,
        };
}

/// <summary>Console-logging audit writer for development.</summary>
public sealed class ConsoleIngestionAuditWriter : IIngestionAuditWriter
{
    public Task WriteAsync(string jobId, string stage, string? detail, CancellationToken ct = default)
    {
        Console.WriteLine($"[Ingestion][{jobId[..8]}] {stage}{(detail is not null ? ": " + detail : "")}");
        return Task.CompletedTask;
    }
}
