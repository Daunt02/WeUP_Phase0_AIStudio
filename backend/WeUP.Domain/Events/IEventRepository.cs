using WeUP.Contracts.Events;

namespace WeUP.Domain.Events;

/// <summary>
/// Core event query interface. Infrastructure implements against EF Core + Postgres.
/// </summary>
public interface IEventRepository
{
    Task<MapFeedResponse> GetMapFeedAsync(MapFeedRequest request, CancellationToken ct = default);
    Task<CalendarFeedResponse> GetCalendarFeedAsync(CalendarFeedRequest request, CancellationToken ct = default);
    Task<EventDetailResponse> GetEventDetailAsync(string eventId, CancellationToken ct = default);
}

/// <summary>
/// Event submission write interface.
/// </summary>
public interface IEventSubmissionRepository
{
    Task<string> CreateSubmissionAsync(string userId, EventSubmissionRequest request, CancellationToken ct = default);
    Task<string?> GetSubmissionStatusAsync(string submissionId, CancellationToken ct = default);
}
