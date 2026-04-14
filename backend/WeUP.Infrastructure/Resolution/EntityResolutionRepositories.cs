using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Resolution;
using WeUP.Domain.Resolution;
using WeUP.Infrastructure.Ingestion;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Resolution;

public sealed class InMemoryEntityResolutionRepository(
    StubEventRepository events,
    InMemoryIngestionJobRepository ingestionJobs) : IEntityResolutionRepository
{
    private readonly Dictionary<string, EntityResolutionResult> _results = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public Task<ResolutionComparisonRecord[]> GetComparisonRecordsAsync(
        NormalizedEventCandidate candidate,
        int maxComparisons,
        CancellationToken ct = default)
    {
        var records = new List<ResolutionComparisonRecord>();

        foreach (var detail in events.SnapshotDetails())
        {
            var projected = new NormalizedEventCandidate(
                Title: detail.Title,
                VenueName: detail.VenueName,
                Address: detail.Address,
                StartUtc: detail.StartUtc.ToString("O", CultureInfo.InvariantCulture),
                EndUtc: detail.EndUtc?.ToString("O", CultureInfo.InvariantCulture),
                Timezone: detail.Timezone,
                Category: detail.Category,
                Description: detail.Description,
                Tags: detail.Tags,
                SourceKind: detail.SourceKind,
                SourceRef: detail.Id,
                ExtractionConfidence: detail.Confidence,
                GeocodeConfidence: 0.5,
                TemporalConfidence: 0.8,
                EvidenceRefs: [],
                ExternalSourceId: detail.Id,
                Attributes: new Dictionary<string, string?>
                {
                    ["lat"] = detail.Lat.ToString(CultureInfo.InvariantCulture),
                    ["lng"] = detail.Lng.ToString(CultureInfo.InvariantCulture),
                });

            records.Add(new ResolutionComparisonRecord(
                RecordId: detail.Id,
                RecordType: "event",
                CanonicalEventId: detail.Id,
                Candidate: projected,
                SourceRefs: [$"{detail.SourceKind}:{detail.Id}"],
                EvidenceRefs: [],
                ReviewRefs: [],
                ExistingConfidence: detail.Confidence,
                UpdatedAtUtc: detail.StartUtc,
                IsCandidateRecord: false,
                IsExistingEventRecord: true));
        }

        foreach (var job in ingestionJobs.SnapshotJobs().Where(j => j.Candidate is not null))
        {
            var normalized = ToNormalized(job.Candidate!);
            if (normalized.SourceRef.Equals(candidate.SourceRef, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            records.Add(new ResolutionComparisonRecord(
                RecordId: job.JobId,
                RecordType: "candidate",
                CanonicalEventId: null,
                Candidate: normalized,
                SourceRefs: [normalized.SourceRef],
                EvidenceRefs: normalized.EvidenceRefs ?? [],
                ReviewRefs: [],
                ExistingConfidence: normalized.ExtractionConfidence,
                UpdatedAtUtc: job.UpdatedAtUtc,
                IsCandidateRecord: true,
                IsExistingEventRecord: false));
        }

        return Task.FromResult(records
            .OrderByDescending(r => r.UpdatedAtUtc)
            .Take(Math.Max(1, maxComparisons))
            .ToArray());
    }

    public Task SaveResultAsync(EntityResolutionResult result, CancellationToken ct = default)
    {
        lock (_gate)
        {
            _results[result.ResolutionId] = result;
        }

        return Task.CompletedTask;
    }

    public Task<EntityResolutionResult?> GetResultAsync(string resolutionId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            _results.TryGetValue(resolutionId, out var result);
            return Task.FromResult(result);
        }
    }

    public Task<MergeCommitResult> CommitMergeAsync(MergeCommitCommand command, CancellationToken ct = default)
    {
        var exists = events.SnapshotDetails().Any(e => e.Id.Equals(command.Plan.CanonicalEventId, StringComparison.OrdinalIgnoreCase));
        if (!exists)
        {
            return Task.FromResult(new MergeCommitResult(
                Success: false,
                RequiresManualReview: true,
                Message: "Canonical event target not found.",
                CanonicalEventId: command.Plan.CanonicalEventId,
                AuditTrail: ["Merge commit failed: canonical target not found in in-memory store."]));
        }

        return Task.FromResult(new MergeCommitResult(
            Success: true,
            RequiresManualReview: false,
            Message: "Merge committed in in-memory mode. Provenance and audit were preserved in resolution records.",
            CanonicalEventId: command.Plan.CanonicalEventId,
            AuditTrail:
            [
                $"Merged candidate '{command.Candidate.SourceRef}' into '{command.Plan.CanonicalEventId}'.",
                "In-memory mode stores merge audit but does not persist canonical event mutations.",
            ]));
    }

    private static NormalizedEventCandidate ToNormalized(CanonicalEventCandidate candidate)
        => new(
            candidate.Title,
            candidate.VenueName,
            candidate.Address,
            candidate.StartUtc,
            candidate.EndUtc,
            candidate.Timezone,
            candidate.Category,
            candidate.Description,
            candidate.Tags,
            candidate.SourceKind,
            candidate.SourceRef,
            candidate.ExtractionConfidence,
            candidate.GeocodeConfidence,
            candidate.TemporalConfidence,
            candidate.EvidenceRefs,
            candidate.ExternalSourceId,
            candidate.Attributes);
}

public sealed class EfEntityResolutionRepository(WeUpDbContext db) : IEntityResolutionRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ResolutionComparisonRecord[]> GetComparisonRecordsAsync(
        NormalizedEventCandidate candidate,
        int maxComparisons,
        CancellationToken ct = default)
    {
        var take = Math.Clamp(maxComparisons, 1, 200);
        var records = new List<ResolutionComparisonRecord>(take * 2);

        var eventQuery = db.Events
            .AsNoTracking()
            .Include(e => e.Sources)
            .Include(e => e.Reviews)
            .OrderByDescending(e => e.UpdatedAt)
            .Take(take);

        var events = await eventQuery.ToListAsync(ct);
        foreach (var entity in events)
        {
            records.Add(new ResolutionComparisonRecord(
                RecordId: entity.Id.ToString("N"),
                RecordType: "event",
                CanonicalEventId: entity.Id.ToString("N"),
                Candidate: new NormalizedEventCandidate(
                    Title: entity.CanonicalTitle,
                    VenueName: entity.VenueName,
                    Address: entity.AddressRaw,
                    StartUtc: entity.StartUtc.ToString("O", CultureInfo.InvariantCulture),
                    EndUtc: entity.EndUtc?.ToString("O", CultureInfo.InvariantCulture),
                    Timezone: entity.Timezone,
                    Category: entity.Category,
                    Description: entity.CanonicalDescription,
                    Tags: string.IsNullOrWhiteSpace(entity.TagsCsv)
                        ? []
                        : entity.TagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    SourceKind: "existing_event",
                    SourceRef: entity.Id.ToString("N"),
                    ExtractionConfidence: entity.Confidence,
                    GeocodeConfidence: 0.8,
                    TemporalConfidence: 0.8,
                    EvidenceRefs: [],
                    ExternalSourceId: entity.Id.ToString("N"),
                    Attributes: new Dictionary<string, string?>
                    {
                        ["lat"] = entity.Latitude.ToString(CultureInfo.InvariantCulture),
                        ["lng"] = entity.Longitude.ToString(CultureInfo.InvariantCulture),
                    }),
                SourceRefs: entity.Sources.Select(s => s.SourceRef).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                EvidenceRefs: [],
                ReviewRefs: entity.Reviews.Select(r => r.Id.ToString("N")).ToArray(),
                ExistingConfidence: entity.Confidence,
                UpdatedAtUtc: entity.UpdatedAt,
                IsCandidateRecord: false,
                IsExistingEventRecord: true));
        }

        var ingestionCandidates = await db.IngestionCandidates
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

        foreach (var candidateEntity in ingestionCandidates)
        {
            var canonical = JsonSerializer.Deserialize<CanonicalEventCandidate>(candidateEntity.CandidateJson, JsonOptions);
            if (canonical is null)
            {
                continue;
            }

            var normalized = ToNormalized(canonical);
            if (normalized.SourceRef.Equals(candidate.SourceRef, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            records.Add(new ResolutionComparisonRecord(
                RecordId: candidateEntity.JobId,
                RecordType: "candidate",
                CanonicalEventId: null,
                Candidate: normalized,
                SourceRefs: [normalized.SourceRef],
                EvidenceRefs: normalized.EvidenceRefs ?? [],
                ReviewRefs: [],
                ExistingConfidence: normalized.ExtractionConfidence,
                UpdatedAtUtc: candidateEntity.CreatedAt,
                IsCandidateRecord: true,
                IsExistingEventRecord: false));
        }

        return records
            .OrderByDescending(r => r.UpdatedAtUtc)
            .Take(take)
            .ToArray();
    }

    public async Task SaveResultAsync(EntityResolutionResult result, CancellationToken ct = default)
    {
        var existing = await db.EntityResolutionRecords.FirstOrDefaultAsync(r => r.ResolutionId == result.ResolutionId, ct);
        var json = JsonSerializer.Serialize(result, JsonOptions);

        if (existing is null)
        {
            db.EntityResolutionRecords.Add(new EntityResolutionRecordEntity
            {
                Id = Guid.NewGuid(),
                ResolutionId = result.ResolutionId,
                CandidateSourceRef = result.Candidate.SourceRef,
                Status = result.Status,
                ResultJson = json,
                CanonicalEventId = result.MergePlan?.CanonicalEventId,
                CreatedAt = result.CreatedAtUtc,
                UpdatedAt = result.UpdatedAtUtc,
            });
        }
        else
        {
            existing.CandidateSourceRef = result.Candidate.SourceRef;
            existing.Status = result.Status;
            existing.ResultJson = json;
            existing.CanonicalEventId = result.MergePlan?.CanonicalEventId;
            existing.UpdatedAt = result.UpdatedAtUtc;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<EntityResolutionResult?> GetResultAsync(string resolutionId, CancellationToken ct = default)
    {
        var entity = await db.EntityResolutionRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ResolutionId == resolutionId, ct);

        if (entity is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<EntityResolutionResult>(entity.ResultJson, JsonOptions);
    }

    public async Task<MergeCommitResult> CommitMergeAsync(MergeCommitCommand command, CancellationToken ct = default)
    {
        if (!Guid.TryParse(command.Plan.CanonicalEventId, out var eventId))
        {
            return new MergeCommitResult(
                Success: false,
                RequiresManualReview: true,
                Message: "Canonical event ID is invalid.",
                CanonicalEventId: command.Plan.CanonicalEventId,
                AuditTrail: ["Merge commit blocked: invalid canonical event ID."]);
        }

        var entity = await db.Events
            .Include(e => e.Sources)
            .FirstOrDefaultAsync(e => e.Id == eventId, ct);

        if (entity is null)
        {
            return new MergeCommitResult(
                Success: false,
                RequiresManualReview: true,
                Message: "Canonical event not found.",
                CanonicalEventId: command.Plan.CanonicalEventId,
                AuditTrail: ["Merge commit blocked: canonical event not found."]);
        }

        if (!command.Plan.AutoMergeAllowed)
        {
            return new MergeCommitResult(
                Success: false,
                RequiresManualReview: true,
                Message: "Merge plan marked unsafe for auto-merge.",
                CanonicalEventId: command.Plan.CanonicalEventId,
                AuditTrail: ["Merge commit blocked by merge plan safety gate."]);
        }

        ApplyPlan(entity, command.Plan);

        var existingRefs = entity.Sources
            .Select(s => $"{s.SourceKind}|{s.SourceRef}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceRef in command.Plan.UnionedSourceRefs)
        {
            var key = $"resolution_source|{sourceRef}";
            if (!existingRefs.Contains(key))
            {
                entity.Sources.Add(new EventSourceEntity
                {
                    Id = Guid.NewGuid(),
                    EventId = entity.Id,
                    SourceKind = "resolution_source",
                    SourceRef = sourceRef,
                    IngestedAt = command.RequestedAtUtc,
                    SubmitterId = command.RequestedBy,
                });
            }
        }

        foreach (var evidenceRef in command.Plan.UnionedEvidenceRefs)
        {
            var key = $"evidence_ref|{evidenceRef}";
            if (!existingRefs.Contains(key))
            {
                entity.Sources.Add(new EventSourceEntity
                {
                    Id = Guid.NewGuid(),
                    EventId = entity.Id,
                    SourceKind = "evidence_ref",
                    SourceRef = evidenceRef,
                    IngestedAt = command.RequestedAtUtc,
                    SubmitterId = command.RequestedBy,
                });
            }
        }

        db.IngestionAudits.Add(new IngestionAuditEntity
        {
            Id = Guid.NewGuid(),
            JobId = command.ResolutionId,
            Stage = "RESOLUTION_MERGE",
            Detail = JsonSerializer.Serialize(new
            {
                command.ResolutionId,
                command.Plan.CanonicalEventId,
                command.Candidate.SourceRef,
                command.MatchReasons,
                command.Plan.MergeRationale,
                command.Plan.ManualReviewReasons,
                command.Plan.Conflicts,
            }, JsonOptions),
            Timestamp = command.RequestedAtUtc,
        });

        entity.UpdatedAt = command.RequestedAtUtc;
        await db.SaveChangesAsync(ct);

        return new MergeCommitResult(
            Success: true,
            RequiresManualReview: false,
            Message: "Merge committed successfully.",
            CanonicalEventId: command.Plan.CanonicalEventId,
            AuditTrail:
            [
                $"Merged '{command.Candidate.SourceRef}' into canonical event '{command.Plan.CanonicalEventId}'.",
                "Merged source refs and evidence refs were preserved in provenance entries.",
                "Resolution audit trail was persisted to ingestion_audit.",
            ]);
    }

    private static void ApplyPlan(EventEntity entity, MergePlan plan)
    {
        if (!string.IsNullOrWhiteSpace(plan.Title)) entity.CanonicalTitle = plan.Title;
        if (!string.IsNullOrWhiteSpace(plan.Description)) entity.CanonicalDescription = plan.Description;
        if (!string.IsNullOrWhiteSpace(plan.Category)) entity.Category = plan.Category;
        if (!string.IsNullOrWhiteSpace(plan.VenueName)) entity.VenueName = plan.VenueName;
        if (!string.IsNullOrWhiteSpace(plan.Address))
        {
            entity.AddressRaw = plan.Address;
            entity.AddressLine1 = plan.Address;
        }

        if (!string.IsNullOrWhiteSpace(plan.StartUtc) && DateTimeOffset.TryParse(plan.StartUtc, out var start))
        {
            entity.StartUtc = start;
        }

        if (!string.IsNullOrWhiteSpace(plan.EndUtc) && DateTimeOffset.TryParse(plan.EndUtc, out var end))
        {
            entity.EndUtc = end;
        }

        if (!string.IsNullOrWhiteSpace(plan.Timezone)) entity.Timezone = plan.Timezone;

        entity.Confidence = Math.Max(entity.Confidence, plan.MergedConfidence);
        entity.TagsCsv = plan.Tags.Length == 0
            ? entity.TagsCsv
            : string.Join(',', plan.Tags.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static NormalizedEventCandidate ToNormalized(CanonicalEventCandidate candidate)
        => new(
            candidate.Title,
            candidate.VenueName,
            candidate.Address,
            candidate.StartUtc,
            candidate.EndUtc,
            candidate.Timezone,
            candidate.Category,
            candidate.Description,
            candidate.Tags,
            candidate.SourceKind,
            candidate.SourceRef,
            candidate.ExtractionConfidence,
            candidate.GeocodeConfidence,
            candidate.TemporalConfidence,
            candidate.EvidenceRefs,
            candidate.ExternalSourceId,
            candidate.Attributes);
}
