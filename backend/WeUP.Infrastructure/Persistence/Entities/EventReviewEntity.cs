namespace WeUP.Infrastructure.Persistence.Entities;

/// <summary>
/// Moderation review record. Each review action creates a new row — full history preserved.
/// </summary>
public sealed class EventReviewEntity
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }

    /// <summary>PENDING | APPROVED | REJECTED | CHANGES_REQUESTED</summary>
    public string ReviewStatus { get; set; } = "PENDING";
    public string? ReviewerId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? Notes { get; set; }
    public string? RejectionReason { get; set; }
    public string? ChangeRequestInstructions { get; set; }

    /// <summary>Reason manual review was required (low confidence, missing field, etc.)</summary>
    public string? RequiredReason { get; set; }

    /// <summary>auto_approved | manually_approved | rejected</summary>
    public string? PublishDecision { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public EventEntity Event { get; set; } = null!;
}
