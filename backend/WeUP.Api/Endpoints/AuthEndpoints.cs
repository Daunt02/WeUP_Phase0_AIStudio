using System.Security.Claims;
using WeUP.Application.Users;
using WeUP.Contracts.Auth;
using WeUP.Domain.Users;

namespace WeUP.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        // POST /auth/register
        group.MapPost("/register", async (RegisterRequest request, UserAuthService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return Results.BadRequest(new { error = "Email is required." });

            var result = await svc.RegisterAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("RegisterUser")
        .WithSummary("Register or retrieve a user by email (Phase 0: passwordless)");

        // POST /auth/login
        group.MapPost("/login", async (LoginRequest request, UserAuthService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return Results.BadRequest(new { error = "Email is required." });

            var result = await svc.LoginAsync(request, ct);
            return result is null
                ? Results.NotFound(new { error = "Email not registered. Use /auth/register first." })
                : Results.Ok(result);
        })
        .WithName("LoginUser")
        .WithSummary("Login by email and receive a bearer token");

        // GET /auth/me
        group.MapGet("/me", async (HttpContext ctx, UserAuthService svc, ITokenService tokens, CancellationToken ct) =>
        {
            var userId = ResolveUserId(ctx, tokens);
            if (userId is null) return Results.Unauthorized();

            var profile = await svc.GetProfileAsync(userId, ct);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        })
        .WithName("GetCurrentUser")
        .WithSummary("Return the authenticated user's profile");

        // PATCH /auth/profile
        group.MapPatch("/profile", async (UpdateProfileRequest request, HttpContext ctx, UserAuthService svc, ITokenService tokens, CancellationToken ct) =>
        {
            var userId = ResolveUserId(ctx, tokens);
            if (userId is null) return Results.Unauthorized();

            var updated = await svc.UpdateProfileAsync(userId, request, ct);
            return Results.Ok(updated);
        })
        .WithName("UpdateProfile")
        .WithSummary("Update display name, home market, and onboarding state");

        return app;
    }

    /// <summary>
    /// Extracts the user ID from the Authorization: Bearer &lt;token&gt; header.
    /// Phase 0: validated against in-memory BearerTokenService.
    /// Phase 0.5+: replace with ClaimsPrincipal from JwtBearer middleware.
    /// </summary>
    internal static string? ResolveUserId(HttpContext ctx, ITokenService tokens)
    {
        return ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
