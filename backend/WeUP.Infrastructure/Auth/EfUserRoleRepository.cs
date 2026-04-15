using Microsoft.EntityFrameworkCore;
using WeUP.Contracts.Auth;
using WeUP.Domain.Users;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Auth;

public sealed class EfUserRoleRepository(WeUpDbContext db) : IUserRoleRepository
{
    public async Task<string[]> GetRolesAsync(string userId, CancellationToken ct = default)
    {
        var internalUserId = await db.UserProfiles
            .AsNoTracking()
            .Where(u => u.PublicId == userId)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);

        if (internalUserId is null)
        {
            return [UserRoles.User];
        }

        var roles = await db.UserRoles
            .AsNoTracking()
            .Where(r => r.UserId == internalUserId.Value)
            .Select(r => r.Role)
            .ToListAsync(ct);

        if (!roles.Contains(UserRoles.User, StringComparer.OrdinalIgnoreCase))
        {
            roles.Add(UserRoles.User);
        }

        return [.. roles.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<string[]> SetRolesAsync(string userId, IReadOnlyCollection<string> roles, CancellationToken ct = default)
    {
        var user = await db.UserProfiles
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.PublicId == userId, ct)
            ?? throw new InvalidOperationException($"User '{userId}' not found.");

        var normalized = roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        normalized.Add(UserRoles.User);

        var existing = user.Roles.Select(r => r.Role).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var role in existing.Except(normalized, StringComparer.OrdinalIgnoreCase))
        {
            var entity = user.Roles.First(r => string.Equals(r.Role, role, StringComparison.OrdinalIgnoreCase));
            db.UserRoles.Remove(entity);
        }

        foreach (var role in normalized.Except(existing, StringComparer.OrdinalIgnoreCase))
        {
            user.Roles.Add(new UserRoleEntity
            {
                UserId = user.Id,
                Role = role,
                AssignedAt = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
        return [.. normalized];
    }
}
