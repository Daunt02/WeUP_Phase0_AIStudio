using WeUP.Contracts.Saves;

namespace WeUP.Domain.Users;

/// <summary>
/// Manages user saved-event signals.
/// </summary>
public interface ISaveRepository
{
    Task<SavedEventsResponse> GetSavesAsync(string userId, int page, int pageSize, CancellationToken ct = default);
    /// <summary>Idempotent — safe to call multiple times.</summary>
    Task<SaveEventResponse> SaveEventAsync(string userId, string eventId, CancellationToken ct = default);
    /// <summary>Idempotent — safe to call when event is not saved.</summary>
    Task<SaveEventResponse> UnsaveEventAsync(string userId, string eventId, CancellationToken ct = default);
}
