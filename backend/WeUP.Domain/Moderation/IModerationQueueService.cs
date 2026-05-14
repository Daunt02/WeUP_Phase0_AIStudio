using System;
using System.Threading.Tasks;
using WeUP.Contracts.Moderation;

namespace WeUP.Domain.Moderation;

/// <summary>
/// Service that implements the moderation‑queue workflow.
/// All methods must be **transactional** and enforce the monotonic state machine.
/// </summary>
public interface IModerationQueueService
{
    /// <summary>
    /// Enqueue a new candidate for moderation. Returns the created queue item.
    /// </summary>
    Task<ModerationQueueItem> EnqueueAsync(Guid candidateId);

    /// <summary>
    /// Get the next item that should be reviewed, ordered by:
    ///   1️⃣ Highest risk level (Low → High)  
    ///   2️⃣ Oldest CreatedAtUtc
    /// Returns null if the queue is empty.
    /// </summary>
    Task<ModerationQueueItem?> GetNextItemAsync();

    /// <summary>
    /// Assign the item to a reviewer and transition it to <c>InReview</c>.
    /// </summary>
    Task ClaimAsync(Guid queueItemId, Guid reviewerId);

    /// <summary>
    /// Update the status of an item (Approved / Rejected / NeedsEdit) and optionally store reviewer notes.
    /// </summary>
    Task UpdateStatusAsync(Guid queueItemId, ModerationStatus newStatus, string? reviewerNotes = null);
}
