using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeUP.Contracts.Events;
using WeUP.Domain.Events;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Submissions;

public sealed class EfSubmissionRepository(WeUpDbContext db) : IEventSubmissionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SubmissionDto> CreateDraftAsync(string userId, DraftSubmissionRequest request, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new EventSubmissionEntity
        {
            Id = Guid.NewGuid(),
            SubmissionId = $"submission-{Guid.NewGuid():N}",
            SubmittedByUserId = userId,
            Status = SubmissionStatus.Draft.ToString(),
            Title = request.Title,
            VenueName = request.VenueName,
            Address = request.Address,
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            Timezone = request.Timezone,
            Category = request.Category,
            Description = request.Description,
            TagsJson = Serialize(request.Tags),
            FlyerAssetIdsJson = Serialize(request.FlyerAssetIds),
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.EventSubmissions.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<SubmissionDto?> GetAsync(string submissionId, CancellationToken ct = default)
    {
        var entity = await db.EventSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SubmissionId == submissionId, ct);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<SubmissionListResponse> ListByUserAsync(string userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.EventSubmissions
            .AsNoTracking()
            .Where(s => s.SubmittedByUserId == userId)
            .OrderByDescending(s => s.UpdatedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new SubmissionListResponse([.. items.Select(ToDto)], totalCount);
    }

    public async Task<SubmissionDto?> UpdateDraftAsync(string submissionId, string userId, DraftSubmissionRequest request, CancellationToken ct = default)
    {
        var entity = await db.EventSubmissions.FirstOrDefaultAsync(s => s.SubmissionId == submissionId, ct);
        if (entity is null || entity.SubmittedByUserId != userId)
        {
            return null;
        }

        var status = ParseStatus(entity.Status);
        if (status is not SubmissionStatus.Draft and not SubmissionStatus.ChangesRequested)
        {
            return null;
        }

        entity.Status = SubmissionStatus.Draft.ToString();
        entity.Title = request.Title ?? entity.Title;
        entity.VenueName = request.VenueName ?? entity.VenueName;
        entity.Address = request.Address ?? entity.Address;
        entity.StartUtc = request.StartUtc ?? entity.StartUtc;
        entity.EndUtc = request.EndUtc ?? entity.EndUtc;
        entity.Timezone = request.Timezone ?? entity.Timezone;
        entity.Category = request.Category ?? entity.Category;
        entity.Description = request.Description ?? entity.Description;
        entity.TagsJson = request.Tags is null ? entity.TagsJson : Serialize(request.Tags);
        entity.FlyerAssetIdsJson = request.FlyerAssetIds is null ? entity.FlyerAssetIdsJson : Serialize(request.FlyerAssetIds);
        entity.ReviewNote = null;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<SubmitForReviewResponse> SubmitForReviewAsync(string submissionId, string userId, CancellationToken ct = default)
    {
        var entity = await db.EventSubmissions.FirstOrDefaultAsync(s => s.SubmissionId == submissionId, ct);
        if (entity is null)
        {
            return new SubmitForReviewResponse(submissionId, SubmissionStatus.Draft, "Submission not found.");
        }

        if (entity.SubmittedByUserId != userId)
        {
            return new SubmitForReviewResponse(submissionId, ParseStatus(entity.Status), "Not authorized.");
        }

        var status = ParseStatus(entity.Status);
        if (status is not SubmissionStatus.Draft and not SubmissionStatus.ChangesRequested)
        {
            return new SubmitForReviewResponse(submissionId, status, $"Cannot submit — current status is '{status}'.");
        }

        var errors = ValidateForSubmission(entity);
        if (errors.Length > 0)
        {
            return new SubmitForReviewResponse(
                submissionId,
                status,
                $"Validation failed: {string.Join("; ", errors.Select(e => $"{e.Field}: {e.Message}"))}");
        }

        entity.Status = SubmissionStatus.SubmittedForReview.ToString();
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return new SubmitForReviewResponse(submissionId, SubmissionStatus.SubmittedForReview, "Submitted for review.");
    }

    public async Task<SubmissionDto?> ApplyReviewFeedbackAsync(string submissionId, SubmissionStatus newStatus, string? reviewNote, CancellationToken ct = default)
    {
        var entity = await db.EventSubmissions.FirstOrDefaultAsync(s => s.SubmissionId == submissionId, ct);
        if (entity is null)
        {
            return null;
        }

        entity.Status = newStatus.ToString();
        entity.ReviewNote = reviewNote;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    private static SubmissionDto ToDto(EventSubmissionEntity entity) =>
        new(
            entity.SubmissionId,
            entity.SubmittedByUserId,
            ParseStatus(entity.Status),
            entity.Title,
            entity.VenueName,
            entity.Address,
            entity.StartUtc,
            entity.EndUtc,
            entity.Timezone,
            entity.Category,
            entity.Description,
            Deserialize(entity.TagsJson),
            Deserialize(entity.FlyerAssetIdsJson),
            entity.ReviewNote,
            entity.CreatedAt,
            entity.UpdatedAt);

    private static SubmissionStatus ParseStatus(string status) =>
        Enum.TryParse<SubmissionStatus>(status, ignoreCase: true, out var parsed)
            ? parsed
            : SubmissionStatus.Draft;

    private static string? Serialize(string[]? values) => values is null ? null : JsonSerializer.Serialize(values, JsonOptions);

    private static string[]? Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<string[]>(json, JsonOptions);

    private static SubmissionValidationError[] ValidateForSubmission(EventSubmissionEntity entity)
    {
        var errors = new List<SubmissionValidationError>();
        if (string.IsNullOrWhiteSpace(entity.Title)) errors.Add(new("title", "Title is required."));
        if (string.IsNullOrWhiteSpace(entity.VenueName)) errors.Add(new("venueName", "Venue name is required."));
        if (string.IsNullOrWhiteSpace(entity.Address)) errors.Add(new("address", "Address is required."));
        if (entity.StartUtc is null) errors.Add(new("startUtc", "Start time is required."));
        if (string.IsNullOrWhiteSpace(entity.Category)) errors.Add(new("category", "Category is required."));
        return [.. errors];
    }
}