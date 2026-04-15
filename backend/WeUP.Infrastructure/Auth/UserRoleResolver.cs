using WeUP.Contracts.Auth;
using WeUP.Domain.Users;

namespace WeUP.Infrastructure.Auth;

public sealed class UserRoleResolver(IUserRoleRepository roles) : IUserRoleResolver
{
    public async Task<string[]> ResolveRolesAsync(string userId, CancellationToken ct = default)
    {
        var assigned = await roles.GetRolesAsync(userId, ct);
        if (assigned.Contains(UserRoles.User, StringComparer.OrdinalIgnoreCase))
        {
            return assigned;
        }

        return [.. assigned, UserRoles.User];
    }

    public async Task<bool> IsInRoleAsync(string userId, string role, CancellationToken ct = default)
    {
        var assigned = await roles.GetRolesAsync(userId, ct);
        return assigned.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
