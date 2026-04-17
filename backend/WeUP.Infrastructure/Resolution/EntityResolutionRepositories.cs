using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeUP.Contracts.Events;
using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Resolution;
using WeUP.Domain.Dedupe;
using WeUP.Domain.Events;
using WeUP.Domain.Moderation;
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
    private readonly Dictionary<string, EntityResolutionResult> _results = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ProvenanceEntry[]> _provenanceByCanonicalEventId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EventAggregate> _canonicalStates = new(StringComparer.OrdinalIgnoreCase);
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
                    Title: state.Title,
                    VenueName: state.VenueName,
                    Address: state.Address.RawAddress,
                    StartUtc: state.StartUtc.ToString("O", CultureInfo.InvariantCulture),
                    EndUtc: state.EndUtc?.ToString("O", CultureInfo.InvariantCulture),
                    Timezone: state.TimeZone,
                    Category: state.Category,
                    Description: state.Description,
                    Tags: state.Tags,
                    SourceKind: detail.SourceKind,
                    SourceRef: detail.Id,
                    ExtractionConfidence: state.ConfidenceScore,
                    GeocodeConfidence: 0.5,
                    TemporalConfidence: 0.8,
                    EvidenceRefs: state.Provenance.EvidenceRefs,
                    ExternalSourceId: state.ExternalReferences.FirstOrDefault()?.ReferenceId,
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
                    SourceRefs: state.Provenance.SourceRefs,
                    EvidenceRefs: state.Provenance.EvidenceRefs,
                    ReviewRefs: [],
                    ExistingConfidence: state.ConfidenceScore,
                    UpdatedAtUtc: state.UpdatedAtUtc,
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
            var beforeFields = BuildFields(currentState);
            var existingEntries = _provenanceByCanonicalEventId.TryGetValue(command.Plan.CanonicalEventId, out var entries)
                ? entries
                : [];

            var nextState = ApplyPlan(currentState, command.Plan);
            var afterSnapshot = ToSnapshot(nextState);
            var afterFields = BuildFields(nextState);

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

    private EventAggregate GetOrCreateCanonicalState(EventDetailDto detail)
    {
        if (_canonicalStates.TryGetValue(detail.Id, out var existing))
        {
            return existing;
        }

        var created = new EventAggregate(
            CanonicalEventId: detail.Id,
            SourceEventIds: [detail.Id],
            ExternalReferences: [new ExternalEventReference("snapshot", detail.SourceKind, detail.Id)],
            Title: detail.Title,
            Description: detail.Description,
            Tags: detail.Tags,
            Category: detail.Category,
            VenueName: detail.VenueName,
            Address: new EventAddress(
                AddressLine1: detail.Address,
                City: string.Empty,
                State: null,
                PostalCode: null,
                Country: "US",
                RawAddress: detail.Address),
            Latitude: detail.Lat,
            Longitude: detail.Lng,
            TimeZone: detail.Timezone,
            StartUtc: detail.StartUtc,
            EndUtc: detail.EndUtc,
            LocalStartDisplay: null,
            LocalEndDisplay: null,
            EventStatus: EventLifecycleStatusMapper.FromStorage(detail.Status),
            PublishStatus: detail.Status.Equals("PUBLISHED", StringComparison.OrdinalIgnoreCase)
                ? EventPublishStatus.Published
                : EventPublishStatus.EligibilityPending,
            ModerationStatus: detail.Status.Equals("REJECTED", StringComparison.OrdinalIgnoreCase)
                ? EventModerationStatus.Rejected
                : EventModerationStatus.Unreviewed,
            RiskLevel: detail.Confidence >= 0.8 ? EventRiskLevel.Low : EventRiskLevel.Medium,
            ConfidenceScore: detail.Confidence,
            Provenance: new EventProvenanceMetadata(
                PrimarySourceKind: detail.SourceKind,
                PrimarySourceRef: detail.Id,
                EvidenceRefs: [],
                FirstObservedAtUtc: detail.StartUtc,
                LastObservedAtUtc: detail.StartUtc,
                SourceRefs: [$"{detail.SourceKind}:{detail.Id}"]),
            CreatedAtUtc: detail.StartUtc,
            UpdatedAtUtc: detail.StartUtc,
            Version: 1,
            MergeLineage: new EventMergeLineage(null, [], [], null, null));

        created.Validate();

        _canonicalStates[detail.Id] = created;
        return created;
    }

    private static EventAggregate ApplyPlan(EventAggregate currentState, MergePlan plan)
    {
        var startUtc = !string.IsNullOrWhiteSpace(plan.StartUtc) && DateTimeOffset.TryParse(plan.StartUtc, out var parsedStart)
            ? parsedStart
            : currentState.StartUtc;
        var endUtc = !string.IsNullOrWhiteSpace(plan.EndUtc) && DateTimeOffset.TryParse(plan.EndUtc, out var parsedEnd)
            ? parsedEnd
            : currentState.EndUtc;

        var changedFields = new List<string>();
        if (!string.IsNullOrWhiteSpace(plan.Title) && !string.Equals(plan.Title, currentState.Title, StringComparison.Ordinal))
            changedFields.Add(nameof(EventAggregate.Title));
        if (!string.IsNullOrWhiteSpace(plan.Description) && !string.Equals(plan.Description, currentState.Description, StringComparison.Ordinal))
            changedFields.Add(nameof(EventAggregate.Description));
        if (!string.IsNullOrWhiteSpace(plan.Category) && !string.Equals(plan.Category, currentState.Category, StringComparison.Ordinal))
            changedFields.Add(nameof(EventAggregate.Category));
        if (plan.Tags.Length > 0 && !plan.Tags.SequenceEqual(currentState.Tags, StringComparer.Ordinal))
            changedFields.Add(nameof(EventAggregate.Tags));
        if (!string.IsNullOrWhiteSpace(plan.VenueName) && !string.Equals(plan.VenueName, currentState.VenueName, StringComparison.Ordinal))
            changedFields.Add(nameof(EventAggregate.VenueName));
        if (!string.IsNullOrWhiteSpace(plan.Address) && !string.Equals(plan.Address, currentState.Address.RawAddress, StringComparison.Ordinal))
            changedFields.Add(nameof(EventAggregate.Address));
        if (startUtc != currentState.StartUtc || endUtc != currentState.EndUtc)
            changedFields.Add(nameof(EventAggregate.StartUtc));
        if (!string.IsNullOrWhiteSpace(plan.Timezone) && !string.Equals(plan.Timezone, currentState.TimeZone, StringComparison.Ordinal))
            changedFields.Add(nameof(EventAggregate.TimeZone));
        if (Math.Max(currentState.ConfidenceScore, plan.MergedConfidence) != currentState.ConfidenceScore)
            changedFields.Add(nameof(EventAggregate.ConfidenceScore));

        changedFields.Add(nameof(EventAggregate.Provenance));
        changedFields.Add(nameof(EventAggregate.MergeLineage));

        var updatedProvenance = currentState.Provenance with
        {
            SourceRefs = MergeDistinct(currentState.Provenance.SourceRefs, plan.UnionedSourceRefs),
            EvidenceRefs = MergeDistinct(currentState.Provenance.EvidenceRefs, plan.UnionedEvidenceRefs),
            LastObservedAtUtc = DateTimeOffset.UtcNow,
        };

        var updatedMergeLineage = currentState.MergeLineage with
        {
            MergedCanonicalEventIds = MergeDistinct(currentState.MergeLineage.MergedCanonicalEventIds, plan.UnionedSourceRefs),
            AppliedMergePlanIds = MergeDistinct(currentState.MergeLineage.AppliedMergePlanIds, [$"{plan.CanonicalEventId}:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"]),
            LastMergedAtUtc = DateTimeOffset.UtcNow,
            LastMergedBy = "entity-resolution",
        };

        return currentState.ApplyUpdate(new EventAggregateUpdateRequest(
            ExpectedVersion: currentState.Version,
            ChangedAtUtc: DateTimeOffset.UtcNow,
            ChangedBy: "entity-resolution",
            Reason: EventVersionReason.MergeApplied,
            ChangedFields: changedFields.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            RequiresModerationReview: true,
            Notes: "Merge plan applied to canonical aggregate.",
            Title: string.IsNullOrWhiteSpace(plan.Title) ? null : plan.Title,
            Description: string.IsNullOrWhiteSpace(plan.Description) ? null : plan.Description,
            Category: string.IsNullOrWhiteSpace(plan.Category) ? null : plan.Category,
            Tags: plan.Tags.Length == 0 ? null : plan.Tags,
            VenueName: string.IsNullOrWhiteSpace(plan.VenueName) ? null : plan.VenueName,
            Address: string.IsNullOrWhiteSpace(plan.Address)
                ? null
                : currentState.Address with { AddressLine1 = plan.Address, RawAddress = plan.Address },
            StartUtc: startUtc,
            EndUtc: endUtc,
            TimeZone: string.IsNullOrWhiteSpace(plan.Timezone) ? null : plan.Timezone,
            ConfidenceScore: Math.Max(currentState.ConfidenceScore, plan.MergedConfidence),
            Provenance: updatedProvenance,
            MergeLineage: updatedMergeLineage));
    }

    private static EventAggregateSnapshot ToSnapshot(EventAggregate state)
        => new(
            CanonicalEventId: state.CanonicalEventId,
            Title: state.Title,
            VenueName: state.VenueName,
            Address: state.Address.RawAddress,
            Latitude: state.Latitude,
            Longitude: state.Longitude,
            StartUtc: state.StartUtc.ToString("O", CultureInfo.InvariantCulture),
            EndUtc: state.EndUtc?.ToString("O", CultureInfo.InvariantCulture),
            Timezone: state.TimeZone,
            Category: state.Category,
            Confidence: state.ConfidenceScore,
            SourceRefs: state.Provenance.SourceRefs,
            EvidenceRefs: state.Provenance.EvidenceRefs,
            IsApproved: state.EventStatus is EventLifecycleStatus.Approved or EventLifecycleStatus.Published,
            ExternalSourceId: state.ExternalReferences.FirstOrDefault()?.ReferenceId);

    private static IReadOnlyDictionary<string, string?> BuildFields(EventAggregate state)
        => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Title"] = state.Title,
            ["VenueName"] = state.VenueName,
            ["Address"] = state.Address.RawAddress,
            ["StartUtc"] = state.StartUtc.ToString("O", CultureInfo.InvariantCulture),
            ["EndUtc"] = state.EndUtc?.ToString("O", CultureInfo.InvariantCulture),
            ["Timezone"] = state.TimeZone,
            ["Category"] = state.Category,
            ["Description"] = state.Description,
            ["Tags"] = state.Tags.Length == 0 ? null : string.Join(',', state.Tags),
        };

    private static string[] MergeDistinct(IEnumerable<string> left, IEnumerable<string> right)
        => left.Concat(right)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

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
            var aggregate = entity.ToCanonicalAggregate();
            records.Add(new ResolutionComparisonRecord(
                RecordId: aggregate.CanonicalEventId,
                RecordType: "event",
                CanonicalEventId: aggregate.CanonicalEventId,
                Candidate: new NormalizedEventCandidate(
                    Title: aggregate.Title,
                    VenueName: aggregate.VenueName,
                    Address: aggregate.Address.RawAddress,
                    StartUtc: aggregate.StartUtc.ToString("O", CultureInfo.InvariantCulture),
                    EndUtc: aggregate.EndUtc?.ToString("O", CultureInfo.InvariantCulture),
                    Timezone: aggregate.TimeZone,
                    Category: aggregate.Category,
                    Description: aggregate.Description,
                    Tags: string.IsNullOrWhiteSpace(entity.TagsCsv)
                        ? []
                        : entity.TagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    SourceKind: "existing_event",
                    SourceRef: aggregate.CanonicalEventId,
                    ExtractionConfidence: aggregate.ConfidenceScore,
                    GeocodeConfidence: 0.8,
                    TemporalConfidence: 0.8,
                    EvidenceRefs: aggregate.Provenance.EvidenceRefs,
                    ExternalSourceId: aggregate.CanonicalEventId,
                    Attributes: new Dictionary<string, string?>
                    {
                        ["lat"] = aggregate.Latitude.ToString(CultureInfo.InvariantCulture),
                        ["lng"] = aggregate.Longitude.ToString(CultureInfo.InvariantCulture),
                    }),
                SourceRefs: aggregate.Provenance.SourceRefs,
                EvidenceRefs: aggregate.Provenance.EvidenceRefs,
                ReviewRefs: entity.Reviews.Select(r => r.Id.ToString("N")).ToArray(),
                ExistingConfidence: aggregate.ConfidenceScore,
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
        EventEntity? entity;
        if (Guid.TryParse(command.Plan.CanonicalEventId, out var eventId))
        {
            entity = await db.Events
                .Include(e => e.Sources)
                .FirstOrDefaultAsync(e => e.Id == eventId, ct);
        }
        else
        {
            entity = await db.Events
                .Include(e => e.Sources)
                .FirstOrDefaultAsync(e => e.PublicId == command.Plan.CanonicalEventId, ct);
        }

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
        => entity.ToAggregateSnapshot();

    private static IReadOnlyDictionary<string, string?> BuildFields(EventEntity entity)
    {
        var aggregate = entity.ToCanonicalAggregate();
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Title"] = aggregate.Title,
            ["VenueName"] = aggregate.VenueName,
            ["Address"] = aggregate.Address.RawAddress,
            ["StartUtc"] = aggregate.StartUtc.ToString("O", CultureInfo.InvariantCulture),
            ["EndUtc"] = aggregate.EndUtc?.ToString("O", CultureInfo.InvariantCulture),
            ["Timezone"] = aggregate.TimeZone,
            ["Category"] = aggregate.Category,
            ["Description"] = aggregate.Description,
            ["Tags"] = entity.TagsCsv,
        };
    }

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
