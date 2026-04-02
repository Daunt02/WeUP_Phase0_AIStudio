using WeUP.Contracts.Events;

namespace WeUP.Domain.Events;

/// <summary>
/// Full submission lifecycle — create draft, edit, submit for review, track status.
/// </summary>
public interface IEventSubmissionService
{
    Task<SubmissionDto> CreateDraftAsync(string userId, DraftSubmissionRequest request, CancellationToken ct = default);
    Task<SubmissionDto?> GetAsync(string submissionId, CancellationToken ct = default);
    Task<SubmissionListResponse> ListByUserAsync(string userId, int page, int pageSize, CancellationToken ct = default);
    Task<SubmissionDto?> UpdateDraftAsync(string submissionId, string userId, DraftSubmissionRequest request, CancellationToken ct = default);
    Task<SubmitForReviewResponse> SubmitForReviewAsync(string submissionId, string userId, CancellationToken ct = default);
    Task<SubmissionDto?> ApplyReviewFeedbackAsync(string submissionId, SubmissionStatus newStatus, string? reviewNote, CancellationToken ct = default);
}
