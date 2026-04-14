using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeUP.Domain.Media;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Media;

public sealed class EfFlyerEvidenceRepository : IFlyerEvidenceRepository
{
    private readonly WeUpDbContext _db;

    public EfFlyerEvidenceRepository(WeUpDbContext db)
    {
        _db = db;
    }

    public async Task<FlyerEvidenceRecord> SaveAsync(FlyerEvidenceRecord record, CancellationToken ct = default)
    {
        var existing = await _db.FlyerEvidence.FirstOrDefaultAsync(x => x.EvidenceId == record.EvidenceId, ct);
        if (existing is null)
        {
            _db.FlyerEvidence.Add(ToEntity(record));
        }
        else
        {
            UpdateEntity(existing, record);
        }

        await _db.SaveChangesAsync(ct);
        return record;
    }

    public async Task<FlyerEvidenceRecord?> GetAsync(string evidenceId, CancellationToken ct = default)
    {
        var entity = await _db.FlyerEvidence.AsNoTracking().FirstOrDefaultAsync(x => x.EvidenceId == evidenceId, ct);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<FlyerEvidenceRecord?> GetByAssetIdAsync(string assetId, CancellationToken ct = default)
    {
        var entity = await _db.FlyerEvidence.AsNoTracking().FirstOrDefaultAsync(x => x.AssetId == assetId, ct);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<FlyerEvidenceRecord?> GetByIngestionJobIdAsync(string ingestionJobId, CancellationToken ct = default)
    {
        var entity = await _db.FlyerEvidence.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IngestionJobId == ingestionJobId, ct);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<FlyerEvidenceRecord[]> ListByEventIdAsync(string eventId, CancellationToken ct = default)
    {
        var entities = await _db.FlyerEvidence.AsNoTracking()
            .Where(x => x.EventId == eventId || x.CanonicalEventId == eventId)
            .OrderByDescending(x => x.CreatedAt)
            .ToArrayAsync(ct);

        return entities.Select(ToDomain).ToArray();
    }

    public async Task<FlyerEvidenceRecord[]> ListBySubmissionIdAsync(string submissionId, CancellationToken ct = default)
    {
        var entities = await _db.FlyerEvidence.AsNoTracking()
            .Where(x => x.SubmissionId == submissionId)
            .OrderByDescending(x => x.CreatedAt)
            .ToArrayAsync(ct);

        return entities.Select(ToDomain).ToArray();
    }

    public async Task<FlyerEvidenceRecord[]> ListPendingAsync(CancellationToken ct = default)
    {
        var entities = await _db.FlyerEvidence.AsNoTracking()
            .Where(x => x.Status == EvidenceStatus.Pending.ToString())
            .OrderByDescending(x => x.CreatedAt)
            .ToArrayAsync(ct);

        return entities.Select(ToDomain).ToArray();
    }

    public Task<FlyerEvidenceRecord> UpdateAsync(FlyerEvidenceRecord record, CancellationToken ct = default) => SaveAsync(record, ct);

    private static FlyerEvidenceEntity ToEntity(FlyerEvidenceRecord record) => new()
    {
        Id = Guid.NewGuid(),
        EvidenceId = record.EvidenceId,
        AssetId = record.AssetId,
        OriginalAssetId = record.OriginalAssetId,
        ProvenanceId = record.ProvenanceId,
        EventId = record.EventId,
        SubmissionId = record.SubmissionId,
        FlyerType = record.FlyerType.ToString(),
        Status = record.Status.ToString(),
        OcrText = record.OcrText,
        OcrExtractionId = record.OcrExtractionId,
        OcrEngineVersion = record.OcrEngineVersion,
        OcrBlocksJson = record.OcrBlocksJson,
        OcrReady = record.OcrReady,
        ConfidenceScore = record.ConfidenceScore,
        DerivativeAssetIdsJson = SerializeArray(record.DerivativeAssetIds),
        ProcessingHistoryJson = SerializeArray(record.ProcessingHistory),
        ValidationFailuresJson = SerializeArray(record.ValidationFailures),
        IngestionJobId = record.IngestionJobId,
        ModerationItemId = record.ModerationItemId,
        CanonicalEventId = record.CanonicalEventId,
        LinkedWorkflowIdsJson = SerializeArray(record.LinkedWorkflowIds),
        NormalizationRunId = record.NormalizationRunId,
        NormalizationVersion = record.NormalizationVersion,
        NormalizationSnapshotJson = record.NormalizationSnapshotJson,
        ReviewReasonsJson = SerializeArray(record.ReviewReasons),
        CreatedAt = record.CreatedAt,
        ReviewNotes = record.ReviewNotes,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static void UpdateEntity(FlyerEvidenceEntity entity, FlyerEvidenceRecord record)
    {
        entity.AssetId = record.AssetId;
        entity.OriginalAssetId = record.OriginalAssetId;
        entity.ProvenanceId = record.ProvenanceId;
        entity.EventId = record.EventId;
        entity.SubmissionId = record.SubmissionId;
        entity.FlyerType = record.FlyerType.ToString();
        entity.Status = record.Status.ToString();
        entity.OcrText = record.OcrText;
        entity.OcrExtractionId = record.OcrExtractionId;
        entity.OcrEngineVersion = record.OcrEngineVersion;
        entity.OcrBlocksJson = record.OcrBlocksJson;
        entity.OcrReady = record.OcrReady;
        entity.ConfidenceScore = record.ConfidenceScore;
        entity.DerivativeAssetIdsJson = SerializeArray(record.DerivativeAssetIds);
        entity.ProcessingHistoryJson = SerializeArray(record.ProcessingHistory);
        entity.ValidationFailuresJson = SerializeArray(record.ValidationFailures);
        entity.IngestionJobId = record.IngestionJobId;
        entity.ModerationItemId = record.ModerationItemId;
        entity.CanonicalEventId = record.CanonicalEventId;
        entity.LinkedWorkflowIdsJson = SerializeArray(record.LinkedWorkflowIds);
        entity.NormalizationRunId = record.NormalizationRunId;
        entity.NormalizationVersion = record.NormalizationVersion;
        entity.NormalizationSnapshotJson = record.NormalizationSnapshotJson;
        entity.ReviewReasonsJson = SerializeArray(record.ReviewReasons);
        entity.ReviewNotes = record.ReviewNotes;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static FlyerEvidenceRecord ToDomain(FlyerEvidenceEntity entity)
    {
        Enum.TryParse<FlyerType>(entity.FlyerType, true, out var flyerType);
        Enum.TryParse<EvidenceStatus>(entity.Status, true, out var status);

        return new FlyerEvidenceRecord
        {
            EvidenceId = entity.EvidenceId,
            AssetId = entity.AssetId,
            OriginalAssetId = entity.OriginalAssetId,
            ProvenanceId = entity.ProvenanceId,
            EventId = entity.EventId,
            SubmissionId = entity.SubmissionId,
            FlyerType = flyerType,
            Status = status,
            OcrText = entity.OcrText,
            OcrExtractionId = entity.OcrExtractionId,
            OcrEngineVersion = entity.OcrEngineVersion,
            OcrBlocksJson = entity.OcrBlocksJson,
            OcrReady = entity.OcrReady,
            ConfidenceScore = entity.ConfidenceScore,
            DerivativeAssetIds = DeserializeArray(entity.DerivativeAssetIdsJson),
            ProcessingHistory = DeserializeArray(entity.ProcessingHistoryJson),
            ValidationFailures = DeserializeArray(entity.ValidationFailuresJson),
            IngestionJobId = entity.IngestionJobId,
            ModerationItemId = entity.ModerationItemId,
            CanonicalEventId = entity.CanonicalEventId,
            LinkedWorkflowIds = DeserializeArray(entity.LinkedWorkflowIdsJson),
            NormalizationRunId = entity.NormalizationRunId,
            NormalizationVersion = entity.NormalizationVersion,
            NormalizationSnapshotJson = entity.NormalizationSnapshotJson,
            ReviewReasons = DeserializeArray(entity.ReviewReasonsJson),
            CreatedAt = entity.CreatedAt,
            ReviewNotes = entity.ReviewNotes,
        };
    }

    private static string SerializeArray(string[] values) => JsonSerializer.Serialize(values);

    private static string[] DeserializeArray(string? values)
    {
        if (string.IsNullOrWhiteSpace(values))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(values) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
