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

// ---------------------------------------------------------------------------
// Core moderation queue item DTO
// ---------------------------------------------------------------------------

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
