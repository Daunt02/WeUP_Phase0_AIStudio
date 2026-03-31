using System.Collections.Concurrent;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;

namespace WeUP.Infrastructure.Ingestion;

/// <summary>
/// In-memory ingestion job repository for Phase 0 development.
/// Replace with EF-backed implementation when persistence is required.
/// </summary>
public sealed class InMemoryIngestionJobRepository : IIngestionJobRepository
{
    private sealed class JobRecord
    {
        public string JobId { get; init; } = string.Empty;
        public IngestionJobStatus Status { get; set; }
        public string? CandidateEventId { get; set; }
        public string? FailureReason { get; set; }
    }

    private readonly ConcurrentDictionary<string, JobRecord> _jobs = new();

    public Task<string> CreateJobAsync(IngestionSourceKind kind, string sourceRef, CancellationToken ct = default)
    {
        var jobId = Guid.NewGuid().ToString("N");
        _jobs[jobId] = new JobRecord { JobId = jobId, Status = IngestionJobStatus.Queued };
        return Task.FromResult(jobId);
    }

    public Task UpdateStatusAsync(string jobId, IngestionJobStatus status, string? failureReason = null, CancellationToken ct = default)
    {
        if (_jobs.TryGetValue(jobId, out var record))
        {
            record.Status = status;
            if (failureReason is not null) record.FailureReason = failureReason;
        }
        return Task.CompletedTask;
    }

    public Task SetCandidateAsync(string jobId, string candidateEventId, CancellationToken ct = default)
    {
        if (_jobs.TryGetValue(jobId, out var record))
            record.CandidateEventId = candidateEventId;
        return Task.CompletedTask;
    }

    public Task<IngestionJobResponse?> GetJobAsync(string jobId, CancellationToken ct = default)
    {
        _jobs.TryGetValue(jobId, out var record);
        return Task.FromResult(record is null ? null : new IngestionJobResponse(
            record.JobId, record.Status, record.CandidateEventId, record.FailureReason));
    }
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
