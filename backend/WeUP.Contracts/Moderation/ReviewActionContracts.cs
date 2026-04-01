namespace WeUP.Contracts.Moderation;

// ---------------------------------------------------------------------------
// Review action request payloads
// ---------------------------------------------------------------------------

public record ApproveRequest(string ActorId, string? Note);
public record RejectRequest(string ActorId, string RejectionReason, string? Note);
public record RequestChangesRequest(string ActorId, string CorrectionInstructions, string? Note);
public record MarkDuplicateRequest(string ActorId, string ExistingEventId, string? Note);
public record MergeRequest(string ActorId, string TargetEventId, string? Note);
public record ArchiveRequest(string ActorId, string? Note);
public record ReopenRequest(string ActorId, string? Note);
public record RollbackRequest(string ActorId, string RollbackReason, string? Note);

// ---------------------------------------------------------------------------
// Review action response
// ---------------------------------------------------------------------------

public record ReviewActionResponse(
    string ItemId,
    string Action,
    string PreviousStatus,
    string NewStatus,
    bool Success,
    string? ErrorMessage);

// ---------------------------------------------------------------------------
// Audit log DTO (operator-readable projection)
// ---------------------------------------------------------------------------

public record AuditLogEntryDto(
    string EntryId,
    string ItemId,
    string ItemKind,
    string Action,
    string ActorId,
    string PreviousStatus,
    string NextStatus,
    string? Note,
    string[] EvidenceSnapshotRefs,
    string CorrelationId,
    string Timestamp);

public record AuditLogResponse(
    AuditLogEntryDto[] Entries,
    int TotalCount,
    string? NextCursor);
