namespace WeUP.Contracts.Auth;

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

// ---------------------------------------------------------------------------
// Responses
// ---------------------------------------------------------------------------

/// <summary>Returned after register or login.</summary>
public sealed record AuthResponse(
    string UserId,
    string Token,
    string TokenType,
    int ExpiresInSeconds,
    UserProfileDto Profile);

/// <summary>Full profile shape returned on GET /auth/me.</summary>
public sealed record UserProfileDto(
    string UserId,
    string Email,
    string? DisplayName,
    string? HomeMarket,
    string OnboardingState,
    DateTimeOffset CreatedAt);
