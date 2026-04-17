using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeUP.Contracts.Events;
using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Resolution;
using WeUP.Domain.Dedupe;
using WeUP.Domain.Resolution;
using WeUP.Infrastructure.Ingestion;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Resolution;

public sealed class InMemoryEntityResolutionRepository(
    StubEventRepository events,
    InMemoryIngestionJobRepository ingestionJobs,
    IProvenanceService provenanceService) : IEntityResolutionRepository
{
    private sealed record CanonicalEventState(
        string CanonicalEventId,
        IReadOnlyDictionary<string, string?> Fields,
        double Latitude,
        double Longitude,
        double Confidence,
        string[] SourceRefs,
        string[] EvidenceRefs,
        bool IsApproved,
        string? ExternalSourceId);

    private readonly Dictionary<string, EntityResolutionResult> _results = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ProvenanceEntry[]> _provenanceByCanonicalEventId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CanonicalEventState> _canonicalStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public Task<ResolutionComparisonRecord[]> GetComparisonRecordsAsync(
        NormalizedEventCandidate candidate,
        int maxComparisons,
        CancellationToken ct = default)
    {
        var records = new List<ResolutionComparisonRecord>();

        lock (_gate)
        {
            foreach (var detail in events.SnapshotDetails())
            {
                var state = GetOrCreateCanonicalState(detail);
                var projected = new NormalizedEventCandidate(
                    Title: GetField(state.Fields, "Title"),
                    VenueName: GetField(state.Fields, "VenueName"),
                    Address: GetField(state.Fields, "Address"),
                    StartUtc: GetField(state.Fields, "StartUtc"),
                    EndUtc: GetField(state.Fields, "EndUtc"),
                    Timezone: GetField(state.Fields, "Timezone"),
                    Category: GetField(state.Fields, "Category"),
                    Description: GetField(state.Fields, "Description"),
                    Tags: SplitTags(GetField(state.Fields, "Tags")),
                    SourceKind: detail.SourceKind,
                    SourceRef: detail.Id,
                    ExtractionConfidence: state.Confidence,
                    GeocodeConfidence: 0.5,
                    TemporalConfidence: 0.8,
                    EvidenceRefs: state.EvidenceRefs,
                    ExternalSourceId: state.ExternalSourceId,
                    Attributes: new Dictionary<string, string?>
                    {
                        ["lat"] = state.Latitude.ToString(CultureInfo.InvariantCulture),
                        ["lng"] = state.Longitude.ToString(CultureInfo.InvariantCulture),
                    });

                records.Add(new ResolutionComparisonRecord(
                    RecordId: detail.Id,
                    RecordType: "event",
                    CanonicalEventId: detail.Id,
                    Candidate: projected,
                    SourceRefs: state.SourceRefs,
                    EvidenceRefs: state.EvidenceRefs,
                    ReviewRefs: [],
                    ExistingConfidence: state.Confidence,
                    UpdatedAtUtc: ParseUpdatedAt(GetField(state.Fields, "StartUtc"), detail.StartUtc),
                    IsCandidateRecord: false,
                    IsExistingEventRecord: true));
            }
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

    public Task<ProvenanceEntry[]> GetProvenanceAsync(string canonicalEventId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(
                _provenanceByCanonicalEventId.TryGetValue(canonicalEventId, out var entries)
                    ? entries.ToArray()
                    : []);
        }
    }

    public Task<MergeCommitResult> CommitMergeAsync(MergeCommitCommand command, CancellationToken ct = default)
    {
        var detail = events.SnapshotDetails().FirstOrDefault(e => e.Id.Equals(command.Plan.CanonicalEventId, StringComparison.OrdinalIgnoreCase));
        if (detail is null)
        {
            return Task.FromResult(new MergeCommitResult(
                Success: false,
                RequiresManualReview: true,
                Message: "Canonical event target not found.",
                CanonicalEventId: command.Plan.CanonicalEventId,
                AuditTrail: ["Merge commit failed: canonical target not found in in-memory store."]));
        }

        ProvenanceEntry[] appendedEntries;

        lock (_gate)
        {
            var currentState = GetOrCreateCanonicalState(detail);
            var beforeSnapshot = ToSnapshot(currentState);
            var beforeFields = CloneFields(currentState.Fields);
            var existingEntries = _provenanceByCanonicalEventId.TryGetValue(command.Plan.CanonicalEventId, out var entries)
                ? entries
                : [];

            var nextState = ApplyPlan(currentState, command.Plan);
            var afterSnapshot = ToSnapshot(nextState);
            var afterFields = CloneFields(nextState.Fields);

            var provenanceEntry = provenanceService.CreateAppendOnlyEntry(new ProvenanceBuildCommand(
                ResolutionId: command.ResolutionId,
                CanonicalBeforeMerge: beforeSnapshot,
                CanonicalAfterMerge: afterSnapshot,
                CanonicalBeforeFields: beforeFields,
                CanonicalAfterFields: afterFields,
                Candidate: command.Candidate,
                Plan: command.Plan,
                ExistingEntries: existingEntries,
                SourceRequestIds: command.SourceRequestIds ?? [command.ResolutionId],
                CandidateIds: [command.Candidate.SourceRef],
                EvidenceBundleRefs: command.EvidenceBundleRefs ?? [],
                MergeActor: string.IsNullOrWhiteSpace(command.RequestedBy) ? "system:entity-resolution" : command.RequestedBy,
                MergeReason: BuildMergeReason(command),
                MergedAtUtc: command.RequestedAtUtc));

            appendedEntries = provenanceService.Append(existingEntries, provenanceEntry);
            _canonicalStates[command.Plan.CanonicalEventId] = nextState;
            _provenanceByCanonicalEventId[command.Plan.CanonicalEventId] = appendedEntries;
        }

        return Task.FromResult(new MergeCommitResult(
            Success: true,
            RequiresManualReview: false,
            Message: "Merge committed in in-memory mode. Provenance and audit were preserved in append-only records.",
            CanonicalEventId: command.Plan.CanonicalEventId,
            AuditTrail:
            [
                $"Merged candidate '{command.Candidate.SourceRef}' into '{command.Plan.CanonicalEventId}'.",
                "In-memory mode updated simulated canonical state and appended immutable provenance.",
            ],
            ProvenanceEntries: appendedEntries));
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

    private CanonicalEventState GetOrCreateCanonicalState(EventDetailDto detail)
    {
        if (_canonicalStates.TryGetValue(detail.Id, out var existing))
        {
            return existing;
        }

        var created = new CanonicalEventState(
            CanonicalEventId: detail.Id,
            Fields: BuildFields(
                detail.Title,
                detail.VenueName,
                detail.Address,
                detail.StartUtc.ToString("O", CultureInfo.InvariantCulture),
                detail.EndUtc?.ToString("O", CultureInfo.InvariantCulture),
                detail.Timezone,
                detail.Category,
                detail.Description,
                JoinTags(detail.Tags)),
            Latitude: detail.Lat,
            Longitude: detail.Lng,
            Confidence: detail.Confidence,
            SourceRefs: [$"{detail.SourceKind}:{detail.Id}"],
            EvidenceRefs: [],
            IsApproved: detail.Status.Equals("APPROVED", StringComparison.OrdinalIgnoreCase),
            ExternalSourceId: detail.Id);

        _canonicalStates[detail.Id] = created;
        return created;
    }

    private static CanonicalEventState ApplyPlan(CanonicalEventState currentState, MergePlan plan)
    {
        var updatedFields = CloneFields(currentState.Fields);
        SetIfProvided(updatedFields, "Title", plan.Title);
        SetIfProvided(updatedFields, "VenueName", plan.VenueName);
        SetIfProvided(updatedFields, "Address", plan.Address);
        SetIfProvided(updatedFields, "StartUtc", plan.StartUtc);
        SetIfProvided(updatedFields, "EndUtc", plan.EndUtc);
        SetIfProvided(updatedFields, "Timezone", plan.Timezone);
        SetIfProvided(updatedFields, "Category", plan.Category);
        SetIfProvided(updatedFields, "Description", plan.Description);
        if (plan.Tags.Length > 0)
        {
            updatedFields["Tags"] = JoinTags(plan.Tags);
        }

        return currentState with
        {
            Fields = updatedFields,
            Confidence = Math.Max(currentState.Confidence, plan.MergedConfidence),
            SourceRefs = MergeDistinct(currentState.SourceRefs, plan.UnionedSourceRefs),
            EvidenceRefs = MergeDistinct(currentState.EvidenceRefs, plan.UnionedEvidenceRefs),
        };
    }

    private static EventAggregateSnapshot ToSnapshot(CanonicalEventState state)
        => new(
            CanonicalEventId: state.CanonicalEventId,
            Title: GetField(state.Fields, "Title"),
            VenueName: GetField(state.Fields, "VenueName"),
            Address: GetField(state.Fields, "Address"),
            Latitude: state.Latitude,
            Longitude: state.Longitude,
            StartUtc: GetField(state.Fields, "StartUtc"),
            EndUtc: GetField(state.Fields, "EndUtc"),
            Timezone: GetField(state.Fields, "Timezone"),
            Category: GetField(state.Fields, "Category"),
            Confidence: state.Confidence,
            SourceRefs: state.SourceRefs,
            EvidenceRefs: state.EvidenceRefs,
            IsApproved: state.IsApproved,
            ExternalSourceId: state.ExternalSourceId);

    private static Dictionary<string, string?> BuildFields(
        string? title,
        string? venueName,
        string? address,
        string? startUtc,
        string? endUtc,
        string? timezone,
        string? category,
        string? description,
        string? tags)
        => new(StringComparer.Ordinal)
        {
            ["Title"] = title,
            ["VenueName"] = venueName,
            ["Address"] = address,
            ["StartUtc"] = startUtc,
            ["EndUtc"] = endUtc,
            ["Timezone"] = timezone,
            ["Category"] = category,
            ["Description"] = description,
            ["Tags"] = tags,
        };

    private static Dictionary<string, string?> CloneFields(IReadOnlyDictionary<string, string?> fields)
        => fields.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static string? GetField(IReadOnlyDictionary<string, string?> fields, string key)
        => fields.TryGetValue(key, out var value) ? value : null;

    private static void SetIfProvided(IDictionary<string, string?> fields, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields[key] = value;
        }
    }

    private static string[] SplitTags(string? tags)
        => string.IsNullOrWhiteSpace(tags)
            ? []
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? JoinTags(IEnumerable<string> tags)
    {
        var values = tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values.Length == 0 ? null : string.Join(',', values);
    }

    private static string[] MergeDistinct(IEnumerable<string> left, IEnumerable<string> right)
        => left.Concat(right)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static DateTimeOffset ParseUpdatedAt(string? value, DateTimeOffset fallback)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : fallback;

    private static string BuildMergeReason(MergeCommitCommand command)
    {
        if (command.Plan.ManualReviewReasons.Length > 0)
        {
            return string.Join(" | ", command.Plan.ManualReviewReasons);
        }

        if (command.MatchReasons.Length > 0)
        {
            return string.Join(" | ", command.MatchReasons);
        }

        return string.Join(" | ", command.Plan.MergeRationale);
    }
}

public sealed class EfEntityResolutionRepository(WeUpDbContext db, IProvenanceService provenanceService) : IEntityResolutionRepository
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

    public async Task<ProvenanceEntry[]> GetProvenanceAsync(string canonicalEventId, CancellationToken ct = default)
    {
        var jsonEntries = await db.EventMergeProvenance
            .AsNoTracking()
            .Where(entry => entry.CanonicalEventId == canonicalEventId)
            .OrderBy(entry => entry.SequenceNumber)
            .ThenBy(entry => entry.RecordedAtUtc)
            .Select(entry => entry.EntryJson)
            .ToArrayAsync(ct);

        return jsonEntries
            .Select(json => JsonSerializer.Deserialize<ProvenanceEntry>(json, JsonOptions))
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .ToArray();
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

            var existingProvenance = await GetProvenanceAsync(command.Plan.CanonicalEventId, ct);
            var beforeSnapshot = ToSnapshot(entity);
            var beforeFields = BuildFields(entity);

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

        var afterSnapshot = ToSnapshot(entity);
        var afterFields = BuildFields(entity);
        var provenanceEntry = provenanceService.CreateAppendOnlyEntry(new ProvenanceBuildCommand(
            ResolutionId: command.ResolutionId,
            CanonicalBeforeMerge: beforeSnapshot,
            CanonicalAfterMerge: afterSnapshot,
            CanonicalBeforeFields: beforeFields,
            CanonicalAfterFields: afterFields,
            Candidate: command.Candidate,
            Plan: command.Plan,
            ExistingEntries: existingProvenance,
            SourceRequestIds: command.SourceRequestIds ?? [command.ResolutionId],
            CandidateIds: [command.Candidate.SourceRef],
            EvidenceBundleRefs: command.EvidenceBundleRefs ?? [],
            MergeActor: string.IsNullOrWhiteSpace(command.RequestedBy) ? "system:entity-resolution" : command.RequestedBy,
            MergeReason: BuildMergeReason(command),
            MergedAtUtc: command.RequestedAtUtc));

        var appendedProvenance = provenanceService.Append(existingProvenance, provenanceEntry);
        db.EventMergeProvenance.Add(new EventMergeProvenanceEntity
        {
            Id = Guid.NewGuid(),
            EntryId = provenanceEntry.EntryId,
            CanonicalEventId = provenanceEntry.CanonicalEventId,
            ResolutionId = provenanceEntry.ResolutionId,
            SequenceNumber = provenanceEntry.SequenceNumber,
            RecordedAtUtc = provenanceEntry.RecordedAtUtc,
            MergeActor = provenanceEntry.MergeHistory.MergeActor,
            MergeReason = provenanceEntry.MergeHistory.MergeReason,
            EntryJson = JsonSerializer.Serialize(provenanceEntry, JsonOptions),
        });

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
            ],
            ProvenanceEntries: appendedProvenance);
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

    private static EventAggregateSnapshot ToSnapshot(EventEntity entity)
        => new(
            CanonicalEventId: entity.Id.ToString("N"),
            Title: entity.CanonicalTitle,
            VenueName: entity.VenueName,
            Address: entity.AddressRaw,
            Latitude: entity.Latitude,
            Longitude: entity.Longitude,
            StartUtc: entity.StartUtc.ToString("O", CultureInfo.InvariantCulture),
            EndUtc: entity.EndUtc?.ToString("O", CultureInfo.InvariantCulture),
            Timezone: entity.Timezone,
            Category: entity.Category,
            Confidence: entity.Confidence,
            SourceRefs: entity.Sources.Select(source => source.SourceRef).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            EvidenceRefs: entity.Sources
                .Where(source => source.SourceKind.Equals("evidence_ref", StringComparison.OrdinalIgnoreCase))
                .Select(source => source.SourceRef)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            IsApproved: entity.Status.Equals("APPROVED", StringComparison.OrdinalIgnoreCase),
            ExternalSourceId: entity.PublicId);

    private static IReadOnlyDictionary<string, string?> BuildFields(EventEntity entity)
        => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Title"] = entity.CanonicalTitle,
            ["VenueName"] = entity.VenueName,
            ["Address"] = entity.AddressRaw,
            ["StartUtc"] = entity.StartUtc.ToString("O", CultureInfo.InvariantCulture),
            ["EndUtc"] = entity.EndUtc?.ToString("O", CultureInfo.InvariantCulture),
            ["Timezone"] = entity.Timezone,
            ["Category"] = entity.Category,
            ["Description"] = entity.CanonicalDescription,
            ["Tags"] = entity.TagsCsv,
        };

    private static string BuildMergeReason(MergeCommitCommand command)
    {
        if (command.Plan.ManualReviewReasons.Length > 0)
        {
            return string.Join(" | ", command.Plan.ManualReviewReasons);
        }

        if (command.MatchReasons.Length > 0)
        {
            return string.Join(" | ", command.MatchReasons);
        }

        return string.Join(" | ", command.Plan.MergeRationale);
    }
}
