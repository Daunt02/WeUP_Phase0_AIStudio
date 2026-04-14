using WeUP.Contracts.Moderation;
using WeUP.Domain.Events;
using WeUP.Domain.Media;
using WeUP.Domain.Moderation;

namespace WeUP.Application.Moderation;

public interface IModerationEvidenceService
{
    Task<ModerationEvidenceBundle?> GetEvidenceBundleAsync(string queueItemId, CancellationToken ct = default);
}

public interface IReviewDecisionService
{
    Task<ReviewDecisionResponse> SubmitDecisionAsync(string queueItemId, ReviewDecisionRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ReviewAuditRecord>> GetReviewHistoryAsync(string queueItemId, int pageSize, string? cursor, CancellationToken ct = default);
}

public sealed class ModerationEvidenceService(
    IModerationQueueRepository queue,
    IFlyerEvidenceRepository flyerEvidenceRepository) : IModerationEvidenceService
{
    public async Task<ModerationEvidenceBundle?> GetEvidenceBundleAsync(string queueItemId, CancellationToken ct = default)
    {
        var item = await queue.GetItemAsync(queueItemId, ct);
        if (item is null)
        {
            return null;
        }

        var evidenceRefs = item.Provenance.EvidenceRefs;
        FlyerEvidenceRecord? primaryEvidence = null;

        foreach (var evidenceRef in evidenceRefs)
        {
            primaryEvidence = await flyerEvidenceRepository.GetAsync(evidenceRef, ct);
            if (primaryEvidence is not null)
            {
                break;
            }
        }

        if (primaryEvidence is null && item.IngestionJob?.JobId is not null)
        {
            primaryEvidence = await flyerEvidenceRepository.GetByIngestionJobIdAsync(item.IngestionJob.JobId, ct);
        }

        var blockerReasons = item.ReviewReasons
            .Concat(item.Confidence.ReviewBlockers)
            .Concat(primaryEvidence?.ValidationFailures ?? [])
            .Concat(primaryEvidence?.ReviewReasons ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sourceRefs = new List<string>
        {
            $"{item.Provenance.SourceKind}:{item.Provenance.SourceRef}",
        };

        if (!string.IsNullOrWhiteSpace(item.Provenance.IngestionJobId))
        {
            sourceRefs.Add($"ingestion:{item.Provenance.IngestionJobId}");
        }

        if (!string.IsNullOrWhiteSpace(item.LinkedEventId))
        {
            sourceRefs.Add($"event:{item.LinkedEventId}");
        }

        var confidence = new ConfidenceVector(
            Extraction: item.Confidence.Extraction,
            Geocode: item.Confidence.Geocode,
            Temporal: item.Confidence.Temporal,
            VenueMatch: item.Confidence.VenueMatch,
            DupeRisk: item.Confidence.DupeRisk,
            Aggregate: item.Confidence.Aggregate,
            ReviewConfidence: 0,
            Bucket: item.Confidence.Bucket);

        return new ModerationEvidenceBundle(
            QueueItemId: item.ItemId,
            EventId: item.LinkedEventId,
            CandidateId: item.IngestionJob?.JobId,
            SourceKind: item.Provenance.SourceKind,
            SourceRef: item.Provenance.SourceRef,
            EvidenceRefs: evidenceRefs,
            Confidence: confidence,
            BlockerReasons: blockerReasons,
            SourceRefs: sourceRefs.ToArray(),
            OcrText: primaryEvidence?.OcrText,
            RawExtractionText: primaryEvidence?.NormalizationSnapshotJson,
            ResolutionExplanation: BuildResolutionExplanation(item),
            RetrievedAtUtc: DateTimeOffset.UtcNow.ToString("O"));
    }

    private static string? BuildResolutionExplanation(WeUP.Domain.Moderation.ModerationQueueItem item)
    {
        if (item.DedupeMatch is null)
        {
            return null;
        }

        var reasons = item.DedupeMatch.MatchReasons.Length == 0
            ? "No explicit match reasons were recorded."
            : string.Join("; ", item.DedupeMatch.MatchReasons);

        return $"Dedupe severity={item.DedupeMatch.Severity}, score={item.DedupeMatch.MatchScore:F2}. {reasons}";
    }
}

public sealed class ReviewDecisionService(
    IModerationQueueRepository queue,
    IModerationQueueService moderationQueueService,
    IReviewActionService reviewActionService,
    IEventLifecycleRepository eventLifecycleRepository) : IReviewDecisionService
{
    public async Task<ReviewDecisionResponse> SubmitDecisionAsync(
        string queueItemId,
        ReviewDecisionRequest request,
        CancellationToken ct = default)
    {
        var before = await queue.GetItemAsync(queueItemId, ct);
        if (before is null)
        {
            var missingAudit = BuildAuditRecord(
                queueItemId,
                request,
                ModerationReviewStatus.NEEDS_REVIEW,
                ModerationReviewStatus.NEEDS_REVIEW,
                null,
                "NEEDS_REVIEW",
                "NEEDS_REVIEW");

            return new ReviewDecisionResponse(
                QueueItemId: queueItemId,
                Accepted: false,
                PreviousReviewStatus: ModerationReviewStatus.NEEDS_REVIEW,
                NewReviewStatus: ModerationReviewStatus.NEEDS_REVIEW,
                LifecycleFrom: "NEEDS_REVIEW",
                LifecycleTo: "NEEDS_REVIEW",
                AuditRecord: missingAudit,
                ErrorMessage: "Moderation queue item was not found.");
        }

        var previousReviewStatus = ResolveCurrentReviewStatus(before);
        var lifecycleFrom = await ResolveLifecycleStatusAsync(before.LinkedEventId, previousReviewStatus, ct);

        var legacyActionResponse = await DispatchLegacyActionAsync(queueItemId, request, ct);
        if (!legacyActionResponse.Success)
        {
            var rejectedAudit = BuildAuditRecord(
                queueItemId,
                request,
                previousReviewStatus,
                previousReviewStatus,
                null,
                lifecycleFrom,
                lifecycleFrom);

            return new ReviewDecisionResponse(
                QueueItemId: queueItemId,
                Accepted: false,
                PreviousReviewStatus: previousReviewStatus,
                NewReviewStatus: previousReviewStatus,
                LifecycleFrom: lifecycleFrom,
                LifecycleTo: lifecycleFrom,
                AuditRecord: rejectedAudit,
                ErrorMessage: legacyActionResponse.ErrorMessage ?? "Decision was rejected.");
        }

        var after = await queue.GetItemAsync(queueItemId, ct) ?? before;
        var updatedReviewStatus = ResolveCurrentReviewStatus(after);

        var lifecycleTo = MapDecisionToLifecycleStatus(request.Decision);
        if (!string.IsNullOrWhiteSpace(after.LinkedEventId))
        {
            await eventLifecycleRepository.TransitionLifecycleStatusAsync(after.LinkedEventId!, lifecycleTo, ct);
        }

        var latest = after.History.OrderByDescending(h => h.Timestamp).FirstOrDefault();

        var audit = BuildAuditRecord(
            queueItemId,
            request,
            previousReviewStatus,
            updatedReviewStatus,
            latest,
            lifecycleFrom,
            lifecycleTo);

        return new ReviewDecisionResponse(
            QueueItemId: queueItemId,
            Accepted: true,
            PreviousReviewStatus: previousReviewStatus,
            NewReviewStatus: updatedReviewStatus,
            LifecycleFrom: lifecycleFrom,
            LifecycleTo: lifecycleTo,
            AuditRecord: audit,
            ErrorMessage: null);
    }

    public Task<IReadOnlyList<ReviewAuditRecord>> GetReviewHistoryAsync(
        string queueItemId,
        int pageSize,
        string? cursor,
        CancellationToken ct = default) =>
        moderationQueueService.GetReviewHistoryAsync(queueItemId, pageSize, cursor, ct);

    private async Task<ReviewActionResponse> DispatchLegacyActionAsync(
        string queueItemId,
        ReviewDecisionRequest request,
        CancellationToken ct)
    {
        return request.Decision switch
        {
            ReviewDecisionKind.Approve => await reviewActionService.ApproveAsync(
                queueItemId,
                new ApproveRequest(request.ActorId, request.Comment),
                ct),
            ReviewDecisionKind.Reject => await reviewActionService.RejectAsync(
                queueItemId,
                new RejectRequest(request.ActorId, string.Join("; ", request.Reasons ?? []), request.Comment),
                ct),
            _ => await reviewActionService.RequestChangesAsync(
                queueItemId,
                new RequestChangesRequest(request.ActorId, request.Comment ?? "Requested changes.", request.Comment),
                ct),
        };
    }

    private async Task<string> ResolveLifecycleStatusAsync(string? eventId, ModerationReviewStatus fallback, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return ModerationQueueService.LifecycleFromReviewStatus(fallback);
        }

        var current = await eventLifecycleRepository.GetLifecycleStatusAsync(eventId!, ct);
        return string.IsNullOrWhiteSpace(current) ? ModerationQueueService.LifecycleFromReviewStatus(fallback) : current;
    }

    private static ModerationReviewStatus ResolveCurrentReviewStatus(WeUP.Domain.Moderation.ModerationQueueItem item)
    {
        var latest = item.History.OrderByDescending(h => h.Timestamp).FirstOrDefault()?.Action;
        if (string.Equals(latest, "approve", StringComparison.OrdinalIgnoreCase))
            return ModerationReviewStatus.APPROVED;
        if (string.Equals(latest, "reject", StringComparison.OrdinalIgnoreCase))
            return ModerationReviewStatus.REJECTED;
        if (string.Equals(latest, "request-changes", StringComparison.OrdinalIgnoreCase))
            return ModerationReviewStatus.CHANGES_REQUESTED;
        return ModerationReviewStatus.NEEDS_REVIEW;
    }

    private static string MapDecisionToLifecycleStatus(ReviewDecisionKind decision) => decision switch
    {
        ReviewDecisionKind.Approve => "APPROVED",
        ReviewDecisionKind.Reject => "REJECTED",
        _ => "NEEDS_REVIEW",
    };

    private static ReviewAuditRecord BuildAuditRecord(
        string queueItemId,
        ReviewDecisionRequest request,
        ModerationReviewStatus previous,
        ModerationReviewStatus current,
        ReviewHistoryEntry? history,
        string lifecycleFrom,
        string lifecycleTo)
    {
        return new ReviewAuditRecord(
            RecordId: history?.RecordId ?? Guid.NewGuid().ToString("N"),
            QueueItemId: queueItemId,
            Decision: request.Decision.ToString(),
            ActorId: request.ActorId,
            PreviousReviewStatus: previous,
            NewReviewStatus: current,
            Comment: request.Comment,
            Reasons: request.Reasons ?? [],
            TimestampUtc: (history?.Timestamp ?? DateTimeOffset.UtcNow).ToString("O"),
            CorrelationId: request.CorrelationId,
            LifecycleFrom: lifecycleFrom,
            LifecycleTo: lifecycleTo);
    }
}
