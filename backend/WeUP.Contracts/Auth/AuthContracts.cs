namespace WeUP.Contracts.Auth;

public static class TokenTypes
{
    public const string Bearer = "Bearer";
}

public static class UserRoles
{
    public const string User = "user";
    public const string Moderator = "moderator";
}

public static class OnboardingStates
{
    public const string New      = "NEW";
    public const string Complete = "COMPLETE";
}

// ---------------------------------------------------------------------------
// Requests
// ---------------------------------------------------------------------------

/// <summary>Register a new user. Phase 0: email-only, no password.</summary>
public sealed record RegisterRequest(
    string Email,
    string? DisplayName,
    string? HomeMarket);

/// <summary>Login by email. Phase 0: passwordless — issues token on first lookup.</summary>
public sealed record LoginRequest(string Email);

/// <summary>Update mutable profile fields.</summary>
public sealed record UpdateProfileRequest(
    string? DisplayName,
    string? HomeMarket,
    string? OnboardingState);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

// ---------------------------------------------------------------------------
// Responses
// ---------------------------------------------------------------------------

/// <summary>Returned after register or login.</summary>
public sealed record AuthResponse(
    string UserId,
    string Token,
    string TokenType,
    int ExpiresInSeconds,
    UserProfileDto Profile,
    string[] Roles,
    string? RefreshToken = null);

/// <summary>Full profile shape returned on GET /auth/me.</summary>
public sealed record UserProfileDto(
    string UserId,
    string Email,
    string? DisplayName,
    string? HomeMarket,
    string OnboardingState,
    DateTimeOffset CreatedAt,
    string[]? Roles = null);

public sealed record UpdateUserRolesRequest(string[] Roles);

public sealed record UserRolesResponse(
    string UserId,
    string[] Roles,
    DateTimeOffset UpdatedAt);
