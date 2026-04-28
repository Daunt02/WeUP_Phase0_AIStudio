using System.Globalization;
using WeUP.Contracts.Auth;
using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Moderation;
using WeUP.Contracts.Resolution;
using WeUP.Domain.Dedupe;
using WeUP.Domain.Moderation;
using WeUP.Domain.Resolution;
using WeUP.Domain.Users;

namespace WeUP.Application.Moderation;

internal sealed class ModerationActionValidationException(string message) : Exception(message);

public sealed record ModerationActionMutation(
    ModerationItemStatus NewStatus,
    string? AuditNote,
    CandidateSnapshotDto? UpdatedCandidate = null,
    ConfidenceSummaryDto? UpdatedConfidence = null,
    string? UpdatedLinkedEventId = null,
    DedupeSummaryDto? UpdatedDedupe = null,
    string[]? AppendedReviewReasons = null,
    string[]? ValidationNotes = null);

public interface IModerationActionHandler
{
    ModeratorAction Action { get; }

    Task<ModerationActionMutation> ApplyAsync(
        WeUP.Domain.Moderation.ModerationQueueItem item,
        ModerationActionCommand command,
        ModerationItemStatus nextStatus,
        CancellationToken ct);
}

/// <summary>
/// Executes moderator actions with explicit safety guarantees:
/// 1. Role-aware validation for every action.
/// 2. Deterministic status transitions.
/// 3. Fail-closed audit policy (no queue mutation happens before audit writes succeed).
/// </summary>
public sealed class ModerationActionService(
    IModerationQueueRepository queue,
    IAuditTrailService auditTrail,
    IModerationAuditService moderationAudit,
    IUserRoleResolver roleResolver,
    IEnumerable<IModerationActionHandler> handlers,
    IModerationTelemetry? telemetry = null) : IModerationActionService
{
    private static readonly IReadOnlyDictionary<(ModeratorAction Action, ModerationItemStatus From), ModerationItemStatus> TransitionTable
        = new Dictionary<(ModeratorAction Action, ModerationItemStatus From), ModerationItemStatus>
    {
        [(ModeratorAction.Approve, ModerationItemStatus.Open)] = ModerationItemStatus.Resolved,
        [(ModeratorAction.Approve, ModerationItemStatus.InReview)] = ModerationItemStatus.Resolved,

        [(ModeratorAction.Reject, ModerationItemStatus.Open)] = ModerationItemStatus.Resolved,
        [(ModeratorAction.Reject, ModerationItemStatus.InReview)] = ModerationItemStatus.Resolved,

        [(ModeratorAction.Edit, ModerationItemStatus.Open)] = ModerationItemStatus.InReview,
        [(ModeratorAction.Edit, ModerationItemStatus.InReview)] = ModerationItemStatus.InReview,

        [(ModeratorAction.Merge, ModerationItemStatus.Open)] = ModerationItemStatus.Resolved,
        [(ModeratorAction.Merge, ModerationItemStatus.InReview)] = ModerationItemStatus.Resolved,
    };

    private readonly IReadOnlyDictionary<ModeratorAction, IModerationActionHandler> _handlers =
        handlers.ToDictionary(h => h.Action);
    private readonly IModerationTelemetry _telemetry = telemetry ?? NullModerationTelemetry.Instance;

    public Task<ModerationActionResult> ApproveAsync(
        string itemId,
        string actorId,
        string? note = null,
        DateTimeOffset? actionedAtUtc = null,
        CancellationToken ct = default) =>
        ExecuteAsync(new ModerationActionCommand(itemId, ModeratorAction.Approve, actorId, Note: note, ActionedAtUtc: actionedAtUtc), ct);

    public Task<ModerationActionResult> RejectAsync(
        string itemId,
        string actorId,
        string rejectReason,
        string? note = null,
        DateTimeOffset? actionedAtUtc = null,
        CancellationToken ct = default) =>
        ExecuteAsync(new ModerationActionCommand(itemId, ModeratorAction.Reject, actorId, Note: note, RejectReason: rejectReason, ActionedAtUtc: actionedAtUtc), ct);

    public Task<ModerationActionResult> EditAsync(
        string itemId,
        string actorId,
        ModerationEditPatch patch,
        string? note = null,
        ProvenanceEntry[]? existingProvenanceEntries = null,
        DateTimeOffset? actionedAtUtc = null,
        CancellationToken ct = default) =>
        ExecuteAsync(new ModerationActionCommand(itemId, ModeratorAction.Edit, actorId, Note: note, EditPatch: patch, ExistingProvenanceEntries: existingProvenanceEntries, ActionedAtUtc: actionedAtUtc), ct);

    public Task<ModerationActionResult> MergeAsync(
        string itemId,
        string actorId,
        MergePlanDetail mergePlan,
        string? note = null,
        ProvenanceEntry[]? existingProvenanceEntries = null,
        DateTimeOffset? actionedAtUtc = null,
        CancellationToken ct = default) =>
        ExecuteAsync(new ModerationActionCommand(itemId, ModeratorAction.Merge, actorId, Note: note, MergePlan: mergePlan, ExistingProvenanceEntries: existingProvenanceEntries, ActionedAtUtc: actionedAtUtc), ct);

    public async Task<ModerationActionResult> ExecuteAsync(ModerationActionCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.ItemId))
            return Failed(command, ModerationItemStatus.Open, ModerationItemStatus.Open, "itemId is required.");

        if (string.IsNullOrWhiteSpace(command.ActorId))
            return Failed(command, ModerationItemStatus.Open, ModerationItemStatus.Open, "actorId is required.");

        if (!await roleResolver.IsInRoleAsync(command.ActorId, UserRoles.Moderator, ct))
            return Failed(command, ModerationItemStatus.Open, ModerationItemStatus.Open, "actor must be in moderator role.");

        var item = await queue.GetItemAsync(command.ItemId, ct);
        if (item is null)
            return Failed(command, ModerationItemStatus.Open, ModerationItemStatus.Open, "moderation queue item not found.");

        if (!TransitionTable.TryGetValue((command.Action, item.Status), out var nextStatus))
        {
            return Failed(
                command,
                item.Status,
                item.Status,
                $"Action '{command.Action}' is not allowed from status '{item.Status}'.");
        }

        if (!_handlers.TryGetValue(command.Action, out var handler))
        {
            return Failed(command, item.Status, item.Status, $"No handler registered for action '{command.Action}'.");
        }

        ModerationActionMutation mutation;
        try
        {
            mutation = await handler.ApplyAsync(item, command, nextStatus, ct);
        }
        catch (ModerationActionValidationException ex)
        {
            return Failed(command, item.Status, item.Status, ex.Message);
        }

        var timestamp = command.ActionedAtUtc ?? DateTimeOffset.UtcNow;
        var prevStatus = item.Status;
        var actionName = command.Action.ToString().ToLowerInvariant();
        var auditNote = mutation.AuditNote ?? command.Note;
    var processingStartedAtUtc = ModerationTelemetryDimensions.ResolveProcessingStartedAtUtc(item);

        // Fail-closed: write immutable audit entries first. If this step fails,
        // the queue item remains untouched and no mutation can happen silently.
        await auditTrail.AppendAsync(BuildAuditEntry(item, command.ActorId, actionName, prevStatus, mutation.NewStatus, auditNote, timestamp), ct);
        await moderationAudit.AppendAsync(
            item,
            reviewerId: command.ActorId,
            actorId: command.ActorId,
            action: actionName,
            previousStatus: prevStatus,
            newStatus: mutation.NewStatus,
            reasonComment: auditNote,
            actionTimestampUtc: timestamp,
            ct: ct);

        item.Status = mutation.NewStatus;
        item.Candidate = mutation.UpdatedCandidate ?? item.Candidate;
        item.Confidence = mutation.UpdatedConfidence ?? item.Confidence;
        item.DedupeMatch = mutation.UpdatedDedupe ?? item.DedupeMatch;

        if (!string.IsNullOrWhiteSpace(mutation.UpdatedLinkedEventId))
        {
            item.LinkedEventId = mutation.UpdatedLinkedEventId;
        }

        if (mutation.AppendedReviewReasons is { Length: > 0 })
        {
            item.ReviewReasons = item.ReviewReasons
                .Concat(mutation.AppendedReviewReasons)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        item.AppendHistory(new ReviewHistoryEntry(
            RecordId: Guid.NewGuid().ToString("N"),
            Action: actionName,
            ActorId: command.ActorId,
            Note: auditNote,
            PreviousStatus: prevStatus,
            NextStatus: mutation.NewStatus,
            Timestamp: timestamp));

        await queue.UpdateItemAsync(item, ct);
        _telemetry.TrackProcessingCompletion(
            item,
            actionName,
            ModerationTelemetryDimensions.ResolveWorkflowStatus(mutation.NewStatus, actionName),
            processingStartedAtUtc,
            timestamp,
            command.ActorId);
        await _telemetry.RefreshBacklogAsync(ct);

        return new ModerationActionResult(
            ItemId: item.ItemId,
            Action: command.Action,
            PreviousStatus: prevStatus,
            NewStatus: mutation.NewStatus,
            Success: true,
            ErrorMessage: null,
            ValidationNotes: mutation.ValidationNotes ?? []);
    }

    private static AuditTrailEntry BuildAuditEntry(
        WeUP.Domain.Moderation.ModerationQueueItem item,
        string actorId,
        string action,
        ModerationItemStatus previousStatus,
        ModerationItemStatus nextStatus,
        string? note,
        DateTimeOffset timestamp) =>
        new(
            EntryId: Guid.NewGuid().ToString("N"),
            ItemId: item.ItemId,
            ItemKind: item.Kind,
            Action: action,
            ActorId: actorId,
            PreviousStatus: previousStatus,
            NextStatus: nextStatus,
            Note: note,
            EvidenceSnapshotRefs: item.Provenance.EvidenceRefs,
            CorrelationId: Guid.NewGuid().ToString("N"),
            Timestamp: timestamp);

    private static ModerationActionResult Failed(
        ModerationActionCommand command,
        ModerationItemStatus previous,
        ModerationItemStatus next,
        string error) =>
        new(
            ItemId: command.ItemId,
            Action: command.Action,
            PreviousStatus: previous,
            NewStatus: next,
            Success: false,
            ErrorMessage: error,
            ValidationNotes: []);
}

public sealed class ApproveHandler(IPublishEligibilityService publishEligibility) : IModerationActionHandler
{
    public ModeratorAction Action => ModeratorAction.Approve;

    public Task<ModerationActionMutation> ApplyAsync(
        WeUP.Domain.Moderation.ModerationQueueItem item,
        ModerationActionCommand command,
        ModerationItemStatus nextStatus,
        CancellationToken ct)
    {
        _ = ct;

        if (item.Candidate is null)
        {
            throw new ModerationActionValidationException("Approve action requires a candidate snapshot.");
        }

        var normalized = ToNormalizedCandidate(item.Candidate, item.Provenance, item.Confidence);
        var eligibility = publishEligibility.Evaluate(new PublishEligibilityContext(
            Candidate: normalized,
            ReviewState: PublishReviewState.Approved,
            LifecycleStatus: "APPROVED",
            HasUnresolvedDedupeConflict: (item.DedupeMatch?.MatchScore ?? 0d) >= 0.55,
            SourceIntegrityValid: !string.IsNullOrWhiteSpace(item.Provenance.SourceKind) && !string.IsNullOrWhiteSpace(item.Provenance.SourceRef),
            EvidenceChainComplete: item.Provenance.EvidenceRefs.Length > 0,
            VenueResolved: !string.IsNullOrWhiteSpace(item.Candidate.VenueName),
            GeoValidated: !string.IsNullOrWhiteSpace(item.Candidate.Address) && item.Confidence.Geocode >= 0.5,
            DedupeMatchScore: item.DedupeMatch?.MatchScore ?? 0d,
            ReviewConfidence: 1.0));

        if (!eligibility.Eligible)
        {
            var blocker = string.Join("; ", eligibility.Blockers.Select(b => b.Message));
            throw new ModerationActionValidationException($"Approve blocked by publish eligibility gate: {blocker}");
        }

        return Task.FromResult(new ModerationActionMutation(
            NewStatus: nextStatus,
            AuditNote: command.Note,
            ValidationNotes:
            [
                "publish-eligibility:passed",
                $"confidence-aggregate:{eligibility.ConfidenceSummary.Aggregate.ToString("F4", CultureInfo.InvariantCulture)}",
            ]));
    }

    private static NormalizedEventCandidate ToNormalizedCandidate(
        CandidateSnapshotDto candidate,
        ProvenanceSummaryDto provenance,
        ConfidenceSummaryDto confidence) =>
        new(
            Title: candidate.Title,
            VenueName: candidate.VenueName,
            Address: candidate.Address,
            StartUtc: candidate.StartUtc,
            EndUtc: candidate.EndUtc,
            Timezone: candidate.Timezone,
            Category: candidate.Category,
            Description: candidate.Description,
            Tags: candidate.Tags,
            SourceKind: provenance.SourceKind,
            SourceRef: provenance.SourceRef,
            ExtractionConfidence: confidence.Extraction,
            GeocodeConfidence: confidence.Geocode,
            TemporalConfidence: confidence.Temporal,
            EvidenceRefs: provenance.EvidenceRefs,
            ExternalSourceId: null,
            Attributes: null);
}

public sealed class RejectHandler : IModerationActionHandler
{
    public ModeratorAction Action => ModeratorAction.Reject;

    public Task<ModerationActionMutation> ApplyAsync(
        WeUP.Domain.Moderation.ModerationQueueItem item,
        ModerationActionCommand command,
        ModerationItemStatus nextStatus,
        CancellationToken ct)
    {
        _ = item;
        _ = ct;

        if (string.IsNullOrWhiteSpace(command.RejectReason))
        {
            throw new ModerationActionValidationException("Reject action requires a non-empty reason.");
        }

        var note = string.IsNullOrWhiteSpace(command.Note)
            ? $"Reason: {command.RejectReason}."
            : $"Reason: {command.RejectReason}. {command.Note}";

        return Task.FromResult(new ModerationActionMutation(
            NewStatus: nextStatus,
            AuditNote: note,
            ValidationNotes: ["reject-reason:present"]));
    }
}

public sealed class EditHandler(
    IPublishEligibilityService publishEligibility,
    IProvenanceService provenanceService) : IModerationActionHandler
{
    public ModeratorAction Action => ModeratorAction.Edit;

    public Task<ModerationActionMutation> ApplyAsync(
        WeUP.Domain.Moderation.ModerationQueueItem item,
        ModerationActionCommand command,
        ModerationItemStatus nextStatus,
        CancellationToken ct)
    {
        _ = ct;

        if (command.EditPatch is null)
            throw new ModerationActionValidationException("Edit action requires an edit patch.");

        if (item.Candidate is null)
            throw new ModerationActionValidationException("Edit action requires a candidate snapshot.");

        var updatedCandidate = ApplyPatch(item.Candidate, command.EditPatch);
        var provenanceNotes = BuildEditProvenanceNotes(
            before: item.Candidate,
            after: updatedCandidate,
            command.ExistingProvenanceEntries ?? [],
            provenanceService,
            item.Provenance.SourceRef,
            command.ActionedAtUtc ?? DateTimeOffset.UtcNow);

        var updatedConfidence = command.RecalculateConfidence
            ? RecalculateConfidence(item, updatedCandidate, publishEligibility)
            : item.Confidence;

        return Task.FromResult(new ModerationActionMutation(
            NewStatus: nextStatus,
            AuditNote: command.Note,
            UpdatedCandidate: updatedCandidate,
            UpdatedConfidence: updatedConfidence,
            AppendedReviewReasons: provenanceNotes,
            ValidationNotes: ["edit-provenance:appended", "candidate:updated"]));
    }

    private static CandidateSnapshotDto ApplyPatch(CandidateSnapshotDto original, ModerationEditPatch patch) =>
        new(
            Title: patch.Title ?? original.Title,
            VenueName: patch.VenueName ?? original.VenueName,
            Address: patch.Address ?? original.Address,
            StartUtc: patch.StartUtc ?? original.StartUtc,
            EndUtc: patch.EndUtc ?? original.EndUtc,
            Timezone: patch.Timezone ?? original.Timezone,
            Category: patch.Category ?? original.Category,
            Description: patch.Description ?? original.Description,
            Tags: patch.Tags ?? original.Tags,
            SourceKind: original.SourceKind,
            SourceRef: original.SourceRef);

    private static string[] BuildEditProvenanceNotes(
        CandidateSnapshotDto before,
        CandidateSnapshotDto after,
        ProvenanceEntry[] existingEntries,
        IProvenanceService provenanceService,
        string fallbackOriginalSource,
        DateTimeOffset changedAt)
    {
        var notes = new List<string>();

        AppendIfChanged("Title", before.Title, after.Title);
        AppendIfChanged("VenueName", before.VenueName, after.VenueName);
        AppendIfChanged("Address", before.Address, after.Address);
        AppendIfChanged("StartUtc", before.StartUtc, after.StartUtc);
        AppendIfChanged("EndUtc", before.EndUtc, after.EndUtc);
        AppendIfChanged("Timezone", before.Timezone, after.Timezone);
        AppendIfChanged("Category", before.Category, after.Category);
        AppendIfChanged("Description", before.Description, after.Description);
        AppendIfChanged("Tags", JoinTags(before.Tags), JoinTags(after.Tags));

        return notes.ToArray();

        void AppendIfChanged(string fieldName, string? prior, string? current)
        {
            if (string.Equals(prior, current, StringComparison.Ordinal))
            {
                return;
            }

            var priorLineage = provenanceService
                .GetFieldLineage(existingEntries, fieldName)
                .OrderBy(x => x.ChangedAtUtc)
                .FirstOrDefault();

            var originalSource = priorLineage?.OriginalSourceRef ?? fallbackOriginalSource;
            notes.Add($"edit-provenance:{fieldName}|old={prior ?? "<null>"}|new={current ?? "<null>"}|origin={originalSource}|at={changedAt:O}");
        }

        static string JoinTags(string[]? tags)
            => tags is { Length: > 0 } ? string.Join(",", tags) : "<null>";
    }

    internal static ConfidenceSummaryDto RecalculateConfidence(
        WeUP.Domain.Moderation.ModerationQueueItem item,
        CandidateSnapshotDto updatedCandidate,
        IPublishEligibilityService publishEligibility)
    {
        var normalized = new NormalizedEventCandidate(
            Title: updatedCandidate.Title,
            VenueName: updatedCandidate.VenueName,
            Address: updatedCandidate.Address,
            StartUtc: updatedCandidate.StartUtc,
            EndUtc: updatedCandidate.EndUtc,
            Timezone: updatedCandidate.Timezone,
            Category: updatedCandidate.Category,
            Description: updatedCandidate.Description,
            Tags: updatedCandidate.Tags,
            SourceKind: item.Provenance.SourceKind,
            SourceRef: item.Provenance.SourceRef,
            ExtractionConfidence: item.Confidence.Extraction,
            GeocodeConfidence: item.Confidence.Geocode,
            TemporalConfidence: item.Confidence.Temporal,
            EvidenceRefs: item.Provenance.EvidenceRefs,
            ExternalSourceId: null,
            Attributes: null);

        var result = publishEligibility.Evaluate(normalized, item.DedupeMatch?.MatchScore ?? 0d);
        var dimensions = result.ConfidenceSummary.DimensionScores;

        var extraction = dimensions.TryGetValue("extraction", out var extractionScore) ? extractionScore : item.Confidence.Extraction;
        var geocode = dimensions.TryGetValue("geocode", out var geoScore) ? geoScore : item.Confidence.Geocode;
        var temporal = dimensions.TryGetValue("temporal", out var temporalScore) ? temporalScore : item.Confidence.Temporal;
        var venue = dimensions.TryGetValue("venueMatch", out var venueScore) ? venueScore : item.Confidence.VenueMatch;
        var dupe = dimensions.TryGetValue("dupeRisk", out var dupeScore) ? dupeScore : item.Confidence.DupeRisk;
        var aggregate = Math.Round(result.ConfidenceSummary.Aggregate, 4);

        return new ConfidenceSummaryDto(
            Extraction: extraction,
            Geocode: geocode,
            Temporal: temporal,
            VenueMatch: venue,
            DupeRisk: dupe,
            Aggregate: aggregate,
            Bucket: aggregate >= 0.85 ? ConfidenceBucket.High : aggregate >= 0.55 ? ConfidenceBucket.Medium : ConfidenceBucket.Low,
            ReviewBlockers: result.Blockers.Select(b => b.Code.ToString()).Distinct(StringComparer.Ordinal).ToArray());
    }
}

public sealed class MergeHandler(
    IPublishEligibilityService publishEligibility,
    IProvenanceService provenanceService) : IModerationActionHandler
{
    public ModeratorAction Action => ModeratorAction.Merge;

    public Task<ModerationActionMutation> ApplyAsync(
        WeUP.Domain.Moderation.ModerationQueueItem item,
        ModerationActionCommand command,
        ModerationItemStatus nextStatus,
        CancellationToken ct)
    {
        _ = ct;

        if (command.MergePlan is null)
            throw new ModerationActionValidationException("Merge action requires a merge plan.");

        if (item.Candidate is null)
            throw new ModerationActionValidationException("Merge action requires a candidate snapshot.");

        ValidateMergePlan(item, command.MergePlan);

        var updatedCandidate = ApplyMergePlan(item.Candidate, command.MergePlan);
        var updatedConfidence = command.RecalculateConfidence
            ? EditHandler.RecalculateConfidence(item, updatedCandidate, publishEligibility)
            : item.Confidence;

        var priorMerges = provenanceService.GetMergeHistory(command.ExistingProvenanceEntries ?? []);
        var mergeNote = string.IsNullOrWhiteSpace(command.Note)
            ? $"Merge applied to canonical event '{command.MergePlan.CanonicalEventId}'."
            : command.Note;

        return Task.FromResult(new ModerationActionMutation(
            NewStatus: nextStatus,
            AuditNote: mergeNote,
            UpdatedCandidate: updatedCandidate,
            UpdatedConfidence: updatedConfidence,
            UpdatedLinkedEventId: command.MergePlan.CanonicalEventId,
            AppendedReviewReasons:
            [
                $"merge-plan-version:{command.MergePlan.Audit.MergePlannerVersion}",
                $"merge-plan-manual-review:{command.MergePlan.RequiresManualReview}",
                $"merge-history-count-before:{priorMerges.Length}",
            ],
            ValidationNotes:
            [
                "merge-plan:validated",
                $"merge-field-count:{command.MergePlan.FieldDecisions.Count}",
            ]));
    }

    private static void ValidateMergePlan(WeUP.Domain.Moderation.ModerationQueueItem item, MergePlanDetail plan)
    {
        if (plan.RejectMerge)
            throw new ModerationActionValidationException("Merge plan is marked RejectMerge and cannot be executed.");

        if (plan.FieldDecisions.Values.Any(d => d.Action is MergeFieldAction.RequireReview or MergeFieldAction.RejectMerge))
        {
            throw new ModerationActionValidationException("Merge plan still contains unresolved review/reject field actions.");
        }

        var sourceRef = item.Candidate?.SourceRef ?? item.Provenance.SourceRef;
        if (!string.Equals(plan.CandidateSourceRef, sourceRef, StringComparison.Ordinal))
        {
            throw new ModerationActionValidationException("Merge plan candidate source does not match moderation candidate source.");
        }

        if (!string.IsNullOrWhiteSpace(item.DedupeMatch?.ExistingEventId) &&
            !string.Equals(plan.CanonicalEventId, item.DedupeMatch.ExistingEventId, StringComparison.Ordinal))
        {
            throw new ModerationActionValidationException("Merge plan canonical event does not match dedupe target for this queue item.");
        }
    }

    private static CandidateSnapshotDto ApplyMergePlan(CandidateSnapshotDto candidate, MergePlanDetail plan)
    {
        string? title = candidate.Title;
        string? venue = candidate.VenueName;
        string? address = candidate.Address;
        string? startUtc = candidate.StartUtc;
        string? endUtc = candidate.EndUtc;
        string? timezone = candidate.Timezone;
        string? category = candidate.Category;
        string? description = candidate.Description;
        string[]? tags = candidate.Tags;

        foreach (var decision in plan.FieldDecisions.Values)
        {
            if (decision.Action is MergeFieldAction.KeepExisting)
            {
                continue;
            }

            if (decision.Action is not MergeFieldAction.ReplaceWithCandidate and not MergeFieldAction.MergeValues)
            {
                continue;
            }

            switch (decision.FieldName)
            {
                case "Title":
                    title = decision.ResultValue;
                    break;
                case "VenueName":
                    venue = decision.ResultValue;
                    break;
                case "Address":
                    address = decision.ResultValue;
                    break;
                case "StartUtc":
                    startUtc = decision.ResultValue;
                    break;
                case "EndUtc":
                    endUtc = decision.ResultValue;
                    break;
                case "Timezone":
                    timezone = decision.ResultValue;
                    break;
                case "Category":
                    category = decision.ResultValue;
                    break;
                case "Description":
                    description = decision.ResultValue;
                    break;
                case "Tags":
                    tags = ParseTags(decision.ResultValue, tags);
                    break;
            }
        }

        return new CandidateSnapshotDto(
            Title: title,
            VenueName: venue,
            Address: address,
            StartUtc: startUtc,
            EndUtc: endUtc,
            Timezone: timezone,
            Category: category,
            Description: description,
            Tags: tags,
            SourceKind: candidate.SourceKind,
            SourceRef: candidate.SourceRef);
    }

    private static string[]? ParseTags(string? resultValue, string[]? fallback)
    {
        if (string.IsNullOrWhiteSpace(resultValue))
            return fallback;

        var tags = resultValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return tags.Length == 0 ? fallback : tags;
    }
}
