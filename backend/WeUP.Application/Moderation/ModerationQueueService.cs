using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WeUP.Contracts.Moderation;
using WeUP.Domain.Moderation;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Application.Moderation;
using WeUP.Contracts.Moderation;
using WeUP.Domain.Dedupe;
using WeUP.Domain.Flyer;
using WeUP.Domain.Moderation;
using ContractQueueItem = WeUP.Contracts.Moderation.ModerationQueueItem;
using DomainQueueItem = WeUP.Domain.Moderation.ModerationQueueItem;

namespace WeUP.Application.Moderation;

public enum ModerationStatus
{
    Pending,
    InReview,
    Approved,
    Rejected,
    NeedsEdit,
}

public sealed record ModerationStatusTransition(
    ModerationStatus From,
    ModerationStatus To,
    string ActorId,
    DateTimeOffset ChangedAtUtc);

public sealed record ModerationQueueItem(
    string ItemId,
    string CandidateId,
    ModerationStatus Status,
    EventRiskScore RiskScore,
    double Confidence,
    int Priority,
    string? AssignedReviewerId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ModerationStatusTransition> Transitions);

public interface IModerationQueueService
{
    Task<ModerationQueueResponse> GetQueueAsync(ModerationQueueFilter filter, CancellationToken ct = default);
    Task<ModerationQueueResponse> GetQueueAsync(ModerationQueueQuery query, CancellationToken ct = default);
    Task<ContractQueueItem?> GetItemAsync(string itemId, CancellationToken ct = default);
    Task<ModerationStatsDto> GetStatsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReviewAuditRecord>> GetReviewHistoryAsync(string itemId, int pageSize, string? cursor, CancellationToken ct = default);
    Task<ModerationQueueItem> EnqueueCandidateAsync(
        EventCandidateV2 candidate,
        EventRiskScore riskScore,
        DuplicateAssessment duplicateAssessment,
        CancellationToken ct = default);
    Task<ModerationQueueItem?> FetchNextItemAsync(CancellationToken ct = default);
    Task<ModerationQueueItem?> AssignReviewerAsync(string itemId, string reviewerId, string actorId, CancellationToken ct = default);
    Task<ModerationQueueItem?> UpdateStatusAsync(string itemId, ModerationStatus nextStatus, string actorId, string? note = null, CancellationToken ct = default);
}

public sealed class ModerationQueueService(
    IModerationQueueRepository queue,
    IModerationAuditService moderationAuditService,
    IModerationTelemetry? telemetry = null) : IModerationQueueService
{
    private const string RiskScoreMarker = "risk-score:";
    private const string RiskLevelMarker = "risk-level:";
    private const string CandidateMarker = "candidate-id:";
    private readonly IModerationTelemetry _telemetry = telemetry ?? NullModerationTelemetry.Instance;

    public async Task<ModerationQueueResponse> GetQueueAsync(ModerationQueueFilter filter, CancellationToken ct = default)
    {
        var legacy = new ModerationQueueQuery(
            Status: filter.Status,
            Kind: filter.Kind,
            SourceKind: filter.SourceKind,
            ReviewReason: filter.ReviewReason,
            ConfidenceBucket: filter.ConfidenceBucket,
            MinDuplicateSeverity: filter.MinDuplicateSeverity,
            AssignedReviewerId: filter.AssignedReviewerId,
            IngestionJobId: filter.IngestionJobId,
            AfterUtc: filter.AfterUtc,
            BeforeUtc: filter.BeforeUtc,
            PageSize: filter.PageSize,
            Cursor: filter.Cursor);

        var response = await GetQueueAsync(legacy, ct);

        IEnumerable<ContractQueueItem> filtered = response.Items;

        if (filter.ReviewStatus.HasValue)
            filtered = filtered.Where(i => i.ReviewStatus == filter.ReviewStatus.Value);

        if (filter.MinConfidence.HasValue)
            filtered = filtered.Where(i => i.Confidence >= filter.MinConfidence.Value);

        if (filter.MaxConfidence.HasValue)
            filtered = filtered.Where(i => i.Confidence <= filter.MaxConfidence.Value);

        var materialized = filtered.ToArray();
        return new ModerationQueueResponse(materialized, materialized.Length, response.NextCursor, response.PreviousCursor);
    }

    public async Task<ModerationQueueResponse> GetQueueAsync(ModerationQueueQuery query, CancellationToken ct = default)
    {
        var (items, total) = await queue.QueryAsync(query, ct);

        var queueItems = items.Select(ToQueueItem).ToArray();
        var nextCursor = queueItems.Length == query.PageSize && queueItems.Length > 0
            ? EncodeCursor(items[^1].CreatedAt)
            : null;

        return new ModerationQueueResponse(queueItems, total, nextCursor, null);
    }

    public async Task<ContractQueueItem?> GetItemAsync(string itemId, CancellationToken ct = default)
    {
        var item = await queue.GetItemAsync(itemId, ct);
        return item is null ? null : ToQueueItem(item);
    }

    public Task<ModerationStatsDto> GetStatsAsync(CancellationToken ct = default) =>
        queue.GetStatsAsync(ct);

    public async Task<IReadOnlyList<ReviewAuditRecord>> GetReviewHistoryAsync(
        string itemId,
        int pageSize,
        string? cursor,
        CancellationToken ct = default)
    {
        var history = await queue.GetHistoryAsync(itemId, pageSize, cursor, ct);
        var item = await queue.GetItemAsync(itemId, ct);

        if (item is null)
        {
            return [];
        }

        return history
            .OrderByDescending(h => h.Timestamp)
            .Select(h => new ReviewAuditRecord(
                RecordId: h.RecordId,
                QueueItemId: itemId,
                Decision: h.Action,
                ActorId: h.ActorId,
                PreviousReviewStatus: ToReviewStatus(h.Action, h.PreviousStatus),
                NewReviewStatus: ToReviewStatus(h.Action, h.NextStatus),
                Comment: h.Note,
                Reasons: item.ReviewReasons,
                TimestampUtc: h.Timestamp.ToString("O"),
                CorrelationId: null,
                LifecycleFrom: LifecycleFromReviewStatus(ToReviewStatus(h.Action, h.PreviousStatus)),
                LifecycleTo: LifecycleFromReviewStatus(ToReviewStatus(h.Action, h.NextStatus))))
            .ToArray();
    }

    public async Task<ModerationQueueItem> EnqueueCandidateAsync(
        EventCandidateV2 candidate,
        EventRiskScore riskScore,
        DuplicateAssessment duplicateAssessment,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(riskScore);
        ArgumentNullException.ThrowIfNull(duplicateAssessment);

        var now = DateTimeOffset.UtcNow;
        var candidateId = candidate.EvidenceBundle.BundleId;
        var reviewReasons = BuildReviewReasons(candidate, riskScore, duplicateAssessment, candidateId);

        var item = new DomainQueueItem
        {
            ItemId = Guid.NewGuid().ToString("N"),
            Kind = ModerationItemKind.CandidateReview,
            Status = ModerationItemStatus.Open,
            Candidate = new CandidateSnapshotDto(
                candidate.Title?.Value,
                candidate.Venue?.Value,
                candidate.Address?.Value,
                candidate.StartUtc?.Value.ToString("O"),
                candidate.EndUtc?.Value.ToString("O"),
                Timezone: null,
                Category: candidate.Category?.Value,
                Description: candidate.Description?.Value,
                Tags: candidate.Tags?.Value?.ToArray(),
                SourceKind: candidate.EvidenceBundle.SourceKind,
                SourceRef: candidate.EvidenceBundle.BundleId),
            Provenance = new ProvenanceSummaryDto(
                SourceKind: candidate.EvidenceBundle.SourceKind,
                SourceRef: candidate.EvidenceBundle.BundleId,
                IngestionJobId: null,
                EvidenceRefs: BuildEvidenceRefs(candidate),
                SubmittedAt: now.ToString("O")),
            Confidence = new ConfidenceSummaryDto(
                Extraction: candidate.OverallConfidence,
                Geocode: candidate.Address?.Confidence ?? 0,
                Temporal: candidate.StartUtc?.Confidence ?? 0,
                VenueMatch: candidate.Venue?.Confidence ?? 0,
                DupeRisk: duplicateAssessment.Breakdown.CompositeScore,
                Aggregate: candidate.OverallConfidence,
                Bucket: ResolveBucket(candidate.OverallConfidence),
                ReviewBlockers: duplicateAssessment.Rationale),
            DedupeMatch = new DedupeSummaryDto(
                ExistingEventId: duplicateAssessment.CanonicalEventId,
                ExistingEventTitle: null,
                MatchScore: duplicateAssessment.Breakdown.CompositeScore,
                Severity: ToDuplicateSeverity(duplicateAssessment.Level),
                MatchReasons: duplicateAssessment.Rationale),
            IngestionJob = null,
            ReviewReasons = reviewReasons,
            AssignedReviewerId = null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        item.AppendHistory(new ReviewHistoryEntry(
            RecordId: Guid.NewGuid().ToString("N"),
            Action: "enqueue",
            ActorId: "system",
            Note: "Candidate enqueued for moderation review.",
            PreviousStatus: ModerationItemStatus.Open,
            NextStatus: ModerationItemStatus.Open,
            Timestamp: now));

        await queue.AddItemAsync(item, ct);
        await moderationAuditService.AppendAsync(
            item,
            reviewerId: "system",
            actorId: "system",
            action: "enqueue",
            previousStatus: ModerationItemStatus.Open,
            newStatus: ModerationItemStatus.Open,
            reasonComment: "Candidate enqueued for moderation review.",
            actionTimestampUtc: now,
            ct: ct);
        await _telemetry.RefreshBacklogAsync(ct);

        return ToWorkflowItem(item);
    }

    public async Task<ModerationQueueItem?> FetchNextItemAsync(CancellationToken ct = default)
    {
        var (items, _) = await queue.QueryAsync(new ModerationQueueQuery(PageSize: 500), ct);

        // Queue prioritization is explicit and deterministic:
        // 1) Higher risk level first.
        // 2) Lower confidence next.
        // 3) Older items first to avoid starvation.
        var next = items
            .Where(IsActionableForWorkflow)
            .OrderByDescending(i => ComputePriority(i))
            .ThenBy(i => i.CreatedAt)
            .FirstOrDefault();

        return next is null ? null : ToWorkflowItem(next);
    }

    public async Task<ModerationQueueItem?> AssignReviewerAsync(
        string itemId,
        string reviewerId,
        string actorId,
        CancellationToken ct = default)
    {
        var item = await queue.GetItemAsync(itemId, ct);
        if (item is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        item.AssignedReviewerId = reviewerId;

        var current = ResolveWorkflowStatus(item);
        var previousStatus = item.Status;
        var nextStatus = previousStatus;

        if (current is ModerationStatus.Pending or ModerationStatus.NeedsEdit)
        {
            nextStatus = ModerationItemStatus.InReview;
            item.Status = nextStatus;
        }

        item.AppendHistory(new ReviewHistoryEntry(
            RecordId: Guid.NewGuid().ToString("N"),
            Action: "assign-reviewer",
            ActorId: actorId,
            Note: $"Assigned reviewer '{reviewerId}'.",
            PreviousStatus: previousStatus,
            NextStatus: nextStatus,
            Timestamp: now));

        await queue.UpdateItemAsync(item, ct);
        await moderationAuditService.AppendAsync(
            item,
            reviewerId: reviewerId,
            actorId: actorId,
            action: "assign-reviewer",
            previousStatus: previousStatus,
            newStatus: nextStatus,
            reasonComment: $"Assigned reviewer '{reviewerId}'.",
            actionTimestampUtc: now,
            ct: ct);
        await _telemetry.RefreshBacklogAsync(ct);

        return ToWorkflowItem(item);
    }

    public async Task<ModerationQueueItem?> UpdateStatusAsync(
        string itemId,
        ModerationStatus nextStatus,
        string actorId,
        string? note = null,
        CancellationToken ct = default)
    {
        var item = await queue.GetItemAsync(itemId, ct);
        if (item is null)
        {
            return null;
        }

        var currentStatus = ResolveWorkflowStatus(item);
        if (!IsTransitionAllowed(currentStatus, nextStatus))
        {
            throw new InvalidOperationException(
                $"Illegal moderation transition from '{currentStatus}' to '{nextStatus}'.");
        }

        // Explicitly prevent shortcut approvals directly from Pending.
        if (currentStatus == ModerationStatus.Pending && nextStatus == ModerationStatus.Approved)
        {
            throw new InvalidOperationException("Queue does not allow auto-approval from Pending.");
        }

        var now = DateTimeOffset.UtcNow;
        var previousStatus = item.Status;
        var nextItemStatus = ToItemStatus(nextStatus);
        var processingStartedAtUtc = ModerationTelemetryDimensions.ResolveProcessingStartedAtUtc(item);
        var actionName = ToActionName(nextStatus);

        item.Status = nextItemStatus;
        item.AppendHistory(new ReviewHistoryEntry(
            RecordId: Guid.NewGuid().ToString("N"),
            Action: actionName,
            ActorId: actorId,
            Note: note,
            PreviousStatus: previousStatus,
            NextStatus: nextItemStatus,
            Timestamp: now));

        await queue.UpdateItemAsync(item, ct);
        await moderationAuditService.AppendAsync(
            item,
            reviewerId: actorId,
            actorId: actorId,
            action: actionName,
            previousStatus: previousStatus,
            newStatus: nextItemStatus,
            reasonComment: note,
            actionTimestampUtc: now,
            ct: ct);

        if (nextStatus is ModerationStatus.Approved or ModerationStatus.Rejected or ModerationStatus.NeedsEdit)
        {
            _telemetry.TrackProcessingCompletion(
                item,
                actionName,
                ModerationTelemetryDimensions.ResolveWorkflowStatus(nextItemStatus, actionName),
                processingStartedAtUtc,
                now,
                actorId);
        }

        await _telemetry.RefreshBacklogAsync(ct);

        return ToWorkflowItem(item);
    }

    private static ContractQueueItem ToQueueItem(DomainQueueItem item)
    {
        var reviewStatus = ResolveReviewStatus(item);
        var confidence = item.Confidence.Aggregate;
        var blockerReasons = item.ReviewReasons
            .Concat(item.Confidence.ReviewBlockers)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ContractQueueItem(
            ItemId: item.ItemId,
            Kind: item.Kind,
            Status: item.Status,
            ReviewStatus: reviewStatus,
            EventId: item.LinkedEventId,
            CandidateId: item.IngestionJob?.JobId,
            SourceKind: item.Provenance.SourceKind,
            Confidence: confidence,
            BlockerReasons: blockerReasons,
            EvidenceAvailable: item.Provenance.EvidenceRefs.Length > 0,
            Urgency: DetermineUrgency(item, confidence, blockerReasons),
            CreatedAt: item.CreatedAt.ToString("O"),
            UpdatedAt: item.UpdatedAt.ToString("O"),
            Candidate: item.Candidate,
            Provenance: item.Provenance,
            ConfidenceSummary: item.Confidence,
            DedupeMatch: item.DedupeMatch,
            IngestionJob: item.IngestionJob,
            ReviewReasons: item.ReviewReasons,
            AssignedReviewerId: item.AssignedReviewerId);
    }

    private static ModerationReviewUrgency DetermineUrgency(
        WeUP.Domain.Moderation.ModerationQueueItem item,
        double confidence,
        string[] blockerReasons)
    {
        if (item.Kind == ModerationItemKind.IngestionFailure)
            return ModerationReviewUrgency.Critical;
        if (blockerReasons.Any(r => r.Contains("duplicate", StringComparison.OrdinalIgnoreCase)))
            return ModerationReviewUrgency.High;
        if (confidence < 0.55)
            return ModerationReviewUrgency.High;
        if (confidence < 0.75)
            return ModerationReviewUrgency.Normal;
        return ModerationReviewUrgency.Low;
    }

    private static ModerationReviewStatus ResolveReviewStatus(WeUP.Domain.Moderation.ModerationQueueItem item)
    {
        var lastAction = item.History.LastOrDefault()?.Action;
        return ToReviewStatus(lastAction, item.Status);
    }

    private static ModerationReviewStatus ToReviewStatus(string? action, ModerationItemStatus status)
    {
        if (string.Equals(action, "approve", StringComparison.OrdinalIgnoreCase))
            return ModerationReviewStatus.APPROVED;
        if (string.Equals(action, "reject", StringComparison.OrdinalIgnoreCase))
            return ModerationReviewStatus.REJECTED;
        if (string.Equals(action, "request-changes", StringComparison.OrdinalIgnoreCase))
            return ModerationReviewStatus.CHANGES_REQUESTED;

        return status switch
        {
            ModerationItemStatus.Resolved => ModerationReviewStatus.APPROVED,
            _ => ModerationReviewStatus.NEEDS_REVIEW,
        };
    }

    internal static string LifecycleFromReviewStatus(ModerationReviewStatus status) => status switch
    {
        ModerationReviewStatus.APPROVED => "APPROVED",
        ModerationReviewStatus.REJECTED => "REJECTED",
        _ => "NEEDS_REVIEW",
    };

    private static string EncodeCursor(DateTimeOffset dt) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dt.UtcTicks.ToString()));

    private static string[] BuildReviewReasons(
        EventCandidateV2 candidate,
        EventRiskScore riskScore,
        DuplicateAssessment duplicateAssessment,
        string candidateId)
    {
        var reasons = new List<string>
        {
            $"{CandidateMarker}{candidateId}",
            $"{RiskScoreMarker}{riskScore.OverallScore}",
            $"{RiskLevelMarker}{riskScore.Level}",
            $"dedupe-level:{duplicateAssessment.Level}",
        };

        reasons.AddRange(riskScore.ContributingFactors.Select(f => $"risk-factor:{f.Code}"));
        reasons.AddRange(duplicateAssessment.Rationale.Select(r => $"dedupe-rationale:{r}"));

        if (candidate.OverallConfidence < 0.55)
        {
            reasons.Add("low-confidence");
        }

        return reasons.ToArray();
    }

    private static string[] BuildEvidenceRefs(EventCandidateV2 candidate)
    {
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            candidate.EvidenceBundle.BundleId,
        };

        AddFieldEvidence(refs, candidate.Title?.EvidenceRefs);
        AddFieldEvidence(refs, candidate.StartUtc?.EvidenceRefs);
        AddFieldEvidence(refs, candidate.EndUtc?.EvidenceRefs);
        AddFieldEvidence(refs, candidate.Venue?.EvidenceRefs);
        AddFieldEvidence(refs, candidate.Address?.EvidenceRefs);
        AddFieldEvidence(refs, candidate.Category?.EvidenceRefs);
        AddFieldEvidence(refs, candidate.Description?.EvidenceRefs);
        AddFieldEvidence(refs, candidate.Tags?.EvidenceRefs);
        return refs.ToArray();
    }

    private static void AddFieldEvidence(HashSet<string> refs, IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return;
        }

        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                refs.Add(value);
            }
        }
    }

    private static ConfidenceBucket ResolveBucket(double confidence)
    {
        if (confidence >= 0.85)
        {
            return ConfidenceBucket.High;
        }

        if (confidence >= 0.55)
        {
            return ConfidenceBucket.Medium;
        }

        return ConfidenceBucket.Low;
    }

    private static DuplicateSeverity ToDuplicateSeverity(DuplicateAssessmentLevel level) => level switch
    {
        DuplicateAssessmentLevel.PossibleDuplicate => DuplicateSeverity.Possible,
        DuplicateAssessmentLevel.ProbableDuplicate => DuplicateSeverity.Probable,
        DuplicateAssessmentLevel.ExactDuplicate => DuplicateSeverity.Definite,
        _ => DuplicateSeverity.None,
    };

    private static bool IsActionableForWorkflow(DomainQueueItem item)
    {
        var status = ResolveWorkflowStatus(item);
        return status is ModerationStatus.Pending or ModerationStatus.InReview or ModerationStatus.NeedsEdit;
    }

    private static ModerationQueueItem ToWorkflowItem(DomainQueueItem item)
    {
        var riskScore = ParseRiskScore(item);
        var status = ResolveWorkflowStatus(item);
        var candidateId = ModerationAuditService.ResolveCandidateId(item);

        var transitions = item.History
            .OrderBy(h => h.Timestamp)
            .Select(ToStatusTransition)
            .ToArray();

        return new ModerationQueueItem(
            ItemId: item.ItemId,
            CandidateId: candidateId,
            Status: status,
            RiskScore: riskScore,
            Confidence: item.Confidence.Aggregate,
            Priority: ComputePriority(item),
            AssignedReviewerId: item.AssignedReviewerId,
            CreatedAtUtc: item.CreatedAt,
            UpdatedAtUtc: item.UpdatedAt,
            Transitions: transitions);
    }

    private static ModerationStatusTransition ToStatusTransition(ReviewHistoryEntry entry)
    {
        var from = ToWorkflowStatus(entry.PreviousStatus, entry.Action, isNext: false);
        var to = ToWorkflowStatus(entry.NextStatus, entry.Action, isNext: true);

        return new ModerationStatusTransition(
            From: from,
            To: to,
            ActorId: entry.ActorId,
            ChangedAtUtc: entry.Timestamp);
    }

    private static ModerationStatus ResolveWorkflowStatus(DomainQueueItem item)
    {
        var lastAction = item.History.LastOrDefault()?.Action;
        return ToWorkflowStatus(item.Status, lastAction, isNext: true);
    }

    private static ModerationStatus ToWorkflowStatus(ModerationItemStatus itemStatus, string? action, bool isNext)
    {
        if (isNext && string.Equals(action, "request-changes", StringComparison.OrdinalIgnoreCase))
        {
            return ModerationStatus.NeedsEdit;
        }

        if (isNext && string.Equals(action, "reject", StringComparison.OrdinalIgnoreCase))
        {
            return ModerationStatus.Rejected;
        }

        if (isNext && string.Equals(action, "approve", StringComparison.OrdinalIgnoreCase))
        {
            return ModerationStatus.Approved;
        }

        return itemStatus switch
        {
            ModerationItemStatus.Open => ModerationStatus.Pending,
            ModerationItemStatus.InReview => ModerationStatus.InReview,
            ModerationItemStatus.Resolved => ModerationStatus.Approved,
            _ => ModerationStatus.Rejected,
        };
    }

    private static bool IsTransitionAllowed(ModerationStatus current, ModerationStatus next) => (current, next) switch
    {
        (ModerationStatus.Pending, ModerationStatus.InReview) => true,
        (ModerationStatus.Pending, ModerationStatus.Rejected) => true,
        (ModerationStatus.InReview, ModerationStatus.Approved) => true,
        (ModerationStatus.InReview, ModerationStatus.Rejected) => true,
        (ModerationStatus.InReview, ModerationStatus.NeedsEdit) => true,
        (ModerationStatus.NeedsEdit, ModerationStatus.InReview) => true,
        (ModerationStatus.NeedsEdit, ModerationStatus.Rejected) => true,
        _ => false,
    };

    private static ModerationItemStatus ToItemStatus(ModerationStatus status) => status switch
    {
        ModerationStatus.Pending => ModerationItemStatus.Open,
        ModerationStatus.InReview => ModerationItemStatus.InReview,
        ModerationStatus.NeedsEdit => ModerationItemStatus.InReview,
        ModerationStatus.Approved => ModerationItemStatus.Resolved,
        ModerationStatus.Rejected => ModerationItemStatus.Resolved,
        _ => ModerationItemStatus.Open,
    };

    private static string ToActionName(ModerationStatus status) => status switch
    {
        ModerationStatus.InReview => "start-review",
        ModerationStatus.Approved => "approve",
        ModerationStatus.Rejected => "reject",
        ModerationStatus.NeedsEdit => "request-changes",
        ModerationStatus.Pending => "reopen",
        _ => "update-status",
    };

    private static int ComputePriority(DomainQueueItem item)
    {
        var risk = ParseRiskScore(item);
        var riskWeight = risk.Level switch
        {
            EventRiskLevel.Restricted => 400,
            EventRiskLevel.High => 300,
            EventRiskLevel.Medium => 200,
            _ => 100,
        };

        var confidenceWeight = (int)Math.Round((1 - Math.Clamp(item.Confidence.Aggregate, 0, 1)) * 100d);
        var ageWeight = (int)Math.Clamp((DateTimeOffset.UtcNow - item.CreatedAt).TotalHours, 0, 72);
        return riskWeight + confidenceWeight + ageWeight;
    }

    private static EventRiskScore ParseRiskScore(DomainQueueItem item)
    {
        var rawScore = ExtractReviewReasonValue(item.ReviewReasons, RiskScoreMarker);
        var rawLevel = ExtractReviewReasonValue(item.ReviewReasons, RiskLevelMarker);

        if (int.TryParse(rawScore, out var score) && Enum.TryParse<EventRiskLevel>(rawLevel, true, out var level))
        {
            return new EventRiskScore(
                OverallScore: score,
                Level: level,
                ContributingFactors: [],
                Explanation: "Recovered from persisted queue metadata.");
        }

        var fallbackLevel = item.Confidence.Aggregate < 0.55 ? EventRiskLevel.High : EventRiskLevel.Low;
        var fallbackScore = (int)Math.Round((1 - Math.Clamp(item.Confidence.Aggregate, 0, 1)) * 100d);
        return new EventRiskScore(
            OverallScore: fallbackScore,
            Level: fallbackLevel,
            ContributingFactors: [],
            Explanation: "Derived from confidence fallback.");
    }

    private static string? ExtractReviewReasonValue(IEnumerable<string> reviewReasons, string marker)
    {
        var token = reviewReasons.FirstOrDefault(r => r.StartsWith(marker, StringComparison.OrdinalIgnoreCase));
        return token is null ? null : token[marker.Length..];
    }
}
