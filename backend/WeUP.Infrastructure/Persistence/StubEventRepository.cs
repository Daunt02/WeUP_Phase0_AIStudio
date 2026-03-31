using WeUP.Contracts.Events;
using WeUP.Domain.Events;

namespace WeUP.Infrastructure.Persistence;

/// <summary>
/// Stub implementation — replaced by EF Core / Postgres implementation in P08–P09.
/// Returns empty collections with correct shapes so the API surface can be tested.
/// </summary>
public sealed class StubEventRepository : IEventRepository, IEventSubmissionRepository
{
    public Task<MapFeedResponse> GetMapFeedAsync(MapFeedRequest request, CancellationToken ct = default)
        => Task.FromResult(new MapFeedResponse([], 0));

    public Task<CalendarFeedResponse> GetCalendarFeedAsync(CalendarFeedRequest request, CancellationToken ct = default)
        => Task.FromResult(new CalendarFeedResponse([], 0, request.Page, request.PageSize, false));

    public Task<EventDetailResponse> GetEventDetailAsync(string eventId, CancellationToken ct = default)
        => Task.FromResult(new EventDetailResponse(null));

    public Task<string> CreateSubmissionAsync(string userId, EventSubmissionRequest request, CancellationToken ct = default)
        => Task.FromResult(Guid.NewGuid().ToString("N"));

    public Task<string?> GetSubmissionStatusAsync(string submissionId, CancellationToken ct = default)
        => Task.FromResult<string?>("DRAFT");
}
