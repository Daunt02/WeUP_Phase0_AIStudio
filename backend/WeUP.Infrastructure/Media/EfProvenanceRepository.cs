using Microsoft.EntityFrameworkCore;
using WeUP.Domain.Media;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Media;

public sealed class EfProvenanceRepository : IProvenanceRepository
{
    private readonly WeUpDbContext _db;

    public EfProvenanceRepository(WeUpDbContext db)
    {
        _db = db;
    }

    public async Task<ProvenanceRecord> SaveAsync(ProvenanceRecord record, CancellationToken ct = default)
    {
        var existing = await _db.FlyerProvenances.FirstOrDefaultAsync(x => x.ProvenanceId == record.ProvenanceId, ct);
        if (existing is null)
        {
            _db.FlyerProvenances.Add(ToEntity(record));
        }
        else
        {
            UpdateEntity(existing, record);
        }

        await _db.SaveChangesAsync(ct);
        return record;
    }

    public async Task<ProvenanceRecord?> GetAsync(string provenanceId, CancellationToken ct = default)
    {
        var entity = await _db.FlyerProvenances.AsNoTracking().FirstOrDefaultAsync(x => x.ProvenanceId == provenanceId, ct);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<ProvenanceRecord?> GetByAssetIdAsync(string assetId, CancellationToken ct = default)
    {
        var entity = await _db.FlyerProvenances.AsNoTracking().FirstOrDefaultAsync(x => x.AssetId == assetId, ct);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<ProvenanceRecord[]> ListBySubmitterHashAsync(string submitterHash, CancellationToken ct = default)
    {
        var entities = await _db.FlyerProvenances.AsNoTracking()
            .Where(x => x.SubmitterHash == submitterHash)
            .OrderByDescending(x => x.RecordedAt)
            .ToArrayAsync(ct);

        return entities.Select(ToDomain).ToArray();
    }

    private static FlyerProvenanceEntity ToEntity(ProvenanceRecord record) => new()
    {
        Id = Guid.NewGuid(),
        ProvenanceId = record.ProvenanceId,
        AssetId = record.AssetId,
        SourceTier = record.SourceTier.ToString(),
        UploaderUserId = record.UploaderUserId,
        UploadOrigin = record.UploadOrigin.ToString(),
        SourceType = record.SourceType.ToString(),
        SubmitterHash = record.SubmitterHash,
        RecordedAt = record.RecordedAt,
        BaselineAuthority = record.BaselineAuthority,
        SourceUrl = record.SourceUrl,
        SubmissionId = record.SubmissionId,
        IngestionJobId = record.IngestionJobId,
        PartnerProvider = record.PartnerProvider,
        SubmitterNote = record.SubmitterNote,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static void UpdateEntity(FlyerProvenanceEntity entity, ProvenanceRecord record)
    {
        entity.AssetId = record.AssetId;
        entity.SourceTier = record.SourceTier.ToString();
        entity.UploaderUserId = record.UploaderUserId;
        entity.UploadOrigin = record.UploadOrigin.ToString();
        entity.SourceType = record.SourceType.ToString();
        entity.SubmitterHash = record.SubmitterHash;
        entity.RecordedAt = record.RecordedAt;
        entity.BaselineAuthority = record.BaselineAuthority;
        entity.SourceUrl = record.SourceUrl;
        entity.SubmissionId = record.SubmissionId;
        entity.IngestionJobId = record.IngestionJobId;
        entity.PartnerProvider = record.PartnerProvider;
        entity.SubmitterNote = record.SubmitterNote;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static ProvenanceRecord ToDomain(FlyerProvenanceEntity entity)
    {
        Enum.TryParse<SourceTier>(entity.SourceTier, true, out var sourceTier);
        Enum.TryParse<FlyerUploadOrigin>(entity.UploadOrigin, true, out var uploadOrigin);
        Enum.TryParse<FlyerSourceType>(entity.SourceType, true, out var sourceType);

        return new ProvenanceRecord
        {
            ProvenanceId = entity.ProvenanceId,
            AssetId = entity.AssetId,
            SourceTier = sourceTier,
            UploaderUserId = entity.UploaderUserId,
            UploadOrigin = uploadOrigin,
            SourceType = sourceType,
            SubmitterHash = entity.SubmitterHash,
            RecordedAt = entity.RecordedAt,
            BaselineAuthority = entity.BaselineAuthority,
            SourceUrl = entity.SourceUrl,
            SubmissionId = entity.SubmissionId,
            IngestionJobId = entity.IngestionJobId,
            PartnerProvider = entity.PartnerProvider,
            SubmitterNote = entity.SubmitterNote,
        };
    }
}
