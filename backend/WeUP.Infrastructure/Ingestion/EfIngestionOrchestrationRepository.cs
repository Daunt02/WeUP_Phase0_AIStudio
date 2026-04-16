using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Ocr;
using WeUP.Domain.Ingestion;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Ingestion;

public sealed class EfIngestionOrchestrationRepository(WeUpDbContext db) : IIngestionOrchestrationRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IngestionJob> CreateAsync(IngestionJob job, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new IngestionJobEntity
        {
            Id = Guid.NewGuid(),
            JobId = job.JobId,
            RequestId = job.RequestId,
            SourceKind = job.SourceType.ToString(),
            SourceRef = job.Payload?.ContentSha256 ?? $"request:{job.RequestId}",
            SubmittedBy = job.SubmittedBy,
            IdempotencyKey = job.RequestId,
            RawPayloadJson = JsonSerializer.Serialize(job.Payload, JsonOptions),
            MetadataJson = SerializeMetadata(job),
            Status = job.Status.ToString(),
            FailureReason = job.ErrorMessage,
            IssuesJson = "[]",
            AdapterKey = "ingestion-orchestrator-v1",
            AdapterVersion = "1.0",
            IsRetrySafe = true,
            AttemptCount = job.OcrAttemptCount,
            CandidateEventId = job.Normalized?.Title,
            CreatedAt = job.CreatedAtUtc,
            UpdatedAt = job.UpdatedAtUtc,
            CompletedAt = job.CompletedAtUtc,
        };

        db.IngestionJobs.Add(entity);
        foreach (var step in job.Lifecycle)
        {
            db.IngestionAudits.Add(new IngestionAuditEntity
            {
                Id = Guid.NewGuid(),
                JobId = job.JobId,
                Stage = step.Status.ToString(),
                Detail = step.Detail,
                Timestamp = step.TimestampUtc,
            });
        }

        await db.SaveChangesAsync(ct);
        return job;
    }

    public async Task SaveAsync(IngestionJob job, CancellationToken ct = default)
    {
        var entity = await db.IngestionJobs.FirstOrDefaultAsync(x => x.JobId == job.JobId, ct)
            ?? throw new InvalidOperationException($"Ingestion job '{job.JobId}' was not found.");

        entity.SourceKind = job.SourceType.ToString();
        entity.SourceRef = job.Payload?.ContentSha256 ?? $"request:{job.RequestId}";
        entity.SubmittedBy = job.SubmittedBy;
        entity.RawPayloadJson = JsonSerializer.Serialize(job.Payload, JsonOptions);
        entity.MetadataJson = SerializeMetadata(job);
        entity.Status = job.Status.ToString();
        entity.FailureReason = job.ErrorMessage;
        entity.AttemptCount = job.OcrAttemptCount;
        entity.CandidateEventId = job.Normalized?.Title;
        entity.UpdatedAt = job.UpdatedAtUtc;
        entity.CompletedAt = job.CompletedAtUtc;

        var audits = db.IngestionAudits.Where(x => x.JobId == job.JobId);
        db.IngestionAudits.RemoveRange(audits);
        foreach (var step in job.Lifecycle)
        {
            db.IngestionAudits.Add(new IngestionAuditEntity
            {
                Id = Guid.NewGuid(),
                JobId = job.JobId,
                Stage = step.Status.ToString(),
                Detail = step.Detail,
                Timestamp = step.TimestampUtc,
            });
        }

        var evidenceEntities = db.IngestionEvidence.Where(x => x.JobId == job.JobId);
        db.IngestionEvidence.RemoveRange(evidenceEntities);
        foreach (var evidence in job.Evidence)
        {
            var metadata = new Dictionary<string, string?>(evidence.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["stage"] = evidence.Stage,
            };

            db.IngestionEvidence.Add(new IngestionEvidenceEntity
            {
                Id = Guid.NewGuid(),
                JobId = job.JobId,
                EvidenceId = evidence.EvidenceId,
                Kind = evidence.Kind,
                Reference = evidence.Reference,
                MimeType = null,
                Payload = evidence.Payload,
                Confidence = 0,
                MetadataJson = JsonSerializer.Serialize(metadata, JsonOptions),
                CreatedAt = evidence.ObservedAtUtc,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IngestionJob?> GetAsync(string jobId, CancellationToken ct = default)
    {
        var entity = await db.IngestionJobs.FirstOrDefaultAsync(x => x.JobId == jobId, ct);
        if (entity is null)
        {
            return null;
        }

        var audits = await db.IngestionAudits
            .Where(x => x.JobId == jobId)
            .OrderBy(x => x.Timestamp)
            .ToListAsync(ct);

        var evidenceEntities = await db.IngestionEvidence
            .Where(x => x.JobId == jobId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

        var metadataJson = ParseMetadata(entity.MetadataJson);
        var payload = Deserialize<RawIngestionPayload>(metadataJson, "payload")
            ?? JsonSerializer.Deserialize<RawIngestionPayload>(entity.RawPayloadJson, JsonOptions);
        var ocr = Deserialize<OcrResult>(metadataJson, "ocr");
        var normalized = Deserialize<NormalizedPayloadSnapshot>(metadataJson, "normalized");
        var jobMetadata = Deserialize<Dictionary<string, string?>>(metadataJson, "jobMetadata")
            ?? new Dictionary<string, string?>();

        var status = Enum.TryParse<IngestionJobStatus>(entity.Status, true, out var parsedStatus)
            ? parsedStatus
            : IngestionJobStatus.FAILED;

        var sourceType = Enum.TryParse<IngestionSourceType>(entity.SourceKind, true, out var parsedSource)
            ? parsedSource
            : IngestionSourceType.ManualEntry;

        var lifecycle = audits
            .Select(step => new IngestionStatusRecord(
                Enum.TryParse<IngestionJobStatus>(step.Stage, true, out var stageStatus) ? stageStatus : status,
                step.Timestamp,
                step.Detail))
            .ToArray();

        var evidence = evidenceEntities
            .Select(item =>
            {
                var evidenceMetadata = JsonSerializer.Deserialize<Dictionary<string, string?>>(item.MetadataJson, JsonOptions)
                    ?? new Dictionary<string, string?>();
                var stage = evidenceMetadata.TryGetValue("stage", out var stageValue) && !string.IsNullOrWhiteSpace(stageValue)
                    ? stageValue
                    : item.Kind;

                return new IngestionEvidenceRecord(
                    EvidenceId: item.EvidenceId,
                    Stage: stage,
                    Kind: item.Kind,
                    Reference: item.Reference,
                    Payload: item.Payload,
                    ObservedAtUtc: item.CreatedAt,
                    Metadata: evidenceMetadata);
            })
            .ToArray();

        return new IngestionJob(
            JobId: entity.JobId,
            RequestId: entity.RequestId,
            SourceType: sourceType,
            SubmittedBy: entity.SubmittedBy,
            Status: status,
            Payload: payload,
            Ocr: ocr,
            Normalized: normalized,
            Evidence: evidence,
            Lifecycle: lifecycle,
            OcrAttemptCount: entity.AttemptCount,
            ErrorMessage: entity.FailureReason,
            Metadata: jobMetadata,
            CreatedAtUtc: entity.CreatedAt,
            UpdatedAtUtc: entity.UpdatedAt,
            CompletedAtUtc: entity.CompletedAt);
    }

    private static string SerializeMetadata(IngestionJob job)
    {
        var envelope = new Dictionary<string, object?>
        {
            ["jobMetadata"] = job.Metadata,
            ["payload"] = job.Payload,
            ["ocr"] = job.Ocr,
            ["normalized"] = job.Normalized,
        };

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    private static JsonElement ParseMetadata(string metadataJson)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson);
        return doc.RootElement.Clone();
    }

    private static T? Deserialize<T>(JsonElement json, string propertyName)
    {
        if (!json.TryGetProperty(propertyName, out var property))
        {
            return default;
        }

        return property.Deserialize<T>(JsonOptions);
    }
}
