using Microsoft.AspNetCore.Http;

namespace WeUP.Api.Endpoints;

public interface IModerationAuthorizationService
{
    ValueTask<bool> IsModeratorAuthorizedAsync(HttpContext context, CancellationToken ct = default);
}

/// <summary>
/// Authorization seam for moderator APIs.
/// Replace with policy/role-backed enforcement when full auth is enabled.
/// </summary>
public sealed class AllowAllModerationAuthorizationService : IModerationAuthorizationService
{
    public ValueTask<bool> IsModeratorAuthorizedAsync(HttpContext context, CancellationToken ct = default) =>
        ValueTask.FromResult(true);
}

public sealed class ModeratorAuthorizationFilter(IModerationAuthorizationService authorization) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var allowed = await authorization.IsModeratorAuthorizedAsync(http, http.RequestAborted);
        if (!allowed)
        {
            return Results.Forbid();
        }

        return await next(context);
    }
}
