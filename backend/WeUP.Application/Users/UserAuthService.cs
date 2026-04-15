using WeUP.Contracts.Auth;
using WeUP.Domain.Users;

namespace WeUP.Application.Users;

/// <summary>
/// Orchestrates registration and login flows.
/// Delegates persistence to IUserProfileRepository and token issuance to ITokenService.
/// </summary>
public sealed class UserAuthService(
    IUserProfileRepository users,
    ITokenService tokens,
    IUserRoleRepository roleRepository,
    IUserRoleResolver roles)
{
    /// <summary>
    /// Register a new user. If the email already exists, returns the existing profile
    /// (idempotent). Phase 0: no password — email is the identity.
    /// </summary>
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var existing = await users.GetByEmailAsync(request.Email, ct);
        var profile  = existing ?? await users.CreateAsync(request.Email, request.DisplayName, request.HomeMarket, ct);
        if (existing is null)
        {
            await roleRepository.SetRolesAsync(profile.UserId, [UserRoles.User], ct);
        }

        var token = tokens.IssueToken(profile.UserId);
        var resolvedRoles = await roles.ResolveRolesAsync(profile.UserId, ct);
        return new AuthResponse(
            profile.UserId,
            token,
            TokenTypes.Bearer,
            tokens.ExpiresInSeconds,
            profile with { Roles = resolvedRoles },
            resolvedRoles);
    }

    /// <summary>
    /// Login by email. Phase 0: creates profile if not found (no separate registration step).
    /// Returns 404 if the email is unknown and auto-create is disabled.
    /// </summary>
    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var profile = await users.GetByEmailAsync(request.Email, ct);
        if (profile is null) return null;

        var token = tokens.IssueToken(profile.UserId);
        var resolvedRoles = await roles.ResolveRolesAsync(profile.UserId, ct);
        return new AuthResponse(
            profile.UserId,
            token,
            TokenTypes.Bearer,
            tokens.ExpiresInSeconds,
            profile with { Roles = resolvedRoles },
            resolvedRoles);
    }

    public async Task<UserProfileDto?> GetProfileAsync(string userId, CancellationToken ct = default)
    {
        var profile = await users.GetByIdAsync(userId, ct);
        if (profile is null)
        {
            return null;
        }

        var resolvedRoles = await roles.ResolveRolesAsync(userId, ct);
        return profile with { Roles = resolvedRoles };
    }

    public async Task<UserProfileDto> UpdateProfileAsync(string userId, UpdateProfileRequest request, CancellationToken ct = default) =>
        (await users.UpdateAsync(userId, request, ct)) with
        {
            Roles = await roles.ResolveRolesAsync(userId, ct),
        };
}
