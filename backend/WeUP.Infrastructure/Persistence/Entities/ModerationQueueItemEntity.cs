using System;

namespace WeUP.Infrastructure.Persistence.Entities;

/// <summary>
/// Persisted representation of a moderation queue item.
/// Complex nested objects (Candidate, Provenance, etc.) are stored as JSONB.
/// Review history entries are stored inline as JSONB to avoid a separate join table.
/// </summary>
public sealed class ModerationQueueItemEntity
{
    public Guid Id { get; set; }

    /// <summary>Stable public identifier — matches ModerationQueueItem.ItemId.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>Discriminator: EventCandidate | ManualSubmission | etc.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Open | UnderReview | Approved | Rejected | ChangesRequested | Archived</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Serialised CandidateSnapshotDto — nullable when item has no candidate.</summary>
    public string? CandidateJson { get; set; }

    /// <summary>Serialised ProvenanceSummaryDto — always present.</summary>
    public string ProvenanceJson { get; set; } = string.Empty;

    /// <summary>Serialised ConfidenceSummaryDto — always present.</summary>
    public string ConfidenceJson { get; set; } = string.Empty;

    /// <summary>Serialised DedupeSummaryDto — nullable when no duplicate match detected.</summary>
    public string? DedupeMatchJson { get; set; }

    /// <summary>Serialised IngestionJobSummaryDto — nullable when not originating from an ingestion job.</summary>
    public string? IngestionJobJson { get; set; }

    /// <summary>Serialised string[] of review reasons.</summary>
    public string ReviewReasonsJson { get; set; } = "[]";

    /// <summary>Serialised List&lt;ReviewHistoryEntry&gt; — appended on every review action.</summary>
    public string HistoryJson { get; set; } = "[]";

    public string? AssignedReviewerId { get; set; }
    public string? LinkedEventId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
