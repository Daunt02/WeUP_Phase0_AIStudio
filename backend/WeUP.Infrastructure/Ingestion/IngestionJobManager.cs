// ------------------------------------------------------------
// File: WeUP.Infrastructure/Ingestion/IngestionJobManager.cs
// M1-P05 – Ingestion Job Lifecycle and State Tracking v1.0
// ------------------------------------------------------------
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Ingestion;

/// <summary>
/// EF Core–backed implementation of <see cref="IIngestionJobManager"/> that enforces
/// monotonic state transitions and appends an audit record on every change.
/// </summary>
public sealed class IngestionJobManager : IIngestionJobManager
{
    // Allowed forward transitions; terminal states map to empty arrays.
    private static readonly IReadOnlyDictionary<IngestionJobStatus, IngestionJobStatus[]> AllowedTransitions
        = new Dictionary<IngestionJobStatus, IngestionJobStatus[]>
        {
            [IngestionJobStatus.Pending]          = new[] { IngestionJobStatus.Processing, IngestionJobStatus.FAILED },
            [IngestionJobStatus.Processing]       = new[] { IngestionJobStatus.OCR, IngestionJobStatus.Normalized, IngestionJobStatus.FAILED, IngestionJobStatus.RETRYABLE_FAILURE },
            [IngestionJobStatus.OCR]              = new[] { IngestionJobStatus.Normalized, IngestionJobStatus.FAILED, IngestionJobStatus.RETRYABLE_FAILURE },
            [IngestionJobStatus.Normalized]       = new[] { IngestionJobStatus.ReadyForDedup, IngestionJobStatus.REQUIRES_REVIEW, IngestionJobStatus.FAILED },
            [IngestionJobStatus.ReadyForDedup]    = new[] { IngestionJobStatus.Completed, IngestionJobStatus.FAILED },
            [IngestionJobStatus.REQUIRES_REVIEW]  = new[] { IngestionJobStatus.Completed, IngestionJobStatus.FAILED },
            [IngestionJobStatus.Completed]        = System.Array.Empty<IngestionJobStatus>(),
            [IngestionJobStatus.FAILED]           = System.Array.Empty<IngestionJobStatus>(),
            // RETRYABLE_FAILURE allows a retry back into Processing
            [IngestionJobStatus.RETRYABLE_FAILURE] = new[] { IngestionJobStatus.Processing },
            // Legacy ALL-CAPS states: forward-only
            [IngestionJobStatus.RECEIVED]          = new[] { IngestionJobStatus.VALIDATING, IngestionJobStatus.FAILED },
            [IngestionJobStatus.VALIDATING]        = new[] { IngestionJobStatus.NORMALIZING, IngestionJobStatus.FAILED, IngestionJobStatus.RETRYABLE_FAILURE },
            [IngestionJobStatus.NORMALIZING]       = new[] { IngestionJobStatus.CANDIDATE_CREATED, IngestionJobStatus.REQUIRES_REVIEW, IngestionJobStatus.FAILED },
            [IngestionJobStatus.CANDIDATE_CREATED] = new[] { IngestionJobStatus.Completed, IngestionJobStatus.FAILED },
        };

    private readonly WeUpDbContext _db;
    private readonly ILogger<IngestionJobManager> _log;

    public IngestionJobManager(WeUpDbContext db, ILogger<IngestionJobManager> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<IngestionJob> CreateJobAsync(IngestionRequest request, CancellationToken ct = default)
    {
        var jobId = System.Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;

        var entity = new IngestionJobEntity
        {
            Id = System.Guid.NewGuid(),
            JobId = jobId,
            RequestId = request.RequestId.ToString(),
            SourceKind = request.SourceType.ToString(),
            SourceRef = request.RawInput,
            SubmittedBy = request.SubmittedBy,
            IdempotencyKey = jobId,
            RawPayloadJson = "{}",
            MetadataJson = "{}",
            Status = IngestionJobStatus.Pending.ToString(),
            IssuesJson = "[]",
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.IngestionJobs.Add(entity);

        _db.IngestionAudits.Add(new IngestionAuditEntity
        {
            Id = System.Guid.NewGuid(),
            JobId = jobId,
            Stage = IngestionJobStatus.Pending.ToString(),
            Detail = "Job created via IIngestionJobManager.",
            Timestamp = now,
        });

        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Ingestion job {JobId} created (Pending).", jobId);

        return MapToContract(entity);
    }

    public async Task TransitionJobAsync(
        string jobId,
        IngestionJobStatus newStatus,
        string? failureReason = null,
        CancellationToken ct = default)
    {
        var entity = await _db.IngestionJobs
                              .FirstOrDefaultAsync(j => j.JobId == jobId, ct);

        if (entity is null)
            throw new InvalidOperationException($"Ingestion job '{jobId}' not found.");

        if (!Enum.TryParse<IngestionJobStatus>(entity.Status, ignoreCase: true, out var current))
            throw new InvalidOperationException($"Ingestion job '{jobId}' has unrecognised status '{entity.Status}'.");

        if (!AllowedTransitions.TryGetValue(current, out var allowed) ||
            !System.Array.Exists(allowed, s => s == newStatus))
        {
            _log.LogError(
                "Invalid state transition for job {JobId}: {From} => {To}",
                jobId, current, newStatus);
            throw new InvalidOperationException(
                $"Transition from '{current}' to '{newStatus}' is not allowed.");
        }

        var now = DateTimeOffset.UtcNow;

        entity.Status = newStatus.ToString();
        entity.UpdatedAt = now;

        if (failureReason is not null)
            entity.FailureReason = failureReason;

        if (newStatus is IngestionJobStatus.Completed
                      or IngestionJobStatus.FAILED
                      or IngestionJobStatus.RETRYABLE_FAILURE
                      or IngestionJobStatus.REQUIRES_REVIEW)
            entity.CompletedAt = now;

        _db.IngestionAudits.Add(new IngestionAuditEntity
        {
            Id = System.Guid.NewGuid(),
            JobId = jobId,
            Stage = newStatus.ToString(),
            Detail = failureReason ?? $"Transitioned from {current}.",
            Timestamp = now,
        });

        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "Ingestion job {JobId} transitioned {From} => {To}.",
            jobId, current, newStatus);
    }

    public async Task<IngestionJob?> GetJobAsync(string jobId, CancellationToken ct = default)
    {
        var entity = await _db.IngestionJobs
                              .AsNoTracking()
                              .FirstOrDefaultAsync(j => j.JobId == jobId, ct);

        return entity is null ? null : MapToContract(entity);
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private static IngestionJob MapToContract(IngestionJobEntity e)
    {
        var status = Enum.TryParse<IngestionJobStatus>(e.Status, ignoreCase: true, out var parsed)
            ? parsed
            : IngestionJobStatus.FAILED;

        var sourceType = Enum.TryParse<IngestionSourceType>(e.SourceKind, ignoreCase: true, out var parsedSource)
            ? parsedSource
            : IngestionSourceType.ManualEntry;

        return new IngestionJob(
            JobId: e.JobId,
            RequestId: e.RequestId,
            SourceType: sourceType,
            SubmittedBy: e.SubmittedBy,
            Status: status,
            Payload: null,
            Ocr: null,
            Normalized: null,
            Evidence: System.Array.Empty<IngestionEvidenceRecord>(),
            Lifecycle: new[] { new IngestionStatusRecord(status, e.UpdatedAt) },
            OcrAttemptCount: e.AttemptCount,
            ErrorMessage: e.FailureReason,
            Metadata: new Dictionary<string, string?>(),
            CreatedAtUtc: e.CreatedAt,
            UpdatedAtUtc: e.UpdatedAt,
            CompletedAtUtc: e.CompletedAt);
    }
}
