using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Ingestion;

public sealed class EfIngestionJobRepository : IIngestionJobRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly WeUpDbContext _db;

    public EfIngestionJobRepository(WeUpDbContext db)
    {
        _db = db;
    }

    public async Task<IngestionResult> CreateAsync(IngestionRequestEnvelope request, CancellationToken ct = default)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var entity = new IngestionJobEntity
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            RequestId = request.RequestId,
            SourceKind = request.SourceKind.ToString(),
            SourceRef = request.SourceReference,
            SubmittedBy = request.SubmittedBy,
            IdempotencyKey = request.IdempotencyKey,
            RawPayloadJson = request.RawPayloadJson,
            MetadataJson = JsonSerializer.Serialize(request.Metadata, JsonOptions),
            Status = IngestionJobStatus.RECEIVED.ToString(),
            IssuesJson = "[]",
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.IngestionJobs.Add(entity);
        _db.IngestionAudits.Add(new IngestionAuditEntity
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            Stage = IngestionJobStatus.RECEIVED.ToString(),
            Detail = "Ingestion request accepted.",
            Timestamp = now,
        });
        await _db.SaveChangesAsync(ct);

        return new IngestionResult(
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
    }

    public async Task SaveAsync(IngestionResult result, CancellationToken ct = default)
    {
        var entity = await _db.IngestionJobs.FirstOrDefaultAsync(j => j.JobId == result.JobId, ct);
        if (entity is null)
        {
            throw new InvalidOperationException($"Ingestion job '{result.JobId}' was not found.");
        }

        entity.RequestId = result.Request.RequestId;
        entity.SourceKind = result.Request.SourceKind.ToString();
        entity.SourceRef = result.Request.SourceReference;
        entity.SubmittedBy = result.Request.SubmittedBy;
        entity.IdempotencyKey = result.Request.IdempotencyKey;
        entity.RawPayloadJson = result.Request.RawPayloadJson;
        entity.MetadataJson = JsonSerializer.Serialize(result.Request.Metadata, JsonOptions);
        entity.Status = result.Status.ToString();
        entity.FailureReason = result.FailureReason;
        entity.IssuesJson = JsonSerializer.Serialize(result.Issues, JsonOptions);
        entity.AdapterKey = result.Execution?.AdapterKey;
        entity.AdapterVersion = result.Execution?.AdapterVersion;
        entity.IsRetrySafe = result.Execution?.IsRetrySafe ?? false;
        entity.AttemptCount = result.Execution?.AttemptCount ?? 0;
        entity.CandidateEventId = result.Candidate?.ExternalSourceId ?? result.Candidate?.SourceRef;
        entity.UpdatedAt = result.UpdatedAtUtc;
        entity.CompletedAt = result.Status is IngestionJobStatus.REQUIRES_REVIEW or IngestionJobStatus.FAILED or IngestionJobStatus.RETRYABLE_FAILURE
            ? result.UpdatedAtUtc
            : null;

        var candidateEntity = await _db.IngestionCandidates.FirstOrDefaultAsync(c => c.JobId == result.JobId, ct);
        if (result.Candidate is null)
        {
            if (candidateEntity is not null)
            {
                _db.IngestionCandidates.Remove(candidateEntity);
            }
        }
        else if (candidateEntity is null)
        {
            _db.IngestionCandidates.Add(new IngestionCandidateEntity
            {
                Id = Guid.NewGuid(),
                JobId = result.JobId,
                CandidateSourceRef = result.Candidate.SourceRef,
                CandidateJson = JsonSerializer.Serialize(result.Candidate, JsonOptions),
                CreatedAt = result.UpdatedAtUtc,
            });
        }
        else
        {
            candidateEntity.CandidateSourceRef = result.Candidate.SourceRef;
            candidateEntity.CandidateJson = JsonSerializer.Serialize(result.Candidate, JsonOptions);
            candidateEntity.CreatedAt = result.UpdatedAtUtc;
        }

        var existingEvidence = _db.IngestionEvidence.Where(e => e.JobId == result.JobId);
        _db.IngestionEvidence.RemoveRange(existingEvidence);
        foreach (var evidence in result.Evidence)
        {
            _db.IngestionEvidence.Add(new IngestionEvidenceEntity
            {
                Id = Guid.NewGuid(),
                JobId = result.JobId,
                EvidenceId = evidence.EvidenceId,
                Kind = evidence.EvidenceKind,
                Reference = evidence.Reference,
                MimeType = evidence.MimeType,
                Payload = evidence.PayloadSnippet,
                Confidence = evidence.Confidence,
                MetadataJson = JsonSerializer.Serialize(evidence.Metadata, JsonOptions),
                CreatedAt = evidence.ObservedAtUtc,
            });
        }

        var existingAudits = _db.IngestionAudits.Where(a => a.JobId == result.JobId);
        _db.IngestionAudits.RemoveRange(existingAudits);
        foreach (var lifecycle in result.Lifecycle)
        {
            _db.IngestionAudits.Add(new IngestionAuditEntity
            {
                Id = Guid.NewGuid(),
                JobId = result.JobId,
                Stage = lifecycle.Status.ToString(),
                Detail = lifecycle.Detail,
                Timestamp = lifecycle.TimestampUtc,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IngestionResult?> GetAsync(string jobId, CancellationToken ct = default)
    {
        var entity = await _db.IngestionJobs.FirstOrDefaultAsync(j => j.JobId == jobId, ct);
        if (entity is null)
        {
            return null;
        }

        var candidateEntity = await _db.IngestionCandidates.FirstOrDefaultAsync(c => c.JobId == jobId, ct);
        var evidenceEntities = await _db.IngestionEvidence.Where(e => e.JobId == jobId).ToListAsync(ct);
        var auditEntities = await _db.IngestionAudits.Where(a => a.JobId == jobId).OrderBy(a => a.Timestamp).ToListAsync(ct);

        var request = new IngestionRequestEnvelope(
            entity.RequestId,
            Enum.TryParse<IngestionSourceKind>(entity.SourceKind, true, out var sourceKind) ? sourceKind : IngestionSourceKind.ExternalFeed,
            entity.SourceRef,
            entity.SubmittedBy,
            entity.RawPayloadJson,
            entity.IdempotencyKey,
            entity.CreatedAt,
            DeserializeDictionary(entity.MetadataJson));

        var status = Enum.TryParse<IngestionJobStatus>(entity.Status, true, out var parsedStatus)
            ? parsedStatus
            : IngestionJobStatus.FAILED;
        var issues = DeserializeArray<CanonicalIngestionIssue>(entity.IssuesJson);
        var candidate = candidateEntity is null
            ? null
            : JsonSerializer.Deserialize<CanonicalEventCandidate>(candidateEntity.CandidateJson, JsonOptions);
        var evidence = evidenceEntities
            .Select(e => new CanonicalSourceEvidence(
                e.EvidenceId,
                e.Kind,
                e.Reference,
                e.MimeType,
                e.Payload,
                e.CreatedAt,
                e.Confidence,
                DeserializeDictionary(e.MetadataJson)))
            .ToArray();
        var lifecycle = auditEntities
            .Select(a => new IngestionStatusRecord(
                Enum.TryParse<IngestionJobStatus>(a.Stage, true, out var auditStatus) ? auditStatus : status,
                a.Timestamp,
                a.Detail))
            .ToArray();

        AdapterExecutionMetadata? execution = null;
        if (!string.IsNullOrWhiteSpace(entity.AdapterKey) && !string.IsNullOrWhiteSpace(entity.AdapterVersion))
        {
            var normalizationStep = lifecycle.FirstOrDefault(step => step.Status == IngestionJobStatus.NORMALIZING);
            var startedAt = normalizationStep?.TimestampUtc ?? entity.CreatedAt;
            var completedAt = entity.CompletedAt ?? entity.UpdatedAt;
            var durationMs = (long)Math.Max(0, (completedAt - startedAt).TotalMilliseconds);
            execution = new AdapterExecutionMetadata(
                entity.AdapterKey,
                entity.AdapterVersion,
                entity.IsRetrySafe,
                entity.AttemptCount,
                startedAt == default ? entity.CreatedAt : startedAt,
                completedAt,
                durationMs,
                new AdapterCapabilityDescriptor(
                    entity.AdapterKey,
                    entity.AdapterKey,
                    entity.AdapterVersion,
                    [request.SourceKind],
                    entity.IsRetrySafe,
                    false,
                    Array.Empty<string>(),
                    Array.Empty<string>()));
        }

        return new IngestionResult(
            entity.JobId,
            request,
            status,
            candidate,
            evidence,
            issues,
            execution,
            lifecycle,
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private static T[] DeserializeArray<T>(string json)
        => string.IsNullOrWhiteSpace(json)
            ? Array.Empty<T>()
            : JsonSerializer.Deserialize<T[]>(json, JsonOptions) ?? Array.Empty<T>();

    private static IReadOnlyDictionary<string, string?> DeserializeDictionary(string json)
        => JsonSerializer.Deserialize<Dictionary<string, string?>>(json, JsonOptions) ?? new Dictionary<string, string?>();
}
