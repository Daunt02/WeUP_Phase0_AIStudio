using WeUP.Contracts.Moderation;
using WeUP.Contracts.Resolution;
using WeUP.Domain.Dedupe;

namespace WeUP.Domain.Moderation;

/// <summary>
/// Canonical moderator action surface for queue items and linked candidates.
/// </summary>
public enum ModeratorAction
{
    Approve,
    Reject,
    Edit,
    Merge,
}

/// <summary>
/// Optional candidate patch used by Edit actions.
/// Null properties mean "leave existing value unchanged".
/// </summary>
public sealed record ModerationEditPatch(
    string? Title = null,
    string? VenueName = null,
    string? Address = null,
    string? StartUtc = null,
    string? EndUtc = null,
    string? Timezone = null,
    string? Category = null,
    string? Description = null,
    string[]? Tags = null);

/// <summary>
/// Action request envelope consumed by IModerationActionService.
/// 
/// Safety notes:
/// - Reject requires RejectReason.
/// - Edit requires EditPatch.
/// - Merge requires MergePlan and validates all merge-plan invariants.
/// - ExistingProvenanceEntries is optional context used by edit/merge handlers
///   to preserve append-only provenance semantics.
/// </summary>
public sealed record ModerationActionCommand(
    string ItemId,
    ModeratorAction Action,
    string ActorId,
    string? Note = null,
    string? RejectReason = null,
    ModerationEditPatch? EditPatch = null,
    MergePlanDetail? MergePlan = null,
    ProvenanceEntry[]? ExistingProvenanceEntries = null,
    DateTimeOffset? ActionedAtUtc = null,
    bool RecalculateConfidence = true);

public sealed record ModerationActionResult(
    string ItemId,
    ModeratorAction Action,
    ModerationItemStatus PreviousStatus,
    ModerationItemStatus NewStatus,
    bool Success,
    string? ErrorMessage,
    string[] ValidationNotes);

public interface IModerationActionService
{
    Task<ModerationActionResult> ExecuteAsync(ModerationActionCommand command, CancellationToken ct = default);

    Task<ModerationActionResult> ApproveAsync(string itemId, string actorId, string? note = null, DateTimeOffset? actionedAtUtc = null, CancellationToken ct = default);

    Task<ModerationActionResult> RejectAsync(string itemId, string actorId, string rejectReason, string? note = null, DateTimeOffset? actionedAtUtc = null, CancellationToken ct = default);

    Task<ModerationActionResult> EditAsync(string itemId, string actorId, ModerationEditPatch patch, string? note = null, ProvenanceEntry[]? existingProvenanceEntries = null, DateTimeOffset? actionedAtUtc = null, CancellationToken ct = default);

    Task<ModerationActionResult> MergeAsync(string itemId, string actorId, MergePlanDetail mergePlan, string? note = null, ProvenanceEntry[]? existingProvenanceEntries = null, DateTimeOffset? actionedAtUtc = null, CancellationToken ct = default);
}
