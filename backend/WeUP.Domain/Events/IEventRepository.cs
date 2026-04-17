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

    /// <summary>
    /// Returns the canonical event aggregate for internal/operational use only.
    /// NEVER expose the returned aggregate directly to the frontend — pass it through
    /// <see cref="WeUP.Application.Events.EventDtoMapper"/> before serialising.
    /// </summary>
    Task<EventAggregate?> GetAggregateAsync(string eventId, CancellationToken ct = default);
}

/// <summary>
/// Event submission write interface.
/// </summary>
public interface IEventSubmissionRepository
{
    Task<string> CreateSubmissionAsync(string userId, EventSubmissionRequest request, CancellationToken ct = default);
    Task<string?> GetSubmissionStatusAsync(string submissionId, CancellationToken ct = default);
}

/// <summary>
/// Lifecycle mutation seam used by moderation decision workflows.
/// </summary>
public interface IEventLifecycleRepository
{
    Task<string?> GetLifecycleStatusAsync(string eventId, CancellationToken ct = default);
    Task<bool> TransitionLifecycleStatusAsync(string eventId, string newStatus, CancellationToken ct = default);
}
