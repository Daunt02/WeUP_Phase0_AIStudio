using System.Collections.Concurrent;
using WeUP.Domain.Moderation;

namespace WeUP.Infrastructure.Moderation;

/// <summary>
/// Append-only in-memory audit trail.
/// Replaced by a persisted audit table when the database is provisioned.
/// Entries are never mutated or deleted — rollback creates a new entry.
/// </summary>
public sealed class InMemoryAuditTrail : IAuditTrailService
{
    // Ordered list: ConcurrentBag is unordered, so we use a locked list for ordering guarantee.
    private readonly List<AuditTrailEntry> _entries = [];
    private readonly object _lock = new();

    public Task AppendAsync(AuditTrailEntry entry, CancellationToken ct = default)
    {
        lock (_lock)
            _entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<AuditTrailEntry> Entries, int Total)> GetEntriesAsync(
        int pageSize, string? cursor, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var sorted = _entries.OrderByDescending(e => e.Timestamp).AsEnumerable();

            if (cursor is not null && long.TryParse(
                System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor)), out var ticks))
            {
                sorted = sorted.Where(e => e.Timestamp.UtcTicks < ticks);
            }

            var page = sorted.Take(pageSize).ToList();
            IReadOnlyList<AuditTrailEntry> result = page;
            return Task.FromResult((result, _entries.Count));
        }
    }
}
