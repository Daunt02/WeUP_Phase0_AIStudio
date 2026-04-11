using Microsoft.EntityFrameworkCore;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Ingestion;

public sealed class EfIngestionJobRepository : IIngestionJobRepository
{
    private readonly WeUpDbContext _db;

    public EfIngestionJobRepository(WeUpDbContext db)
    {
        _db = db;
    }

    public async Task<string> CreateJobAsync(IngestionSourceKind kind, string sourceRef, CancellationToken ct = default)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var entity = new IngestionJobEntity
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            SourceKind = kind.ToString(),
            SourceRef = sourceRef,
            Status = IngestionJobStatus.Queued.ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.IngestionJobs.Add(entity);
        await _db.SaveChangesAsync(ct);
        return jobId;
    }

    public async Task UpdateStatusAsync(string jobId, IngestionJobStatus status, string? failureReason = null, CancellationToken ct = default)
    {
        var entity = await _db.IngestionJobs.FirstOrDefaultAsync(j => j.JobId == jobId, ct);
        if (entity is null) return;
        entity.Status = status.ToString();
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        if (failureReason is not null) entity.FailureReason = failureReason;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetCandidateAsync(string jobId, string candidateEventId, CancellationToken ct = default)
    {
        var entity = await _db.IngestionJobs.FirstOrDefaultAsync(j => j.JobId == jobId, ct);
        if (entity is null) return;
        entity.CandidateEventId = candidateEventId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IngestionJobResponse?> GetJobAsync(string jobId, CancellationToken ct = default)
    {
        var entity = await _db.IngestionJobs.FirstOrDefaultAsync(j => j.JobId == jobId, ct);
        if (entity is null) return null;

        if (!Enum.TryParse<IngestionJobStatus>(entity.Status, true, out var status))
            status = IngestionJobStatus.Failed;

        return new IngestionJobResponse(entity.JobId, status, entity.CandidateEventId, entity.FailureReason);
    }
}
