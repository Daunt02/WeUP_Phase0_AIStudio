using WeUP.Contracts.Auth;

namespace WeUP.Domain.Users;

/// <summary>
/// Persistent user profile store. Infrastructure: in-memory (Phase 0) or EF Core (Phase 0.5+).
/// </summary>
public interface IUserProfileRepository
{
    Task<UserProfileDto?> GetByIdAsync(string userId, CancellationToken ct = default);
    Task<UserProfileDto?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<UserProfileDto> CreateAsync(string email, string? displayName, string? homeMarket, CancellationToken ct = default);
    Task<UserProfileDto> UpdateAsync(string userId, UpdateProfileRequest request, CancellationToken ct = default);
}

/// <summary>
/// Issues and validates opaque bearer tokens mapped to user IDs.
/// Phase 0: in-memory. Real JWT (P16.5+): Microsoft.AspNetCore.Authentication.JwtBearer.
/// </summary>
public interface ITokenService
{
    string IssueToken(string userId);
    string? ValidateToken(string token);
    int ExpiresInSeconds { get; }
}
