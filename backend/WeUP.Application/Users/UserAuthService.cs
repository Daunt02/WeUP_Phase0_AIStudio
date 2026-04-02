using WeUP.Contracts.Auth;
using WeUP.Domain.Users;

namespace WeUP.Application.Users;

/// <summary>
/// Orchestrates registration and login flows.
/// Delegates persistence to IUserProfileRepository and token issuance to ITokenService.
/// </summary>
public sealed class UserAuthService(
    IUserProfileRepository users,
    ITokenService tokens)
{
    /// <summary>
    /// Register a new user. If the email already exists, returns the existing profile
    /// (idempotent). Phase 0: no password — email is the identity.
    /// </summary>
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var existing = await users.GetByEmailAsync(request.Email, ct);
        var profile  = existing ?? await users.CreateAsync(request.Email, request.DisplayName, request.HomeMarket, ct);

        var token = tokens.IssueToken(profile.UserId);
        return new AuthResponse(profile.UserId, token, TokenTypes.Bearer, tokens.ExpiresInSeconds, profile);
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
        return new AuthResponse(profile.UserId, token, TokenTypes.Bearer, tokens.ExpiresInSeconds, profile);
    }

    public Task<UserProfileDto?> GetProfileAsync(string userId, CancellationToken ct = default) =>
        users.GetByIdAsync(userId, ct);

    public async Task<UserProfileDto> UpdateProfileAsync(string userId, UpdateProfileRequest request, CancellationToken ct = default) =>
        await users.UpdateAsync(userId, request, ct);
}
