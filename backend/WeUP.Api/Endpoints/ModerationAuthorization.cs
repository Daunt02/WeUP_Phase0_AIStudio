using Microsoft.AspNetCore.Http;
using WeUP.Contracts.Auth;
using WeUP.Domain.Users;

namespace WeUP.Api.Endpoints;

public interface IModerationAuthorizationService
{
    ValueTask<ModerationAuthorizationDecision> AuthorizeAsync(HttpContext context, CancellationToken ct = default);
}

public readonly record struct ModerationAuthorizationDecision(bool IsAuthenticated, bool IsAuthorized, string? UserId)
{
    public static ModerationAuthorizationDecision Anonymous => new(false, false, null);
    public static ModerationAuthorizationDecision Forbidden(string userId) => new(true, false, userId);
    public static ModerationAuthorizationDecision Allowed(string userId) => new(true, true, userId);
};

public sealed class RoleBasedModerationAuthorizationService(
    IUserRoleResolver roles) : IModerationAuthorizationService
{
    public async ValueTask<ModerationAuthorizationDecision> AuthorizeAsync(HttpContext context, CancellationToken ct = default)
    {
        var userId = AuthEndpoints.ResolveUserId(context);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ModerationAuthorizationDecision.Anonymous;
        }

        var authorized = await roles.IsInRoleAsync(userId, UserRoles.Moderator, ct);
        return authorized
            ? ModerationAuthorizationDecision.Allowed(userId)
            : ModerationAuthorizationDecision.Forbidden(userId);
    }
}

public sealed class ModeratorAuthorizationFilter(IModerationAuthorizationService authorization) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var decision = await authorization.AuthorizeAsync(http, http.RequestAborted);
        if (!decision.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!decision.IsAuthorized)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        http.Items["moderatorUserId"] = decision.UserId;

        return await next(context);
    }
}
