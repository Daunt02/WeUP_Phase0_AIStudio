using System.Collections.Concurrent;
using WeUP.Contracts.Auth;
using WeUP.Domain.Users;
using WeUP.Infrastructure.Seed;

namespace WeUP.Infrastructure.Auth;

public sealed class InMemoryUserRoleRepository : IUserRoleRepository
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _rolesByUser =
        new(StringComparer.OrdinalIgnoreCase);

    public void Reset(Phase0SeedDataset dataset)
    {
        _rolesByUser.Clear();

        foreach (var user in dataset.Users)
        {
            var seeded = user.Roles is { Length: > 0 }
                ? user.Roles
                : [UserRoles.User];

            _rolesByUser[user.UserId] = seeded
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim().ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _rolesByUser[user.UserId].Add(UserRoles.User);
        }
    }

    public Task<string[]> GetRolesAsync(string userId, CancellationToken ct = default)
    {
        if (!_rolesByUser.TryGetValue(userId, out var roles))
        {
            return Task.FromResult(new[] { UserRoles.User });
        }

        return Task.FromResult(roles.ToArray());
    }

    public Task<string[]> SetRolesAsync(string userId, IReadOnlyCollection<string> roles, CancellationToken ct = default)
    {
        var normalized = roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        normalized.Add(UserRoles.User);

        _rolesByUser[userId] = normalized;
        return Task.FromResult(normalized.ToArray());
    }
}
