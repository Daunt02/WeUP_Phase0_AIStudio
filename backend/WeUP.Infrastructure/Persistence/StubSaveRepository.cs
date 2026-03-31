using WeUP.Contracts.Saves;
using WeUP.Domain.Users;

namespace WeUP.Infrastructure.Persistence;

/// <summary>
/// In-memory stub for saves — replaced by EF Core implementation in P08–P09.
/// </summary>
public sealed class StubSaveRepository : ISaveRepository
{
    private readonly HashSet<(string UserId, string EventId)> _saves = [];

    public Task<SavedEventsResponse> GetSavesAsync(string userId, int page, int pageSize, CancellationToken ct = default)
    {
        var items = _saves
            .Where(s => s.UserId == userId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SavedEventDto(s.EventId, "—", "—", DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow))
            .ToArray();

        return Task.FromResult(new SavedEventsResponse(items, _saves.Count(s => s.UserId == userId), page, pageSize, false));
    }

    public Task<SaveEventResponse> SaveEventAsync(string userId, string eventId, CancellationToken ct = default)
    {
        _saves.Add((userId, eventId));
        return Task.FromResult(new SaveEventResponse(eventId, true, "Saved"));
    }

    public Task<SaveEventResponse> UnsaveEventAsync(string userId, string eventId, CancellationToken ct = default)
    {
        _saves.Remove((userId, eventId));
        return Task.FromResult(new SaveEventResponse(eventId, false, "Unsaved"));
    }
}
