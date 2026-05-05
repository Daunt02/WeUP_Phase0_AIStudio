using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WeUP.Contracts.Resolution;
using WeUP.Domain.Resolution;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Resolution;

namespace WeUP.Application.Resolution;

/// <summary>
/// Append‑only provenance service.  All writes are performed via INSERT only.
/// </summary>
public sealed class ProvenanceService : IProvenanceService
{
    private readonly WeUpDbContext _db;
    private readonly ILogger<ProvenanceService> _log;

    public ProvenanceService(WeUpDbContext db, ILogger<ProvenanceService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task RecordAsync(ProvenanceEntry entry)
    {
        // Defensive: ensure no duplicate Ids.
        var exists = await _db.Provenance.AnyAsync(p => p.Id == entry.Id);
        if (exists)
        {
            _log.LogWarning("Duplicate provenance entry ignored: {Id}", entry.Id);
            return;
        }

        var entity = new ProvenanceEntity
        {
            Id = entry.Id,
            EventId = entry.EventId,
            CandidateId = entry.CandidateId,
            FieldName = entry.FieldName,
            OldValue = entry.OldValue,
            NewValue = entry.NewValue,
            ChangedAtUtc = entry.ChangedAtUtc,
            ChangedBy = entry.ChangedBy,
            Reason = entry.Reason
        };

        _db.Provenance.Add(entity);
        await _db.SaveChangesAsync();

        _log.LogInformation(
            "Provenance recorded: Event {EventId}, Field {Field}, From '{Old}' → '{New}'",
            entry.EventId, entry.FieldName, entry.OldValue, entry.NewValue);
    }

    public async Task<IReadOnlyList<ProvenanceEntry>> GetLineageAsync(Guid eventId)
    {
        var rows = await _db.Provenance
            .Where(p => p.EventId == eventId)
            .OrderBy(p => p.ChangedAtUtc)
            .ToListAsync();

        var result = rows.Select(p => new ProvenanceEntry(
            Id: p.Id,
            EventId: p.EventId,
            CandidateId: p.CandidateId,
            FieldName: p.FieldName,
            OldValue: p.OldValue,
            NewValue: p.NewValue,
            ChangedAtUtc: p.ChangedAtUtc,
            ChangedBy: p.ChangedBy,
            Reason: p.Reason)).ToArray();

        return result;
    }
}
using WeUP.Contracts.Resolution;
using WeUP.Domain.Resolution;

namespace WeUP.Application.Resolution;

/// <summary>
/// Append-only provenance service for canonical merge execution.
///
/// Lineage semantics:
/// - The first lineage entry for a field establishes the original source/confidence anchor.
/// - Later lineage entries preserve that anchor while capturing the immediately prior and current values.
/// - Only fields that materially changed are appended; unchanged fields remain explained by older entries.
///
/// Audit expectations:
/// - Each merge generates exactly one ProvenanceEntry.
/// - Sequence numbers are monotonic per canonical event.
/// - Query methods never synthesize or rewrite history; they only project immutable entries.
/// </summary>
public sealed class ProvenanceService : IProvenanceService
{
    private static readonly string[] OrderedTrackedFields =
    [
        "Title",
        "VenueName",
        "Address",
        "StartUtc",
        "EndUtc",
        "Timezone",
        "Category",
        "Description",
        "Tags",
    ];

    public ProvenanceEntry CreateAppendOnlyEntry(ProvenanceBuildCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var canonicalEventId = command.CanonicalAfterMerge.CanonicalEventId;
        var orderedExisting = command.ExistingEntries
            .OrderBy(entry => entry.SequenceNumber)
            .ThenBy(entry => entry.RecordedAtUtc)
            .ToArray();

        var previousEntry = orderedExisting.LastOrDefault();
        var sequenceNumber = previousEntry?.SequenceNumber + 1 ?? 1;
        var evidenceBundleRefs = MergeDistinct(command.EvidenceBundleRefs, command.EvidenceBundle is null ? [] : [command.EvidenceBundle.BundleId]);
        var evidenceRefs = MergeDistinct(command.Plan.UnionedEvidenceRefs, command.Candidate.EvidenceRefs ?? []);
        var sourceRefs = MergeDistinct(command.Plan.UnionedSourceRefs, command.CanonicalAfterMerge.SourceRefs);

        var lineage = OrderedTrackedFields
            .Select(fieldName => BuildFieldLineage(fieldName, command, orderedExisting, evidenceRefs, evidenceBundleRefs))
            .Where(lineageEntry => lineageEntry is not null)
            .Cast<FieldLineage>()
            .ToArray();

        var mergeHistory = new MergeHistoryEntry(
            MergeId: BuildMergeId(command, sequenceNumber),
            SourceRequestIds: DistinctOrFallback(command.SourceRequestIds, command.ResolutionId),
            CandidateIds: DistinctOrFallback(command.CandidateIds, command.Candidate.SourceRef),
            EvidenceBundleRefs: evidenceBundleRefs,
            ChangedFields: lineage.Select(item => item.FieldName).ToArray(),
            MergedAtUtc: command.MergedAtUtc,
            MergeActor: string.IsNullOrWhiteSpace(command.MergeActor) ? "system:entity-resolution" : command.MergeActor,
            MergeReason: BuildMergeReason(command));

        return new ProvenanceEntry(
            EntryId: $"{canonicalEventId}:prov:{sequenceNumber:D4}",
            CanonicalEventId: canonicalEventId,
            SequenceNumber: sequenceNumber,
            PreviousEntryId: previousEntry?.EntryId,
            ResolutionId: command.ResolutionId,
            SourceRequestIds: mergeHistory.SourceRequestIds,
            CandidateIds: mergeHistory.CandidateIds,
            EvidenceBundleRefs: evidenceBundleRefs,
            SourceRefs: sourceRefs,
            EvidenceRefs: evidenceRefs,
            FieldLineage: lineage,
            MergeHistory: mergeHistory,
            RecordedAtUtc: command.MergedAtUtc);
    }

    public ProvenanceEntry[] Append(ProvenanceEntry[] existingEntries, ProvenanceEntry nextEntry)
    {
        ArgumentNullException.ThrowIfNull(existingEntries);
        ArgumentNullException.ThrowIfNull(nextEntry);

        return existingEntries
            .Concat([nextEntry])
            .OrderBy(entry => entry.SequenceNumber)
            .ThenBy(entry => entry.RecordedAtUtc)
            .ToArray();
    }

    public FieldLineage[] GetFieldLineage(ProvenanceEntry[] entries, string? fieldName = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        IEnumerable<FieldLineage> lineage = entries
            .OrderBy(entry => entry.SequenceNumber)
            .ThenBy(entry => entry.RecordedAtUtc)
            .SelectMany(entry => entry.FieldLineage)
            .OrderBy(item => item.ChangedAtUtc)
            .ThenBy(item => item.FieldName, StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(fieldName))
        {
            lineage = lineage.Where(item => item.FieldName.Equals(fieldName, StringComparison.Ordinal));
        }

        return lineage.ToArray();
    }

    public MergeHistoryEntry[] GetMergeHistory(ProvenanceEntry[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries
            .OrderBy(entry => entry.SequenceNumber)
            .ThenBy(entry => entry.RecordedAtUtc)
            .Select(entry => entry.MergeHistory)
            .ToArray();
    }

    public EventEvolutionHistoryEntry[] GetEvolutionHistory(ProvenanceEntry[] entries, string canonicalEventId)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries
            .OrderBy(entry => entry.SequenceNumber)
            .ThenBy(entry => entry.RecordedAtUtc)
            .Select(entry =>
            {
                var changedFields = entry.MergeHistory.ChangedFields;
                var evolutionType = ClassifyEvolutionType(changedFields, entry.MergeHistory.MergeReason);
                var requiresManualReview = entry.MergeHistory.MergeReason.Contains("manual review", StringComparison.OrdinalIgnoreCase);

                return new EventEvolutionHistoryEntry(
                    CanonicalEventId: canonicalEventId,
                    MergeId: entry.MergeHistory.MergeId,
                    EvolutionType: evolutionType,
                    ChangedFields: changedFields,
                    OccurredAtUtc: entry.MergeHistory.MergedAtUtc,
                    Actor: entry.MergeHistory.MergeActor,
                    Reason: entry.MergeHistory.MergeReason,
                    RequiresManualReview: requiresManualReview);
            })
            .ToArray();
    }

    private static FieldLineage? BuildFieldLineage(
        string fieldName,
        ProvenanceBuildCommand command,
        ProvenanceEntry[] orderedExisting,
        string[] evidenceRefs,
        string[] evidenceBundleRefs)
    {
        var priorValue = GetField(command.CanonicalBeforeFields, fieldName);
        var currentValue = GetField(command.CanonicalAfterFields, fieldName);
        if (string.Equals(priorValue, currentValue, StringComparison.Ordinal))
        {
            return null;
        }

        var priorLineage = orderedExisting
            .SelectMany(entry => entry.FieldLineage)
            .Where(entry => entry.FieldName.Equals(fieldName, StringComparison.Ordinal))
            .OrderBy(entry => entry.ChangedAtUtc)
            .FirstOrDefault();

        var originalSourceRef = priorLineage?.OriginalSourceRef ?? ResolveOriginalSourceRef(command);
        var originalConfidence = priorLineage?.OriginalConfidence ?? ResolveCanonicalConfidence(command, fieldName);

        return new FieldLineage(
            FieldName: fieldName,
            PriorValue: priorValue,
            CurrentValue: currentValue,
            OriginalSourceRef: originalSourceRef,
            OriginalConfidence: originalConfidence,
            CurrentSourceRef: ResolveCurrentSourceRef(command),
            CurrentConfidence: ResolveCurrentConfidence(command, fieldName),
            EvidenceRefs: evidenceRefs,
            EvidenceBundleRefs: evidenceBundleRefs,
            ChangedAtUtc: command.MergedAtUtc,
            Reason: BuildFieldReason(command, fieldName, priorValue, currentValue));
    }

    private static string BuildMergeId(ProvenanceBuildCommand command, int sequenceNumber)
        => string.IsNullOrWhiteSpace(command.ResolutionId)
            ? $"merge:{command.CanonicalAfterMerge.CanonicalEventId}:{sequenceNumber:D4}"
            : command.ResolutionId;

    private static string BuildMergeReason(ProvenanceBuildCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.MergeReason))
        {
            return command.MergeReason;
        }

        if (command.Plan.ManualReviewReasons.Length > 0)
        {
            return string.Join(" | ", command.Plan.ManualReviewReasons);
        }

        return string.Join(" | ", command.Plan.MergeRationale);
    }

    private static string BuildFieldReason(ProvenanceBuildCommand command, string fieldName, string? priorValue, string? currentValue)
    {
        var conflict = command.Plan.Conflicts.FirstOrDefault(item => item.FieldName.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (conflict is not null && !string.IsNullOrWhiteSpace(conflict.Reason))
        {
            return conflict.Reason;
        }

        if (string.IsNullOrWhiteSpace(priorValue))
        {
            return $"{fieldName} populated from candidate evidence during merge append.";
        }

        if (string.IsNullOrWhiteSpace(currentValue))
        {
            return $"{fieldName} was cleared by merge plan; prior canonical value preserved in lineage.";
        }

        return $"{fieldName} changed from prior canonical value to merged candidate value.";
    }

    private static string ResolveOriginalSourceRef(ProvenanceBuildCommand command)
    {
        return command.CanonicalBeforeMerge.SourceRefs.FirstOrDefault(source => !string.IsNullOrWhiteSpace(source))
            ?? command.CanonicalBeforeMerge.ExternalSourceId
            ?? "canonical:seed";
    }

    private static string ResolveCurrentSourceRef(ProvenanceBuildCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.Candidate.SourceRef))
        {
            return command.Candidate.SourceRef;
        }

        if (command.CandidateIds.Length > 0)
        {
            return command.CandidateIds[0];
        }

        return "candidate:unknown";
    }

    private static double ResolveCanonicalConfidence(ProvenanceBuildCommand command, string fieldName)
    {
        return fieldName switch
        {
            "Address" or "VenueName" => command.CanonicalBeforeMerge.Confidence,
            "StartUtc" or "EndUtc" or "Timezone" => command.CanonicalBeforeMerge.Confidence,
            _ => command.CanonicalBeforeMerge.Confidence,
        };
    }

    private static double ResolveCurrentConfidence(ProvenanceBuildCommand command, string fieldName)
    {
        if (command.OriginalEventCandidate?.FieldScores is not null)
        {
            foreach (var key in GetCandidateFieldAliases(fieldName))
            {
                if (command.OriginalEventCandidate.FieldScores.TryGetValue(key, out var score))
                {
                    return score.Confidence;
                }
            }
        }

        return fieldName switch
        {
            "Address" => command.Candidate.GeocodeConfidence,
            "StartUtc" or "EndUtc" or "Timezone" => command.Candidate.TemporalConfidence,
            _ => command.Candidate.ExtractionConfidence,
        };
    }

    private static IEnumerable<string> GetCandidateFieldAliases(string fieldName)
    {
        yield return fieldName;

        if (fieldName.Equals("VenueName", StringComparison.Ordinal))
        {
            yield return "Venue";
        }
    }

    private static string? GetField(IReadOnlyDictionary<string, string?> fields, string fieldName)
        => fields.TryGetValue(fieldName, out var value) ? value : null;

    private static string[] DistinctOrFallback(string[] values, string fallback)
    {
        var distinct = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return distinct.Length > 0 ? distinct : [fallback];
    }

    private static string[] MergeDistinct(IEnumerable<string> left, IEnumerable<string> right)
        => left.Concat(right)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string ClassifyEvolutionType(string[] changedFields, string mergeReason)
    {
        if (changedFields.Any(field => field.Equals("Title", StringComparison.OrdinalIgnoreCase)))
            return "TitleUpdate";

        if (changedFields.Any(field => field.Equals("VenueName", StringComparison.OrdinalIgnoreCase) || field.Equals("Address", StringComparison.OrdinalIgnoreCase)))
            return "VenueCorrection";

        if (changedFields.Any(field =>
                field.Equals("StartUtc", StringComparison.OrdinalIgnoreCase) ||
                field.Equals("EndUtc", StringComparison.OrdinalIgnoreCase) ||
                field.Equals("Timezone", StringComparison.OrdinalIgnoreCase)))
        {
            return mergeReason.Contains("resched", StringComparison.OrdinalIgnoreCase)
                ? "Reschedule"
                : "TimeCorrection";
        }

        return "MergeApplied";
    }
}