using System.Collections.Concurrent;
using WeUP.Contracts.Events;
using WeUP.Domain.Events;

namespace WeUP.Infrastructure.Submissions;

/// <summary>
/// Phase 0 in-memory submission store.
/// Thread-safe. Replace with EfSubmissionRepository + Postgres when DB is active.
/// </summary>
public sealed class InMemorySubmissionRepository : IEventSubmissionService
{
    private readonly ConcurrentDictionary<string, SubmissionRecord> _byId   = new();
    // userId → list of submissionIds
    private readonly ConcurrentDictionary<string, List<string>>     _byUser = new();

    // ---------------------------------------------------------------------------
    // IEventSubmissionService
    // ---------------------------------------------------------------------------

    public Task<SubmissionDto> CreateDraftAsync(string userId, DraftSubmissionRequest request, CancellationToken ct = default)
    {
        var rec = new SubmissionRecord(
            SubmissionId: Guid.NewGuid().ToString("N"),
            SubmittedByUserId: userId,
            Status: SubmissionStatus.Draft,
            Request: request,
            ReviewNote: null,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);

        _byId[rec.SubmissionId] = rec;
        _byUser.GetOrAdd(userId, _ => []).Add(rec.SubmissionId);

        return Task.FromResult(rec.ToDto());
    }

    public Task<SubmissionDto?> GetAsync(string submissionId, CancellationToken ct = default)
    {
        _byId.TryGetValue(submissionId, out var rec);
        return Task.FromResult(rec?.ToDto());
    }

    public Task<SubmissionListResponse> ListByUserAsync(string userId, int page, int pageSize, CancellationToken ct = default)
    {
        if (!_byUser.TryGetValue(userId, out var ids))
            return Task.FromResult(new SubmissionListResponse([], 0));

        var all = ids
            .Select(id => _byId.TryGetValue(id, out var r) ? r : null)
            .OfType<SubmissionRecord>()
            .OrderByDescending(r => r.UpdatedAt)
            .ToList();

        var items = all
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => r.ToDto())
            .ToArray();

        return Task.FromResult(new SubmissionListResponse(items, all.Count));
    }

    public Task<SubmissionDto?> UpdateDraftAsync(string submissionId, string userId, DraftSubmissionRequest request, CancellationToken ct = default)
    {
        if (!_byId.TryGetValue(submissionId, out var rec)) return Task.FromResult<SubmissionDto?>(null);
        if (rec.SubmittedByUserId != userId) return Task.FromResult<SubmissionDto?>(null);
        if (rec.Status is not SubmissionStatus.Draft and not SubmissionStatus.ChangesRequested)
            return Task.FromResult<SubmissionDto?>(null); // Cannot edit non-draft

        var merged = new DraftSubmissionRequest(
            Title:         request.Title         ?? rec.Request.Title,
            VenueName:     request.VenueName     ?? rec.Request.VenueName,
            Address:       request.Address       ?? rec.Request.Address,
            StartUtc:      request.StartUtc      ?? rec.Request.StartUtc,
            EndUtc:        request.EndUtc        ?? rec.Request.EndUtc,
            Timezone:      request.Timezone      ?? rec.Request.Timezone,
            Category:      request.Category      ?? rec.Request.Category,
            Description:   request.Description   ?? rec.Request.Description,
            Tags:          request.Tags          ?? rec.Request.Tags,
            FlyerAssetIds: request.FlyerAssetIds ?? rec.Request.FlyerAssetIds);

        var updated = rec with
        {
            Status = SubmissionStatus.Draft,
            Request = merged,
            ReviewNote = null,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _byId[submissionId] = updated;
        return Task.FromResult<SubmissionDto?>(updated.ToDto());
    }

    public Task<SubmitForReviewResponse> SubmitForReviewAsync(string submissionId, string userId, CancellationToken ct = default)
    {
        if (!_byId.TryGetValue(submissionId, out var rec))
            return Task.FromResult(new SubmitForReviewResponse(submissionId, SubmissionStatus.Draft, "Submission not found."));

        if (rec.SubmittedByUserId != userId)
            return Task.FromResult(new SubmitForReviewResponse(submissionId, rec.Status, "Not authorized."));

        if (rec.Status is not SubmissionStatus.Draft and not SubmissionStatus.ChangesRequested)
            return Task.FromResult(new SubmitForReviewResponse(submissionId, rec.Status,
                $"Cannot submit — current status is '{rec.Status}'."));

        var errors = ValidateForSubmission(rec.Request);
        if (errors.Length > 0)
            return Task.FromResult(new SubmitForReviewResponse(submissionId, rec.Status,
                $"Validation failed: {string.Join("; ", errors.Select(e => $"{e.Field}: {e.Message}"))}"));

        var updated = rec with
        {
            Status = SubmissionStatus.SubmittedForReview,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _byId[submissionId] = updated;

        return Task.FromResult(new SubmitForReviewResponse(
            submissionId, SubmissionStatus.SubmittedForReview, "Submitted for review."));
    }

    public Task<SubmissionDto?> ApplyReviewFeedbackAsync(
        string submissionId, SubmissionStatus newStatus, string? reviewNote, CancellationToken ct = default)
    {
        if (!_byId.TryGetValue(submissionId, out var rec)) return Task.FromResult<SubmissionDto?>(null);

        var updated = rec with
        {
            Status = newStatus,
            ReviewNote = reviewNote,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _byId[submissionId] = updated;
        return Task.FromResult<SubmissionDto?>(updated.ToDto());
    }

    // ---------------------------------------------------------------------------
    // Validation
    // ---------------------------------------------------------------------------

    private static SubmissionValidationError[] ValidateForSubmission(DraftSubmissionRequest r)
    {
        var errors = new List<SubmissionValidationError>();
        if (string.IsNullOrWhiteSpace(r.Title))       errors.Add(new("title", "Title is required."));
        if (string.IsNullOrWhiteSpace(r.VenueName))   errors.Add(new("venueName", "Venue name is required."));
        if (string.IsNullOrWhiteSpace(r.Address))     errors.Add(new("address", "Address is required."));
        if (r.StartUtc is null)                       errors.Add(new("startUtc", "Start time is required."));
        if (string.IsNullOrWhiteSpace(r.Category))    errors.Add(new("category", "Category is required."));
        return [.. errors];
    }

    // ---------------------------------------------------------------------------
    // Record
    // ---------------------------------------------------------------------------

    private sealed record SubmissionRecord(
        string SubmissionId,
        string SubmittedByUserId,
        SubmissionStatus Status,
        DraftSubmissionRequest Request,
        string? ReviewNote,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt)
    {
        public SubmissionDto ToDto() =>
            new(SubmissionId, SubmittedByUserId, Status,
                Request.Title, Request.VenueName, Request.Address,
                Request.StartUtc, Request.EndUtc, Request.Timezone,
                Request.Category, Request.Description, Request.Tags,
                Request.FlyerAssetIds, ReviewNote, CreatedAt, UpdatedAt);
    }
}
