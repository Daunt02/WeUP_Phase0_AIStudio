using WeUP.Domain.Moderation;

namespace WeUP.Infrastructure.Moderation;

/// <summary>
/// In-memory append-only moderation audit repository.
/// Entries are copied on write and exposed via ordered reads only.
/// </summary>
public sealed class InMemoryModerationAuditRepository : IModerationAuditRepository
{
    private readonly List<ModerationHistoryEntry> _entries = [];
    private readonly object _lock = new();

    public Task AppendAsync(ModerationHistoryEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_lock)
        {
            _entries.Add(entry with { });
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ModerationHistoryEntry>> GetByCandidateAsync(string candidateId, CancellationToken ct = default) =>
        Task.FromResult(Filter(entry => string.Equals(entry.CandidateId, candidateId, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<ModerationHistoryEntry>> GetByReviewerAsync(string reviewerId, CancellationToken ct = default) =>
        Task.FromResult(Filter(entry => string.Equals(entry.ReviewerId, reviewerId, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<ModerationHistoryEntry>> GetByEventAsync(string eventId, CancellationToken ct = default) =>
        Task.FromResult(Filter(entry => string.Equals(entry.EventId, eventId, StringComparison.OrdinalIgnoreCase)));

    private IReadOnlyList<ModerationHistoryEntry> Filter(Func<ModerationHistoryEntry, bool> predicate)
    {
        lock (_lock)
        {
            return _entries
                .Where(predicate)
                .Select(entry => entry with { })
                .ToArray();
        }
    }
}