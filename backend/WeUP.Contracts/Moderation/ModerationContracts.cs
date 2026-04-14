namespace WeUP.Contracts.Moderation;

// ---------------------------------------------------------------------------
// Item kinds — typed discriminated union, not a generic blob
// ---------------------------------------------------------------------------

public enum ModerationItemKind
{
    CandidateReview,       // ingested candidate awaiting approval
    DedupeReview,          // dedupe conflict requiring resolution
    IngestionFailure,      // pipeline failure requiring operator attention
    PublishBlocked,        // event approved but blocked from publication
}

public enum ModerationItemStatus
{
    Open,
    InReview,
    Resolved,
    Closed,
}

public enum ModerationReviewStatus
{
    NEEDS_REVIEW,
    APPROVED,
    REJECTED,
    CHANGES_REQUESTED,
}

public enum ModerationReviewUrgency
{
    Low,
    Normal,
    High,
    Critical,
}

public enum ConfidenceBucket
{
    High,    // >= 0.85
    Medium,  // 0.55–0.84
    Low,     // < 0.55
}

public enum DuplicateSeverity
{
    None,
    Possible,     // score 0.35–0.54
    Probable,     // score 0.55–0.79
    Definite,     // score >= 0.80
}

// ---------------------------------------------------------------------------
// Projection snapshots embedded in queue items
// ---------------------------------------------------------------------------

public record CandidateSnapshotDto(
    string? Title,
    string? VenueName,
    string? Address,
    string? StartUtc,
    string? EndUtc,
    string? Timezone,
    string? Category,
    string? Description,
    string[]? Tags,
    string SourceKind,
    string SourceRef);

public record ProvenanceSummaryDto(
    string SourceKind,
    string SourceRef,
    string? IngestionJobId,
    string[] EvidenceRefs,
    string SubmittedAt);

public record ConfidenceSummaryDto(
    double Extraction,
    double Geocode,
    double Temporal,
    double VenueMatch,
    double DupeRisk,
    double Aggregate,
    ConfidenceBucket Bucket,
    string[] ReviewBlockers);

public record DedupeSummaryDto(
    string? ExistingEventId,
    string? ExistingEventTitle,
    double MatchScore,
    DuplicateSeverity Severity,
    string[] MatchReasons);

public record IngestionJobSummaryDto(
    string JobId,
    string Status,
    string? FailureReason,
    string CreatedAt,
    string? CompletedAt);

public record ConfidenceVector(
    double Extraction,
    double Geocode,
    double Temporal,
    double VenueMatch,
    double DupeRisk,
    double Aggregate,
    double ReviewConfidence,
    ConfidenceBucket Bucket);

// ---------------------------------------------------------------------------
// Core moderation queue item DTO
// ---------------------------------------------------------------------------

public record ModerationQueueItem(
    string ItemId,
    ModerationItemKind Kind,
    ModerationItemStatus Status,
    ModerationReviewStatus ReviewStatus,
    string? EventId,
    string? CandidateId,
    string SourceKind,
    double Confidence,
    string[] BlockerReasons,
    bool EvidenceAvailable,
    ModerationReviewUrgency Urgency,
    string CreatedAt,
    string UpdatedAt,
    CandidateSnapshotDto? Candidate,
    ProvenanceSummaryDto Provenance,
    ConfidenceSummaryDto ConfidenceSummary,
    DedupeSummaryDto? DedupeMatch,
    IngestionJobSummaryDto? IngestionJob,
    string[] ReviewReasons,
    string? AssignedReviewerId);

public record ModerationQueueItemDto(
    string ItemId,
    ModerationItemKind Kind,
    ModerationItemStatus Status,
    CandidateSnapshotDto? Candidate,
    ProvenanceSummaryDto Provenance,
    ConfidenceSummaryDto Confidence,
    DedupeSummaryDto? DedupeMatch,
    IngestionJobSummaryDto? IngestionJob,
    string[] ReviewReasons,
    string? AssignedReviewerId,
    string CreatedAt,
    string UpdatedAt);

// ---------------------------------------------------------------------------
// Query / filter contracts
// ---------------------------------------------------------------------------

public record ModerationQueueFilter(
    ModerationItemStatus? Status = null,
    ModerationReviewStatus? ReviewStatus = null,
    ModerationItemKind? Kind = null,
    string? SourceKind = null,
    double? MinConfidence = null,
    double? MaxConfidence = null,
    string? ReviewReason = null,
    ConfidenceBucket? ConfidenceBucket = null,
    DuplicateSeverity? MinDuplicateSeverity = null,
    string? AssignedReviewerId = null,
    string? IngestionJobId = null,
    string? AfterUtc = null,
    string? BeforeUtc = null,
    int PageSize = 25,
    string? Cursor = null);

public record ModerationQueueQuery(
    ModerationItemStatus? Status = null,
    ModerationItemKind? Kind = null,
    string? SourceKind = null,
    string? ReviewReason = null,
    ConfidenceBucket? ConfidenceBucket = null,
    DuplicateSeverity? MinDuplicateSeverity = null,
    string? AssignedReviewerId = null,
    string? IngestionJobId = null,
    string? AfterUtc = null,
    string? BeforeUtc = null,
    int PageSize = 25,
    string? Cursor = null);

public record ModerationQueueResponse(
    ModerationQueueItem[] Items,
    int TotalCount,
    string? NextCursor,
    string? PreviousCursor);

public record ModerationQueueItemLegacyResponse(
    ModerationQueueItemDto[] Items,
    int TotalCount,
    string? NextCursor,
    string? PreviousCursor);

// ---------------------------------------------------------------------------
// Stats contract
// ---------------------------------------------------------------------------

public record ModerationStatsDto(
    int TotalOpen,
    int TotalInReview,
    int TotalResolved,
    int CandidateReviewOpen,
    int DedupeReviewOpen,
    int IngestionFailureOpen,
    int PublishBlockedOpen,
    int HighConfidenceOpen,
    int MediumConfidenceOpen,
    int LowConfidenceOpen,
    string AsOf);

// ---------------------------------------------------------------------------
// Review history
// ---------------------------------------------------------------------------

public record ReviewHistoryItemDto(
    string ItemId,
    string ItemKind,
    string Action,
    string ActorId,
    string? Note,
    string PreviousStatus,
    string NextStatus,
    string Timestamp);

public record ReviewHistoryResponse(
    ReviewHistoryItemDto[] Items,
    int TotalCount,
    string? NextCursor);

public record ReviewAuditRecord(
    string RecordId,
    string QueueItemId,
    string Decision,
    string ActorId,
    ModerationReviewStatus PreviousReviewStatus,
    ModerationReviewStatus NewReviewStatus,
    string? Comment,
    string[] Reasons,
    string TimestampUtc,
    string? CorrelationId,
    string? LifecycleFrom,
    string? LifecycleTo);

public record ModerationEvidenceBundle(
    string QueueItemId,
    string? EventId,
    string? CandidateId,
    string SourceKind,
    string SourceRef,
    string[] EvidenceRefs,
    ConfidenceVector Confidence,
    string[] BlockerReasons,
    string[] SourceRefs,
    string? OcrText,
    string? RawExtractionText,
    string? ResolutionExplanation,
    string RetrievedAtUtc);
