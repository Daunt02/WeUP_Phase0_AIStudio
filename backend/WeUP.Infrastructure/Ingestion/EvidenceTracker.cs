using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Ingestion;

/// <summary>
/// EF Core backed implementation of <see cref="IEvidenceTracker"/>.
/// Stores candidate-level evidence in the table <c>ingestion_evidence</c>.
/// </summary>
public sealed class EvidenceTracker : IEvidenceTracker
{
    private readonly WeUpDbContext _db;
    private readonly ILogger<EvidenceTracker> _log;
    private readonly ConfidenceOptions _options;

    public EvidenceTracker(
        WeUpDbContext db,
        ILogger<EvidenceTracker> log,
        IOptions<ConfidenceOptions> options)
    {
        _db = db;
        _log = log;
        _options = options.Value;
    }

    public async Task AttachEvidenceAsync(EvidenceRecord record)
    {
        var exists = await _db.IngestionEvidence.AnyAsync(e =>
            e.CandidateId == record.CandidateId &&
            e.Source == record.Source &&
            e.RawMatchedText == record.RawMatchedText);

        if (exists)
        {
            _log.LogDebug(
                "Duplicate evidence ignored for Candidate {CandidateId}, Source {Source}",
                record.CandidateId,
                record.Source);
            return;
        }

        var entity = new IngestionEvidenceEntity
        {
            Id = Guid.NewGuid(),
            CandidateId = record.CandidateId,
            Source = record.Source,
            RawMatchedText = record.RawMatchedText,
            LocalConfidence = record.LocalConfidence,
            CreatedAtUtc = DateTimeOffset.UtcNow,

            // Backward compatibility for existing ingestion orchestration mapping.
            JobId = string.Empty,
            EvidenceId = Guid.NewGuid().ToString("N"),
            Kind = "candidate-evidence",
            Reference = record.Source,
            MetadataJson = "{}",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.IngestionEvidence.Add(entity);
        await _db.SaveChangesAsync();

        _log.LogInformation(
            "Evidence attached: Candidate {CandidateId}, Source {Source}",
            record.CandidateId,
            record.Source);
    }

    public async Task<ConfidenceVector> CalculateVectorAsync(Guid candidateId)
    {
        var evidences = await _db.IngestionEvidence
            .Where(e => e.CandidateId == candidateId)
            .ToListAsync();

        if (!evidences.Any())
        {
            return new ConfidenceVector(0f, 0f, 0f);
        }

        var temporal = evidences
            .Where(e => e.Source.Contains("date", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.LocalConfidence);
        var spatial = evidences
            .Where(e => e.Source.Contains("loc", StringComparison.OrdinalIgnoreCase) ||
                        e.Source.Contains("venue", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.LocalConfidence);
        var semantic = evidences
            .Where(e => !e.Source.Contains("date", StringComparison.OrdinalIgnoreCase) &&
                        !e.Source.Contains("loc", StringComparison.OrdinalIgnoreCase) &&
                        !e.Source.Contains("venue", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.LocalConfidence);

        var temporalConf = temporal.Any() ? temporal.Max() : 0f;
        var spatialConf = spatial.Any() ? spatial.Max() : 0f;
        var semanticConf = semantic.Any() ? semantic.Max() : 0f;

        var vector = new ConfidenceVector(temporalConf, spatialConf, semanticConf);
        var threshold = _options.ReviewThreshold;

        if (temporalConf < threshold || spatialConf < threshold || semanticConf < threshold)
        {
            _log.LogDebug(
                "Candidate {CandidateId} confidence below threshold {Threshold}: ({Temporal}, {Spatial}, {Semantic})",
                candidateId,
                threshold,
                temporalConf,
                spatialConf,
                semanticConf);
        }

        return vector;
    }
}
