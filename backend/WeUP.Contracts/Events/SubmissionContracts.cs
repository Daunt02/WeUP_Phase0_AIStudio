namespace WeUP.Contracts.Events;

// ---------------------------------------------------------------------------
// Submission state machine
// DRAFT → SUBMITTED_FOR_REVIEW → APPROVED (published) | REJECTED | CHANGES_REQUESTED
// CHANGES_REQUESTED → DRAFT (user edits again)
// ---------------------------------------------------------------------------

public enum SubmissionStatus
{
    Draft,
    SubmittedForReview,
    ChangesRequested,
    Approved,
    Rejected,
}

// ---------------------------------------------------------------------------
// Requests
// ---------------------------------------------------------------------------

/// <summary>Create or update a draft submission. All fields optional on update.</summary>
public sealed record DraftSubmissionRequest(
    string? Title,
    string? VenueName,
    string? Address,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    string? Timezone,
    string? Category,
    string? Description,
    string[]? Tags,
    string[]? FlyerAssetIds);

// ---------------------------------------------------------------------------
// Responses
// ---------------------------------------------------------------------------

public sealed record SubmissionDto(
    string SubmissionId,
    string SubmittedByUserId,
    SubmissionStatus Status,
    string? Title,
    string? VenueName,
    string? Address,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    string? Timezone,
    string? Category,
    string? Description,
    string[]? Tags,
    string[]? FlyerAssetIds,
    string? ReviewNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SubmissionListResponse(
    SubmissionDto[] Items,
    int TotalCount);

public sealed record SubmitForReviewResponse(
    string SubmissionId,
    SubmissionStatus Status,
    string Message);

public sealed record SubmissionValidationError(string Field, string Message);
